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
        private decimal _discountPercent;
        private decimal _discountFlat;

        public string ProductId   { get; set; } = string.Empty;
        public string Barcode     { get; set; } = string.Empty;
        public string Name        { get; set; } = string.Empty;
        public decimal UnitPrice  { get; set; }

        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
        }

        /// <summary>Percentage discount for this item (0-100).</summary>
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { _discountPercent = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
        }

        /// <summary>Flat amount discount for this item.</summary>
        public decimal DiscountFlat
        {
            get => _discountFlat;
            set { _discountFlat = value; OnPropertyChanged(); OnPropertyChanged(nameof(Total)); }
        }

        /// <summary>Line total after discounts.</summary>
        public decimal Total
        {
            get
            {
                var gross = UnitPrice * Quantity;
                var percentOff = gross * (DiscountPercent / 100m);
                return Math.Max(0, gross - percentOff - DiscountFlat);
            }
        }

        public decimal DiscountAmount => (UnitPrice * Quantity) - Total;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
