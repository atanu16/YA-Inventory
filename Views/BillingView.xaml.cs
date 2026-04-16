using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YAInventory.ViewModels;

namespace YAInventory.Views
{
    public partial class BillingView : UserControl
    {
        public BillingView() => InitializeComponent();

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-focus the barcode input field when the billing page loads.
            BarcodeBox.Focus();
        }

        private void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is BillingViewModel vm)
            {
                _ = vm.HandleScanAsync(null);
                e.Handled = true;
                BarcodeBox.Focus();  // keep focus on scanner box
            }
        }
    }
}
