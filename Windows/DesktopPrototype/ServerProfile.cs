using System.Globalization;
using System.Text;
using System.Text.Json;

namespace V2rayWatch.DesktopPrototype;

public sealed class ServerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string RawUri { get; set; } = "";
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Endpoint => $"{Host}:{Port}";
}

public static class ServerProfileParser
{
    private static readonly HashSet<string> SupportedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "vless", "vmess", "trojan", "ss", "shadowsocks", "hy2", "hysteria2",
        "socks", "socks5", "http"
    };

    public static IReadOnlyList<ServerProfile> ParseMany(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("لینک یا متن اشتراک خالی است.");

        var normalized = input.Trim();
        if (!normalized.Contains("://", StringComparison.Ordinal))
        {
            var decoded = TryDecodeBase64(normalized);
            if (decoded is not null && decoded.Contains("://", StringComparison.Ordinal))
                normalized = decoded;
        }

        var profiles = new List<ServerProfile>();
        var errors = new List<string>();

        foreach (var line in normalized.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            try
            {
                profiles.Add(ParseOne(line));
            }
            catch (FormatException ex)
            {
                errors.Add($"{Shorten(line)} — {ex.Message}");
            }
        }

        if (profiles.Count == 0)
            throw new FormatException(errors.Count == 0
                ? "هیچ لینک پشتیبانی‌شده‌ای پیدا نشد."
                : string.Join(Environment.NewLine, errors.Take(4)));

        return profiles;
    }

    public static ServerProfile ParseOne(string value)
    {
        var raw = value.Trim();
        var separator = raw.IndexOf("://", StringComparison.Ordinal);
        if (separator <= 0)
            throw new FormatException("قالب لینک معتبر نیست.");

        var scheme = raw[..separator].ToLowerInvariant();
        if (!SupportedSchemes.Contains(scheme))
            throw new FormatException($"پروتکل {scheme} در این مرحله پشتیبانی نمی‌شود.");

        if (scheme == "vmess")
            return ParseVmess(raw);

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            throw new FormatException("آدرس سرور قابل خواندن نیست.");

        var host = uri.Host;
        var port = uri.IsDefaultPort ? -1 : uri.Port;

        if (scheme is "ss" or "shadowsocks")
            (host, port) = ReadShadowsocksEndpoint(raw, uri, host, port);

        if (string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
            throw new FormatException("آدرس یا پورت سرور معتبر نیست.");

        var fragment = uri.Fragment.TrimStart('#');
        var name = string.IsNullOrWhiteSpace(fragment)
            ? $"{host}:{port}"
            : SafeDecode(fragment);

        var protocol = scheme switch
        {
            "ss" or "shadowsocks" => "Shadowsocks",
            "vless" => "VLESS",
            "vmess" => "VMess",
            "trojan" => "Trojan",
            "hy2" or "hysteria2" => "Hysteria2",
            "socks" or "socks5" => "SOCKS",
            "http" => "HTTP",
            _ => char.ToUpperInvariant(scheme[0]) + scheme[1..]
        };

        return new ServerProfile
        {
            Name = name,
            Protocol = protocol,
            Host = host,
            Port = port,
            RawUri = raw
        };
    }

    private static ServerProfile ParseVmess(string raw)
    {
        var payload = raw["vmess://".Length..].Split('#')[0].Trim();
        var jsonText = TryDecodeBase64(payload);
        if (string.IsNullOrWhiteSpace(jsonText))
            throw new FormatException("داده VMess قابل رمزگشایی نیست.");

        try
        {
            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;
            var host = ReadString(root, "add");
            var portText = ReadString(root, "port");
            if (string.IsNullOrWhiteSpace(host) ||
                !int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
                port is < 1 or > 65535)
                throw new FormatException("میزبان یا پورت VMess معتبر نیست.");

            var name = ReadString(root, "ps");
            if (string.IsNullOrWhiteSpace(name))
                name = $"{host}:{port}";

            return new ServerProfile
            {
                Name = name,
                Protocol = "VMess",
                Host = host,
                Port = port,
                RawUri = raw
            };
        }
        catch (JsonException)
        {
            throw new FormatException("ساختار JSON لینک VMess معتبر نیست.");
        }
    }

    private static (string Host, int Port) ReadShadowsocksEndpoint(
        string raw, Uri uri, string host, int port)
    {
        if (!string.IsNullOrWhiteSpace(host) && port > 0)
            return (host, port);

        // Legacy SIP008-style URL: ss://base64(method:password@host:port)#tag
        var payload = raw["ss://".Length..].Split('#')[0].Split('?')[0];
        var decoded = TryDecodeBase64(payload);
        if (decoded is null)
            return (host, port);

        var at = decoded.LastIndexOf('@');
        var endpoint = at >= 0 ? decoded[(at + 1)..] : decoded;
        var endpointUri = Uri.TryCreate("ss://" + endpoint, UriKind.Absolute, out var parsed)
            ? parsed
            : null;

        if (endpointUri is not null && endpointUri.Port > 0)
            return (endpointUri.Host, endpointUri.Port);

        var colon = endpoint.LastIndexOf(':');
        if (colon > 0 && int.TryParse(endpoint[(colon + 1)..], out var parsedPort))
            return (endpoint[..colon].Trim('[', ']'), parsedPort);

        return (host, port);
    }

    private static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
            return "";

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            _ => ""
        };
    }

    private static string? TryDecodeBase64(string value)
    {
        try
        {
            var normalized = value.Trim().Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(normalized)).Trim();
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string SafeDecode(string value)
    {
        try { return Uri.UnescapeDataString(value); }
        catch (UriFormatException) { return value; }
    }

    private static string Shorten(string value) =>
        value.Length <= 46 ? value : value[..43] + "...";
}
