using System.Collections.ObjectModel;
using System.Windows;

namespace V2rayWatch.DesktopPrototype;

public partial class ServerManagerWindow : Window
{
    private readonly ObservableCollection<ServerProfile> _profiles;
    private readonly ServerProfileStore _store = new();

    public ServerProfile? SelectedProfile { get; private set; }

    public ServerManagerWindow(ObservableCollection<ServerProfile> profiles)
    {
        InitializeComponent();
        _profiles = profiles;
        ProfilesGrid.ItemsSource = _profiles;
        ProfilesGrid.SelectionChanged += (_, _) =>
        {
            RemoveButton.IsEnabled = ProfilesGrid.SelectedItem is ServerProfile;
            SelectButton.IsEnabled = ProfilesGrid.SelectedItem is ServerProfile;
        };
        RefreshCount();
    }

    private void Clipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
                ImportTextBox.Text = Clipboard.GetText();
            else
                SetStatus("متن معتبری در Clipboard وجود ندارد.", "Clipboard does not contain text.", error: true);
        }
        catch (Exception ex)
        {
            SetStatus($"خواندن Clipboard ناموفق بود: {ex.Message}", $"Clipboard read failed: {ex.Message}", error: true);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var parsed = ServerProfileParser.ParseMany(ImportTextBox.Text);
            var existing = _profiles.Select(p => p.RawUri).ToHashSet(StringComparer.Ordinal);
            var added = 0;
            foreach (var profile in parsed)
            {
                if (existing.Add(profile.RawUri))
                {
                    _profiles.Add(profile);
                    added++;
                }
            }

            _store.Save(_profiles);
            ImportTextBox.Clear();
            RefreshCount();
            SetStatus($"{added} سرور اضافه شد.", $"{added} server(s) imported.", error: false);
        }
        catch (FormatException ex)
        {
            SetStatus(ex.Message, ex.Message, error: true);
        }
        catch (Exception ex)
        {
            SetStatus($"ذخیره‌سازی ناموفق بود: {ex.Message}", $"Could not save profiles: {ex.Message}", error: true);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesGrid.SelectedItem is not ServerProfile profile)
            return;

        var answer = MessageBox.Show(
            $"سرور «{profile.Name}» حذف شود؟",
            "V2rayWatch",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
            return;

        _profiles.Remove(profile);
        _store.Save(_profiles);
        RefreshCount();
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        SelectedProfile = ProfilesGrid.SelectedItem as ServerProfile;
        DialogResult = SelectedProfile is not null;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RefreshCount()
    {
        CountText.Text = $"{_profiles.Count} سرور";
    }

    private void SetStatus(string fa, string en, bool error)
    {
        ImportStatus.Text = fa;
        ImportStatus.Foreground = error
            ? System.Windows.Media.Brushes.Orange
            : System.Windows.Media.Brushes.LightGreen;
    }
}
