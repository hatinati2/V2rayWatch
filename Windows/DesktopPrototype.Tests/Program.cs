using System.Text;
using V2rayWatch.DesktopPrototype;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException("Test failed: " + message);
}

var vless = ServerProfileParser.ParseOne("vless://11111111-1111-1111-1111-111111111111@edge.example.net:443?type=ws&security=tls#VLESS%20Main");
Check(vless.Protocol == "Vless", "VLESS protocol detected");
Check(vless.Host == "edge.example.net" && vless.Port == 443, "VLESS host and port parsed");
Check(vless.Name == "VLESS Main", "VLESS profile name decoded");

var trojan = ServerProfileParser.ParseOne("trojan://secret@trojan.example.net:8443#Trojan");
Check(trojan.Protocol == "Trojan" && trojan.Port == 8443, "Trojan parsed");

var ss = ServerProfileParser.ParseOne("ss://YWVzLTI1Ni1nY206cGFzcw==@ss.example.net:8388#Shadowsocks");
Check(ss.Protocol == "Shadowsocks" && ss.Host == "ss.example.net" && ss.Port == 8388, "Shadowsocks parsed");

var vmessJson = """{"v":"2","ps":"VMess Node","add":"vmess.example.net","port":"2053","id":"11111111-1111-1111-1111-111111111111","net":"ws","tls":"tls"}""";
var vmessPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(vmessJson));
var vmess = ServerProfileParser.ParseOne("vmess://" + vmessPayload);
Check(vmess.Protocol == "VMess" && vmess.Name == "VMess Node" && vmess.Port == 2053, "VMess JSON/base64 parsed");

var hy2 = ServerProfileParser.ParseOne("hysteria2://password@hy2.example.net:443#Hysteria");
Check(hy2.Protocol == "Hysteria2" && hy2.Port == 443, "Hysteria2 parsed");

var multi = ServerProfileParser.ParseMany(
    "vless://id@one.example.net:443#One\n" +
    "trojan://secret@two.example.net:443#Two");
Check(multi.Count == 2, "multiple profile lines imported");

var encodedSubscription = Convert.ToBase64String(Encoding.UTF8.GetBytes(
    "vless://id@one.example.net:443#One\n" +
    "trojan://secret@two.example.net:443#Two"));
Check(ServerProfileParser.ParseMany(encodedSubscription).Count == 2, "base64 subscription imported");

var rejected = false;
try { ServerProfileParser.ParseOne("ftp://not-a-proxy.example.net:21"); }
catch (FormatException) { rejected = true; }
Check(rejected, "unsupported scheme rejected");

Console.WriteLine("All V2rayWatch profile parser smoke tests passed.");
