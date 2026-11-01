using System.Net;
using System.Net.Sockets;
using System.Text;

namespace V2rayWatch.Server.Web;

/// <summary>
/// Fetches subscription bodies over HTTPS only. Redirects are followed manually,
/// and the connection callback refuses loopback, private, link-local and reserved IPs.
/// This prevents a stored subscription URL from being used to reach local services.
/// </summary>
public sealed class SubscriptionClient
{
    private const int MaximumBytes = 2 * 1024 * 1024;
    private const int MaximumRedirects = 5;
    private readonly HttpClient _client;

    public SubscriptionClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseProxy = false,
            ConnectCallback = ConnectPublicAddressAsync
        };
        _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<string> FetchTextAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var current))
            throw new InvalidOperationException("آدرس Subscription معتبر نیست.");

        ValidateUri(current);
        for (var redirect = 0; redirect <= MaximumRedirects; redirect++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd("V2rayWatch-Linux/0.4");
            request.Headers.Accept.ParseAdd("text/plain");
            request.Headers.Accept.ParseAdd("*/*;q=0.8");

            using var response = await _client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);

            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                if (redirect == MaximumRedirects)
                    throw new InvalidOperationException("تعداد تغییرمسیرهای Subscription بیش از حد مجاز است.");
                var location = response.Headers.Location
                    ?? throw new InvalidOperationException("پاسخ Subscription آدرس مقصد جدید ندارد.");
                var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                ValidateUri(next);
                current = next;
                continue;
            }

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"دریافت Subscription ناموفق بود (HTTP {(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength is > MaximumBytes)
                throw new InvalidOperationException("حجم Subscription بیش از ۲ مگابایت است.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > MaximumBytes)
                    throw new InvalidOperationException("حجم Subscription بیش از ۲ مگابایت است.");
                buffer.Write(chunk, 0, read);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(buffer.ToArray())
                    .TrimStart('\uFEFF', '\u0000', ' ', '\r', '\n', '\t');
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidOperationException("متن Subscription باید UTF-8 معتبر باشد.");
            }
        }

        throw new InvalidOperationException("دریافت Subscription ناموفق بود.");
    }

    private static void ValidateUri(Uri uri)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("آدرس Subscription باید HTTPS معتبر و بدون نام کاربری/رمز در URL باشد.");

        if (IPAddress.TryParse(uri.Host, out var address) && !IsPublicAddress(address))
            throw new InvalidOperationException("آدرس‌های داخلی و خصوصی برای Subscription مجاز نیستند.");
    }

    private static async ValueTask<Stream> ConnectPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        if (IPAddress.TryParse(context.DnsEndPoint.Host, out var literal))
            addresses = [literal];
        else
            addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken).ConfigureAwait(false);

        foreach (var address in addresses.Where(IsPublicAddress))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
            }
        }

        throw new SocketException((int)SocketError.AddressNotAvailable);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            if (b[0] is 0 or 10 or 127 or >= 224) return false;
            if (b[0] == 100 && b[1] is >= 64 and <= 127) return false;
            if (b[0] == 169 && b[1] == 254) return false;
            if (b[0] == 172 && b[1] is >= 16 and <= 31) return false;
            if (b[0] == 192 && (b[1] == 168 || (b[1] == 0 && b[2] == 0) ||
                (b[1] == 0 && b[2] == 2) || (b[1] == 88 && b[2] == 99) ||
                (b[1] == 168 && b[2] == 0))) return false;
            if (b[0] == 198 && (b[1] == 18 || b[1] == 19 || (b[1] == 51 && b[2] == 100))) return false;
            if (b[0] == 203 && b[1] == 0 && b[2] == 113) return false;
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6Loopback || address.IsIPv6LinkLocal ||
                address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                return false;
            var bytes = address.GetAddressBytes();
            // Only globally routable 2000::/3 is accepted. This excludes ULA,
            // unspecified, translation, and documentation ranges by default.
            return (bytes[0] & 0xE0) == 0x20;
        }

        return false;
    }
}
