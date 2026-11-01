using System.Net;
using System.Text.RegularExpressions;
namespace V2rayWatch.Server.Web;
public sealed class ServerOptions
{
 public string ListenAddress { get; init; } = "0.0.0.0";
 public int Port { get; init; } = 6100;
 public string BasePath { get; init; } = "/vw-dev-6f2e0a5f9d4c8b1e";
 public string DataDirectory { get; init; } = "/var/lib/v2raywatch";
 public void Validate()
 {
  if (!IPAddress.TryParse(ListenAddress, out _)) throw new InvalidOperationException("ListenAddress must be an IP address.");
  if (Port is < 1024 or > 65535) throw new InvalidOperationException("Port must be between 1024 and 65535.");
  if (!Regex.IsMatch(BasePath, "^/[A-Za-z0-9_-]{16,64}$", RegexOptions.CultureInvariant)) throw new InvalidOperationException("BasePath must be / followed by 16-64 safe characters.");
  if (string.IsNullOrWhiteSpace(DataDirectory)) throw new InvalidOperationException("DataDirectory cannot be empty.");
 }
}
