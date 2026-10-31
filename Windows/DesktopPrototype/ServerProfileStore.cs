using System.IO;
using System.Text.Json;

namespace V2rayWatch.DesktopPrototype;

public sealed class ServerProfileStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ServerProfileStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "V2rayWatch");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "profiles.json");
    }

    public List<ServerProfile> Load()
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<ServerProfile>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            var backup = _filePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            File.Move(_filePath, backup, overwrite: true);
            return [];
        }
    }

    public void Save(IEnumerable<ServerProfile> profiles)
    {
        var temporaryPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(temporaryPath, json);

        if (File.Exists(_filePath))
            File.Replace(temporaryPath, _filePath, null);
        else
            File.Move(temporaryPath, _filePath);
    }
}
