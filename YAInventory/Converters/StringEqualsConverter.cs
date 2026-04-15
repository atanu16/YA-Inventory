using System;
using System.Globalization;
using System.Windows.Data;

namespace YAInventory.Converters
{
    /// <summary>
    /// Returns true when the binding value string equals ConverterParameter.
    /// Used for the sidebar RadioButton IsChecked binding.
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? (parameter?.ToString() ?? Binding.DoNothing) : Binding.DoNothing;
    }
}
