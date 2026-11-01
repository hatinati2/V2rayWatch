using System.Text;
using QRCoder;

namespace V2rayWatch.Server.Web;

public static class QrCodeService
{
    public static byte[] GeneratePng(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("QR content is empty.", nameof(content));

        if (Encoding.UTF8.GetByteCount(content) > 1_800)
            throw new InvalidDataException("This profile link is too long for a reliable QR code.");

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(8);
    }
}
