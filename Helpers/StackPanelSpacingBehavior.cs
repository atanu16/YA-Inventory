using System.Windows;
using System.Windows.Controls;

namespace YAInventory.Helpers
{
    /// <summary>
    /// Attached property that adds uniform spacing between StackPanel children.
    /// Usage: helpers:Spacing.Between="10"
    /// </summary>
    public static class Spacing
    {
        public static readonly DependencyProperty BetweenProperty =
            DependencyProperty.RegisterAttached(
                "Between",
                typeof(double),
                typeof(Spacing),
                new PropertyMetadata(0.0, OnBetweenChanged));

        public static void SetBetween(DependencyObject d, double value)
            => d.SetValue(BetweenProperty, value);

        public static double GetBetween(DependencyObject d)
            => (double)d.GetValue(BetweenProperty);

        private static void OnBetweenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StackPanel panel)
            {
                panel.Loaded -= Panel_Loaded;
                panel.Loaded += Panel_Loaded;
                ApplySpacing(panel, (double)e.NewValue);
            }
        }

        private static void Panel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is StackPanel panel)
                ApplySpacing(panel, GetBetween(panel));
        }

        private static void ApplySpacing(StackPanel panel, double spacing)
        {
            foreach (UIElement child in panel.Children)
            {
                if (child is FrameworkElement fe)
                {
                    fe.Margin = panel.Orientation == Orientation.Horizontal
                        ? new Thickness(0, 0, spacing, 0)
                        : new Thickness(0, 0, 0, spacing);
                }
            }
        }
    }
}
