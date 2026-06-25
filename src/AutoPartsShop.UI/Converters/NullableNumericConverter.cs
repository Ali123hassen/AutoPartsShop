using System.Globalization;
using System.Windows.Data;

namespace AutoPartsShop.UI.Converters;

/// <summary>
/// Converts between nullable numeric types (decimal?, int?, double?) and string.
/// Handles empty strings gracefully without throwing FormatException.
/// Supports both Arabic and English decimal separators.
/// </summary>
public class NullableNumericConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        // For decimal values
        if (value is decimal d)
            return d.ToString("0.##", CultureInfo.InvariantCulture);

        // For integer values
        if (value is int i)
            return i.ToString(CultureInfo.InvariantCulture);

        // For double values
        if (value is double dbl)
            return dbl.ToString("0.##", CultureInfo.InvariantCulture);

        // For float values
        if (value is float f)
            return f.ToString("0.##", CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string ?? string.Empty;
        str = str.Trim();

        // Empty string → null for nullable types, 0 for non-nullable
        if (string.IsNullOrEmpty(str))
        {
            if (IsNullableType(targetType))
                return null!;
            return GetDefault(targetType);
        }

        // استبدال الفاصلة العربية والحروف المشابهة بالنقطة الإنجليزية
        // Replace Arabic decimal separator (٫) and common Arabic comma with standard dot
        str = str.Replace('٫', '.').Replace('،', '.').Replace(',', '.');

        // Remove any non-numeric characters except dot, minus, and plus
        // This handles cases like '0ز' where Arabic letters are typed accidentally
        var cleanStr = new System.Text.StringBuilder();
        bool hasDot = false;
        for (int idx = 0; idx < str.Length; idx++)
        {
            char c = str[idx];
            if (char.IsDigit(c))
            {
                cleanStr.Append(c);
            }
            else if (c == '.' && !hasDot)
            {
                cleanStr.Append(c);
                hasDot = true;
            }
            else if (c == '-' && idx == 0)
            {
                cleanStr.Append(c);
            }
            else if (c == '+')
            {
                // Skip plus sign
            }
            // Ignore all other characters (Arabic letters, etc.)
        }

        str = cleanStr.ToString();

        // Handle cases like "." or "-" alone
        if (string.IsNullOrEmpty(str) || str == "." || str == "-")
        {
            if (IsNullableType(targetType))
                return null!;
            return GetDefault(targetType);
        }

        // Try to parse the string using InvariantCulture for consistent decimal point
        if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalResult))
        {
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                return decimalResult;
            if (targetType == typeof(int) || targetType == typeof(int?))
                return (int)decimalResult;
            if (targetType == typeof(double) || targetType == typeof(double?))
                return (double)decimalResult;
            if (targetType == typeof(float) || targetType == typeof(float?))
                return (float)decimalResult;
            return decimalResult;
        }

        // Try integer
        if (int.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var intResult))
        {
            if (targetType == typeof(int) || targetType == typeof(int?))
                return intResult;
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
                return (decimal)intResult;
            if (targetType == typeof(double) || targetType == typeof(double?))
                return (double)intResult;
            return intResult;
        }

        // Parse failed — return null for nullable types, don't throw
        if (IsNullableType(targetType))
            return null!;

        return GetDefault(targetType);
    }

    private static bool IsNullableType(Type type)
    {
        return Nullable.GetUnderlyingType(type) != null;
    }

    private static object GetDefault(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return null!;
        if (type == typeof(decimal)) return 0m;
        if (type == typeof(int)) return 0;
        if (type == typeof(double)) return 0.0;
        if (type == typeof(float)) return 0f;
        return 0;
    }
}
