using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
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
    public async Task BaleNotificationSettings_RoundTripThroughEncryptedStorage()
    {
        using var env = new TestEnvironment();
        var state = new SqliteServerStateRepository(env.Options, env.Protector);
        await state.InitializeAsync();

        var expected = new BaleNotificationSettings
        {
            Enabled = true,
            BotToken = "123456789:TestToken_abcdefghijklmnopqrstuvwxyz",
            ChatId = "1234567890123"
        };

        await state.SaveBaleNotificationSettingsAsync(expected);
        var actual = await state.GetBaleNotificationSettingsAsync();

        Assert.True(actual.Enabled);
        Assert.Equal(expected.BotToken, actual.BotToken);
        Assert.Equal(expected.ChatId, actual.ChatId);
    }

    [Fact]
    public async Task BaleNotificationService_SendsDisconnectAndRecoveryTransitions()
    {
        using var env = new TestEnvironment();
        var state = new SqliteServerStateRepository(env.Options, env.Protector);
        await state.InitializeAsync();
        await state.SaveBaleNotificationSettingsAsync(new BaleNotificationSettings
        {
            Enabled = true,
            BotToken = "123456789:TestToken_abcdefghijklmnopqrstuvwxyz",
            ChatId = "1234567890123"
        });

        var handler = new RecordingBaleHandler();
        var clients = new TestHttpClientFactory(handler);
        var service = new BaleNotificationService(
            clients, state, NullLogger<BaleNotificationService>.Instance);
        var profile = ServerProfileParser.ParseOne(
            "vless://00000000-0000-0000-0000-000000000111@edge.example:443?security=tls#Test");

        await service.NotifyStatusChangeAsync(profile, 1, "Unstable", "Disconnected", CancellationToken.None);
        await service.NotifyStatusChangeAsync(profile, 1, "Disconnected", "Connected", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/bot123456789:TestToken_abcdefghijklmnopqrstuvwxyz/sendMessage", handler.Requests[0].Path);
        Assert.Equal("/bot123456789:TestToken_abcdefghijklmnopqrstuvwxyz/sendMessage", handler.Requests[1].Path);
        using var disconnected = JsonDocument.Parse(handler.Requests[0].Body);
        using var recovered = JsonDocument.Parse(handler.Requests[1].Body);
        Assert.Equal("no 1 red", disconnected.RootElement.GetProperty("text").GetString());
        Assert.Equal(1234567890123L, disconnected.RootElement.GetProperty("chat_id").GetInt64());
        Assert.Equal("no 1 green", recovered.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task BaleNotificationService_DoesNotSendWhenDisabled_AndDoesNotThrowOnApiFailure()
    {
        using var env = new TestEnvironment();
        var state = new SqliteServerStateRepository(env.Options, env.Protector);
        await state.InitializeAsync();
        await state.SaveBaleNotificationSettingsAsync(new BaleNotificationSettings
        {
            Enabled = false,
            BotToken = "123456789:TestToken_abcdefghijklmnopqrstuvwxyz",
            ChatId = "1234567890123"
        });

        var handler = new RecordingBaleHandler();
        var service = new BaleNotificationService(
            new TestHttpClientFactory(handler), state, NullLogger<BaleNotificationService>.Instance);
        var profile = ServerProfileParser.ParseOne(
            "vless://00000000-0000-0000-0000-000000000112@edge.example:443?security=tls#Test");

        await service.NotifyStatusChangeAsync(profile, 2, "Connected", "Disconnected", CancellationToken.None);
        Assert.Empty(handler.Requests);

        await state.SaveBaleNotificationSettingsAsync(new BaleNotificationSettings
        {
            Enabled = true,
            BotToken = "123456789:TestToken_abcdefghijklmnopqrstuvwxyz",
            ChatId = "1234567890123"
        });
        handler.RespondWithFailure = true;

        // Notification failures are intentionally swallowed so monitoring continues.
        await service.NotifyStatusChangeAsync(profile, 2, "Connected", "Disconnected", CancellationToken.None);
        Assert.Single(handler.Requests);
    }

    private sealed class TestHttpClientFactory(RecordingBaleHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _client = new(handler) { BaseAddress = new Uri("https://tapi.bale.ai/") };
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class RecordingBaleHandler : HttpMessageHandler
    {
        public List<(string Path, string Body)> Requests { get; } = [];
        public bool RespondWithFailure { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.RequestUri?.PathAndQuery ?? "", body));
            return RespondWithFailure
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        "{\\"ok\\":false,\\"description\\":\\"test failure\\"}", Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\\"ok\\":true,\\"result\\":{}}", Encoding.UTF8, "application/json")
                };
        }
    }

    [Fact]
    public async Task ProfileStatusSummary_CountsStatusesWithoutDecryptingProfilePayloads()
    {
        using var env = new TestEnvironment();
        var profiles = new SqliteProfileRepository(env.Options, env.Protector);
        await profiles.InitializeAsync();

        var rows = new[]
        {
            ServerProfileParser.ParseOne("vless://00000000-0000-0000-0000-000000000101@one.example:443?security=tls#One"),
            ServerProfileParser.ParseOne("vless://00000000-0000-0000-0000-000000000102@two.example:443?security=tls#Two"),
            ServerProfileParser.ParseOne("vless://00000000-0000-0000-0000-000000000103@three.example:443?security=tls#Three")
        };

        var imported = await profiles.ImportAsync(rows);
        Assert.Equal(3, imported.Imported);

        rows[0].Status = "Connected";
        rows[1].Status = "Disconnected";
        rows[2].Status = "Unstable";
        foreach (var profile in rows)
            await profiles.UpdateAsync(profile);

        var summary = await profiles.GetStatusSummaryAsync();

        Assert.Equal((3, 1, 1, 1), summary);
    }

    [Theory]
    [InlineData(1, "Disconnected", "no 1 red")]
    [InlineData(2, "Disconnected", "no 2 red")]
    [InlineData(1, "Connected", "no 1 green")]
    [InlineData(12, "Connected", "no 12 green")]
    public void BaleNotificationMessageFormatter_UsesOnlyNumberAndColor(
        int number, string status, string expected)
    {
        Assert.Equal(expected, BaleNotificationMessageFormatter.Format(number, status));
    }

    [Theory]
    [InlineData(0, "Connected")]
    [InlineData(1, "Unstable")]
    public void BaleNotificationMessageFormatter_RejectsInvalidNumberOrStatus(
        int number, string status)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BaleNotificationMessageFormatter.Format(number, status));
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

        Assert.Equal(0, await state.CountSubscriptionsAsync());

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
        Assert.Equal(1, await state.CountSubscriptionsAsync());
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
