using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace AutoPartsShop.UI.Converters;

/// <summary>
/// Converts a file path string to a BitmapImage for Image.Source binding.
/// Uses BitmapCacheOption.OnLoad + Freeze() to release the file handle immediately,
/// preventing "file in use" errors when deleting or replacing the logo.
/// Returns null for empty/invalid paths so the Image shows nothing (fallback element is visible).
/// </summary>
public class StringToImageSourceConverter : IValueConverter
{
    public static StringToImageSourceConverter Instance { get; } = new StringToImageSourceConverter();

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // Read entire file into memory
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();
                bitmap.Freeze(); // Release file handle & allow cross-thread access
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null; // One-way only
    }
}
