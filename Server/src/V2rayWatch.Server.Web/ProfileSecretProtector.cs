using System.Security.Cryptography;

namespace V2rayWatch.Server.Web;

public sealed class ProfileSecretProtector : IDisposable
{
    private readonly byte[] _key;

    public ProfileSecretProtector(string keyFile)
    {
        if (!File.Exists(keyFile))
            throw new FileNotFoundException("Profile encryption key is missing. Run the Ubuntu installer again after backing up the key.", keyFile);
        _key = Convert.FromBase64String(File.ReadAllText(keyFile).Trim());
        if (_key.Length != 32)
            throw new InvalidOperationException("Profile encryption key must decode to exactly 32 bytes.");
    }

    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[plaintext.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var envelope = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, envelope, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, envelope, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, envelope, nonce.Length + tag.Length, ciphertext.Length);
        CryptographicOperations.ZeroMemory(ciphertext);
        return envelope;
    }

    public byte[] Unprotect(ReadOnlySpan<byte> envelope)
    {
        if (envelope.Length < 28)
            throw new CryptographicException("Encrypted profile payload is truncated.");
        var nonce = envelope[..12];
        var tag = envelope.Slice(12, 16);
        var ciphertext = envelope[28..];
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(_key);
}
