using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Web;

public sealed class ServerAppSettings
{
    public MonitorSettings Settings { get; set; } = new();
    public bool MonitoringEnabled { get; set; }

    public void Validate()
    {
        if (Settings.IntervalSeconds is < 1 or > 9999)
            throw new InvalidOperationException("فاصلهٔ پایش باید بین ۱ تا ۹۹۹۹ ثانیه باشد.");
        if (Settings.ConsecutiveFailureThreshold is < 1 or > 9999)
            throw new InvalidOperationException("تعداد شکست متوالی باید بین ۱ تا ۹۹۹۹ باشد.");
        if (Settings.TimeoutSeconds is < 3 or > 60)
            throw new InvalidOperationException("مهلت هر تست باید بین ۳ تا ۶۰ ثانیه باشد.");
        if (!IsHttps(Settings.PrimaryTestUrl) ||
            (!string.IsNullOrWhiteSpace(Settings.FallbackTestUrl) && !IsHttps(Settings.FallbackTestUrl)))
            throw new InvalidOperationException("آدرس‌های تست باید HTTPS باشند.");
    }

    private static bool IsHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}

public sealed record ServerHistorySample(
    DateTimeOffset TimestampUtc,
    bool Success,
    long? DelayMs);

public sealed record SubscriptionRequest(string? Name, string? Url);
public sealed record MonitoringRequest(bool Enabled);
