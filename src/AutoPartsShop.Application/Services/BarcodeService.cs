using AutoPartsShop.Application.Interfaces;

namespace AutoPartsShop.Application.Services;

public class BarcodeService : IBarcodeService
{
    private int _counter;
    private readonly object _lock = new();

    public BarcodeService()
    {
        // Initialize counter based on current time to ensure uniqueness
        _counter = (int)(DateTime.UtcNow.Ticks % 1000000);
    }

    /// <summary>
    /// Generates a sequential barcode with the specified prefix.
    /// Format: {prefix}{6-digit sequential number}
    /// Example: AP000001, AP000002, etc.
    /// </summary>
    public string GenerateBarcode(string prefix = "AP")
    {
        lock (_lock)
        {
            _counter++;
            return $"{prefix}{_counter:D6}";
        }
    }

    /// <summary>
    /// Generates a barcode image as a byte array.
    /// Actual image generation via ZXing would be in the Infrastructure layer.
    /// This returns a placeholder byte array.
    /// </summary>
    public byte[] GenerateBarcodeImage(string barcode, int width = 300, int height = 100)
    {
        // Placeholder — actual barcode image generation would use
        // ZXing.Net or similar library in the Infrastructure layer.
        // For now, return a simple placeholder that represents a minimal PNG.

        // Create a simple 1x1 white pixel PNG as placeholder
        var placeholderPng = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE, // 8-bit RGB
            0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, // IDAT chunk
            0x08, 0xD7, 0x63, 0xF8, 0xFF, 0xFF, 0x3F, 0x00, // White pixel
            0x05, 0xFE, 0x02, 0xFE, 0xDC, 0xCC, 0x59, 0xE7, // CRC
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, // IEND chunk
            0xAE, 0x42, 0x60, 0x82
        };

        return placeholderPng;
    }

    /// <summary>
    /// Validates whether the given barcode string matches the expected format.
    /// Expected format: 2-letter prefix followed by 6 digits (e.g., AP000001).
    /// </summary>
    public bool ValidateBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return false;

        if (barcode.Length < 3)
            return false;

        // Check that the barcode starts with a 2-letter prefix
        var prefix = barcode[..2];
        if (!prefix.All(char.IsLetter))
            return false;

        // Check that the remaining characters are digits
        var numericPart = barcode[2..];
        return numericPart.All(char.IsDigit);
    }
}
