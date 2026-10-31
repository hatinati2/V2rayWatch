using System.IO;
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

var hy2 = ServerProfileParser.ParseOne("hysteria2://password@hy2.example.net:443,5000-6000?sni=hy2.example.net&obfs=salamander&obfs-password=secret&insecure=1&pinSHA256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa#Hysteria");
Check(hy2.Protocol == "Hysteria2" && hy2.Port == 443, "Hysteria2 parsed");
using (var doc = JsonDocument.Parse(hy2.ConfigJson))
{
    var outbound=doc.RootElement.GetProperty("outbounds")[0];var stream=outbound.GetProperty("streamSettings");
    Check(outbound.GetProperty("protocol").GetString()=="hysteria","Hysteria2 outbound protocol");
    Check(stream.GetProperty("method").GetString()=="hysteria","current Xray Hysteria transport method");
    Check(stream.GetProperty("hysteriaSettings").GetProperty("version").GetInt32()==2,"Hysteria2 version");
    Check(stream.GetProperty("finalmask").GetProperty("udp")[0].GetProperty("type").GetString()=="salamander","Salamander obfuscation retained");
    Check(stream.GetProperty("finalmask").GetProperty("quicParams").GetProperty("udpHop").GetProperty("ports").GetString()=="443,5000-6000","multi-port hopping retained");
    Check(stream.GetProperty("tlsSettings").GetProperty("pinnedPeerCertSha256").GetString()=="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","certificate pin retained");
    Check(hy2.Port==443,"initial Hysteria2 port");
    Check(!hy2.ConfigJson.Contains("allowInsecure",StringComparison.OrdinalIgnoreCase),"legacy insecure TLS bypass not sent to Xray");
    Check(!string.IsNullOrWhiteSpace(hy2.ImportWarning),"legacy insecure TLS request is surfaced as a warning");
}
var vlessLegacyInsecure=ServerProfileParser.ParseOne("vless://11111111-1111-4111-8111-111111111111@vless.example.net:443?security=tls&sni=vless.example.net&insecure=1#legacy-tls");
Check(!vlessLegacyInsecure.ConfigJson.Contains("allowInsecure",StringComparison.OrdinalIgnoreCase),"legacy insecure flag removed from VLESS TLS too");
var hy2Gecko=ServerProfileParser.ParseOne("hy2://password@hy2.example.net:443?obfs=gecko&obfs-password=secret#Gecko");
using(var geckoDoc=JsonDocument.Parse(hy2Gecko.ConfigJson))
{
 var geckoMask=geckoDoc.RootElement.GetProperty("outbounds")[0].GetProperty("streamSettings").GetProperty("finalmask").GetProperty("udp")[0];
 Check(geckoMask.GetProperty("type").GetString()=="salamander","Gecko uses Salamander base mask");
 Check(geckoMask.GetProperty("settings").GetProperty("packetSize").GetString()=="512-1200","Gecko fragmentation range mapped");
}
var hy2Ech=ServerProfileParser.ParseOne("hy2://password@hy2.example.net:443?ech=YWJj#ECH");
using(var echDoc=JsonDocument.Parse(hy2Ech.ConfigJson))
 Check(echDoc.RootElement.GetProperty("outbounds")[0].GetProperty("streamSettings").GetProperty("tlsSettings").GetProperty("echConfigList").GetString()=="YWJj","Hysteria2 ECH config retained");

var badHy2=false;try{ServerProfileParser.ParseOne("hy2://pass@hy2.example.net:443?obfs=salamander#missing-password");}catch(FormatException){badHy2=true;}
Check(badHy2,"Salamander needs a password");
var xrayPath=Environment.GetEnvironmentVariable("V2RAYWATCH_XRAY_PATH");
if(!string.IsNullOrWhiteSpace(xrayPath)&&File.Exists(xrayPath)){ValidateWithXray(xrayPath!,hy2);ValidateWithXray(xrayPath!,hy2Gecko);}

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

var subscriptionLanguage = new SubscriptionSource { English = true };
Check(subscriptionLanguage.LastUpdatedDisplay == "Not fetched yet", "subscription empty state is English");
Check(subscriptionLanguage.RemoveActionDisplay == "Remove", "subscription row remove action is English");
subscriptionLanguage.English = false;
Check(subscriptionLanguage.LastUpdatedDisplay == "هنوز دریافت نشده", "subscription empty state is Persian");
Check(subscriptionLanguage.RemoveActionDisplay == "حذف", "subscription row remove action is Persian");

var englishSubscriptionHeaders = SubscriptionDisplayText.GetColumnHeaders(true);
Check(englishSubscriptionHeaders.SequenceEqual(new[] { "Name", "Links", "Last fetched", "Action" }), "subscription table headers are English");
Check(englishSubscriptionHeaders.All(header => header.All(character => character < 128)), "English subscription headers contain no Persian or Arabic characters");
var persianSubscriptionHeaders = SubscriptionDisplayText.GetColumnHeaders(false);
Check(persianSubscriptionHeaders.SequenceEqual(new[] { "نام", "تعداد", "آخرین دریافت", "عملیات" }), "subscription table headers are Persian");

var subscriptionOwned = new ServerProfile { SourceSubscriptionId = "subscription-a" };
var secondSubscriptionOwned = new ServerProfile { SourceSubscriptionId = "subscription-a" };
var manuallyAdded = new ServerProfile();
var otherSubscriptionOwned = new ServerProfile { SourceSubscriptionId = "subscription-b" };
var profilesForRemoval = new List<ServerProfile> { subscriptionOwned, secondSubscriptionOwned, manuallyAdded, otherSubscriptionOwned };
var removedOwnedCount = SubscriptionProfileOperations.RemoveOwnedProfiles(profilesForRemoval, "subscription-a");
Check(removedOwnedCount == 2 && !profilesForRemoval.Contains(subscriptionOwned) && !profilesForRemoval.Contains(secondSubscriptionOwned), "removing a subscription removes all of its owned profiles");
Check(profilesForRemoval.Contains(manuallyAdded) && profilesForRemoval.Contains(otherSubscriptionOwned), "removing a subscription preserves manual and other-subscription profiles");
Check(SubscriptionProfileOperations.RemoveOwnedProfiles(profilesForRemoval, "") == 0, "empty subscription ID cannot remove profiles");


// Shared cross-platform fixture: independently fixed salt/nonce and PBKDF2-HMAC-SHA256 vector.
var sharedBackupFixture = Convert.FromBase64String("VjJWV0JBSzEBAAST4AABAgMEBQYHCAkKCwwNDg8QERITFBUWFxgZGhvMKv8G4VPN6nt9iBhr6otlXa4yXzwQuhfxh7yZrH5XRKwbJQ1bG/pqQWwxhtEiuARLuQvejw==");
var sharedFixturePlaintext = PortableBackupCodec.Decrypt(
    sharedBackupFixture,
    "V2rayWatch-fixture-passphrase".ToCharArray());
try
{
    Check(Encoding.UTF8.GetString(sharedFixturePlaintext) == "v2raywatch-cross-platform-test-vector",
        "Windows decrypts the shared Android-compatible interoperability fixture");
}
finally
{
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(sharedFixturePlaintext);
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(sharedBackupFixture);
}

// Portable backup interoperability contract (Android V2VWBAK1 format).
var portableSettings = new MonitorSettings
{
    IntervalSeconds = 60,
    ConsecutiveFailureThreshold = 3,
    TimeoutSeconds = 21,
    PrimaryTestUrl = "https://www.google.com/generate_204",
    FallbackTestUrl = "https://www.gstatic.com/generate_204",
    NotifyOnDisconnect = false,
    NotifyOnRecovery = true
};
var portableProfile = new ServerProfile
{
    Name = vless.Name,
    Protocol = vless.Protocol,
    Host = vless.Host,
    Port = vless.Port,
    RawUri = vlessRaw,
    ConfigJson = vless.ConfigJson,
    UptimeHistory = ["N", "G", "R"]
};
var backupPassphrase = "V2rayWatch-Portable-Backup-2026".ToCharArray();
var encryptedBackup = PortableBackupService.CreateBackup([portableProfile], portableSettings, backupPassphrase);
Check(Encoding.ASCII.GetString(encryptedBackup, 0, 8) == "V2VWBAK1", "portable backup uses Android magic header");
Check(encryptedBackup[8] == 1, "portable backup format version matches Android");
var portablePlaintext = PortableBackupCodec.Decrypt(encryptedBackup, backupPassphrase);
try
{
    using var backupJson = JsonDocument.Parse(portablePlaintext);
    var root = backupJson.RootElement;
    Check(root.GetProperty("formatVersion").GetInt32() == 1, "portable JSON format version is Android-compatible");
    Check(root.TryGetProperty("createdAtEpochMs", out _), "portable backup contains Android timestamp property");
    Check(root.GetProperty("profiles")[0].GetProperty("rawLink").GetString() == vlessRaw, "profile rawLink property matches Android schema");
    Check(root.GetProperty("settings").GetProperty("notifyOnDisconnect").GetBoolean() == false, "notification settings use Android property names");
}
finally
{
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(portablePlaintext);
}

var restorePassphrase = "V2rayWatch-Portable-Backup-2026".ToCharArray();
var portableRestore = PortableBackupService.RestoreBackup(encryptedBackup, restorePassphrase, Array.Empty<ServerProfile>());
Check(portableRestore.AddedProfiles == 1 && portableRestore.SkippedProfiles == 0, "portable backup adds profiles on an empty Windows install");
Check(portableRestore.Profiles.Single().RawUri == vlessRaw, "portable restore preserves shareable proxy link");
Check(portableRestore.Profiles.Single().Status == "Unknown" && portableRestore.Profiles.Single().DelayMs is null, "portable restore resets transient connection state");
Check(portableRestore.Profiles.Single().UptimeHistory.TakeLast(3).SequenceEqual(new[] { "N", "G", "R" }), "portable restore maps Android uptime history to Windows history");
Check(portableRestore.Settings.IntervalSeconds == 60 && portableRestore.Settings.ConsecutiveFailureThreshold == 3, "portable restore preserves Android monitoring settings");
Check(!portableRestore.Settings.NotifyOnDisconnect && portableRestore.Settings.NotifyOnRecovery, "portable restore preserves Android notification preferences");
var duplicateRestore = PortableBackupService.RestoreBackup(
    encryptedBackup,
    "V2rayWatch-Portable-Backup-2026".ToCharArray(),
    [portableProfile]);
Check(duplicateRestore.AddedProfiles == 0 && duplicateRestore.SkippedProfiles == 1 && duplicateRestore.Profiles.Count == 1,
    "portable restore skips duplicate raw links without deleting existing profiles");

var wrongPassphraseRejected = false;
try
{
    _ = PortableBackupCodec.Decrypt(encryptedBackup, "Wrong-Passphrase-123".ToCharArray());
}
catch (System.Security.Cryptography.CryptographicException)
{
    wrongPassphraseRejected = true;
}
Check(wrongPassphraseRejected, "portable backup rejects an incorrect passphrase");

var modifiedBackup = encryptedBackup.ToArray();
modifiedBackup[^1] ^= 0x01;
var tamperRejected = false;
try
{
    _ = PortableBackupCodec.Decrypt(modifiedBackup, "V2rayWatch-Portable-Backup-2026".ToCharArray());
}
catch (System.Security.Cryptography.CryptographicException)
{
    tamperRejected = true;
}
Check(tamperRejected, "portable backup rejects modified ciphertext");
System.Security.Cryptography.CryptographicOperations.ZeroMemory(encryptedBackup);
System.Security.Cryptography.CryptographicOperations.ZeroMemory(modifiedBackup);
Array.Clear(backupPassphrase, 0, backupPassphrase.Length);
Array.Clear(restorePassphrase, 0, restorePassphrase.Length);

// QR PNG generation and image-file decoding round trip.
var qrPng = QrCodeService.GeneratePng(vlessRaw);
Check(qrPng.Length > 100, "QR code PNG image generated");
var qrTempPath = Path.Combine(Path.GetTempPath(), "v2vw-qr-test-" + Guid.NewGuid().ToString("N") + ".png");
try
{
    File.WriteAllBytes(qrTempPath, qrPng);
    var decodedQr = QrCodeService.DecodeImageFile(qrTempPath);
    Check(decodedQr == vlessRaw, "QR image scanner recovers the exact proxy link");
}
finally
{
    try { File.Delete(qrTempPath); } catch { }
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(qrPng);
}

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
