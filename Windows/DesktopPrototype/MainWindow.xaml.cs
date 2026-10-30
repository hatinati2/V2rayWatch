using System.Collections.ObjectModel;
using System.Windows;

namespace V2rayWatch.DesktopPrototype;

public partial class MainWindow : Window
{
    private bool _english;
    private readonly ServerProfileStore _profileStore = new();
    private readonly ObservableCollection<ServerProfile> _profiles;

    public MainWindow()
    {
        InitializeComponent();
        _profiles = new ObservableCollection<ServerProfile>(_profileStore.Load());
        RefreshServerSummary();
    }

    private void RefreshServerSummary()
    {
        ServerSummaryText.Text = _profiles.Count == 0
            ? (_english ? "No servers imported yet." : "هنوز سروری اضافه نشده است")
            : (_english
                ? $"{_profiles.Count} server(s) saved. Open Servers to manage profiles."
                : $"{_profiles.Count} سرور ذخیره شده است؛ برای مدیریت وارد بخش سرورها شوید.");
    }

    private void OpenServerManager()
    {
        var dialog = new ServerManagerWindow(_profiles) { Owner = this };
        dialog.ShowDialog();
        RefreshServerSummary();
        if (dialog.SelectedProfile is not null)
        {
            ConnectionState.Text = _english
                ? $"Selected: {dialog.SelectedProfile.Name} (engine not connected)"
                : $"انتخاب‌شده: {dialog.SelectedProfile.Name} (موتور متصل نیست)";
        }
    }

    private void ShowSection(string fa, string en)
    {
        PageTitle.Text = _english ? en : fa;
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) => ShowSection("داشبورد", "Dashboard");
    private void Servers_Click(object sender, RoutedEventArgs e) => OpenServerManager();
    private void ManageServers_Click(object sender, RoutedEventArgs e) => OpenServerManager();
    private void Subscriptions_Click(object sender, RoutedEventArgs e) => ShowSection("اشتراک‌ها", "Subscriptions");
    private void Routing_Click(object sender, RoutedEventArgs e) => ShowSection("مسیریابی", "Routing");
    private void Logs_Click(object sender, RoutedEventArgs e) => ShowSection("گزارش‌ها", "Logs");
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSection("تنظیمات", "Settings");

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        _english = !_english;
        FlowDirection = _english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        LanguageButton.Content = _english ? "EN  |  FA" : "FA  |  EN";
        ShowSection("داشبورد", "Dashboard");
        ConnectionState.Text = _english ? "Disconnected" : "قطع اتصال";
        RefreshServerSummary();
    }
}
