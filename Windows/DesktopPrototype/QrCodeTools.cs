using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QRCoder;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace V2rayWatch.DesktopPrototype;

public static class QrCodeService
{
    public static byte[] GeneratePng(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("QR content is empty.", nameof(content));

        var utf8 = Encoding.UTF8.GetBytes(content);
        try
        {
            // A conservative limit leaves room for reliable scanning on phone screens.
            if (utf8.Length > 1_800)
                throw new InvalidDataException("This profile link is too long for a reliable QR code.");
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            var qr = new PngByteQRCode(data);
            return qr.GetGraphic(8);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(utf8);
        }
    }

    public static string DecodeImageFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            throw new FileNotFoundException("QR image file was not found.", path);
        if (info.Length is <= 0 or > 16 * 1024 * 1024)
            throw new InvalidDataException("QR image must be smaller than 16 MB.");

        using var bitmap = new Bitmap(path);
        if (bitmap.Width <= 0 || bitmap.Height <= 0 ||
            (long)bitmap.Width * bitmap.Height > 50_000_000)
            throw new InvalidDataException("QR image dimensions are too large.");

        var reader = new BarcodeReader
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
            }
        };
        var result = reader.Decode(bitmap);
        if (result is null || string.IsNullOrWhiteSpace(result.Text))
            throw new InvalidDataException("No readable QR code was found in this image.");
        if (Encoding.UTF8.GetByteCount(result.Text) > 256 * 1024)
            throw new InvalidDataException("The QR content is too large.");
        return result.Text.Trim();
    }
}

public static class PassphrasePrompt
{
    public static char[]? Show(Window owner, bool confirm, bool english)
    {
        var title = confirm
            ? (english ? "Create encrypted backup" : "ساخت پشتیبان رمزگذاری‌شده")
            : (english ? "Unlock backup" : "بازکردن قفل پشتیبان");

        var window = new Window
        {
            Title = title,
            Owner = owner,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft,
            Background = System.Windows.Media.Brushes.White,
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = english
                ? $"Use a passphrase with {PortableBackupCodec.MinimumPassphraseLength}–{PortableBackupCodec.MaximumPassphraseLength} characters. The same passphrase is required on Android and Windows."
                : $"یک عبارت عبور {PortableBackupCodec.MinimumPassphraseLength} تا {PortableBackupCodec.MaximumPassphraseLength} کاراکتری وارد کن. همین عبارت برای بازیابی در اندروید و ویندوز لازم است.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = System.Windows.Media.Brushes.Black
        });

        panel.Children.Add(new TextBlock
        {
            Text = english ? "Backup passphrase" : "عبارت عبور پشتیبان",
            Margin = new Thickness(0, 0, 0, 5),
            Foreground = System.Windows.Media.Brushes.Black
        });
        var first = new PasswordBox { MinHeight = 34, Padding = new Thickness(8) };
        panel.Children.Add(first);

        PasswordBox? repeated = null;
        if (confirm)
        {
            panel.Children.Add(new TextBlock
            {
                Text = english ? "Confirm passphrase" : "تکرار عبارت عبور",
                Margin = new Thickness(0, 12, 0, 5),
                Foreground = System.Windows.Media.Brushes.Black
            });
            repeated = new PasswordBox { MinHeight = 34, Padding = new Thickness(8) };
            panel.Children.Add(repeated);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = new Button
        {
            Content = english ? "Cancel" : "انصراف",
            MinWidth = 88,
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        var accept = new Button
        {
            Content = english ? "Continue" : "ادامه",
            MinWidth = 88,
            Padding = new Thickness(12, 6, 12, 6),
            IsDefault = true
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);
        panel.Children.Add(buttons);
        window.Content = panel;

        char[]? answer = null;
        cancel.Click += (_, _) =>
        {
            first.Clear();
            repeated?.Clear();
            window.DialogResult = false;
        };
        accept.Click += (_, _) =>
        {
            var value = first.Password;
            if (value.Length is < PortableBackupCodec.MinimumPassphraseLength or > PortableBackupCodec.MaximumPassphraseLength)
            {
                MessageBox.Show(
                    window,
                    english
                        ? $"Passphrase must contain {PortableBackupCodec.MinimumPassphraseLength} to {PortableBackupCodec.MaximumPassphraseLength} characters."
                        : $"عبارت عبور باید بین {PortableBackupCodec.MinimumPassphraseLength} تا {PortableBackupCodec.MaximumPassphraseLength} کاراکتر باشد.",
                    "V2rayWatch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            if (confirm && repeated?.Password != value)
            {
                MessageBox.Show(
                    window,
                    english ? "The passphrases do not match." : "دو عبارت عبور یکسان نیستند.",
                    "V2rayWatch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            answer = value.ToCharArray();
            first.Clear();
            repeated?.Clear();
            window.DialogResult = true;
        };

        window.ShowDialog();
        first.Clear();
        repeated?.Clear();
        return answer;
    }
}

public sealed class QrCodeWindow : Window
{
    private readonly byte[] _png;
    private readonly bool _english;

    public QrCodeWindow(string profileName, string rawLink, bool english)
    {
        _english = english;
        _png = QrCodeService.GeneratePng(rawLink);
        Title = english ? "Share profile QR code" : "اشتراک‌گذاری QR پروفایل";
        Width = 430;
        Height = 540;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FlowDirection = english ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        Background = System.Windows.Media.Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock
        {
            Text = profileName,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.Black,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var image = new Image
        {
            Width = 330,
            Height = 330,
            Stretch = System.Windows.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            Source = LoadImage(_png)
        };
        panel.Children.Add(image);
        panel.Children.Add(new TextBlock
        {
            Text = english
                ? "This QR contains the complete proxy link, including its credentials. Share it privately."
                : "این QR شامل لینک کامل پروکسی و اطلاعات ورود آن است؛ فقط به‌صورت خصوصی به اشتراک بگذار.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = System.Windows.Media.Brushes.DarkRed,
            Margin = new Thickness(0, 12, 0, 14)
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var save = new Button
        {
            Content = english ? "Save QR image" : "ذخیره تصویر QR",
            MinWidth = 130,
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        save.Click += Save_Click;
        var close = new Button
        {
            Content = english ? "Close" : "بستن",
            MinWidth = 90,
            Padding = new Thickness(12, 7, 12, 7),
            IsCancel = true
        };
        buttons.Children.Add(save);
        buttons.Children.Add(close);
        panel.Children.Add(buttons);
        Content = panel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var safeName = string.Concat(Title.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')).Trim();
        var dialog = new SaveFileDialog
        {
            Filter = "PNG image|*.png",
            FileName = string.IsNullOrWhiteSpace(safeName) ? "V2rayWatch-QR.png" : safeName + ".png",
            AddExtension = true,
            DefaultExt = ".png"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            await File.WriteAllBytesAsync(dialog.FileName, _png);
            MessageBox.Show(
                this,
                _english ? "QR image saved." : "تصویر QR ذخیره شد.",
                "V2rayWatch",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "V2rayWatch",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static BitmapImage LoadImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
