using System;
using System.Globalization;
using System.Windows.Data;

namespace YAInventory.Converters
{
    /// <summary>Formats decimal values as currency strings (e.g. ₹1,234.50).</summary>
    public class CurrencyConverter : IValueConverter
    {
        public string Symbol { get; set; } = "₹";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)  return $"{Symbol}{d:N2}";
            if (value is double db)  return $"{Symbol}{db:N2}";
            if (value is float f)    return $"{Symbol}{f:N2}";
            if (value is int i)      return $"{Symbol}{i:N2}";
            return $"{Symbol}0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                s = s.Replace(Symbol, "").Trim();
                return decimal.TryParse(s, out var d) ? d : 0m;
            }
            return 0m;
        }
    }

    /// <summary>Formats large numbers with K/M suffixes for dashboard cards.</summary>
    public class CompactNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double d = value switch
            {
                decimal dec => (double)dec,
                int i       => i,
                double db   => db,
                _           => 0
            };

            if (d >= 1_000_000) return $"{d / 1_000_000:F1}M";
            if (d >= 1_000)     return $"{d / 1_000:F1}K";
            return d.ToString("N0");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
