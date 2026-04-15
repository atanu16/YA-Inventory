using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using YAInventory.Models;

namespace YAInventory.Converters
{
    /// <summary>Maps StockStatus enum to a brush colour for UI badges.</summary>
    public class StockStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value is StockStatus s ? s :
                         value is string str ? Enum.Parse<StockStatus>(str) : StockStatus.InStock;

            var hex = status switch
            {
                StockStatus.InStock    => "#10B981",   // green
                StockStatus.LowStock   => "#F59E0B",   // amber
                StockStatus.OutOfStock => "#EF4444",   // red
                _                      => "#6B7280"    // grey
            };

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Maps StockStatus to a lighter background colour for badges.</summary>
    public class StockStatusToBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value is StockStatus s ? s :
                         value is string str ? Enum.Parse<StockStatus>(str) : StockStatus.InStock;

            var hex = status switch
            {
                StockStatus.InStock    => "#2010B981",
                StockStatus.LowStock   => "#20F59E0B",
                StockStatus.OutOfStock => "#20EF4444",
                _                      => "#201A1A2E"
            };

            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
