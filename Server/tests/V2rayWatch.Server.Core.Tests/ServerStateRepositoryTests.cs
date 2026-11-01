using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using V2rayWatch.Server.Core;
using V2rayWatch.Server.Web;
using Xunit;

namespace V2rayWatch.Server.Core.Tests;

public sealed class ServerStateRepositoryTests
{
    [Fact]
    public async Task Settings_RoundTrip_PreservesMonitorEnabledAndValues()
    {
        using var env = new TestEnvironment();
        var state = new SqliteServerStateRepository(env.Options, env.Protector);
        await state.InitializeAsync();

        var expected = new ServerAppSettings
        {
            MonitoringEnabled = true,
            Settings = new MonitorSettings
            {
                IntervalSeconds = 45,
                ConsecutiveFailureThreshold = 3,
                TimeoutSeconds = 20,
                PrimaryTestUrl = "https://example.com/generate_204",
                FallbackTestUrl = "https://www.gstatic.com/generate_204",
                NotifyOnDisconnect = false,
                NotifyOnRecovery = true
            }
        };

        await state.SaveSettingsAsync(expected);
        var actual = await state.GetSettingsAsync();

        Assert.True(actual.MonitoringEnabled);
        Assert.Equal(45, actual.Settings.IntervalSeconds);
        Assert.Equal(3, actual.Settings.ConsecutiveFailureThreshold);
        Assert.Equal(20, actual.Settings.TimeoutSeconds);
        Assert.Equal(expected.Settings.PrimaryTestUrl, actual.Settings.PrimaryTestUrl);
        Assert.False(actual.Settings.NotifyOnDisconnect);
        Assert.True(actual.Settings.NotifyOnRecovery);
    }

    [Fact]
    public async Task History_StoresAutomaticSamples_AndPrunesOlderThanSevenDays()
    {
        using var env = new TestEnvironment();
        var state = new SqliteServerStateRepository(env.Options, env.Protector);
        await state.InitializeAsync();

        var profileId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await state.RecordHistoryAsync(
            profileId, sessionId, DateTimeOffset.UtcNow.AddDays(-8), success: false, delayMs: null);
        await state.RecordHistoryAsync(
            profileId, sessionId, DateTimeOffset.UtcNow, success: true, delayMs: 123);

        var samples = await state.GetHistoryAsync(profileId, DateTimeOffset.UtcNow.AddDays(-7));

        var sample = Assert.Single(samples);
        Assert.True(sample.Success);
        Assert.Equal(123, sample.DelayMs);
    }

    [Fact]
    public async Task SubscriptionSource_IsPersisted_AndCanBeListedAfterReload()
    {
        using var env = new TestEnvironment();
        var profiles = new SqliteProfileRepository(env.Options, env.Protector);
        var state = new SqliteServerStateRepository(env.Options, env.Protector);
        await profiles.InitializeAsync();
        await state.InitializeAsync();

        var source = new SubscriptionSource
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Example",
            Url = "https://example.com/subscription"
        };

        var importedProfile = ServerProfileParser.ParseOne(
            "vless://00000000-0000-0000-0000-000000000001@edge.example:443?security=tls&sni=edge.example#Example");
        var result = await state.SaveSubscriptionAsync(
            source, [importedProfile], profiles, replaceExisting: false);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Owned);
        var saved = Assert.Single(await state.ListSubscriptionsAsync());
        Assert.Equal(source.Id, saved.Id);
        Assert.Equal(source.Url, saved.Url);
        Assert.Equal(1, saved.ProfileCount);

        var reopened = await state.GetSubscriptionAsync(source.Id);
        Assert.NotNull(reopened);
        Assert.Equal(source.Name, reopened!.Name);

        var savedProfile = Assert.Single(await profiles.ListAsync());
        await profiles.DeleteAsync(savedProfile.Id);
        await state.RefreshSubscriptionCountsAsync(profiles);
        var refreshed = Assert.Single(await state.ListSubscriptionsAsync());
        Assert.Equal(0, refreshed.ProfileCount);
    }

    [Fact]
    public void PortableBackup_RoundTripsAndKeepsTheSharedWindowsAndroidFormat()
    {
        var rawLink = "vless://00000000-0000-0000-0000-000000000001@edge.example:443?security=tls&sni=edge.example#Backup";
        var profile = ServerProfileParser.ParseOne(rawLink);
        profile.UptimeHistory = ["G", "R", "G"];
        var settings = new MonitorSettings
        {
            IntervalSeconds = 60,
            ConsecutiveFailureThreshold = 3,
            NotifyOnDisconnect = false
        };
        var passphrase = "correct horse battery staple".ToCharArray();

        try
        {
            var encrypted = PortableBackupService.CreateBackup([profile], settings, passphrase);
            Assert.Equal("V2VWBAK1", System.Text.Encoding.ASCII.GetString(encrypted, 0, 8));

            var restored = PortableBackupService.RestoreBackup(encrypted, passphrase, Array.Empty<ServerProfile>());
            var restoredProfile = Assert.Single(restored.Profiles);
            Assert.Equal(rawLink, restoredProfile.RawUri);
            Assert.Equal("Unknown", restoredProfile.Status);
            Assert.Equal("", restoredProfile.SourceSubscriptionId);
            Assert.Equal(60, restored.Settings.IntervalSeconds);
            Assert.Equal(3, restored.Settings.ConsecutiveFailureThreshold);
            Assert.False(restored.Settings.NotifyOnDisconnect);
            Assert.Equal(1, restored.AddedProfiles);
        }
        finally
        {
            Array.Clear(passphrase, 0, passphrase.Length);
        }
    }

    [Fact]
    public void PortableBackup_SkipsProfilesAlreadyPresent()
    {
        var rawLink = "vless://00000000-0000-0000-0000-000000000002@edge.example:443?security=tls&sni=edge.example#Duplicate";
        var profile = ServerProfileParser.ParseOne(rawLink);
        var passphrase = "another secure passphrase".ToCharArray();
        try
        {
            var encrypted = PortableBackupService.CreateBackup([profile], new MonitorSettings(), passphrase);
            var existing = ServerProfileParser.ParseOne(rawLink);
            var restored = PortableBackupService.RestoreBackup(encrypted, passphrase, [existing]);
            Assert.Single(restored.Profiles);
            Assert.Equal(0, restored.AddedProfiles);
            Assert.Equal(1, restored.SkippedProfiles);
        }
        finally
        {
            Array.Clear(passphrase, 0, passphrase.Length);
        }
    }

    [Fact]
    public void PortableBackup_RejectsWrongPassphrase()
    {
        var profile = ServerProfileParser.ParseOne(
            "vless://00000000-0000-0000-0000-000000000003@edge.example:443?security=tls&sni=edge.example#WrongPass");
        var correct = "backup password is long enough".ToCharArray();
        var wrong = "different password is wrong".ToCharArray();
        try
        {
            var encrypted = PortableBackupService.CreateBackup([profile], new MonitorSettings(), correct);
            Assert.Throws<CryptographicException>(() =>
                PortableBackupService.RestoreBackup(encrypted, wrong, Array.Empty<ServerProfile>()));
        }
        finally
        {
            Array.Clear(correct, 0, correct.Length);
            Array.Clear(wrong, 0, wrong.Length);
        }
    }

    [Fact]
    public void QrCodeService_GeneratesPngForProfileLink()
    {
        var png = QrCodeService.GeneratePng(
            "vless://00000000-0000-0000-0000-000000000004@edge.example:443?security=tls&sni=edge.example#QR");
        Assert.True(png.Length > 100);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
    }

    [Theory]
    [InlineData("http://example.com/subscription")]
    [InlineData("https://127.0.0.1/private")]
    [InlineData("https://10.0.0.5/private")]
    [InlineData("https://[::1]/private")]
    [InlineData("https://user:password@example.com/subscription")]
    public async Task SubscriptionClient_RejectsUnsafeAddressesBeforeConnecting(string url)
    {
        var client = new SubscriptionClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchTextAsync(url));
    }

    private sealed class TestEnvironment : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(), "v2raywatch-server-tests-" + Guid.NewGuid().ToString("N"));
        public ServerOptions Options { get; }
        public ProfileSecretProtector Protector { get; }

        public TestEnvironment()
        {
            Directory.CreateDirectory(Root);
            var keyFile = Path.Combine(Root, "profile.key");
            File.WriteAllText(keyFile, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            Protector = new ProfileSecretProtector(keyFile);
            Options = new ServerOptions { DataDirectory = Path.Combine(Root, "data") };
        }

        public void Dispose()
        {
            Protector.Dispose();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
