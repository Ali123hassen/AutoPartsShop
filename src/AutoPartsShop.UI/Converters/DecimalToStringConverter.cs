using System.Globalization;
using System.Windows.Data;

namespace AutoPartsShop.UI.Converters;

public class DecimalToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal decimalValue)
        {
            return decimalValue.ToString("N2", culture);
        }
        if (value is double doubleValue)
        {
            return doubleValue.ToString("N2", culture);
        }
        if (value is float floatValue)
        {
            return floatValue.ToString("N2", culture);
        }
        return "0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue && decimal.TryParse(stringValue, out decimal result))
        {
            return result;
        }
        return 0m;
    }
}
