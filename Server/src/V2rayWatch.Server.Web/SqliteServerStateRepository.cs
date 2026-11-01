using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Web;

/// <summary>
/// Stores monitor preferences, encrypted subscription sources, and automatic
/// check history alongside the encrypted profile database.
/// </summary>
public sealed class SqliteServerStateRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;
    private readonly ProfileSecretProtector _protector;

    public SqliteServerStateRepository(ServerOptions options, ProfileSecretProtector protector)
    {
        Directory.CreateDirectory(options.DataDirectory);
        var path = Path.Combine(options.DataDirectory, "profiles.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        _protector = protector;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS ServerSettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Payload TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Subscriptions (
                Id TEXT PRIMARY KEY NOT NULL,
                Payload BLOB NOT NULL
            );
            CREATE TABLE IF NOT EXISTS HistoryChecks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProfileId TEXT NOT NULL,
                SessionId TEXT NOT NULL,
                CheckedAtUtc TEXT NOT NULL,
                Success INTEGER NOT NULL,
                DelayMs INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS IX_History_Profile_Time
                ON HistoryChecks(ProfileId, CheckedAtUtc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ServerAppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Payload FROM ServerSettings WHERE Id=1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string json) return new ServerAppSettings();
        return JsonSerializer.Deserialize<ServerAppSettings>(json, JsonOptions) ?? new ServerAppSettings();
    }

    public async Task SaveSettingsAsync(ServerAppSettings settings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ServerSettings(Id, Payload) VALUES(1, $payload)
            ON CONFLICT(Id) DO UPDATE SET Payload=excluded.Payload;
            """;
        command.Parameters.AddWithValue("$payload", json);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionSource>> ListSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<SubscriptionSource>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Payload FROM Subscriptions;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(DeserializeSubscription((byte[])reader["Payload"]));
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<SubscriptionSource?> GetSubscriptionAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Payload FROM Subscriptions WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is byte[] encrypted ? DeserializeSubscription(encrypted) : null;
    }

    public async Task<(int Imported, int Ignored, int Owned)> SaveSubscriptionAsync(
        SubscriptionSource source,
        IReadOnlyCollection<ServerProfile> fetchedProfiles,
        SqliteProfileRepository profilesRepository,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source.Id)) throw new ArgumentException("Subscription ID is required.");
        var fetched = fetchedProfiles.ToList();
        var imported = 0;
        var ignored = 0;

        if (replaceExisting)
        {
            var all = await profilesRepository.ListAsync(cancellationToken).ConfigureAwait(false);
            var previous = all.Where(p => string.Equals(p.SourceSubscriptionId, source.Id, StringComparison.Ordinal)).ToList();
            var previousByUri = previous.ToDictionary(p => p.RawUri, StringComparer.Ordinal);
            var retainedIds = new HashSet<Guid>();

            foreach (var profile in fetched)
            {
                profile.SourceSubscriptionId = source.Id;
                if (!previousByUri.TryGetValue(profile.RawUri, out var existing)) continue;
                profile.Id = existing.Id;
                profile.AddedAt = existing.AddedAt;
                profile.Status = existing.Status;
                profile.DelayMs = existing.DelayMs;
                profile.LastCheckedUtc = existing.LastCheckedUtc;
                profile.ConsecutiveFailures = existing.ConsecutiveFailures;
                profile.UptimeHistory = existing.UptimeHistory?.ToList() ?? [];
                retainedIds.Add(existing.Id);
                await profilesRepository.UpdateAsync(profile, cancellationToken).ConfigureAwait(false);
            }

            foreach (var old in previous.Where(p => !retainedIds.Contains(p.Id)))
            {
                await profilesRepository.DeleteAsync(old.Id, cancellationToken).ConfigureAwait(false);
                await DeleteProfileHistoryAsync(old.Id, cancellationToken).ConfigureAwait(false);
            }

            var additions = fetched.Where(p => !retainedIds.Contains(p.Id)).ToList();
            if (additions.Count > 0)
            {
                var result = await profilesRepository.ImportAsync(additions, cancellationToken).ConfigureAwait(false);
                imported += result.Imported;
                ignored += result.Ignored;
            }
        }
        else
        {
            foreach (var profile in fetched) profile.SourceSubscriptionId = source.Id;
            if (fetched.Count > 0)
            {
                var result = await profilesRepository.ImportAsync(fetched, cancellationToken).ConfigureAwait(false);
                imported = result.Imported;
                ignored = result.Ignored;
            }
        }

        var savedProfiles = await profilesRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        source.ProfileCount = savedProfiles.Count(p =>
            string.Equals(p.SourceSubscriptionId, source.Id, StringComparison.Ordinal));
        source.LastUpdatedUtc = DateTimeOffset.UtcNow;
        await SaveSubscriptionRecordAsync(source, cancellationToken).ConfigureAwait(false);
        return (imported, ignored, source.ProfileCount);
    }

    public async Task<bool> DeleteSubscriptionAsync(
        string id,
        SqliteProfileRepository profilesRepository,
        CancellationToken cancellationToken = default)
    {
        var source = await GetSubscriptionAsync(id, cancellationToken).ConfigureAwait(false);
        if (source is null) return false;

        var profiles = await profilesRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var profile in profiles.Where(p =>
            string.Equals(p.SourceSubscriptionId, id, StringComparison.Ordinal)))
        {
            await profilesRepository.DeleteAsync(profile.Id, cancellationToken).ConfigureAwait(false);
            await DeleteProfileHistoryAsync(profile.Id, cancellationToken).ConfigureAwait(false);
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Subscriptions WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task RecordHistoryAsync(
        Guid profileId,
        Guid sessionId,
        DateTimeOffset timestampUtc,
        bool success,
        long? delayMs,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO HistoryChecks(ProfileId, SessionId, CheckedAtUtc, Success, DelayMs)
            VALUES($profile, $session, $checked, $success, $delay);
            """;
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$session", sessionId.ToString("D"));
        command.Parameters.AddWithValue("$checked", timestampUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$success", success ? 1 : 0);
        command.Parameters.AddWithValue("$delay", (object?)delayMs ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-7).ToString("O");
        await using var prune = connection.CreateCommand();
        prune.Transaction = transaction;
        prune.CommandText = "DELETE FROM HistoryChecks WHERE CheckedAtUtc < $cutoff;";
        prune.Parameters.AddWithValue("$cutoff", cutoff);
        await prune.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ServerHistorySample>> GetHistoryAsync(
        Guid profileId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken = default)
    {
        var result = new List<ServerHistorySample>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CheckedAtUtc, Success, DelayMs
            FROM HistoryChecks
            WHERE ProfileId=$profile AND CheckedAtUtc >= $from
            ORDER BY CheckedAtUtc ASC;
            """;
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        command.Parameters.AddWithValue("$from", fromUtc.ToUniversalTime().ToString("O"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ServerHistorySample(
                DateTimeOffset.Parse(reader.GetString(0), System.Globalization.CultureInfo.InvariantCulture),
                reader.GetInt32(1) == 1,
                reader.IsDBNull(2) ? null : reader.GetInt64(2)));
        }
        return result;
    }

    public async Task RefreshSubscriptionCountsAsync(
        SqliteProfileRepository profilesRepository,
        CancellationToken cancellationToken = default)
    {
        var profiles = await profilesRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        var sources = await ListSubscriptionsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var source in sources)
        {
            var count = profiles.Count(p =>
                string.Equals(p.SourceSubscriptionId, source.Id, StringComparison.Ordinal));
            if (source.ProfileCount == count) continue;
            source.ProfileCount = count;
            await SaveSubscriptionRecordAsync(source, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task DeleteProfileHistoryAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM HistoryChecks WHERE ProfileId=$profile;";
        command.Parameters.AddWithValue("$profile", profileId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SaveSubscriptionRecordAsync(SubscriptionSource source, CancellationToken cancellationToken)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(source, JsonOptions);
        var encrypted = _protector.Protect(clear);
        CryptographicOperations.ZeroMemory(clear);
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Subscriptions(Id, Payload) VALUES($id, $payload)
                ON CONFLICT(Id) DO UPDATE SET Payload=excluded.Payload;
                """;
            command.Parameters.AddWithValue("$id", source.Id);
            command.Parameters.AddWithValue("$payload", encrypted);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    private SubscriptionSource DeserializeSubscription(byte[] encrypted)
    {
        var clear = _protector.Unprotect(encrypted);
        try
        {
            return JsonSerializer.Deserialize<SubscriptionSource>(clear, JsonOptions)
                ?? throw new InvalidDataException("Invalid subscription record.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
        }
    }
}
