using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using V2rayWatch.Server.Core;

namespace V2rayWatch.Server.Web;

/// <summary>
/// Starts a short-lived Xray process for outbound-only operations such as Gmail SMTP.
/// The SOCKS inbound is bound to loopback on a temporary port and is never public.
/// </summary>
internal sealed class XraySocksSession : IAsyncDisposable
{
    private readonly Process _process;
    private readonly string _workDirectory;

    private XraySocksSession(Process process, string workDirectory, int port)
    {
        _process = process;
        _workDirectory = workDirectory;
        Port = port;
    }

    public int Port { get; }

    public static async Task<XraySocksSession> StartAsync(
        ServerProfile profile,
        CancellationToken cancellationToken)
    {
        var corePath = Environment.GetEnvironmentVariable("V2RAYWATCH_XRAY_PATH") ?? "/opt/v2raywatch/xray";
        if (!File.Exists(corePath))
            throw new InvalidOperationException("هسته Xray برای مسیر خروجی Gmail پیدا نشد.");

        var runtimeRoot = Environment.GetEnvironmentVariable("V2RAYWATCH_RUNTIME_DIR")
            ?? Path.Combine(Path.GetTempPath(), "v2raywatch");
        Directory.CreateDirectory(runtimeRoot);
        var workDirectory = Path.Combine(runtimeRoot, "gmail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);

        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(workDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        Process? process = null;
        try
        {
            var port = GetFreePort();
            var config = JsonNode.Parse(profile.ConfigJson)?.AsObject()
                ?? throw new InvalidOperationException("پیکربندی پروفایل مسیر خروجی معتبر نیست.");
            if (config["outbounds"] is not JsonArray outbounds || outbounds.Count == 0)
                throw new InvalidOperationException("پروفایل انتخاب‌شده outbound ندارد.");

            config["inbounds"] = new JsonArray(new JsonObject
            {
                ["tag"] = "v2vw-gmail-socks",
                ["listen"] = "127.0.0.1",
                ["port"] = port,
                ["protocol"] = "socks",
                ["settings"] = new JsonObject { ["auth"] = "noauth", ["udp"] = false }
            });
            config["log"] = new JsonObject { ["loglevel"] = "error" };

            var configPath = Path.Combine(workDirectory, "config.json");
            await File.WriteAllTextAsync(configPath, config.ToJsonString(), new UTF8Encoding(false), cancellationToken)
                .ConfigureAwait(false);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(configPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            var start = new ProcessStartInfo
            {
                FileName = corePath,
                WorkingDirectory = Path.GetDirectoryName(corePath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.ArgumentList.Add("run");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(configPath);
            process = new Process { StartInfo = start };
            if (!process.Start())
                throw new InvalidOperationException("اجرای Xray برای مسیر خروجی Gmail شروع نشد.");

            _ = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            await WaitForSocksAsync(process, port, cancellationToken).ConfigureAwait(false);
            return new XraySocksSession(process, workDirectory, port);
        }
        catch
        {
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3))
                        .ConfigureAwait(false);
                }
                catch { }
                process.Dispose();
            }
            try { Directory.Delete(workDirectory, recursive: true); } catch { }
            throw;
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task WaitForSocksAsync(Process process, int port, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
                throw new InvalidOperationException($"Xray مسیر خروجی Gmail با کد {process.ExitCode} متوقف شد.");

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken).ConfigureAwait(false);
                await using var stream = client.GetStream();
                await stream.WriteAsync(new byte[] { 5, 1, 0 }, cancellationToken).ConfigureAwait(false);
                var response = new byte[2];
                await ReadExactlyAsync(stream, response, cancellationToken).ConfigureAwait(false);
                if (response[0] == 5 && response[1] == 0) return;
            }
            catch (SocketException) { }
            catch (IOException) { }
            await Task.Delay(150, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException("SOCKS محلی Xray برای مسیر Gmail در مهلت مقرر آماده نشد.");
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("پاسخ SOCKS ناقص بود.");
            offset += read;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3))
                .ConfigureAwait(false);
        }
        catch { }
        finally
        {
            _process.Dispose();
            try { Directory.Delete(_workDirectory, recursive: true); } catch { }
        }
    }
}
