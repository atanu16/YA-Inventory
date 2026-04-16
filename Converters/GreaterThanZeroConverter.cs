using System;
using System.Globalization;
using System.Windows.Data;

namespace YAInventory.Converters
{
    /// <summary>
    /// Returns true if the input numeric value is greater than zero.
    /// </summary>
    [ValueConversion(typeof(object), typeof(bool))]
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d) return d > 0;
            if (value is double dbl) return dbl > 0;
            if (value is int i) return i > 0;
            if (value is float f) return f > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
