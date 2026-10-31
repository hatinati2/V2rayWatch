using System.Buffers.Binary;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace V2rayWatch.DesktopPrototype;

/// <summary>
/// Stores monitoring samples only when the user has manually started the monitor.
/// Samples are compacted into five-minute SQLite rows and older than seven days are pruned.
/// The BLOB keeps the timestamp offset, outcome, and delay for each individual probe.
/// </summary>
public sealed class LongTermHistoryStore : IDisposable
{
    private const int BucketSeconds = 5 * 60;
    private const long BucketMilliseconds = BucketSeconds * 1000L;
    private const int SampleRecordBytes = 5;
    private const ushort MissingDelay = ushort.MaxValue;
    private readonly object _sync = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public LongTermHistoryStore(string? databasePath = null)
    {
        var path = databasePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "V2rayWatch");
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "history.sqlite");
        }
        else
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            path = fullPath;
        }

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        }.ToString());
        _connection.Open();
        Execute("""
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            CREATE TABLE IF NOT EXISTS history_buckets (
                profile_id TEXT NOT NULL,
                session_id TEXT NOT NULL,
                bucket_start_ms INTEGER NOT NULL,
                outcomes BLOB NOT NULL,
                PRIMARY KEY (profile_id, session_id, bucket_start_ms)
            );
            CREATE INDEX IF NOT EXISTS ix_history_profile_bucket
                ON history_buckets(profile_id, bucket_start_ms);
            """);
    }

    public void RecordProbe(
        Guid profileId,
        Guid sessionId,
        DateTimeOffset timestampUtc,
        bool success,
        long? delayMs)
    {
        if (profileId == Guid.Empty) throw new ArgumentException("Profile ID is required.", nameof(profileId));
        if (sessionId == Guid.Empty) throw new ArgumentException("Monitoring session ID is required.", nameof(sessionId));
        ThrowIfDisposed();

        var timestampMs = timestampUtc.ToUniversalTime().ToUnixTimeMilliseconds();
        var bucketStartMs = timestampMs / BucketMilliseconds * BucketMilliseconds;
        var offsetSeconds = checked((ushort)Math.Clamp((timestampMs - bucketStartMs) / 1000L, 0, BucketSeconds - 1));
        var compactDelay = success && delayMs.HasValue
            ? checked((ushort)Math.Clamp(delayMs.Value, 0, MissingDelay - 1))
            : MissingDelay;
        var record = new byte[SampleRecordBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(0, 2), offsetSeconds);
        record[2] = success ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(3, 2), compactDelay);

        lock (_sync)
        {
            ThrowIfDisposed();
            using var transaction = _connection.BeginTransaction();
            byte[]? existing = null;
            using (var read = _connection.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = """
                    SELECT outcomes FROM history_buckets
                    WHERE profile_id = $profile AND session_id = $session AND bucket_start_ms = $bucket;
                    """;
                read.Parameters.AddWithValue("$profile", profileId.ToString("N"));
                read.Parameters.AddWithValue("$session", sessionId.ToString("N"));
                read.Parameters.AddWithValue("$bucket", bucketStartMs);
                var value = read.ExecuteScalar();
                if (value is byte[] bytes) existing = bytes;
            }

            var combined = new byte[(existing?.Length ?? 0) + record.Length];
            if (existing is not null) Buffer.BlockCopy(existing, 0, combined, 0, existing.Length);
            Buffer.BlockCopy(record, 0, combined, combined.Length - record.Length, record.Length);

            using (var write = _connection.CreateCommand())
            {
                write.Transaction = transaction;
                write.CommandText = """
                    INSERT INTO history_buckets(profile_id, session_id, bucket_start_ms, outcomes)
                    VALUES($profile, $session, $bucket, $outcomes)
                    ON CONFLICT(profile_id, session_id, bucket_start_ms)
                    DO UPDATE SET outcomes = excluded.outcomes;
                    """;
                write.Parameters.AddWithValue("$profile", profileId.ToString("N"));
                write.Parameters.AddWithValue("$session", sessionId.ToString("N"));
                write.Parameters.AddWithValue("$bucket", bucketStartMs);
                write.Parameters.AddWithValue("$outcomes", combined);
                write.ExecuteNonQuery();
            }
            transaction.Commit();
        }
    }

    public IReadOnlyList<LongTermHistorySample> ReadSamples(
        Guid profileId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        if (toUtc < fromUtc) throw new ArgumentOutOfRangeException(nameof(toUtc));
        ThrowIfDisposed();
        var fromMs = fromUtc.ToUniversalTime().ToUnixTimeMilliseconds();
        var toMs = toUtc.ToUniversalTime().ToUnixTimeMilliseconds();
        var firstBucket = fromMs / BucketMilliseconds * BucketMilliseconds;
        var lastBucket = toMs / BucketMilliseconds * BucketMilliseconds;
        var samples = new List<LongTermHistorySample>();

        lock (_sync)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT session_id, bucket_start_ms, outcomes
                FROM history_buckets
                WHERE profile_id = $profile
                  AND bucket_start_ms >= $first
                  AND bucket_start_ms <= $last
                ORDER BY bucket_start_ms ASC, session_id ASC;
                """;
            command.Parameters.AddWithValue("$profile", profileId.ToString("N"));
            command.Parameters.AddWithValue("$first", firstBucket);
            command.Parameters.AddWithValue("$last", lastBucket);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var sessionId = Guid.ParseExact(reader.GetString(0), "N");
                var bucketStartMs = reader.GetInt64(1);
                var outcomes = (byte[])reader[2];
                if (outcomes.Length % SampleRecordBytes != 0)
                    throw new InvalidDataException("Long-term history database contains an invalid sample record.");

                for (var offset = 0; offset < outcomes.Length; offset += SampleRecordBytes)
                {
                    var secondOffset = BinaryPrimitives.ReadUInt16LittleEndian(outcomes.AsSpan(offset, 2));
                    if (secondOffset >= BucketSeconds)
                        throw new InvalidDataException("Long-term history database contains an invalid timestamp.");
                    var sampleMs = bucketStartMs + secondOffset * 1000L;
                    if (sampleMs < fromMs || sampleMs > toMs) continue;

                    var success = outcomes[offset + 2] == 1;
                    var delay = BinaryPrimitives.ReadUInt16LittleEndian(outcomes.AsSpan(offset + 3, 2));
                    samples.Add(new LongTermHistorySample(
                        profileId,
                        sessionId,
                        DateTimeOffset.FromUnixTimeMilliseconds(sampleMs),
                        success,
                        success && delay != MissingDelay ? delay : null));
                }
            }
        }

        return samples.OrderBy(sample => sample.TimestampUtc).ToList();
    }

    public void PruneExpired(DateTimeOffset cutoffUtc)
    {
        ThrowIfDisposed();
        var cutoffMs = cutoffUtc.ToUniversalTime().ToUnixTimeMilliseconds();
        var firstRetainedBucket = cutoffMs / BucketMilliseconds * BucketMilliseconds;
        lock (_sync)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM history_buckets WHERE bucket_start_ms < $cutoff;";
            command.Parameters.AddWithValue("$cutoff", firstRetainedBucket);
            command.ExecuteNonQuery();
        }
    }

    public void DeleteProfile(Guid profileId)
    {
        ThrowIfDisposed();
        lock (_sync)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM history_buckets WHERE profile_id = $profile;";
            command.Parameters.AddWithValue("$profile", profileId.ToString("N"));
            command.ExecuteNonQuery();
        }
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _connection.Dispose();
        }
    }
}

public sealed record LongTermHistorySample(
    Guid ProfileId,
    Guid SessionId,
    DateTimeOffset TimestampUtc,
    bool Success,
    long? DelayMs);

public sealed record LongTermHistoryTimelineBucket(
    string Label,
    int SuccessCount,
    int FailureCount,
    double SuccessHeight,
    double FailureHeight,
    string ToolTip)
{
    public int TotalChecks => SuccessCount + FailureCount;
}

public sealed record LongTermHistorySummary(
    int TotalChecks,
    int SuccessCount,
    int FailureCount,
    double? SuccessRatePercent,
    double? AverageDelayMs,
    long? MinimumDelayMs,
    long? MaximumDelayMs,
    TimeSpan LongestObservedFailureSpan,
    int LongestConsecutiveFailures,
    IReadOnlyList<LongTermHistoryTimelineBucket> Timeline);

public static class LongTermHistoryAnalytics
{
    private const double TimelineHeight = 88d;

    public static LongTermHistorySummary Analyze(
        IEnumerable<LongTermHistorySample> source,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        bool sevenDays,
        bool english)
    {
        if (toUtc <= fromUtc) throw new ArgumentOutOfRangeException(nameof(toUtc));
        var samples = source
            .Where(sample => sample.TimestampUtc >= fromUtc && sample.TimestampUtc <= toUtc)
            .OrderBy(sample => sample.TimestampUtc)
            .ToList();

        var successes = samples.Where(sample => sample.Success).ToList();
        var failures = samples.Count - successes.Count;
        var delays = successes.Where(sample => sample.DelayMs.HasValue).Select(sample => sample.DelayMs!.Value).ToList();
        var timelineCount = sevenDays ? 7 : 24;
        var bucketWidth = (toUtc - fromUtc).Ticks / timelineCount;
        var timeline = new List<LongTermHistoryTimelineBucket>(timelineCount);

        for (var index = 0; index < timelineCount; index++)
        {
            var bucketStart = fromUtc.AddTicks(bucketWidth * index);
            var bucketEnd = index == timelineCount - 1 ? toUtc : fromUtc.AddTicks(bucketWidth * (index + 1));
            var inBucket = samples.Where(sample =>
                sample.TimestampUtc >= bucketStart &&
                (index == timelineCount - 1 ? sample.TimestampUtc <= bucketEnd : sample.TimestampUtc < bucketEnd))
                .ToList();
            var good = inBucket.Count(sample => sample.Success);
            var bad = inBucket.Count - good;
            var label = sevenDays
                ? bucketStart.ToLocalTime().ToString("ddd dd", english ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("fa-IR"))
                : bucketStart.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
            var goodHeight = inBucket.Count == 0 ? 0 : TimelineHeight * good / inBucket.Count;
            var badHeight = inBucket.Count == 0 ? 0 : TimelineHeight * bad / inBucket.Count;
            var tooltip = inBucket.Count == 0
                ? (english ? $"{label}: no monitoring data" : $"{label}: داده‌ای ثبت نشده")
                : (english
                    ? $"{label}: {good} successful, {bad} failed checks"
                    : $"{label}: {good} بررسی موفق، {bad} بررسی ناموفق");
            timeline.Add(new LongTermHistoryTimelineBucket(label, good, bad, goodHeight, badHeight, tooltip));
        }

        var longestSpan = TimeSpan.Zero;
        var longestStreak = 0;
        Guid? currentSession = null;
        DateTimeOffset? failureStart = null;
        DateTimeOffset? lastFailure = null;
        var currentStreak = 0;

        void FinishFailureRun(DateTimeOffset end)
        {
            if (failureStart.HasValue)
            {
                var span = end - failureStart.Value;
                if (span > longestSpan) longestSpan = span;
            }
            longestStreak = Math.Max(longestStreak, currentStreak);
            failureStart = null;
            lastFailure = null;
            currentStreak = 0;
        }

        foreach (var sample in samples)
        {
            if (currentSession.HasValue && currentSession.Value != sample.SessionId)
            {
                if (lastFailure.HasValue) FinishFailureRun(lastFailure.Value);
                else FinishFailureRun(sample.TimestampUtc);
            }
            currentSession = sample.SessionId;

            if (!sample.Success)
            {
                failureStart ??= sample.TimestampUtc;
                lastFailure = sample.TimestampUtc;
                currentStreak++;
            }
            else if (failureStart.HasValue)
            {
                FinishFailureRun(sample.TimestampUtc);
            }
        }
        if (lastFailure.HasValue) FinishFailureRun(lastFailure.Value);

        return new LongTermHistorySummary(
            samples.Count,
            successes.Count,
            failures,
            samples.Count == 0 ? null : successes.Count * 100d / samples.Count,
            delays.Count == 0 ? null : delays.Average(),
            delays.Count == 0 ? null : delays.Min(),
            delays.Count == 0 ? null : delays.Max(),
            longestSpan,
            longestStreak,
            timeline);
    }
}

