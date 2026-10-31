using System.Net;
using System.Net.Http;
using System.Text;

namespace V2rayWatch.DesktopPrototype;

public sealed class SubscriptionClient
{
    private const int MaxSubscriptionBytes = 2 * 1024 * 1024;
    private const int MaxRedirects = 5;

    private readonly HttpClient _client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public async Task<IReadOnlyList<ServerProfile>> FetchProfilesAsync(string url, CancellationToken cancellationToken = default)
    {
        var text = await FetchTextAsync(url, cancellationToken).ConfigureAwait(false);
        return ServerProfileParser.ParseMany(text);
    }

    public async Task<string> FetchTextAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var current) ||
            !current.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(current.UserInfo))
            throw new InvalidOperationException("آدرس Subscription باید یک URL معتبر HTTPS باشد.");

        for (var redirect = 0; redirect <= MaxRedirects; redirect++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("V2rayWatch-Windows/0.3");
            request.Headers.Accept.ParseAdd("text/plain");
            request.Headers.Accept.ParseAdd("*/*;q=0.8");

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                if (redirect == MaxRedirects)
                    throw new InvalidOperationException("تعداد تغییرمسیرهای Subscription بیش از حد مجاز است.");
                var location = response.Headers.Location;
                if (location is null)
                    throw new InvalidOperationException("پاسخ Subscription مسیر تغییرمقصد ندارد.");
                var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (!next.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(next.UserInfo))
                    throw new InvalidOperationException("تغییرمقصد ناامن Subscription رد شد؛ همهٔ مسیرها باید HTTPS باشند.");
                current = next;
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"دریافت Subscription ناموفق بود (HTTP {(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength is > MaxSubscriptionBytes)
                throw new InvalidOperationException("حجم Subscription بیش از ۲ مگابایت است.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > MaxSubscriptionBytes)
                    throw new InvalidOperationException("حجم Subscription بیش از ۲ مگابایت است.");
                buffer.Write(chunk, 0, read);
            }

            var bytes = buffer.ToArray();
            var content = new UTF8Encoding(false, true).GetString(bytes);
            return content.TrimStart('\uFEFF', '\u0000', ' ', '\r', '\n', '\t');
        }

        throw new InvalidOperationException("دریافت Subscription ناموفق بود.");
    }
}
