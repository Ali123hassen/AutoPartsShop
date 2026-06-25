using System;
using System.Globalization;
using System.Windows.Data;

namespace AutoPartsShop.UI.Converters
{
    public class StockLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int stockLevel = 0;

            if (value is int intVal)
                stockLevel = intVal;
            else if (value is decimal decVal)
                stockLevel = (int)decVal;
            else if (value is double dblVal)
                stockLevel = (int)dblVal;

            if (stockLevel <= 0)
                return System.Windows.Media.Brushes.Red;
            else if (stockLevel <= 5)
                return System.Windows.Media.Brushes.Orange;
            else if (stockLevel <= 10)
                return System.Windows.Media.Brushes.Yellow;
            else
                return System.Windows.Media.Brushes.Green;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
