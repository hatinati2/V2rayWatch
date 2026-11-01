using MailKit.Net.Proxy;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Web;

public sealed class GmailNotificationService
{
    private readonly SqliteServerStateRepository _state;
    private readonly SqliteProfileRepository _profiles;
    private readonly ILogger<GmailNotificationService> _logger;

    public GmailNotificationService(
        SqliteServerStateRepository state,
        SqliteProfileRepository profiles,
        ILogger<GmailNotificationService> logger)
    {
        _state = state;
        _profiles = profiles;
        _logger = logger;
    }

    public async Task NotifyStatusChangeAsync(
        ServerProfile profile,
        string previousStatus,
        string currentStatus,
        MonitorSettings monitorSettings,
        CancellationToken cancellationToken)
    {
        var disconnected = currentStatus == "Disconnected" &&
            previousStatus != "Disconnected" && monitorSettings.NotifyOnDisconnect;
        var recovered = previousStatus == "Disconnected" &&
            currentStatus == "Connected" && monitorSettings.NotifyOnRecovery;
        if (!disconnected && !recovered) return;

        var settings = await _state.GetGmailNotificationSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled) return;

        var subject = disconnected
            ? "V2rayWatch | Configuration disconnected"
            : "V2rayWatch | Configuration recovered";
        var stateText = disconnected ? "قطع شد" : "دوباره متصل شد";
        var body = string.Join(Environment.NewLine,
            "V2rayWatch",
            "",
            $"پروفایل: {profile.Name}",
            $"پروتکل: {profile.Protocol}",
            $"سرور: {profile.Host}:{profile.Port}",
            $"وضعیت: {stateText}",
            $"زمان (UTC): {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}",
            "",
            "این پیام به‌صورت خودکار توسط پایش پس‌زمینه V2rayWatch ارسال شده است.");

        try
        {
            await SendMessageAsync(settings, subject, body, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Gmail notification sent for profile {ProfileId}: {Status}",
                profile.Id, currentStatus);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Notification errors must never interrupt the monitoring cycle.
            _logger.LogWarning(ex, "Could not send Gmail notification for profile {ProfileId}", profile.Id);
        }
    }

    public async Task SendTestAsync(CancellationToken cancellationToken)
    {
        var settings = await _state.GetGmailNotificationSettingsAsync(cancellationToken).ConfigureAwait(false);
        await SendMessageAsync(
            settings,
            "V2rayWatch | Gmail notification test",
            "پیام آزمایشی V2rayWatch\n\nاگر این ایمیل را دریافت کرده‌ای، تنظیمات اعلان Gmail درست کار می‌کند.",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SendMessageAsync(
        GmailNotificationSettings settings,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (!MailboxAddress.TryParse(settings.SenderEmail, out var sender) ||
            !MailboxAddress.TryParse(settings.RecipientEmail, out var recipient))
            throw new InvalidOperationException("ایمیل فرستنده یا دریافت‌کننده در تنظیمات Gmail معتبر نیست.");
        if (string.IsNullOrWhiteSpace(settings.AppPassword))
            throw new InvalidOperationException("App Password جیمیل ذخیره نشده است.");

        await using var proxySession = await StartProxyIfSelectedAsync(settings, cancellationToken)
            .ConfigureAwait(false);

        using var smtp = new SmtpClient { Timeout = 20000 };
        if (proxySession is not null)
            smtp.ProxyClient = new Socks5Client("127.0.0.1", proxySession.Port);

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("V2rayWatch", sender.Address));
        message.To.Add(new MailboxAddress("", recipient.Address));
        message.Subject = subject;
        message.Date = DateTimeOffset.Now;
        message.Body = new TextPart("plain") { Text = body };

        await smtp.ConnectAsync(
            "smtp.gmail.com",
            587,
            SecureSocketOptions.StartTls,
            cancellationToken).ConfigureAwait(false);
        await smtp.AuthenticateAsync(
            settings.SenderEmail,
            settings.AppPassword,
            cancellationToken).ConfigureAwait(false);
        await smtp.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await smtp.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<XraySocksSession?> StartProxyIfSelectedAsync(
        GmailNotificationSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.EgressProfileId)) return null;
        if (!Guid.TryParse(settings.EgressProfileId, out var id))
            throw new InvalidOperationException("شناسه پروفایل مسیر خروجی Gmail معتبر نیست.");

        var profile = await _profiles.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (profile is null)
            throw new InvalidOperationException("پروفایل مسیر خروجی Gmail حذف شده است؛ از تنظیمات Gmail یک پروفایل دیگر انتخاب کن.");

        return await XraySocksSession.StartAsync(profile, cancellationToken).ConfigureAwait(false);
    }
}
