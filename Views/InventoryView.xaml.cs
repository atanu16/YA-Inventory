using System;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using YAInventory.ViewModels;

namespace YAInventory.Views
{
    public partial class InventoryView : UserControl
    {
        private StringBuilder _barcodeScannerBuffer = new();
        private DateTime _lastBarcodeScan = DateTime.Now;

        public InventoryView() => InitializeComponent();

        // ── Global Scanner Buffer ────────────────────────────────────
        private void UserControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // If more than 100ms passed since last char, clear buffer (it's human typing)
            if ((DateTime.Now - _lastBarcodeScan).TotalMilliseconds > 100)
                _barcodeScannerBuffer.Clear();

            _barcodeScannerBuffer.Append(e.Text);
            _lastBarcodeScan = DateTime.Now;
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_barcodeScannerBuffer.Length >= 3 && (DateTime.Now - _lastBarcodeScan).TotalMilliseconds <= 100)
                {
                    if (DataContext is InventoryViewModel vm)
                    {
                        var barcode = _barcodeScannerBuffer.ToString().Trim();
                        _barcodeScannerBuffer.Clear();
                        e.Handled = true;
                        
                        // Prevent triggering UI buttons if a button has focus
                        _ = vm.HandleBarcodeAsync(barcode);
                    }
                }
                else
                {
                    _barcodeScannerBuffer.Clear();
                }
            }
        }

        // ── Explicit TextBox support (if they type slowly inside it) ──
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
