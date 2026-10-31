using System.Collections.ObjectModel;
using System.Windows;
using System.Net.Http;
using System.IO;

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

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var originalText = ImportTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(originalText))
        {
            SetStatus("لینک یا متن اشتراک خالی است.", "Subscription input is empty.", error: true);
            return;
        }

        ImportButton.IsEnabled = false;
        try
        {
            var importText = originalText;
            if (!importText.Contains('\n') && !importText.Contains('\r') &&
                Uri.TryCreate(importText, UriKind.Absolute, out var subscriptionUri) &&
                subscriptionUri.Scheme is "http" or "https")
            {
                SetStatus("در حال دریافت اشتراک…", "Downloading subscription…", error: false);
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                using var response = await client.GetAsync(subscriptionUri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > 3_000_000)
                    throw new InvalidOperationException("حجم اشتراک بیش از ۳ مگابایت است.");
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                var builder = new System.Text.StringBuilder();
                var buffer = new char[8192];
                int read;
                while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    builder.Append(buffer, 0, read);
                    if (builder.Length > 3_000_000)
                        throw new InvalidOperationException("حجم اشتراک بیش از ۳ مگابایت است.");
                }
                importText = builder.ToString();
            }

            var parsed = ServerProfileParser.ParseMany(importText);
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
            SetStatus($"واردکردن اشتراک ناموفق بود: {ex.Message}", $"Subscription import failed: {ex.Message}", error: true);
        }
        finally
        {
            ImportButton.IsEnabled = true;
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
