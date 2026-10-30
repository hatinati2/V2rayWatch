using System.Windows;
using System.Windows.Controls;

namespace V2rayWatch.DesktopPrototype;

public partial class MainWindow : Window
{
    private bool _english;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ShowSection(string fa, string en)
    {
        PageTitle.Text = _english ? en : fa;
    }

    private void Dashboard_Click(object sender, RoutedEventArgs e) => ShowSection("داشبورد", "Dashboard");
    private void Servers_Click(object sender, RoutedEventArgs e) => ShowSection("سرورها", "Servers");
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
    }
}
