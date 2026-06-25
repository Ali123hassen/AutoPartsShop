using System.Globalization;
using System.Windows.Data;

namespace AutoPartsShop.UI.Converters;

public class EditingPasswordLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEditing && isEditing)
            return "كلمة المرور الجديدة (اتركها فارغة لعدم التغيير)";
        return "كلمة المرور";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
