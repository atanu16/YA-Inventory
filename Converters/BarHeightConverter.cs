using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace YAInventory.Converters
{
    /// <summary>
    /// Converts a decimal amount to a bar height in pixels (0-120).
    /// Requires a multi-binding: [amount, maxAmount].
    /// Usage: MultiBinding with [Amount, MaxAmount], Converter=BarHeight.
    /// </summary>
    public class BarHeightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return 4.0;

            double amount = values[0] switch
            {
                decimal d => (double)d,
                double db => db,
                int i     => i,
                _         => 0
            };

            double max = values[1] switch
            {
                decimal d => (double)d,
                double db => db,
                int i     => i,
                _         => 1
            };

            if (max <= 0) return 4.0;
            double ratio = amount / max;
            return Math.Max(4.0, ratio * 120.0);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a collection to its max value for use in BarHeightConverter.
    /// </summary>
    public class MaxValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<YAInventory.ViewModels.SalesBarData> items)
            {
                var max = items.Select(i => i.Amount).DefaultIfEmpty(1m).Max();
                return (double)(max <= 0 ? 1m : max);
            }
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
