using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Web;

public sealed class ServerAppSettings
{
    public MonitorSettings Settings { get; set; } = new();
    public bool MonitoringEnabled { get; set; }
}

public sealed record ServerHistorySample(
    DateTimeOffset TimestampUtc,
    bool Success,
    long? DelayMs);
