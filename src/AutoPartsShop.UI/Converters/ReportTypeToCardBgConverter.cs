using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

using WpfColor = System.Windows.Media.Color;

namespace AutoPartsShop.UI.Converters;

/// <summary>
/// Converts a SelectedReportType (int) + ConverterParameter (int) to a background brush.
/// If the card's type matches the selected type, returns a highlighted background; otherwise White.
/// </summary>
public class ReportTypeToCardBgConverter : IValueConverter
{
    private static readonly SolidColorBrush SelectedBrush =
        new SolidColorBrush(WpfColor.FromRgb(0xEF, 0xF6, 0xFF)); // Light blue

    private static readonly SolidColorBrush DefaultBrush =
        new SolidColorBrush(WpfColor.FromRgb(0xFF, 0xFF, 0xFF)); // White

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int selectedType && parameter is string param && int.TryParse(param, out int cardType))
        {
            return selectedType == cardType ? SelectedBrush : DefaultBrush;
        }
        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a SelectedReportType (int) + ConverterParameter (int) to a border brush.
/// If the card's type matches the selected type, returns a colored border; otherwise light gray.
/// </summary>
public class ReportTypeToCardBorderConverter : IValueConverter
{
    private static readonly SolidColorBrush[] SelectedBorders =
    [
        new SolidColorBrush(WpfColor.FromRgb(0x1A, 0x52, 0x76)), // Primary - Sales
        new SolidColorBrush(WpfColor.FromRgb(0x27, 0xAE, 0x60)), // Success - Profit
        new SolidColorBrush(WpfColor.FromRgb(0xF3, 0x9C, 0x12)), // Warning - Stock
        new SolidColorBrush(WpfColor.FromRgb(0x34, 0x98, 0xDB)), // Info - Top Selling
        new SolidColorBrush(WpfColor.FromRgb(0xE7, 0x4C, 0x3C)), // Danger - Returns
    ];

    private static readonly SolidColorBrush DefaultBorder =
        new SolidColorBrush(WpfColor.FromRgb(0xDE, 0xE2, 0xE6));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int selectedType && parameter is string param && int.TryParse(param, out int cardType))
        {
            if (selectedType == cardType && cardType >= 0 && cardType < SelectedBorders.Length)
            {
                return SelectedBorders[cardType];
            }
        }
        return DefaultBorder;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts true to Collapsed, false to Visible (inverse of BoolToVisibilityConverter).
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
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
        return true;
    }
}
