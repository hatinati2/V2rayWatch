using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace V2rayWatch.DesktopPrototype;

public sealed class ServerProfileStore
{
    private readonly string _filePath;
    private readonly string _legacyPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ServerProfileStore()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "V2rayWatch");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "data.v2vw");
        _legacyPath = Path.Combine(directory, "profiles.json");
    }

    public AppData Load()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var stored = File.ReadAllBytes(_filePath);
                if (stored.Length < 6 || Encoding.ASCII.GetString(stored, 0, 5) != "V2VW1")
                    throw new InvalidDataException("فرمت فایل داده شناخته نشد.");
                var clear = Dpapi.Unprotect(stored[5..]);
                return JsonSerializer.Deserialize<AppData>(clear, JsonOptions) ?? new AppData();
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException("بازکردن اطلاعات محلی ممکن نشد. این داده‌ها با حساب کاربری ویندوز فعلی رمزگذاری شده‌اند.");
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("فایل اطلاعات V2rayWatch معتبر نیست.");
            }
        }

        if (File.Exists(_legacyPath))
        {
            try
            {
                var oldProfiles = JsonSerializer.Deserialize<List<ServerProfile>>(File.ReadAllText(_legacyPath), JsonOptions) ?? [];
                return new AppData { Profiles = oldProfiles };
            }
            catch (JsonException) { }
        }
        return new AppData();
    }

    public void Save(AppData data)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        var protectedBytes = Dpapi.Protect(clear);
        var envelope = new byte[5 + protectedBytes.Length];
        Encoding.ASCII.GetBytes("V2VW1").CopyTo(envelope, 0);
        protectedBytes.CopyTo(envelope, 5);
        var tempPath = _filePath + ".tmp";
        File.WriteAllBytes(tempPath, envelope);
        if (File.Exists(_filePath)) File.Replace(tempPath, _filePath, null);
        else File.Move(tempPath, _filePath);
        // Remove the early prototype's plaintext profile file only after the encrypted replacement is safely written.
        if (File.Exists(_legacyPath))
        {
            try { File.Delete(_legacyPath); } catch { }
        }
        CryptographicOperations.ZeroMemory(clear);
        CryptographicOperations.ZeroMemory(protectedBytes);
    }

    private static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob { public int Length; public IntPtr Data; }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob output);
        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob output);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        public static byte[] Protect(byte[] input) => Transform(input, true);
        public static byte[] Unprotect(byte[] input) => Transform(input, false);

        private static byte[] Transform(byte[] input, bool protect)
        {
            var inputPtr = Marshal.AllocHGlobal(input.Length);
            try
            {
                Marshal.Copy(input, 0, inputPtr, input.Length);
                var source = new DataBlob { Length = input.Length, Data = inputPtr };
                var target = new DataBlob();
                var ok = protect
                    ? CryptProtectData(ref source, "V2rayWatch profile store", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref target)
                    : CryptUnprotectData(ref source, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref target);
                if (!ok) throw new CryptographicException(Marshal.GetLastWin32Error());
                try
                {
                    var result = new byte[target.Length];
                    Marshal.Copy(target.Data, result, 0, target.Length);
                    return result;
                }
                finally { if (target.Data != IntPtr.Zero) LocalFree(target.Data); }
            }
            finally { Marshal.FreeHGlobal(inputPtr); }
        }
    }
}
