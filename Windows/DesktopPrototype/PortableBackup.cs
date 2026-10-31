using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace V2rayWatch.DesktopPrototype;

/// <summary>
/// Portable backup format shared with Android EncryptedBackupCodec.
/// The existing Windows DPAPI data store remains unchanged and device-local.
/// </summary>
public static class PortableBackupCodec
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("V2VWBAK1");
    private const byte FormatVersion = 1;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int HeaderSize = 8 + 1 + 4 + SaltSize + NonceSize;
    private const int ExportIterations = 600_000;
    private const int MinimumIterations = 300_000;
    private const int MaximumIterations = 1_000_000;

    public const int MaximumBackupBytes = 25 * 1024 * 1024;
    public const int MinimumPassphraseLength = 12;
    public const int MaximumPassphraseLength = 256;

    public static byte[] Encrypt(byte[] plaintext, char[] passphrase)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ValidatePassphrase(passphrase);
        if (plaintext.Length is < 1 or > MaximumBackupBytes)
            throw new InvalidDataException("Backup content is empty or too large.");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var header = new byte[HeaderSize];
        Magic.CopyTo(header, 0);
        header[8] = FormatVersion;
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(9, 4), ExportIterations);
        salt.CopyTo(header, 13);
        nonce.CopyTo(header, 13 + SaltSize);

        var passwordBytes = Encoding.UTF8.GetBytes(passphrase);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            passwordBytes, salt, ExportIterations, HashAlgorithmName.SHA256, 32);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, header);
            var output = new byte[header.Length + ciphertext.Length + tag.Length];
            header.CopyTo(output, 0);
            ciphertext.CopyTo(output, header.Length);
            tag.CopyTo(output, header.Length + ciphertext.Length);
            if (output.Length > MaximumBackupBytes)
                throw new InvalidDataException("Encrypted backup is too large.");
            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(ciphertext);
            CryptographicOperations.ZeroMemory(tag);
        }
    }

    public static byte[] Decrypt(byte[] backup, char[] passphrase)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ValidatePassphrase(passphrase);
        if (backup.Length < HeaderSize + TagSize || backup.Length > MaximumBackupBytes)
            throw new InvalidDataException("Backup file is truncated or too large.");

        var header = backup.AsSpan(0, HeaderSize);
        if (!header[..Magic.Length].SequenceEqual(Magic))
            throw new InvalidDataException("This is not a V2rayWatch backup file.");
        if (header[8] != FormatVersion)
            throw new InvalidDataException("This backup format version is not supported.");

        var iterations = BinaryPrimitives.ReadInt32BigEndian(header.Slice(9, 4));
        if (iterations is < MinimumIterations or > MaximumIterations)
            throw new InvalidDataException("Invalid backup encryption parameters.");

        var salt = header.Slice(13, SaltSize).ToArray();
        var nonce = header.Slice(13 + SaltSize, NonceSize).ToArray();
        var ciphertextLength = backup.Length - HeaderSize - TagSize;
        if (ciphertextLength <= 0)
            throw new InvalidDataException("Backup file does not contain any data.");

        var passwordBytes = Encoding.UTF8.GetBytes(passphrase);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            passwordBytes, salt, iterations, HashAlgorithmName.SHA256, 32);
        var plaintext = new byte[ciphertextLength];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(
                nonce,
                backup.AsSpan(HeaderSize, ciphertextLength),
                backup.AsSpan(HeaderSize + ciphertextLength, TagSize),
                plaintext,
                header);
            return plaintext;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new CryptographicException("Wrong passphrase or damaged backup file.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(nonce);
        }
    }

    private static void ValidatePassphrase(char[] passphrase)
    {
        ArgumentNullException.ThrowIfNull(passphrase);
        if (passphrase.Length is < MinimumPassphraseLength or > MaximumPassphraseLength)
            throw new ArgumentException(
                $"Use a backup passphrase with {MinimumPassphraseLength} to {MaximumPassphraseLength} characters.");
    }
}

public sealed class AndroidBackupPayload
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    [JsonPropertyName("createdAtEpochMs")]
    public long CreatedAtEpochMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonPropertyName("profiles")]
    public List<AndroidBackupProfile> Profiles { get; set; } = [];

    [JsonPropertyName("settings")]
    public AndroidBackupSettings Settings { get; set; } = new();
}

public sealed class AndroidBackupProfile
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("protocol")] public string Protocol { get; set; } = "";
    [JsonPropertyName("host")] public string Host { get; set; } = "";
    [JsonPropertyName("port")] public int Port { get; set; }
    [JsonPropertyName("rawLink")] public string RawLink { get; set; } = "";
    [JsonPropertyName("configJson")] public string ConfigJson { get; set; } = "";
    [JsonPropertyName("state")] public string State { get; set; } = "UNKNOWN";
    [JsonPropertyName("delayMs")] public long DelayMs { get; set; } = -1;
    [JsonPropertyName("lastCheckedAt")] public long LastCheckedAt { get; set; }
    [JsonPropertyName("consecutiveFailures")] public int ConsecutiveFailures { get; set; }
    [JsonPropertyName("lastError")] public string LastError { get; set; } = "";
    [JsonPropertyName("disconnectedSinceEpochMs")] public long? DisconnectedSinceEpochMs { get; set; }
    [JsonPropertyName("uptimeHistory")] public List<string> UptimeHistory { get; set; } = [];
}

public sealed class AndroidBackupSettings
{
    [JsonPropertyName("intervalSeconds")] public int IntervalSeconds { get; set; } = 20;
    [JsonPropertyName("failureThreshold")] public int FailureThreshold { get; set; } = 2;
    [JsonPropertyName("primaryTestUrl")] public string PrimaryTestUrl { get; set; } = "https://www.google.com/generate_204";
    [JsonPropertyName("fallbackTestUrl")] public string FallbackTestUrl { get; set; } = "https://www.gstatic.com/generate_204";
    [JsonPropertyName("notifyOnDisconnect")] public bool NotifyOnDisconnect { get; set; } = true;
    [JsonPropertyName("notifyOnRecovery")] public bool NotifyOnRecovery { get; set; } = true;
}

public sealed record PortableBackupRestoreResult(
    IReadOnlyList<ServerProfile> Profiles,
    MonitorSettings Settings,
    int AddedProfiles,
    int SkippedProfiles);

public static class PortableBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        MaxDepth = 64
    };

    public static byte[] CreateBackup(
        IEnumerable<ServerProfile> profiles,
        MonitorSettings settings,
        char[] passphrase)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(settings);

        var payload = new AndroidBackupPayload
        {
            Profiles = profiles.Select(ToAndroidProfile).ToList(),
            Settings = new AndroidBackupSettings
            {
                // Android currently enforces 20..3600 seconds and 1..5 failures.
                // Clamp only the portable copy; keep the Windows local settings intact.
                IntervalSeconds = Math.Clamp(settings.IntervalSeconds, 20, 3600),
                FailureThreshold = Math.Clamp(settings.ConsecutiveFailureThreshold, 1, 5),
                PrimaryTestUrl = settings.PrimaryTestUrl,
                FallbackTestUrl = settings.FallbackTestUrl,
                NotifyOnDisconnect = settings.NotifyOnDisconnect,
                NotifyOnRecovery = settings.NotifyOnRecovery
            }
        };

        ValidatePayload(payload);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        try
        {
            return PortableBackupCodec.Encrypt(plaintext, passphrase);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public static PortableBackupRestoreResult RestoreBackup(
        byte[] encryptedBackup,
        char[] passphrase,
        IEnumerable<ServerProfile> existingProfiles)
    {
        ArgumentNullException.ThrowIfNull(existingProfiles);
        var plaintext = PortableBackupCodec.Decrypt(encryptedBackup, passphrase);
        AndroidBackupPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<AndroidBackupPayload>(plaintext, JsonOptions)
                ?? throw new InvalidDataException("Backup data is empty.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Backup data is invalid or unsupported.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        ValidatePayload(payload);
        var settings = new MonitorSettings
        {
            IntervalSeconds = payload.Settings.IntervalSeconds,
            ConsecutiveFailureThreshold = payload.Settings.FailureThreshold,
            PrimaryTestUrl = payload.Settings.PrimaryTestUrl,
            FallbackTestUrl = payload.Settings.FallbackTestUrl,
            NotifyOnDisconnect = payload.Settings.NotifyOnDisconnect,
            NotifyOnRecovery = payload.Settings.NotifyOnRecovery
        };

        var merged = existingProfiles.ToList();
        var knownLinks = merged.Select(p => p.RawUri).ToHashSet(StringComparer.Ordinal);
        var knownIds = merged.Select(p => p.Id).ToHashSet();
        var added = 0;
        var skipped = 0;

        foreach (var item in payload.Profiles)
        {
            if (!knownLinks.Add(item.RawLink))
            {
                skipped++;
                continue;
            }

            var id = Guid.TryParse(item.Id, out var parsedId) && !knownIds.Contains(parsedId)
                ? parsedId
                : Guid.NewGuid();
            knownIds.Add(id);

            var history = item.UptimeHistory
                .TakeLast(20)
                .Select(mark => mark switch
                {
                    "CONNECTED" => "G",
                    "DISCONNECTED" => "R",
                    _ => "N"
                })
                .ToList();
            while (history.Count < 20) history.Insert(0, "N");

            merged.Add(new ServerProfile
            {
                Id = id,
                Name = item.Name,
                Protocol = item.Protocol,
                Host = item.Host,
                Port = item.Port,
                RawUri = item.RawLink,
                ConfigJson = item.ConfigJson,
                SourceSubscriptionId = "",
                ImportWarning = "",
                Status = "Unknown",
                ConsecutiveFailures = 0,
                DelayMs = null,
                LastCheckedUtc = null,
                UptimeHistory = history,
                AddedAt = DateTimeOffset.UtcNow
            });
            added++;
        }

        return new PortableBackupRestoreResult(merged, settings, added, skipped);
    }

    private static AndroidBackupProfile ToAndroidProfile(ServerProfile profile)
    {
        return new AndroidBackupProfile
        {
            Id = profile.Id.ToString("D"),
            Name = profile.Name,
            Protocol = profile.Protocol,
            Host = profile.Host,
            Port = profile.Port,
            RawLink = profile.RawUri,
            ConfigJson = profile.ConfigJson,
            State = "UNKNOWN",
            DelayMs = -1,
            LastCheckedAt = 0,
            ConsecutiveFailures = 0,
            LastError = "",
            DisconnectedSinceEpochMs = null,
            UptimeHistory = (profile.UptimeHistory ?? [])
                .TakeLast(20)
                .Select(mark => mark switch
                {
                    "G" => "CONNECTED",
                    "R" => "DISCONNECTED",
                    _ => "UNKNOWN"
                })
                .ToList()
        };
    }

    private static void ValidatePayload(AndroidBackupPayload payload)
    {
        if (payload.FormatVersion != 1)
            throw new InvalidDataException("This backup data version is not supported.");
        if (payload.Profiles is null || payload.Profiles.Count > 5_000)
            throw new InvalidDataException("Backup contains too many profiles.");
        if (payload.Settings is null)
            throw new InvalidDataException("Backup settings are missing.");

        var settings = payload.Settings;
        if (settings.IntervalSeconds is < 20 or > 3600)
            throw new InvalidDataException("Backup monitoring interval is invalid.");
        if (settings.FailureThreshold is < 1 or > 5)
            throw new InvalidDataException("Backup failure threshold is invalid.");
        if (!IsHttps(settings.PrimaryTestUrl) ||
            (!string.IsNullOrWhiteSpace(settings.FallbackTestUrl) && !IsHttps(settings.FallbackTestUrl)))
            throw new InvalidDataException("Backup test URLs must use HTTPS.");

        foreach (var profile in payload.Profiles)
        {
            if (profile is null ||
                string.IsNullOrWhiteSpace(profile.Id) ||
                string.IsNullOrWhiteSpace(profile.Name) ||
                string.IsNullOrWhiteSpace(profile.Protocol) ||
                string.IsNullOrWhiteSpace(profile.RawLink) ||
                string.IsNullOrWhiteSpace(profile.ConfigJson) ||
                profile.Port is < 0 or > 65535 ||
                profile.ConfigJson.Length > 2 * 1024 * 1024 ||
                profile.RawLink.Length > 256 * 1024 ||
                profile.UptimeHistory is null)
                throw new InvalidDataException("Backup contains an incomplete or oversized profile.");

            try
            {
                using var config = JsonDocument.Parse(profile.ConfigJson, new JsonDocumentOptions { MaxDepth = 64 });
                if (config.RootElement.ValueKind != JsonValueKind.Object)
                    throw new JsonException();
            }
            catch (JsonException)
            {
                throw new InvalidDataException("Backup contains an invalid profile configuration.");
            }
        }
    }

    private static bool IsHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
