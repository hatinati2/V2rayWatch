namespace V2rayWatch.Server.Core;

public enum ProxyProtocol { Vless, Vmess, Trojan, Shadowsocks, Hysteria2, XrayJson }
public sealed record ServerProfile(Guid Id, string Name, ProxyProtocol Protocol, string ShareLink, bool Enabled, DateTimeOffset CreatedAtUtc);
public enum ProbeStatus { Online, Offline, Error }
public sealed record ProbeResult(Guid ProfileId, ProbeStatus Status, long? LatencyMilliseconds, DateTimeOffset CheckedAtUtc, string? ErrorCode = null);
