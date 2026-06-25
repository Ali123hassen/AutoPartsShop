using System.Globalization;
using System.Windows.Data;

namespace AutoPartsShop.UI.Converters;

public class SaveButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEditing && isEditing)
            return "حفظ التعديلات";
        return "إضافة";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
