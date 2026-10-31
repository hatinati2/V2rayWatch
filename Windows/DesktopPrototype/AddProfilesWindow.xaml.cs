using System.Windows;
using System.Windows.Controls;

namespace V2rayWatch.DesktopPrototype;

public partial class AddProfilesWindow : Window
{
    public bool IsSubscription => InputTabs.SelectedItem == SubscriptionTab;
    public string InputValue => IsSubscription ? SubscriptionInput.Text.Trim() : LinksInput.Text.Trim();

    public AddProfilesWindow()
    {
        InitializeComponent();
        LinksInput.Focus();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputValue))
        {
            DialogStatus.Text = IsSubscription ? "آدرس Subscription را وارد کن." : "حداقل یک لینک را وارد کن.";
            return;
        }
        if (IsSubscription && (!Uri.TryCreate(InputValue, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
        {
            DialogStatus.Text = "آدرس Subscription باید HTTPS باشد.";
            return;
        }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void InputTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ImportButton is null || LinksInput is null || SubscriptionInput is null) return;
        ImportButton.Content = IsSubscription ? "دریافت و افزودن لینک‌ها" : "افزودن لینک‌ها";
    }
}
