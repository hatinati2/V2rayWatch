using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Microsoft.Win32;

namespace V2rayWatch.DesktopPrototype;

public partial class MainWindow : Window
{
    private readonly ServerProfileStore _store = new();
    private readonly SubscriptionClient _subscriptionClient = new();
    private readonly XrayDelayTester _tester = new();
    private readonly AppData _data;
    private readonly ObservableCollection<ServerProfile> _profiles;
    private readonly ObservableCollection<SubscriptionSource> _subscriptions;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _monitorCancellation;
    private Task? _monitorTask;
    private Forms.NotifyIcon? _tray;
    private bool _english;
    private bool _busy;
    private bool _closeCompleted;
    private bool _storageUnavailable;

    public MainWindow()
    {
        InitializeComponent();
        try { _data = _store.Load(); }
        catch (Exception ex)
        {
            _storageUnavailable = true;
            MessageBox.Show(ex.Message + Environment.NewLine + "فایل قبلی بازنویسی نمی‌شود تا اطلاعاتت از بین نرود.", "V2rayWatch", MessageBoxButton.OK, MessageBoxImage.Error);
            _data = new AppData();
        }
        _data.Settings ??= new MonitorSettings();
        _data.Profiles ??= [];
        _data.Subscriptions ??= [];

        foreach (var profile in _data.Profiles.ToList())
        {
            if (string.IsNullOrWhiteSpace(profile.ConfigJson) && !string.IsNullOrWhiteSpace(profile.RawUri))
            {
                try
                {
                    var parsed = ServerProfileParser.ParseOne(profile.RawUri);
                    profile.ConfigJson = parsed.ConfigJson;
                    profile.Protocol = parsed.Protocol;
                    profile.Host = parsed.Host;
                    profile.Port = parsed.Port;
                    if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = parsed.Name;
                }
                catch { profile.Status = "NotTestable"; }
            }
            NormalizeHistory(profile);
            profile.English = _data.English;
        }

        _profiles = new ObservableCollection<ServerProfile>(_data.Profiles);
        _subscriptions = new ObservableCollection<SubscriptionSource>(_data.Subscriptions);
        ProfilesGrid.ItemsSource = _profiles;
        SubscriptionsGrid.ItemsSource = _subscriptions;
        _english = _data.English;
        ApplyLanguage();
        LoadSettingsToControls();
        RefreshSummary();
        SaveData();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _tray = new Forms.NotifyIcon { Icon = Drawing.SystemIcons.Application, Visible = true, Text = "V2rayWatch — Link Monitor" };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(_english ? "Open V2rayWatch" : "بازکردن V2rayWatch", null, (_, _) =>
            Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); }));
        menu.Items.Add(_english ? "Exit" : "خروج", null, (_, _) => Dispatcher.Invoke(Close));
        _tray.ContextMenuStrip = menu;
        if (_data.MonitorEnabled) StartMonitoring();
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closeCompleted) return;
        e.Cancel = true;
        _data.MonitorEnabled = false;
        _monitorCancellation?.Cancel();
        _lifetime.Cancel();
        try { if (_monitorTask is not null) await _monitorTask; } catch (OperationCanceledException) { }
        SaveData();
        _tray?.Dispose();
        _tray = null;
        _closeCompleted = true;
        Close();
    }

    private async void AddLinks_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddProfilesWindow { Owner = this };
        dialog.LinksTab.IsSelected = true;
        if (dialog.ShowDialog() == true && !dialog.IsSubscription)
            await ImportLinksAsync(dialog.InputValue);
    }

    private async void AddSubscription_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddProfilesWindow { Owner = this };
        dialog.SubscriptionTab.IsSelected = true;
        if (dialog.ShowDialog() == true && dialog.IsSubscription)
            await AddSubscriptionAsync(dialog.InputValue);
    }

    private async Task ImportLinksAsync(string text)
    {
        if (_busy) return;
        SetBusy(true, _english ? "Parsing proxy links…" : "در حال پردازش لینک‌ها…");
        try
        {
            var parsed = await Task.Run(() => ServerProfileParser.ParseMany(text));
            SearchInput.Clear();
            var existing = _profiles.Select(p => p.RawUri).ToHashSet(StringComparer.Ordinal);
            var added = 0;
            foreach (var profile in parsed)
            {
                if (!existing.Add(profile.RawUri)) continue;
                NormalizeHistory(profile);
                profile.English = _english;
                _profiles.Add(profile);
                added++;
            }
            SaveData();
            RefreshSummary();
            SetStatus(_english ? $"{added} profile(s) added; {parsed.Count - added} duplicate(s) skipped." : $"{added} پروفایل اضافه شد؛ {parsed.Count - added} مورد تکراری نادیده گرفته شد.", false);
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
        finally { SetBusy(false); }
    }

    private async Task AddSubscriptionAsync(string url)
    {
        var existingSource = _subscriptions.FirstOrDefault(source => string.Equals(source.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existingSource is not null)
        {
            await UpdateSubscriptionAsync(existingSource);
            return;
        }
        if (_busy) return;
        SetBusy(true, _english ? "Downloading and parsing subscription…" : "در حال دریافت و استخراج لینک‌های اشتراک…");
        try
        {
            var parsed = await _subscriptionClient.FetchProfilesAsync(url, _lifetime.Token);
            SearchInput.Clear();
            var existing = _profiles.Select(p => p.RawUri).ToHashSet(StringComparer.Ordinal);
            var source = new SubscriptionSource
            {
                Url = url,
                Name = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "Subscription"
            };
            var added = 0;
            foreach (var profile in parsed)
            {
                if (!existing.Add(profile.RawUri)) continue;
                profile.SourceSubscriptionId = source.Id;
                NormalizeHistory(profile);
                profile.English = _english;
                _profiles.Add(profile);
                added++;
            }
            source.ProfileCount = _profiles.Count(p => p.SourceSubscriptionId == source.Id);
            source.LastUpdatedUtc = DateTimeOffset.UtcNow;
            _subscriptions.Add(source);
            SaveData();
            RefreshSummary();
            SetStatus(_english
                ? $"Subscription downloaded: {parsed.Count} link(s) found, {added} added, {parsed.Count - added} duplicate(s) skipped."
                : $"اشتراک دریافت شد: {parsed.Count} لینک پیدا شد، {added} لینک اضافه شد و {parsed.Count - added} تکراری نادیده گرفته شد.", false);
        }
        catch (OperationCanceledException) { SetStatus(_english ? "Subscription download cancelled." : "دریافت اشتراک لغو شد.", true); }
        catch (Exception ex) { SetStatus((_english ? "Subscription import failed: " : "دریافت اشتراک ناموفق بود: ") + ex.Message, true); }
        finally { SetBusy(false); }
    }

    private async Task UpdateSubscriptionAsync(SubscriptionSource source)
    {
        if (_busy) return;
        SetBusy(true, _english ? $"Updating {source.Name}…" : $"در حال به‌روزرسانی {source.Name}…");
        try
        {
            var fetched = await _subscriptionClient.FetchProfilesAsync(source.Url, _lifetime.Token);
            var fetchedByRaw = fetched.ToDictionary(p => p.RawUri, StringComparer.Ordinal);
            var oldSourceProfiles = _profiles.Where(p => p.SourceSubscriptionId == source.Id).ToList();
            var oldByRaw = oldSourceProfiles.ToDictionary(p => p.RawUri, StringComparer.Ordinal);
            var allOtherRaw = _profiles.Where(p => p.SourceSubscriptionId != source.Id).Select(p => p.RawUri).ToHashSet(StringComparer.Ordinal);
            var updated = 0;
            var added = 0;

            foreach (var incoming in fetched)
            {
                if (oldByRaw.TryGetValue(incoming.RawUri, out var existingSourceProfile))
                {
                    existingSourceProfile.Name = incoming.Name;
                    existingSourceProfile.Protocol = incoming.Protocol;
                    existingSourceProfile.Host = incoming.Host;
                    existingSourceProfile.Port = incoming.Port;
                    existingSourceProfile.ConfigJson = incoming.ConfigJson;
                    NormalizeHistory(existingSourceProfile);
                    updated++;
                }
                else if (!allOtherRaw.Contains(incoming.RawUri))
                {
                    incoming.SourceSubscriptionId = source.Id;
                    NormalizeHistory(incoming);
                    incoming.English = _english;
                    _profiles.Add(incoming);
                    added++;
                }
            }
            foreach (var obsolete in oldSourceProfiles.Where(p => !fetchedByRaw.ContainsKey(p.RawUri)).ToList())
                _profiles.Remove(obsolete);

            source.ProfileCount = _profiles.Count(p => p.SourceSubscriptionId == source.Id);
            source.LastUpdatedUtc = DateTimeOffset.UtcNow;
            SaveData();
            RefreshSummary();
            SetStatus(_english
                ? $"{source.Name}: {fetched.Count} fetched, {added} added, {updated} updated. Manual profiles were preserved."
                : $"{source.Name}: {fetched.Count} لینک دریافت شد؛ {added} جدید، {updated} به‌روزرسانی شد. لینک‌های دستی حفظ شدند.", false);
        }
        catch (OperationCanceledException) { SetStatus(_english ? "Subscription update cancelled." : "به‌روزرسانی اشتراک لغو شد.", true); }
        catch (Exception ex) { SetStatus((_english ? "Subscription update failed: " : "به‌روزرسانی اشتراک ناموفق بود: ") + ex.Message, true); }
        finally { SetBusy(false); }
    }

    private async void UpdateSubscription_Click(object sender, RoutedEventArgs e)
    {
        if (SubscriptionsGrid.SelectedItem is SubscriptionSource source) await UpdateSubscriptionAsync(source);
        else SetStatus(_english ? "Select a subscription first." : "ابتدا یک اشتراک را انتخاب کن.", true);
    }

    private async void UpdateAllSubscriptions_Click(object sender, RoutedEventArgs e)
    {
        foreach (var source in _subscriptions.ToList()) await UpdateSubscriptionAsync(source);
    }

    private void RemoveSubscription_Click(object sender, RoutedEventArgs e)
    {
        if (SubscriptionsGrid.SelectedItem is not SubscriptionSource source) return;
        var answer = MessageBox.Show(
            _english ? $"Remove subscription '{source.Name}' and the profiles imported from it?" : $"اشتراک «{source.Name}» و پروفایل‌هایی که از آن وارد شده‌اند حذف شوند؟",
            "V2rayWatch", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        foreach (var profile in _profiles.Where(p => p.SourceSubscriptionId == source.Id).ToList()) _profiles.Remove(profile);
        _subscriptions.Remove(source);
        SaveData();
        RefreshSummary();
        SetStatus(_english ? "Subscription source removed." : "منبع اشتراک حذف شد.", false);
    }

    private async void TestSelected_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesGrid.SelectedItem is not ServerProfile profile)
        {
            SetStatus(_english ? "Select a profile first." : "ابتدا یک پروفایل را انتخاب کن.", true);
            return;
        }
        await TestProfileAsync(profile, false, _lifetime.Token);
    }

    private async void TestAll_Click(object sender, RoutedEventArgs e)
    {
        if (_profiles.Count == 0) { SetStatus(_english ? "No profiles to test." : "پروفایلی برای تست وجود ندارد.", true); return; }
        SetBusy(true, _english ? "Testing all profiles independently…" : "در حال تست مستقل همهٔ پروفایل‌ها…");
        try
        {
            await Parallel.ForEachAsync(_profiles.ToList(), new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = _lifetime.Token },
                async (profile, token) => await TestProfileAsync(profile, false, token));
        }
        catch (OperationCanceledException) { }
        finally { SetBusy(false); }
    }

    private async Task TestProfileAsync(ServerProfile profile, bool fromMonitor, CancellationToken cancellationToken)
    {
        var previousStatus = profile.Status;
        await Dispatcher.InvokeAsync(() => { profile.Status = "Checking"; ProfilesGrid.Items.Refresh(); });
        DelayTestResult result;
        try
        {
            result = await _tester.TestAsync(profile, _data.Settings, cancellationToken);
        }
        catch
        {
            await Dispatcher.InvokeAsync(() => { profile.Status = previousStatus; ProfilesGrid.Items.Refresh(); });
            throw;
        }
        await Dispatcher.InvokeAsync(() =>
        {
            if (!_profiles.Contains(profile)) return;
            ApplyTestResult(profile, result, fromMonitor, previousStatus);
            RefreshSummary();
            if (!fromMonitor)
            {
                SaveData();
                SetStatus(result.Success
                    ? (_english ? $"{profile.Name}: {result.DelayMs} ms" : $"{profile.Name}: {result.DelayMs} میلی‌ثانیه")
                    : (_english ? $"{profile.Name}: test failed — {result.Error}" : $"{profile.Name}: تست ناموفق — {result.Error}"), !result.Success);
            }
        });
    }

    private void ApplyTestResult(ServerProfile profile, DelayTestResult result, bool fromMonitor, string previousStatus)
    {
        profile.LastCheckedUtc = DateTimeOffset.UtcNow;
        if (result.Success)
        {
            profile.DelayMs = result.DelayMs;
            profile.ConsecutiveFailures = 0;
            profile.Status = "Connected";
        }
        else
        {
            profile.DelayMs = null;
            if (fromMonitor)
            {
                profile.ConsecutiveFailures++;
                profile.Status = profile.ConsecutiveFailures < _data.Settings.ConsecutiveFailureThreshold
                    ? "Unstable" : "Disconnected";
            }
            else
            {
                profile.Status = "Disconnected";
            }
        }

        if (fromMonitor)
        {
            profile.UptimeHistory ??= [];
            profile.UptimeHistory.Add(result.Success ? "G" : "R");
            while (profile.UptimeHistory.Count > 20) profile.UptimeHistory.RemoveAt(0);
            if (profile.Status == "Disconnected" && previousStatus != "Disconnected")
                Notify(profile.Name, _english ? "Profile disconnected" : "قطع اتصال پروفایل", true);
            else if (result.Success && previousStatus == "Disconnected")
                Notify(profile.Name, _english ? "Connection recovered" : "اتصال بازیابی شد", false);
        }
        ProfilesGrid.Items.Refresh();
    }

    private async void Monitor_Click(object sender, RoutedEventArgs e)
    {
        if (_monitorCancellation is null) StartMonitoring();
        else await StopMonitoringAsync();
    }

    private void StartMonitoring()
    {
        if (_monitorCancellation is not null) return;
        if (_profiles.Count == 0) { SetStatus(_english ? "Add profiles before starting monitoring." : "قبل از شروع پایش، لینک‌ها را اضافه کن.", true); return; }
        _monitorCancellation = new CancellationTokenSource();
        _data.MonitorEnabled = true;
        SaveData();
        MonitorButton.Content = _english ? "Stop monitoring" : "توقف پایش خودکار";
        SetStatus(_english ? "Automatic monitoring started." : "پایش خودکار شروع شد.", false);
        _monitorTask = MonitorLoopAsync(_monitorCancellation.Token);
    }

    private async Task StopMonitoringAsync()
    {
        var cts = _monitorCancellation;
        if (cts is null) return;
        _monitorCancellation = null;
        cts.Cancel();
        try { if (_monitorTask is not null) await _monitorTask; } catch (OperationCanceledException) { }
        cts.Dispose();
        _data.MonitorEnabled = false;
        SaveData();
        MonitorButton.Content = _english ? "Start monitoring" : "شروع پایش خودکار";
        SetStatus(_english ? "Automatic monitoring stopped." : "پایش خودکار متوقف شد.", false);
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var started = Stopwatch.StartNew();
            var snapshot = _profiles.ToList();
            try
            {
                await Parallel.ForEachAsync(snapshot, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = cancellationToken },
                    async (profile, token) => await TestProfileAsync(profile, true, token));
                await Dispatcher.InvokeAsync(SaveData);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }

            var remaining = TimeSpan.FromSeconds(_data.Settings.IntervalSeconds) - started.Elapsed;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
        }
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TimeoutInput.Text.Trim(), out var timeout) || timeout is < 3 or > 60)
        {
            SetStatus(_english ? "Timeout must be between 3 and 60 seconds." : "مهلت تست باید بین ۳ تا ۶۰ ثانیه باشد.", true);
            return;
        }
        var primary = PrimaryUrlInput.Text.Trim();
        var fallback = FallbackUrlInput.Text.Trim();
        if (!IsHttps(primary) || (fallback.Length > 0 && !IsHttps(fallback)))
        {
            SetStatus(_english ? "Test URLs must use HTTPS." : "آدرس‌های تست باید HTTPS باشند.", true);
            return;
        }
        _data.Settings.IntervalSeconds = GetComboValue(IntervalCombo, 20);
        _data.Settings.ConsecutiveFailureThreshold = GetComboValue(ThresholdCombo, 2);
        _data.Settings.TimeoutSeconds = timeout;
        _data.Settings.PrimaryTestUrl = primary;
        _data.Settings.FallbackTestUrl = fallback;
        SaveData();
        SetStatus(_english ? "Monitoring settings saved." : "تنظیمات پایش ذخیره شد.", false);
        if (_monitorCancellation is not null)
        {
            await StopMonitoringAsync();
            StartMonitoring();
        }
    }

    private void ImportClipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText()) { SetStatus(_english ? "Clipboard is empty." : "Clipboard خالی است.", true); return; }
            _ = ImportLinksAsync(Clipboard.GetText());
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "V2rayWatch links and configs|*.txt;*.conf;*.json|All files|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try { await ImportLinksAsync(await File.ReadAllTextAsync(dialog.FileName)); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesGrid.SelectedItem is not ServerProfile profile) return;
        var answer = MessageBox.Show(_english ? $"Delete profile '{profile.Name}'?" : $"پروفایل «{profile.Name}» حذف شود؟",
            "V2rayWatch", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        _profiles.Remove(profile);
        SaveData();
        RefreshSummary();
        SetStatus(_english ? "Profile removed." : "پروفایل حذف شد.", false);
    }

    private void ProfilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ProfilesGrid.SelectedItem is ServerProfile;
        TestSelectedButton.IsEnabled = selected && !_busy;
        RemoveSelectedButton.IsEnabled = selected && !_busy;
    }

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ProfilesGrid is null || SearchInput is null) return;
        var query = SearchInput.Text.Trim();
        ProfilesGrid.ItemsSource = query.Length == 0 ? _profiles : _profiles.Where(p =>
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Host.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Protocol.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        _english = !_english;
        _data.English = _english;
        ApplyLanguage();
        RefreshSummary();
        SaveData();
    }

    private void ApplyLanguage()
    {
        FlowDirection = _english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        foreach (var profile in _profiles) profile.English = _english;
        LanguageButton.Content = _english ? "EN / FA" : "FA / EN";
        HeaderSubtitle.Text = _english ? "Real proxy profile testing and monitoring" : "پایش و تست واقعی لینک‌های پروکسی";
        AddLinksButton.Content = _english ? "Add links" : "افزودن لینک";
        AddSubscriptionButton.Content = _english ? "Add subscription" : "افزودن Subscription";
        TestAllButton.Content = _english ? "Test all links" : "تست همه لینک‌ها";
        MonitorButton.Content = _monitorCancellation is null ? (_english ? "Start monitoring" : "شروع پایش خودکار") : (_english ? "Stop monitoring" : "توقف پایش خودکار");
        TotalLabel.Text = _english ? "All profiles" : "کل پروفایل‌ها";
        ConnectedLabel.Text = _english ? "Connected" : "متصل";
        DisconnectedLabel.Text = _english ? "Disconnected" : "قطع";
        SubscriptionCountLabel.Text = _english ? "Subscriptions" : "اشتراک‌ها";
        ProfilesTitle.Text = _english ? "Profiles and test results" : "پروفایل‌ها و نتیجهٔ تست";
        ProfilesHint.Text = _english ? "Each profile is tested independently of Windows proxy and VPN settings." : "هر پروفایل مستقل از پراکسی و تنظیمات شبکهٔ ویندوز تست می‌شود.";
        TestSelectedButton.Content = _english ? "Test selected" : "تست انتخاب‌شده";
        RemoveSelectedButton.Content = _english ? "Remove" : "حذف";
        GridFooter.Text = _english ? "Manual tests do not change automatic monitoring history." : "تست دستی، تاریخچهٔ پایش خودکار را تغییر نمی‌دهد.";
        ImportClipboardButton.Content = _english ? "Paste from clipboard" : "چسباندن از Clipboard";
        ImportFileButton.Content = _english ? "Import file" : "ورود از فایل";
        MonitorSettingsTitle.Text = _english ? "Monitoring settings" : "تنظیمات پایش";
        IntervalLabel.Text = _english ? "Check interval" : "فاصلهٔ بررسی";
        ThresholdLabel.Text = _english ? "Consecutive failures before disconnect" : "تعداد خطای متوالی تا قطع";
        TimeoutLabel.Text = _english ? "Test timeout (seconds)" : "مهلت هر تست (ثانیه)";
        PrimaryUrlLabel.Text = _english ? "Primary test URL" : "آدرس تست اصلی";
        FallbackUrlLabel.Text = _english ? "Fallback URL" : "آدرس جایگزین";
        SaveSettingsButton.Content = _english ? "Save settings" : "ذخیره تنظیمات";
        SubscriptionsTitle.Text = _english ? "Subscriptions" : "مدیریت اشتراک‌ها";
        SubscriptionsHint.Text = _english ? "Subscription sources are saved and can be updated individually." : "منابع Subscription ذخیره می‌شوند و هرکدام را می‌توان جداگانه به‌روزرسانی کرد.";
        UpdateSubscriptionButton.Content = _english ? "Update selected" : "به‌روزرسانی اشتراک انتخاب‌شده";
        UpdateAllSubscriptionsButton.Content = _english ? "Update all subscriptions" : "به‌روزرسانی همه اشتراک‌ها";
        RemoveSubscriptionButton.Content = _english ? "Remove subscription" : "حذف منبع اشتراک";
        ProfilesGrid.Columns[0].Header = _english ? "Profile" : "نام پروفایل";
        ProfilesGrid.Columns[1].Header = _english ? "Protocol" : "پروتکل";
        ProfilesGrid.Columns[2].Header = _english ? "Host" : "میزبان";
        ProfilesGrid.Columns[3].Header = _english ? "Status" : "وضعیت";
        ProfilesGrid.Columns[4].Header = _english ? "Delay" : "تأخیر";
        ProfilesGrid.Columns[5].Header = _english ? "Last check" : "آخرین بررسی";
        StatusText.Text = _english
            ? "Ready; each link is tested independently of Windows VPN and proxy settings."
            : "آماده؛ تست هر لینک مستقل از VPN و پراکسی سیستم انجام می‌شود.";
        RefreshSummary();
    }

    private void LoadSettingsToControls()
    {
        SetComboValue(IntervalCombo, _data.Settings.IntervalSeconds);
        SetComboValue(ThresholdCombo, _data.Settings.ConsecutiveFailureThreshold);
        TimeoutInput.Text = _data.Settings.TimeoutSeconds.ToString();
        PrimaryUrlInput.Text = _data.Settings.PrimaryTestUrl;
        FallbackUrlInput.Text = _data.Settings.FallbackTestUrl;
    }

    private static int GetComboValue(ComboBox combo, int fallback) =>
        combo.SelectedValue is object value && int.TryParse(value.ToString(), out var number) ? number : fallback;

    private static void SetComboValue(ComboBox combo, int value)
    {
        foreach (ComboBoxItem item in combo.Items)
            if (int.TryParse(item.Tag?.ToString(), out var parsed) && parsed == value) { combo.SelectedItem = item; return; }
        combo.SelectedIndex = 0;
    }

    private static bool IsHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        AddLinksButton.IsEnabled = !busy;
        AddSubscriptionButton.IsEnabled = !busy;
        TestAllButton.IsEnabled = !busy;
        MonitorButton.IsEnabled = !busy || _monitorCancellation is not null;
        TestSelectedButton.IsEnabled = !busy && ProfilesGrid.SelectedItem is ServerProfile;
        RemoveSelectedButton.IsEnabled = !busy && ProfilesGrid.SelectedItem is ServerProfile;
        UpdateSubscriptionButton.IsEnabled = !busy;
        UpdateAllSubscriptionsButton.IsEnabled = !busy;
        if (message is not null) SetStatus(message, false);
    }

    private static void NormalizeHistory(ServerProfile profile)
    {
        profile.UptimeHistory ??= [];
        if (profile.UptimeHistory.Count > 20)
            profile.UptimeHistory = profile.UptimeHistory.TakeLast(20).ToList();
        while (profile.UptimeHistory.Count < 20)
            profile.UptimeHistory.Insert(0, "N");
    }

    private void RefreshSummary()
    {
        TotalCount.Text = _profiles.Count.ToString();
        ConnectedCount.Text = _profiles.Count(p => p.Status == "Connected").ToString();
        DisconnectedCount.Text = _profiles.Count(p => p.Status == "Disconnected").ToString();
        SubscriptionCount.Text = _subscriptions.Count.ToString();
        ProfilesGrid.Items.Refresh();
        SubscriptionsGrid.Items.Refresh();
    }

    private void SetStatus(string message, bool error)
    {
        StatusText.Text = message;
        StatusText.Foreground = error ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.LightGreen;
    }

    private void SaveData()
    {
        if (_storageUnavailable) return;
        _data.Profiles = _profiles.ToList();
        _data.Subscriptions = _subscriptions.ToList();
        _data.English = _english;
        try { _store.Save(_data); }
        catch (Exception ex) { SetStatus((_english ? "Could not save local data: " : "ذخیره اطلاعات محلی ناموفق بود: ") + ex.Message, true); }
    }

    private void Notify(string profileName, string message, bool warning)
    {
        try { _tray?.ShowBalloonTip(5000, $"V2rayWatch — {message}", profileName, warning ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info); }
        catch { }
    }
}
