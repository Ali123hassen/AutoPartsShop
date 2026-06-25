using System;
using System.Globalization;
using System.Windows.Data;

namespace AutoPartsShop.UI.Converters;

public class InvertBoolConverter : IValueConverter
{
    public static InvertBoolConverter Instance { get; } = new InvertBoolConverter();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}
