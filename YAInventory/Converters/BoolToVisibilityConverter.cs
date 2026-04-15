using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YAInventory.Converters
{
    /// <summary>
    /// Converts bool/int/null to Visibility.
    /// ConverterParameter="Invert" reverses the logic.
    /// Integers > 0 are treated as true.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool bVal = value switch
            {
                bool b   => b,
                int  i   => i > 0,
                null     => false,
                _        => !string.IsNullOrEmpty(value.ToString())
            };

            bool invert = parameter is string s &&
                          (s.Equals("Invert", StringComparison.OrdinalIgnoreCase) ||
                           s.Equals("Inverse", StringComparison.OrdinalIgnoreCase));
            if (invert) bVal = !bVal;

            return bVal ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }
}
