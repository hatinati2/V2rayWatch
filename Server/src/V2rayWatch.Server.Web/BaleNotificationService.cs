using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Web;

public sealed class BaleNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SqliteServerStateRepository _state;
    private readonly ILogger<BaleNotificationService> _logger;

    public BaleNotificationService(
        IHttpClientFactory httpClientFactory,
        SqliteServerStateRepository state,
        ILogger<BaleNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _state = state;
        _logger = logger;
    }

    public async Task NotifyStatusChangeAsync(
        ServerProfile profile,
        int profileNumber,
        string previousStatus,
        string currentStatus,
        CancellationToken cancellationToken)
    {
        // These transitions are server-side Bale alerts. Browser notification
        // preferences (MonitorSettings.NotifyOnDisconnect/Recovery) must not gate them.
        var disconnected = currentStatus == "Disconnected" &&
            previousStatus != "Disconnected";
        var recovered = previousStatus == "Disconnected" &&
            currentStatus == "Connected";
        if (!disconnected && !recovered) return;

        var settings = await _state.GetBaleNotificationSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            _logger.LogInformation(
                "Bale notification skipped for profile {ProfileId}: Bale notifications are disabled.",
                profile.Id);
            return;
        }

        var body = BaleNotificationMessageFormatter.Format(profileNumber, currentStatus);
        try
        {
            await SendMessageAsync(settings, body, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Bale notification sent for profile {ProfileId}: {Status}",
                profile.Id, currentStatus);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // A notification failure must never interrupt the monitoring cycle.
            _logger.LogWarning("Bale notification failed for profile {ProfileId} ({ErrorType}).",
                profile.Id, ex.GetType().Name);
        }
    }

    public async Task SendTestAsync(CancellationToken cancellationToken)
    {
        var settings = await _state.GetBaleNotificationSettingsAsync(cancellationToken).ConfigureAwait(false);
        await SendMessageAsync(
            settings,
            "no 1 green",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> DetectLatestPrivateChatIdAsync(CancellationToken cancellationToken)
    {
        var settings = await _state.GetBaleNotificationSettingsAsync(cancellationToken).ConfigureAwait(false);
        ValidateBotToken(settings);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/bot{settings.BotToken}/getUpdates?limit=100");
        using var response = await _httpClientFactory.CreateClient("BaleBot")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        using var document = await ReadSuccessfulResponseAsync(response, cancellationToken).ConfigureAwait(false);

        if (!document.RootElement.TryGetProperty("result", out var updates) ||
            updates.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("بله آپدیت معتبری برنگرداند. ابتدا در گفت‌وگوی خصوصی ربات /start را ارسال کن.");

        foreach (var update in updates.EnumerateArray().Reverse())
        {
            if (!update.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("chat", out var chat) ||
                !chat.TryGetProperty("type", out var type) ||
                !string.Equals(type.GetString(), "private", StringComparison.OrdinalIgnoreCase) ||
                !chat.TryGetProperty("id", out var id))
                continue;

            var chatId = id.ValueKind == JsonValueKind.String
                ? id.GetString()
                : id.GetRawText();
            if (!string.IsNullOrWhiteSpace(chatId) && long.TryParse(
                chatId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return chatId;
        }

        throw new InvalidOperationException(
            "شناسهٔ گفت‌وگوی خصوصی پیدا نشد. در بله وارد گفت‌وگوی ربات شو، /start را بفرست و دوباره امتحان کن.");
    }

    private async Task SendMessageAsync(
        BaleNotificationSettings settings,
        string text,
        CancellationToken cancellationToken)
    {
        ValidateCredentials(settings);
        if (!long.TryParse(settings.ChatId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId))
            throw new InvalidOperationException("Chat ID بله باید یک عدد صحیح باشد.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/bot{settings.BotToken}/sendMessage")
        {
            Content = JsonContent.Create(new { chat_id = chatId, text })
        };
        using var response = await _httpClientFactory.CreateClient("BaleBot")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        using var document = await ReadSuccessfulResponseAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateCredentials(BaleNotificationSettings settings)
    {
        ValidateBotToken(settings);
        if (string.IsNullOrWhiteSpace(settings.ChatId))
            throw new InvalidOperationException("Chat ID بله ثبت نشده است.");
    }

    private static void ValidateBotToken(BaleNotificationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BotToken))
            throw new InvalidOperationException("توکن ربات بله ذخیره نشده است.");
        if (settings.BotToken.Length > 512 ||
            settings.BotToken.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is ':' or '_' or '-')))
            throw new InvalidOperationException("فرمت توکن ربات بله معتبر نیست.");
    }

    private static async Task<JsonDocument> ReadSuccessfulResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        JsonDocument document;
        try { document = JsonDocument.Parse(json); }
        catch (JsonException)
        {
            var status = (int)response.StatusCode;
            throw new InvalidOperationException(response.IsSuccessStatusCode
                ? "پاسخ API بله قابل‌خواندن نیست."
                : $"ارتباط با API بله ناموفق بود (HTTP {status}) و پاسخ آن JSON معتبر نبود.");
        }

        var apiOk = document.RootElement.TryGetProperty("ok", out var ok) &&
            ok.ValueKind == JsonValueKind.True;
        if (!response.IsSuccessStatusCode || !apiOk)
        {
            var description = document.RootElement.TryGetProperty("description", out var detail)
                ? detail.GetString()
                : null;
            var errorCode = document.RootElement.TryGetProperty("error_code", out var error)
                ? error.ToString()
                : null;
            document.Dispose();

            var codeText = string.IsNullOrWhiteSpace(errorCode) ? "" : $", API code {errorCode}";
            var detailText = string.IsNullOrWhiteSpace(description) ? "" : $": {description}";
            throw new InvalidOperationException(
                $"API بله درخواست را نپذیرفت (HTTP {(int)response.StatusCode}{codeText}){detailText}");
        }

        return document;
    }

}
