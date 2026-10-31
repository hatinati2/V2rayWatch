using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace V2rayWatch.DesktopPrototype;

public sealed class ServerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string RawUri { get; set; } = "";
    public string ConfigJson { get; set; } = "";
    public string SourceSubscriptionId { get; set; } = "";
    public string Status { get; set; } = "Unknown";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool English { get; set; }
    public int ConsecutiveFailures { get; set; }
    public long? DelayMs { get; set; }
    public DateTimeOffset? LastCheckedUtc { get; set; }
    public List<string> UptimeHistory { get; set; } = [];
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Endpoint => Port > 0 ? $"{Host}:{Port}" : Host;
    public string DelayDisplay => DelayMs is null ? "—" : $"{DelayMs.Value} ms";
    public string LastCheckedDisplay => LastCheckedUtc?.ToLocalTime().ToString("HH:mm:ss") ?? "—";
    public string StatusDisplay => Status switch
    {
        "Connected" => English ? "Connected" : "متصل",
        "Unstable" => English ? "Unstable" : "ناپایدار",
        "Disconnected" => English ? "Disconnected" : "قطع",
        "Checking" => English ? "Checking" : "در حال بررسی",
        "NotTestable" => English ? "Not testable" : "غیرقابل تست",
        _ => English ? "Not checked" : "بررسی نشده"
    };
}

public sealed class SubscriptionSource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTimeOffset? LastUpdatedUtc { get; set; }
    public int ProfileCount { get; set; }
    public string LastUpdatedDisplay => LastUpdatedUtc?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") ?? "هنوز دریافت نشده";
}

public sealed class MonitorSettings
{
    public int IntervalSeconds { get; set; } = 20;
    public int ConsecutiveFailureThreshold { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 15;
    public string PrimaryTestUrl { get; set; } = "https://www.google.com/generate_204";
    public string FallbackTestUrl { get; set; } = "https://www.gstatic.com/generate_204";
}

public sealed class AppData
{
    public List<ServerProfile> Profiles { get; set; } = [];
    public List<SubscriptionSource> Subscriptions { get; set; } = [];
    public MonitorSettings Settings { get; set; } = new();
    public bool English { get; set; }
    public bool LightTheme { get; set; }
    public bool MonitorEnabled { get; set; }
}

public sealed record DelayTestResult(bool Success, long? DelayMs = null, string Error = "");

public static class ServerProfileParser
{
    private static readonly HashSet<string> SupportedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "vless", "vmess", "trojan", "ss", "shadowsocks", "hy2", "hysteria2"
    };

    public static IReadOnlyList<ServerProfile> ParseMany(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("لینک یا متن اشتراک خالی است.");

        var normalized = input.Trim().TrimStart('\uFEFF');
        if (normalized.StartsWith('{'))
            return [ParseOne(normalized)];

        if (!normalized.Contains("://", StringComparison.Ordinal))
        {
            var decoded = TryDecodeBase64(normalized);
            if (decoded is not null && decoded.Contains("://", StringComparison.Ordinal))
                normalized = decoded;
        }

        var profiles = new List<ServerProfile>();
        var errors = new List<string>();
        var lineNumber = 0;
        foreach (var line in normalized.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            lineNumber++;
            if (line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal))
                continue;
            try
            {
                profiles.Add(ParseOne(line));
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
            {
                errors.Add($"خط {lineNumber}: {ex.Message}");
            }
        }

        var distinct = profiles.DistinctBy(p => p.RawUri, StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
            throw new FormatException(errors.Count == 0
                ? "هیچ لینک پشتیبانی‌شده‌ای در پاسخ پیدا نشد."
                : string.Join(Environment.NewLine, errors.Take(5)));
        return distinct;
    }

    public static ServerProfile ParseOne(string value)
    {
        var raw = value.Trim();
        if (raw.StartsWith('{'))
            return ParseXrayJson(raw);

        var separator = raw.IndexOf("://", StringComparison.Ordinal);
        if (separator <= 0)
            throw new FormatException("قالب لینک معتبر نیست.");

        var scheme = raw[..separator].ToLowerInvariant();
        if (!SupportedSchemes.Contains(scheme))
            throw new FormatException($"پروتکل {scheme} پشتیبانی نمی‌شود.");

        return scheme switch
        {
            "vmess" => ParseVmess(raw),
            "vless" => ParseVless(raw),
            "trojan" => ParseTrojan(raw),
            "ss" or "shadowsocks" => ParseShadowsocks(raw),
            "hy2" or "hysteria2" => ParseHysteria2(raw),
            _ => throw new FormatException("پروتکل پشتیبانی نمی‌شود.")
        };
    }

    private static ServerProfile ParseVless(string raw)
    {
        var uri = ParseUri(raw);
        var host = RequireHost(uri);
        var port = RequirePort(uri);
        var uuid = Decode(uri.UserInfo);
        if (string.IsNullOrWhiteSpace(uuid))
            throw new FormatException("UUID در لینک VLESS خالی است.");

        var q = Query(uri.Query);
        var network = NormalizeNetwork(Get(q, "type", Get(q, "network", "tcp")));
        var security = Get(q, "security", "none").ToLowerInvariant();
        if (security is not ("none" or "tls" or "reality"))
            throw new FormatException($"روش امنیتی VLESS ({security}) پشتیبانی نمی‌شود.");
        if (security == "reality" && string.IsNullOrWhiteSpace(Get(q, "pbk", Get(q, "publicKey"))))
            throw new FormatException("کلید عمومی Reality (pbk) در لینک وجود ندارد.");

        var user = new JsonObject
        {
            ["id"] = uuid,
            ["encryption"] = Get(q, "encryption", "none"),
            ["level"] = 0
        };
        var flow = Get(q, "flow");
        if (!string.IsNullOrWhiteSpace(flow)) user["flow"] = flow;

        var outbound = new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "vless",
            ["settings"] = new JsonObject
            {
                ["vnext"] = new JsonArray(new JsonObject
                {
                    ["address"] = host, ["port"] = port, ["users"] = new JsonArray(user)
                })
            },
            ["streamSettings"] = BuildStreamSettings(network, security, q, host)
        };
        return CreateProfile(raw, uri.Fragment, "VLESS", host, port, outbound);
    }

    private static ServerProfile ParseVmess(string raw)
    {
        var payload = raw["vmess://".Length..].Split('#')[0].Trim();
        var decoded = TryDecodeBase64(payload) ?? throw new FormatException("داده VMess قابل رمزگشایی نیست.");
        using var document = JsonDocument.Parse(decoded);
        var root = document.RootElement;
        var host = ReadString(root, "add");
        if (host.Length == 0) host = ReadString(root, "address");
        if (!int.TryParse(ReadString(root, "port"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
            string.IsNullOrWhiteSpace(host) || port is < 1 or > 65535)
            throw new FormatException("میزبان یا پورت VMess معتبر نیست.");
        var id = ReadString(root, "id");
        if (string.IsNullOrWhiteSpace(id)) throw new FormatException("شناسه VMess خالی است.");

        var network = NormalizeNetwork(ReadString(root, "net").IfBlank("tcp"));
        var tlsValue = ReadString(root, "tls").ToLowerInvariant();
        if (tlsValue is not ("" or "tls"))
            throw new FormatException("حالت امنیتی VMess پشتیبانی نمی‌شود.");
        var user = new JsonObject
        {
            ["id"] = id,
            ["alterId"] = ReadInt(root, "aid"),
            ["security"] = ReadString(root, "scy").IfBlank("auto")
        };
        var q = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sni"] = ReadString(root, "sni").IfBlank(ReadString(root, "host").IfBlank(host)),
            ["fp"] = ReadString(root, "fp"),
            ["alpn"] = ReadString(root, "alpn"),
            ["path"] = ReadString(root, "path"),
            ["host"] = ReadString(root, "host"),
            ["serviceName"] = ReadString(root, "path")
        };
        var outbound = new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "vmess",
            ["settings"] = new JsonObject
            {
                ["vnext"] = new JsonArray(new JsonObject
                {
                    ["address"] = host, ["port"] = port, ["users"] = new JsonArray(user)
                })
            },
            ["streamSettings"] = BuildStreamSettings(network, tlsValue == "tls" ? "tls" : "none", q, host)
        };
        return CreateProfile(raw, ReadString(root, "ps"), "VMess", host, port, outbound);
    }

    private static ServerProfile ParseTrojan(string raw)
    {
        var uri = ParseUri(raw);
        var host = RequireHost(uri);
        var port = RequirePort(uri);
        var password = Decode(uri.UserInfo);
        if (string.IsNullOrWhiteSpace(password)) throw new FormatException("رمز Trojan خالی است.");
        var q = Query(uri.Query);
        var network = NormalizeNetwork(Get(q, "type", Get(q, "network", "tcp")));
        var security = Get(q, "security", "tls").ToLowerInvariant();
        if (security is not ("none" or "tls"))
            throw new FormatException($"حالت امنیتی Trojan ({security}) پشتیبانی نمی‌شود.");
        var outbound = new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "trojan",
            ["settings"] = new JsonObject
            {
                ["servers"] = new JsonArray(new JsonObject { ["address"] = host, ["port"] = port, ["password"] = password })
            },
            ["streamSettings"] = BuildStreamSettings(network, security, q, host)
        };
        return CreateProfile(raw, uri.Fragment, "Trojan", host, port, outbound);
    }

    private static ServerProfile ParseShadowsocks(string raw)
    {
        var candidate = raw[(raw.IndexOf("://", StringComparison.Ordinal) + 3)..].Split('#')[0].Split('?')[0];
        if (!candidate.Contains('@'))
            candidate = TryDecodeBase64(candidate) ?? throw new FormatException("لینک Shadowsocks قابل رمزگشایی نیست.");
        if (!Uri.TryCreate("ss://" + candidate, UriKind.Absolute, out var uri))
            throw new FormatException("ساختار لینک Shadowsocks معتبر نیست.");
        var host = RequireHost(uri);
        var port = RequirePort(uri);
        var credentials = Decode(uri.UserInfo);
        if (!credentials.Contains(':')) credentials = TryDecodeBase64(credentials) ?? credentials;
        var delimiter = credentials.IndexOf(':');
        if (delimiter <= 0 || delimiter == credentials.Length - 1)
            throw new FormatException("روش رمزنگاری یا رمز Shadowsocks ناقص است.");
        var method = credentials[..delimiter].Trim();
        var password = credentials[(delimiter + 1)..];
        var outbound = new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "shadowsocks",
            ["settings"] = new JsonObject
            {
                ["servers"] = new JsonArray(new JsonObject
                {
                    ["address"] = host, ["port"] = port, ["method"] = method, ["password"] = password
                })
            },
            ["streamSettings"] = new JsonObject { ["network"] = "tcp,udp" }
        };
        return CreateProfile(raw, uri.Fragment, "Shadowsocks", host, port, outbound);
    }

    private static ServerProfile ParseHysteria2(string raw)
    {
        // Hysteria2 URI authorities may contain multi-port ranges, which System.Uri cannot parse.
        var schemeEnd=raw.IndexOf("://",StringComparison.Ordinal);
        if(schemeEnd<=0)throw new FormatException("قالب لینک Hysteria2 معتبر نیست.");
        var rest=raw[(schemeEnd+3)..];var fi=rest.IndexOf('#');var fragment=fi>=0?rest[(fi+1)..]:"";if(fi>=0)rest=rest[..fi];
        var qi=rest.IndexOf('?');var query=qi>=0?rest[qi..]:"";var address=qi>=0?rest[..qi]:rest;
        var si=address.IndexOf('/');var authority=si>=0?address[..si]:address;
        var at=authority.LastIndexOf('@');if(at<0)throw new FormatException("رمز Hysteria2 در لینک وجود ندارد.");
        var auth=Decode(authority[..at]);var hp=authority[(at+1)..];if(string.IsNullOrWhiteSpace(auth))throw new FormatException("رمز Hysteria2 خالی است.");
        string host,portSpec;
        if(hp.StartsWith("[",StringComparison.Ordinal)){
            var close=hp.IndexOf(']');if(close<=1)throw new FormatException("آدرس IPv6 در لینک Hysteria2 معتبر نیست.");
            host=hp[1..close];var suffix=hp[(close+1)..];portSpec=suffix.StartsWith(':')?suffix[1..]:suffix.Length==0?"443":throw new FormatException("پورت Hysteria2 معتبر نیست.");
        }else{var colon=hp.LastIndexOf(':');if(colon<0){host=hp;portSpec="443";}else{host=hp[..colon];portSpec=hp[(colon+1)..];}}
        host=Decode(host);if(string.IsNullOrWhiteSpace(host))throw new FormatException("میزبان Hysteria2 خالی است.");if(string.IsNullOrWhiteSpace(portSpec))portSpec="443";
        var ports=portSpec.Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);if(ports.Length==0)throw new FormatException("پورت Hysteria2 معتبر نیست.");
        foreach(var segment in ports){var range=segment.Split('-',2,StringSplitOptions.TrimEntries);
            if(!int.TryParse(range[0],NumberStyles.None,CultureInfo.InvariantCulture,out var low)||low is <1 or >65535)throw new FormatException("بازهٔ پورت Hysteria2 معتبر نیست.");
            if(range.Length==2&&(!int.TryParse(range[1],NumberStyles.None,CultureInfo.InvariantCulture,out var high)||high<low||high>65535))throw new FormatException("بازهٔ پورت Hysteria2 معتبر نیست.");
        }
        var first=ports[0].Split('-',2,StringSplitOptions.TrimEntries)[0];int.TryParse(first,NumberStyles.None,CultureInfo.InvariantCulture,out var port);
        var q=Query(query);var tls=new JsonObject{["serverName"]=Get(q,"sni",Get(q,"peer",host)),["alpn"]=ToJsonArray(Get(q,"alpn","h3").Split(',').Select(v=>v.Trim()))};
        var fp=Get(q,"fp");if(fp.Length>0)tls["fingerprint"]=fp;if(IsTrue(Get(q,"insecure",Get(q,"allowInsecure","false"))))tls["allowInsecure"]=true;
        var pin=Get(q,"pinSHA256");if(pin.Length>0)tls["pinnedPeerCertSha256"]=pin;
        var stream=new JsonObject{["method"]="hysteria",["security"]="tls",["tlsSettings"]=tls,["hysteriaSettings"]=new JsonObject{["version"]=2,["auth"]=auth}};
        var outbound=new JsonObject{["tag"]="proxy",["protocol"]="hysteria",["settings"]=new JsonObject{["version"]=2,["address"]=host,["port"]=port},["streamSettings"]=stream};
        var masks=new JsonObject();var obfs=Get(q,"obfs").ToLowerInvariant();var obfsPassword=Get(q,"obfs-password");
        if(obfs.Length>0){if(obfs!="salamander")throw new FormatException("این نسخه از Xray برای Hysteria2 فقط obfs از نوع salamander را پشتیبانی می‌کند.");
            if(obfsPassword.Length==0)throw new FormatException("پارامتر obfs-password برای salamander اجباری است.");
            masks["udp"]=new JsonArray(new JsonObject{["type"]="salamander",["settings"]=new JsonObject{["password"]=obfsPassword}});
        }
        var hopping=ports.Length>1||ports[0].Contains('-')?string.Join(',',ports):"";var legacy=Get(q,"mport");if(legacy.Length>0)hopping=legacy;
        if(hopping.Length>0){var iv=Get(q,"interval",Get(q,"mportInterval","30"));
            if(!int.TryParse(iv,NumberStyles.None,CultureInfo.InvariantCulture,out var sec)||sec<5||sec>3600)throw new FormatException("فاصلهٔ تغییر پورت Hysteria2 باید بین ۵ تا ۳۶۰۰ ثانیه باشد.");
            masks["quicParams"]=new JsonObject{["udpHop"]=new JsonObject{["ports"]=hopping,["interval"]=sec}};
        }
        if(masks.Count>0)stream["finalmask"]=masks;
        return CreateProfile(raw,fragment,"Hysteria2",host,port,outbound);
    }

    private static ServerProfile ParseXrayJson(string raw)
    {
        var json = JsonNode.Parse(raw)?.AsObject() ?? throw new FormatException("JSON پیکربندی معتبر نیست.");
        if (json["outbounds"] is not JsonArray outbounds || outbounds.Count == 0 || outbounds[0] is not JsonObject outbound)
            throw new FormatException("JSON فاقد outbound معتبر است.");
        json["inbounds"] = new JsonArray();
        var protocol = outbound["protocol"]?.GetValue<string>() ?? "Custom";
        var settings = outbound["settings"] as JsonObject;
        var (host, port) = ("custom", 0);
        if (settings?["vnext"] is JsonArray vnext && vnext.FirstOrDefault() is JsonObject next)
            (host, port) = (next["address"]?.GetValue<string>() ?? host, ReadInt(next, "port"));
        else if (settings?["servers"] is JsonArray servers && servers.FirstOrDefault() is JsonObject server)
            (host, port) = (server["address"]?.GetValue<string>() ?? host, ReadInt(server, "port"));
        else if (settings is not null)
            (host, port) = (settings["address"]?.GetValue<string>() ?? host, ReadInt(settings, "port"));
        return new ServerProfile
        {
            Name = json["remarks"]?.GetValue<string>() ?? $"Custom {protocol}",
            Protocol = protocol.ToUpperInvariant(),
            Host = host,
            Port = port,
            RawUri = raw,
            ConfigJson = json.ToJsonString()
        };
    }

    private static JsonObject BuildStreamSettings(string network, string security, IReadOnlyDictionary<string, string> q, string host)
    {
        var stream = new JsonObject { ["network"] = network, ["security"] = security };
        if (security == "tls")
        {
            var tls = new JsonObject { ["serverName"] = Get(q, "sni", Get(q, "serverName", host)) };
            AddNonEmpty(tls, "fingerprint", Get(q, "fp"));
            var alpn = Get(q, "alpn");
            if (alpn.Length > 0) tls["alpn"] = ToJsonArray(alpn.Split(',').Select(x => x.Trim()));
            if (IsTrue(Get(q, "allowInsecure", Get(q, "insecure")))) tls["allowInsecure"] = true;
            stream["tlsSettings"] = tls;
        }
        else if (security == "reality")
        {
            stream["realitySettings"] = new JsonObject
            {
                ["serverName"] = Get(q, "sni", Get(q, "serverName", host)),
                ["fingerprint"] = Get(q, "fp", "chrome"),
                ["publicKey"] = Get(q, "pbk", Get(q, "publicKey")),
                ["shortId"] = Get(q, "sid", Get(q, "shortId")),
                ["spiderX"] = Get(q, "spx", Get(q, "spiderX", "/"))
            };
        }

        switch (network)
        {
            case "ws":
                var ws = new JsonObject { ["path"] = Get(q, "path", "/") };
                var wsHost = Get(q, "host");
                if (wsHost.Length > 0) ws["headers"] = new JsonObject { ["Host"] = wsHost };
                stream["wsSettings"] = ws;
                break;
            case "grpc":
                stream["grpcSettings"] = new JsonObject
                {
                    ["serviceName"] = Get(q, "serviceName", Get(q, "path")),
                    ["multiMode"] = Get(q, "mode").Equals("multi", StringComparison.OrdinalIgnoreCase)
                };
                break;
            case "httpupgrade":
                stream["httpupgradeSettings"] = new JsonObject { ["path"] = Get(q, "path", "/"), ["host"] = Get(q, "host") };
                break;
            case "xhttp":
                var xhttp = new JsonObject { ["path"] = Get(q, "path", "/"), ["host"] = Get(q, "host"), ["mode"] = Get(q, "mode", "auto") };
                var extra = Get(q, "extra");
                if (extra.StartsWith('{')) { try { xhttp["extra"] = JsonNode.Parse(extra); } catch (JsonException) { } }
                stream["xhttpSettings"] = xhttp;
                break;
        }
        return stream;
    }

    private static ServerProfile CreateProfile(string raw, string rawName, string protocol, string host, int port, JsonObject outbound)
    {
        var displayName = Decode(rawName.TrimStart('#'));
        if (displayName.Length == 0) displayName = $"{protocol} · {host}";
        var config = new JsonObject
        {
            ["log"] = new JsonObject { ["loglevel"] = "error" },
            ["inbounds"] = new JsonArray(),
            ["outbounds"] = new JsonArray(outbound, new JsonObject { ["tag"] = "direct", ["protocol"] = "freedom" })
        };
        return new ServerProfile
        {
            Name = displayName.Length > 80 ? displayName[..80] : displayName,
            Protocol = protocol, Host = host, Port = port, RawUri = raw, ConfigJson = config.ToJsonString()
        };
    }

    private static Uri ParseUri(string raw) =>
        Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : throw new FormatException("آدرس سرور قابل خواندن نیست.");

    private static string RequireHost(Uri uri) =>
        string.IsNullOrWhiteSpace(uri.Host) ? throw new FormatException("آدرس سرور در لینک معتبر نیست.") : uri.Host;

    private static int RequirePort(Uri uri) =>
        uri.Port is >= 1 and <= 65535 ? uri.Port : throw new FormatException("پورت سرور معتبر نیست.");

    private static Dictionary<string, string> Query(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            result[Decode(pair[0])] = pair.Length > 1 ? Decode(pair[1]) : "";
        }
        return result;
    }

    private static string Decode(string value)
    {
        try { return Uri.UnescapeDataString(value.Replace("+", "%2B", StringComparison.Ordinal)); }
        catch (UriFormatException) { return value; }
    }

    private static string? TryDecodeBase64(string value)
    {
        try
        {
            var compact = string.Concat(value.Where(c => !char.IsWhiteSpace(c))).Replace('-', '+').Replace('_', '/');
            compact = compact.PadRight(compact.Length + ((4 - compact.Length % 4) % 4), '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(compact)).Trim();
        }
        catch (FormatException) { return null; }
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values) =>
        new(values.Where(x => x.Length > 0).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());

    private static void AddNonEmpty(JsonObject node, string key, string value) { if (value.Length > 0) node[key] = value; }
    private static string Get(IReadOnlyDictionary<string, string> q, string key, string fallback = "") => q.TryGetValue(key, out var value) ? value : fallback;
    private static bool IsTrue(string value) => value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    private static int ReadInt(JsonObject node, string key) => int.TryParse(node[key]?.ToString(), out var value) ? value : 0;
    private static int ReadInt(JsonElement node, string key) => int.TryParse(ReadString(node, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    private static string ReadString(JsonElement node, string key) => node.TryGetProperty(key, out var value) ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString() : "";
    private static string NormalizeNetwork(string network)
    {
        var value = network.ToLowerInvariant() switch
        {
            "websocket" => "ws", "httpupgrade" or "http-upgrade" => "httpupgrade", "splithttp" or "split-http" => "xhttp",
            _ => network.ToLowerInvariant()
        };
        if (value is not ("tcp" or "ws" or "grpc" or "httpupgrade" or "xhttp"))
            throw new FormatException($"انتقال {network} در این نسخه پشتیبانی نمی‌شود.");
        return value;
    }
}

internal static class StringExtensions
{
    public static string IfBlank(this string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
