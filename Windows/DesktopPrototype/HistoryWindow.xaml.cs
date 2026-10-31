using System.Globalization;
using System.Windows;

namespace V2rayWatch.DesktopPrototype;

public partial class HistoryWindow : Window
{
    private readonly Guid _profileId;
    private readonly LongTermHistoryStore _historyStore;
    private readonly bool _english;
    private bool _sevenDays;

    public HistoryWindow(
        string profileName,
        Guid profileId,
        LongTermHistoryStore historyStore,
        bool english)
    {
        InitializeComponent();
        _profileId = profileId;
        _historyStore = historyStore;
        _english = english;
        FlowDirection = english ? System.Windows.FlowDirection.LeftToRight : System.Windows.FlowDirection.RightToLeft;
        ProfileNameText.Text = profileName;
        ApplyLanguage();
        RefreshHistory();
    }

    private void Period24_Click(object sender, RoutedEventArgs e)
    {
        _sevenDays = false;
        RefreshHistory();
    }

    private void Period7_Click(object sender, RoutedEventArgs e)
    {
        _sevenDays = true;
        RefreshHistory();
    }

    private void ApplyLanguage()
    {
        Title = _english ? "V2rayWatch — Long-term history" : "V2rayWatch — تاریخچهٔ بلندمدت";
        WindowTitle.Text = _english ? "Long-term connection history" : "تاریخچهٔ بلندمدت اتصال";
        Period24Button.Content = _english ? "Last 24 hours" : "۲۴ ساعت گذشته";
        Period7Button.Content = _english ? "Last 7 days" : "۷ روز گذشته";
        SuccessRateLabel.Text = _english ? "Successful checks" : "درصد بررسی‌های موفق";
        TotalChecksLabel.Text = _english ? "Total checks" : "تعداد کل بررسی‌ها";
        AverageDelayLabel.Text = _english ? "Average delay" : "میانگین تأخیر";
        FailedChecksLabel.Text = _english ? "Failed checks" : "بررسی ناموفق";
        TimelineTitle.Text = _english ? "Connection timeline" : "نمودار وضعیت اتصال";
        SuccessLegend.Text = _english ? "Successful" : "موفق";
        FailureLegend.Text = _english ? "Failed" : "ناموفق";
        NoDataLegend.Text = _english ? "No data" : "بدون داده";
        DetailsText.Text = _english ? "Additional details" : "جزئیات بیشتر";
        DelayRangeText.Text = "";
        LongestFailureText.Text = "";
        DataScopeText.Text = _english
            ? "Only automatic monitoring checks are included. Manual one-off tests are not recorded."
            : "فقط بررسی‌های پایش خودکار در این گزارش ثبت می‌شوند؛ تست‌های دستیِ تکی در تاریخچه وارد نمی‌شوند.";
    }

    private void RefreshHistory()
    {
        var endUtc = DateTimeOffset.UtcNow;
        var startUtc = endUtc.AddDays(_sevenDays ? -7 : -1);
        var samples = _historyStore.ReadSamples(_profileId, startUtc, endUtc);
        var summary = LongTermHistoryAnalytics.Analyze(samples, startUtc, endUtc, _sevenDays, _english);

        Period24Button.FontWeight = _sevenDays ? FontWeights.Normal : FontWeights.Bold;
        Period7Button.FontWeight = _sevenDays ? FontWeights.Bold : FontWeights.Normal;
        TimelineHint.Text = _sevenDays
            ? (_english ? "Each bar represents one day. The numbers below show the number of checks." : "هر ستون نمایانگر یک روز است؛ عدد زیر هر ستون تعداد بررسی‌هاست.")
            : (_english ? "Each bar represents one hour. The numbers below show the number of checks." : "هر ستون نمایانگر یک ساعت است؛ عدد زیر هر ستون تعداد بررسی‌هاست.");

        SuccessRateValue.Text = summary.SuccessRatePercent.HasValue
            ? summary.SuccessRatePercent.Value.ToString("F1", CultureInfo.InvariantCulture) + "%"
            : "—";
        TotalChecksValue.Text = summary.TotalChecks.ToString(CultureInfo.InvariantCulture);
        AverageDelayValue.Text = summary.AverageDelayMs.HasValue
            ? Math.Round(summary.AverageDelayMs.Value).ToString("F0", CultureInfo.InvariantCulture) + " ms"
            : "—";
        FailedChecksValue.Text = summary.FailureCount.ToString(CultureInfo.InvariantCulture);
        TimelineItems.ItemsSource = summary.Timeline;

        if (summary.MinimumDelayMs.HasValue && summary.MaximumDelayMs.HasValue)
        {
            DelayRangeText.Text = _english
                ? $"Successful delay range: {summary.MinimumDelayMs.Value}–{summary.MaximumDelayMs.Value} ms"
                : $"بازهٔ تأخیر در بررسی‌های موفق: {summary.MinimumDelayMs.Value} تا {summary.MaximumDelayMs.Value} میلی‌ثانیه";
        }
        else
        {
            DelayRangeText.Text = _english
                ? "No successful delay measurements in this period."
                : "در این بازه تأخیر موفقی برای اندازه‌گیری ثبت نشده است.";
        }

        if (summary.FailureCount == 0)
        {
            LongestFailureText.Text = _english
                ? "No failed checks were recorded in this period."
                : "در این بازه بررسی ناموفقی ثبت نشده است.";
        }
        else if (summary.LongestObservedFailureSpan > TimeSpan.Zero)
        {
            LongestFailureText.Text = _english
                ? $"Longest observed failure run: {summary.LongestConsecutiveFailures} check(s), {FormatDuration(summary.LongestObservedFailureSpan, true)}."
                : $"طولانی‌ترین رشتهٔ خطای مشاهده‌شده: {summary.LongestConsecutiveFailures} بررسی، به مدت {FormatDuration(summary.LongestObservedFailureSpan, false)}.";
        }
        else
        {
            LongestFailureText.Text = _english
                ? $"Longest observed failure run: {summary.LongestConsecutiveFailures} check(s). The exact outage duration could not be measured."
                : $"طولانی‌ترین رشتهٔ خطای مشاهده‌شده: {summary.LongestConsecutiveFailures} بررسی؛ مدت قطعی دقیق قابل‌اندازه‌گیری نیست.";
        }

        NoDataText.Text = summary.TotalChecks == 0
            ? (_english
                ? "No monitoring data for this period. Start monitoring manually to begin collecting history."
                : "برای این بازه داده‌ای وجود ندارد. برای ثبت تاریخچه، پایش را به‌صورت دستی فعال کن.")
            : "";
    }

    private static string FormatDuration(TimeSpan value, bool english)
    {
        var totalHours = (int)value.TotalHours;
        var minutes = value.Minutes;
        var seconds = value.Seconds;
        if (english)
        {
            if (totalHours > 0) return $"{totalHours}h {minutes}m";
            if (minutes > 0) return $"{minutes}m {seconds}s";
            return $"{seconds}s";
        }

        if (totalHours > 0) return $"{totalHours} ساعت و {minutes} دقیقه";
        if (minutes > 0) return $"{minutes} دقیقه و {seconds} ثانیه";
        return $"{seconds} ثانیه";
    }
}
