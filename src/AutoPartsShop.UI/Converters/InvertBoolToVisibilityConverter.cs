using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AutoPartsShop.UI.Converters;

/// <summary>
/// Converts a bool value to Visibility, but inverted:
/// true  → Collapsed
/// false → Visible
/// </summary>
public class InvertBoolToVisibilityConverter : IValueConverter
{
    public static InvertBoolToVisibilityConverter Instance { get; } = new InvertBoolToVisibilityConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}
