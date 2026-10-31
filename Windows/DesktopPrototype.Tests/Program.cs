using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using V2rayWatch.DesktopPrototype;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException("Test failed: " + message);
}

var vlessRaw = "vless://11111111-1111-1111-1111-111111111111@edge.example.net:443?type=ws&security=tls&sni=edge.example.net&path=%2Fwatch#VLESS%20Main";
var vless = ServerProfileParser.ParseOne(vlessRaw);
Check(vless.Protocol == "VLESS", "VLESS protocol detected");
Check(vless.Host == "edge.example.net" && vless.Port == 443, "VLESS host and port parsed");
Check(vless.Name == "VLESS Main", "VLESS profile name decoded");
using (var doc = JsonDocument.Parse(vless.ConfigJson))
{
    var outbound = doc.RootElement.GetProperty("outbounds")[0];
    Check(outbound.GetProperty("protocol").GetString() == "vless", "VLESS outbound config generated");
    Check(outbound.GetProperty("streamSettings").GetProperty("network").GetString() == "ws", "VLESS transport config generated");
    Check(outbound.GetProperty("streamSettings").GetProperty("tlsSettings").GetProperty("serverName").GetString() == "edge.example.net", "TLS SNI preserved");
}

var reality = ServerProfileParser.ParseOne("vless://11111111-1111-1111-1111-111111111111@reality.example.net:443?type=tcp&security=reality&sni=www.example.com&fp=chrome&pbk=public-key&sid=1234#Reality");
using (var doc = JsonDocument.Parse(reality.ConfigJson))
    Check(doc.RootElement.GetProperty("outbounds")[0].GetProperty("streamSettings").TryGetProperty("realitySettings", out _), "VLESS Reality settings generated");

var trojan = ServerProfileParser.ParseOne("trojan://secret@trojan.example.net:8443?sni=trojan.example.net#Trojan");
Check(trojan.Protocol == "Trojan" && trojan.Port == 8443, "Trojan parsed");
using (var doc = JsonDocument.Parse(trojan.ConfigJson))
    Check(doc.RootElement.GetProperty("outbounds")[0].GetProperty("settings").GetProperty("servers")[0].GetProperty("password").GetString() == "secret", "Trojan outbound password preserved");

var ss = ServerProfileParser.ParseOne("ss://YWVzLTI1Ni1nY206cGFzcw==@ss.example.net:8388#Shadowsocks");
Check(ss.Protocol == "Shadowsocks" && ss.Host == "ss.example.net" && ss.Port == 8388, "Shadowsocks parsed");
using (var doc = JsonDocument.Parse(ss.ConfigJson))
    Check(doc.RootElement.GetProperty("outbounds")[0].GetProperty("protocol").GetString() == "shadowsocks", "Shadowsocks outbound generated");

var vmessJson = """{"v":"2","ps":"VMess Node","add":"vmess.example.net","port":"2053","id":"11111111-1111-1111-1111-111111111111","net":"ws","tls":"tls","host":"cdn.example.net","path":"/vmess"}""";
var vmessPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(vmessJson));
var vmess = ServerProfileParser.ParseOne("vmess://" + vmessPayload);
Check(vmess.Protocol == "VMess" && vmess.Name == "VMess Node" && vmess.Port == 2053, "VMess JSON/base64 parsed");
using (var doc = JsonDocument.Parse(vmess.ConfigJson))
    Check(doc.RootElement.GetProperty("outbounds")[0].GetProperty("streamSettings").GetProperty("wsSettings").GetProperty("path").GetString() == "/vmess", "VMess transport fields preserved");

var hy2 = ServerProfileParser.ParseOne("hysteria2://password@hy2.example.net:443,5000-6000?sni=hy2.example.net&obfs=salamander&obfs-password=secret&insecure=1&pinSHA256=deadbeef#Hysteria");
Check(hy2.Protocol == "Hysteria2" && hy2.Port == 443, "Hysteria2 parsed");
using (var doc = JsonDocument.Parse(hy2.ConfigJson))
{
    var outbound=doc.RootElement.GetProperty("outbounds")[0];var stream=outbound.GetProperty("streamSettings");
    Check(outbound.GetProperty("protocol").GetString()=="hysteria","Hysteria2 outbound protocol");
    Check(stream.GetProperty("method").GetString()=="hysteria","current Xray Hysteria transport method");
    Check(stream.GetProperty("hysteriaSettings").GetProperty("version").GetInt32()==2,"Hysteria2 version");
    Check(stream.GetProperty("finalmask").GetProperty("udp")[0].GetProperty("type").GetString()=="salamander","Salamander obfuscation retained");
    Check(stream.GetProperty("finalmask").GetProperty("quicParams").GetProperty("udpHop").GetProperty("ports").GetString()=="443,5000-6000","multi-port hopping retained");
    Check(stream.GetProperty("tlsSettings").GetProperty("pinnedPeerCertSha256").GetString()=="deadbeef","certificate pin retained");
    Check(hy2.Port==443,"initial Hysteria2 port");
}
var badHy2=false;try{ServerProfileParser.ParseOne("hy2://pass@hy2.example.net:443?obfs=salamander#missing-password");}catch(FormatException){badHy2=true;}
Check(badHy2,"Salamander needs a password");
var xrayPath=Environment.GetEnvironmentVariable("V2RAYWATCH_XRAY_PATH");
if(!string.IsNullOrWhiteSpace(xrayPath)&&File.Exists(xrayPath))ValidateWithXray(xrayPath,hy2);

var multi = ServerProfileParser.ParseMany(
    "vless://id@one.example.net:443#One\n" +
    "trojan://secret@two.example.net:443#Two");
Check(multi.Count == 2, "multiple profile lines imported");

var encodedSubscription = Convert.ToBase64String(Encoding.UTF8.GetBytes(
    "vless://id@one.example.net:443#One\n" +
    "trojan://secret@two.example.net:443#Two"));
Check(ServerProfileParser.ParseMany(encodedSubscription).Count == 2, "base64 subscription payload decoded");

var xrayJson = """{"remarks":"Imported JSON","inbounds":[{"port":10000}],"outbounds":[{"protocol":"vless","settings":{"vnext":[{"address":"json.example.net","port":443,"users":[{"id":"11111111-1111-1111-1111-111111111111"}]}]}}]}""";
var custom = ServerProfileParser.ParseOne(xrayJson);
Check(custom.Name == "Imported JSON" && custom.Host == "json.example.net", "full Xray JSON imported");
using (var doc = JsonDocument.Parse(custom.ConfigJson))
    Check(doc.RootElement.GetProperty("inbounds").GetArrayLength() == 0, "inbounds stripped before temporary tester adds its local SOCKS listener");

var rejected = false;
try { ServerProfileParser.ParseOne("ftp://not-a-proxy.example.net:21"); }
catch (FormatException) { rejected = true; }
Check(rejected, "unsupported scheme rejected");

var malformed = false;
try { ServerProfileParser.ParseMany("this is not a subscription"); }
catch (FormatException) { malformed = true; }
Check(malformed, "invalid subscription response rejected instead of silently adding zero profiles");

Console.WriteLine("All V2rayWatch standalone profile parser smoke tests passed.");
static void ValidateWithXray(string xrayPath,ServerProfile profile)
{
 var config=JsonNode.Parse(profile.ConfigJson)?.AsObject()??throw new InvalidOperationException("Profile config was not a JSON object.");
 config["inbounds"]=new JsonArray(new JsonObject{["tag"]="v2vw-test-socks",["listen"]="127.0.0.1",["port"]=10808,["protocol"]="socks",["settings"]=new JsonObject{["auth"]="noauth",["udp"]=false}});
 var temp=Path.Combine(Path.GetTempPath(),"v2vw-xray-test-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(temp,config.ToJsonString());
 try{
  var start=new ProcessStartInfo{FileName=xrayPath,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  start.ArgumentList.Add("run");start.ArgumentList.Add("-test");start.ArgumentList.Add("-c");start.ArgumentList.Add(temp);
  using var proc=Process.Start(start)??throw new InvalidOperationException("Could not start Xray config test.");
  var stdout=proc.StandardOutput.ReadToEndAsync();var stderr=proc.StandardError.ReadToEndAsync();
  if(!proc.WaitForExit(15000)){try{proc.Kill(true);}catch{}throw new TimeoutException("Xray Hysteria2 config validation timed out.");}
  Task.WaitAll(stdout,stderr);if(proc.ExitCode!=0)throw new InvalidOperationException("Xray rejected Hysteria2 config: "+(stderr.Result+" "+stdout.Result).Trim());
  Console.WriteLine("Official Xray accepted the generated Hysteria2 config.");
 }finally{try{File.Delete(temp);}catch{}}
}
