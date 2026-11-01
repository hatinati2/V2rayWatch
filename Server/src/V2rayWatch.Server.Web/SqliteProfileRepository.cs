using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Web;

public sealed class SqliteProfileRepository
{
    public const int MaximumProfiles = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;
    private readonly ProfileSecretProtector _protector;

    public SqliteProfileRepository(ServerOptions options, ProfileSecretProtector protector)
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
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS Profiles (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL,
                Protocol TEXT NOT NULL,
                Host TEXT NOT NULL,
                Port INTEGER NOT NULL,
                Enabled INTEGER NOT NULL,
                RawUriHash TEXT NOT NULL UNIQUE,
                Payload BLOB NOT NULL,
                AddedAtUtc TEXT NOT NULL,
                Status TEXT NOT NULL DEFAULT 'Unknown',
                DelayMs INTEGER NULL,
                LastCheckedUtc TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Profiles_Name ON Profiles(Name COLLATE NOCASE);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(int Imported, int Ignored, int Total)> ImportAsync(
        IReadOnlyCollection<ServerProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var totalCommand = connection.CreateCommand();
        totalCommand.Transaction = transaction;
        totalCommand.CommandText = "SELECT COUNT(*) FROM Profiles;";
        var total = Convert.ToInt32(await totalCommand.ExecuteScalarAsync(cancellationToken));
        var imported = 0;
        var ignored = 0;

        foreach (var profile in profiles)
        {
            if (total >= MaximumProfiles)
            {
                ignored++;
                continue;
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(profile, JsonOptions);
            var protectedPayload = _protector.Protect(payload);
            CryptographicOperations.ZeroMemory(payload);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile.RawUri)));

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO Profiles
                (Id, Name, Protocol, Host, Port, Enabled, RawUriHash, Payload, AddedAtUtc, Status, DelayMs, LastCheckedUtc)
                VALUES ($id, $name, $protocol, $host, $port, $enabled, $hash, $payload, $added, $status, $delay, $checked);
                """;
            command.Parameters.AddWithValue("$id", profile.Id.ToString("D"));
            command.Parameters.AddWithValue("$name", profile.Name);
            command.Parameters.AddWithValue("$protocol", profile.Protocol);
            command.Parameters.AddWithValue("$host", profile.Host);
            command.Parameters.AddWithValue("$port", profile.Port);
            command.Parameters.AddWithValue("$enabled", profile.Status != "Disabled" ? 1 : 0);
            command.Parameters.AddWithValue("$hash", hash);
            command.Parameters.AddWithValue("$payload", protectedPayload);
            command.Parameters.AddWithValue("$added", profile.AddedAt.ToUniversalTime().ToString("O"));
            command.Parameters.AddWithValue("$status", profile.Status);
            command.Parameters.AddWithValue("$delay", (object?)profile.DelayMs ?? DBNull.Value);
            command.Parameters.AddWithValue("$checked", (object?)profile.LastCheckedUtc?.ToUniversalTime().ToString("O") ?? DBNull.Value);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 1) { imported++; total++; }
            else ignored++;
            CryptographicOperations.ZeroMemory(protectedPayload);
        }

        await transaction.CommitAsync(cancellationToken);
        return (imported, ignored, total);
    }

    public async Task<IReadOnlyList<ServerProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ServerProfile>();
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Payload FROM Profiles ORDER BY Name COLLATE NOCASE, AddedAtUtc;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(Deserialize((byte[])reader["Payload"]));
        return result;
    }

    public async Task<ServerProfile?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Payload FROM Profiles WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is byte[] payload ? Deserialize(payload) : null;
    }

    public async Task UpdateAsync(ServerProfile profile, CancellationToken cancellationToken = default)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(profile, JsonOptions);
        var encrypted = _protector.Protect(clear);
        CryptographicOperations.ZeroMemory(clear);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile.RawUri)));

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Profiles SET Name=$name, Protocol=$protocol, Host=$host, Port=$port,
            Enabled=$enabled, RawUriHash=$hash, Payload=$payload, Status=$status,
            DelayMs=$delay, LastCheckedUtc=$checked WHERE Id=$id;
            """;
        command.Parameters.AddWithValue("$id", profile.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", profile.Name);
        command.Parameters.AddWithValue("$protocol", profile.Protocol);
        command.Parameters.AddWithValue("$host", profile.Host);
        command.Parameters.AddWithValue("$port", profile.Port);
        command.Parameters.AddWithValue("$enabled", profile.Status == "Disabled" ? 0 : 1);
        command.Parameters.AddWithValue("$hash", hash);
        command.Parameters.AddWithValue("$payload", encrypted);
        command.Parameters.AddWithValue("$status", profile.Status);
        command.Parameters.AddWithValue("$delay", (object?)profile.DelayMs ?? DBNull.Value);
        command.Parameters.AddWithValue("$checked", (object?)profile.LastCheckedUtc?.ToUniversalTime().ToString("O") ?? DBNull.Value);
        command.ExecuteNonQuery();
        CryptographicOperations.ZeroMemory(encrypted);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Profiles WHERE Id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private ServerProfile Deserialize(byte[] encrypted)
    {
        var clear = _protector.Unprotect(encrypted);
        try { return JsonSerializer.Deserialize<ServerProfile>(clear, JsonOptions) ?? throw new InvalidDataException("Invalid profile record."); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
}
