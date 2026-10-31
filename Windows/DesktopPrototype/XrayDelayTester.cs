using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Text.Json.Nodes;

namespace V2rayWatch.DesktopPrototype;

public sealed class XrayDelayTester
{
    private static readonly SemaphoreSlim Concurrency = new(2, 2);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ProfileLocks = new();
    private readonly string _corePath;

    public XrayDelayTester()
    {
        var configured = Environment.GetEnvironmentVariable("V2RAYWATCH_XRAY_PATH");
        _corePath = !string.IsNullOrWhiteSpace(configured) ? configured : Path.Combine(AppContext.BaseDirectory, "core", "xray.exe");
    }

    public async Task<DelayTestResult> TestAsync(ServerProfile profile, MonitorSettings settings, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_corePath))
            return new DelayTestResult(false, null, "فایل core/xray.exe پیدا نشد. بستهٔ کامل V2rayWatch را دریافت کن.");

        var profileLock = ProfileLocks.GetOrAdd(profile.Id, _ => new SemaphoreSlim(1, 1));
        await profileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            var workDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "V2rayWatch", "runtime", Guid.NewGuid().ToString("N"));

            Process? process = null;
            try
            {
                Directory.CreateDirectory(workDirectory);
            using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startupTimeout.CancelAfter(TimeSpan.FromSeconds(12));
            var port = GetFreePort();
            var configPath = Path.Combine(workDirectory, "config.json");
            var config = BuildTestConfig(profile.ConfigJson, port);
            await File.WriteAllTextAsync(configPath, config.ToJsonString(), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

            var start = new ProcessStartInfo
            {
                FileName = _corePath,
                WorkingDirectory = Path.GetDirectoryName(_corePath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("run");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(configPath);
            process = new Process { StartInfo = start, EnableRaisingEvents = true };
            if (!process.Start())
                return new DelayTestResult(false, null, "راه‌اندازی Xray Core انجام نشد.");
            _ = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();

            await WaitForCoreAsync(process, port, startupTimeout.Token).ConfigureAwait(false);
            var primary = await ProbeAsync(settings.PrimaryTestUrl, port, settings.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
            if (primary.Success)
                return primary;

            if (Uri.TryCreate(settings.FallbackTestUrl, UriKind.Absolute, out var fallback) &&
                fallback.Scheme == Uri.UriSchemeHttps &&
                !string.Equals(settings.FallbackTestUrl, settings.PrimaryTestUrl, StringComparison.OrdinalIgnoreCase))
            {
                var secondary = await ProbeAsync(settings.FallbackTestUrl, port, settings.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
                if (secondary.Success) return secondary;
                return new DelayTestResult(false, null, $"{primary.Error}؛ مسیر جایگزین: {secondary.Error}");
            }
            return primary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new DelayTestResult(false, null, "مهلت تست تمام شد.");
        }
        catch (Exception ex)
        {
            return new DelayTestResult(false, null, SafeError(ex));
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch { }
                process.Dispose();
            }
                try { Directory.Delete(workDirectory, recursive: true); } catch { }
                Concurrency.Release();
            }
        }
        finally
        {
            profileLock.Release();
        }
    }

    private static JsonObject BuildTestConfig(string json, int socksPort)
    {
        var config = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("پیکربندی Xray معتبر نیست.");
        if (config["outbounds"] is not JsonArray outbounds || outbounds.Count == 0)
            throw new InvalidOperationException("پیکربندی پروفایل outbound ندارد.");
        config["inbounds"] = new JsonArray(new JsonObject
        {
            ["tag"] = "v2vw-socks",
            ["listen"] = "127.0.0.1",
            ["port"] = socksPort,
            ["protocol"] = "socks",
            ["settings"] = new JsonObject { ["auth"] = "noauth", ["udp"] = false }
        });
        config["log"] = new JsonObject { ["loglevel"] = "error" };
        return config;
    }

    private static async Task WaitForCoreAsync(Process process, int port, CancellationToken cancellationToken)
    {
        var end = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
                throw new InvalidOperationException($"Xray Core با کد {process.ExitCode} خارج شد؛ پیکربندی این لینک ممکن است پشتیبانی نشود.");
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
                await using var stream = client.GetStream();
                await stream.WriteAsync(new byte[] { 5, 1, 0 }, cancellationToken).ConfigureAwait(false);
                var greeting = new byte[2];
                await ReadExactlyAsync(stream, greeting, cancellationToken).ConfigureAwait(false);
                if (greeting[0] == 5 && greeting[1] == 0) return;
            }
            catch (SocketException) { }
            catch (IOException) { }
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException("Xray Core در مهلت مقرر آماده نشد.");
    }

    private static async Task<DelayTestResult> ProbeAsync(string testUrl, int socksPort, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(testUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return new DelayTestResult(false, null, "آدرس تست باید HTTPS باشد.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 3, 60)));
        var watch = Stopwatch.StartNew();
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, socksPort, timeout.Token).ConfigureAwait(false);
        await using var network = client.GetStream();

        await network.WriteAsync(new byte[] { 5, 1, 0 }, timeout.Token).ConfigureAwait(false);
        var greeting = new byte[2];
        await ReadExactlyAsync(network, greeting, timeout.Token).ConfigureAwait(false);
        if (greeting[0] != 5 || greeting[1] != 0)
            return new DelayTestResult(false, null, "پاسخ SOCKS محلی نامعتبر است.");

        var hostBytes = Encoding.ASCII.GetBytes(uri.DnsSafeHost);
        if (hostBytes.Length > 255)
            return new DelayTestResult(false, null, "نام میزبان URL تست بیش از حد طولانی است.");
        var connectRequest = new byte[7 + hostBytes.Length];
        connectRequest[0] = 5; connectRequest[1] = 1; connectRequest[2] = 0; connectRequest[3] = 3;
        connectRequest[4] = (byte)hostBytes.Length;
        Buffer.BlockCopy(hostBytes, 0, connectRequest, 5, hostBytes.Length);
        var portBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)uri.Port));
        connectRequest[^2] = portBytes[0];
        connectRequest[^1] = portBytes[1];
        await network.WriteAsync(connectRequest, timeout.Token).ConfigureAwait(false);

        var response = new byte[4];
        await ReadExactlyAsync(network, response, timeout.Token).ConfigureAwait(false);
        if (response[0] != 5 || response[1] != 0)
            return new DelayTestResult(false, null, $"اتصال خروجی از طریق پروفایل ناموفق بود (SOCKS {response[1]}).");
        var addressLength = response[3] switch
        {
            1 => 4,
            4 => 16,
            3 => await ReadByteAsync(network, timeout.Token).ConfigureAwait(false),
            _ => throw new IOException("پاسخ آدرس SOCKS نامعتبر است.")
        };
        var trailing = new byte[addressLength + 2];
        await ReadExactlyAsync(network, trailing, timeout.Token).ConfigureAwait(false);

        using var ssl = new SslStream(network, leaveInnerStreamOpen: true);
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = uri.DnsSafeHost,
            EnabledSslProtocols = SslProtocols.None
        }, timeout.Token).ConfigureAwait(false);

        var path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
        var requestText = $"GET {path} HTTP/1.1\r\nHost: {uri.Authority}\r\nUser-Agent: V2rayWatch-Windows/0.3\r\nAccept: */*\r\nConnection: close\r\n\r\n";
        await ssl.WriteAsync(Encoding.ASCII.GetBytes(requestText), timeout.Token).ConfigureAwait(false);
        await ssl.FlushAsync(timeout.Token).ConfigureAwait(false);
        var header = await ReadHeadersAsync(ssl, timeout.Token).ConfigureAwait(false);
        watch.Stop();

        var firstLine = Encoding.ASCII.GetString(header).Split("\r\n", StringSplitOptions.None).FirstOrDefault() ?? "";
        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var status) || status is not (200 or 204))
            return new DelayTestResult(false, null, $"پاسخ URL تست قابل قبول نبود: {firstLine}");
        return new DelayTestResult(true, watch.ElapsedMilliseconds);
    }

    private static async Task<byte[]> ReadHeadersAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var one = new byte[1];
        while (buffer.Length < 32 * 1024)
        {
            var read = await stream.ReadAsync(one, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            buffer.WriteByte(one[0]);
            var length = buffer.Length;
            if (length >= 4)
            {
                var data = buffer.GetBuffer();
                if (data[length - 4] == 13 && data[length - 3] == 10 && data[length - 2] == 13 && data[length - 1] == 10)
                    return buffer.ToArray();
            }
        }
        throw new IOException("پاسخ HTTP کامل دریافت نشد.");
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("پاسخ SOCKS ناتمام است.");
            offset += read;
        }
    }

    private static async Task<byte> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var single = new byte[1];
        await ReadExactlyAsync(stream, single, cancellationToken).ConfigureAwait(false);
        return single[0];
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string SafeError(Exception ex) => ex switch
    {
        AuthenticationException => "برقراری TLS با مقصد تست ناموفق بود.",
        SocketException => "اتصال شبکه یا SOCKS محلی برقرار نشد.",
        IOException => ex.Message,
        _ => ex.Message.Length > 180 ? ex.Message[..180] : ex.Message
    };
}
