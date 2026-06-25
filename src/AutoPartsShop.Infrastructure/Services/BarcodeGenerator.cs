using AutoPartsShop.Application.Interfaces;
using ZXing;
using ZXing.Common;

namespace AutoPartsShop.Infrastructure.Services;

public class BarcodeGenerator : IBarcodeService
{
    public string GenerateBarcode(string prefix = "AP")
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(100, 999);
        return $"{prefix}{timestamp}{random}";
    }

    public byte[] GenerateBarcodeImage(string barcode, int width = 300, int height = 100)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.CODE_128,
            Options = new EncodingOptions
            {
                Width = width,
                Height = height,
                Margin = 10
            }
        };

        var pixelData = writer.Write(barcode);
        using var bitmap = new System.Drawing.Bitmap(pixelData.Width, pixelData.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using var ms = new MemoryStream();

        var bitmapData = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, pixelData.Width, pixelData.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            bitmap.PixelFormat);

        System.Runtime.InteropServices.Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
        bitmap.UnlockBits(bitmapData);
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

        return ms.ToArray();
    }

    public bool ValidateBarcode(string barcode)
    {
        return !string.IsNullOrWhiteSpace(barcode) && barcode.Length >= 4;
    }
}
