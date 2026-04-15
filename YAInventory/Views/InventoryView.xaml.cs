using System.Windows.Controls;
using System.Windows.Input;
using YAInventory.ViewModels;

namespace YAInventory.Views
{
    public partial class InventoryView : UserControl
    {
        public InventoryView() => InitializeComponent();

        // Barcode scanner fires Enter after the scanned code.
        private void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is InventoryViewModel vm)
            {
                _ = vm.HandleBarcodeAsync(null);
                e.Handled = true;
            }
        }
    }
}
