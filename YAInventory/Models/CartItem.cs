using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YAInventory.Models
{
    /// <summary>
    /// A line item in the active billing session (not persisted until sale is completed).
    /// </summary>
    public class CartItem : INotifyPropertyChanged
    {
        private int _quantity;

        public string ProductId   { get; set; } = string.Empty;
        public string Barcode     { get; set; } = string.Empty;
        public string Name        { get; set; } = string.Empty;

        /// <summary>Original MRP / list price.</summary>
        public decimal OriginalPrice { get; set; }

        /// <summary>Effective sale price per unit (may equal OriginalPrice if no sale).</summary>
        public decimal UnitPrice  { get; set; }

        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); OnPropertyChanged(nameof(DiscountAmount)); }
        }

        /// <summary>Per-unit savings (OriginalPrice − UnitPrice).</summary>
        public decimal UnitSavings => OriginalPrice - UnitPrice;

        /// <summary>Total discount amount for this line.</summary>
        public decimal DiscountAmount => UnitSavings * Quantity;

        /// <summary>Line total (at sale price).</summary>
        public decimal Total => UnitPrice * Quantity;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
