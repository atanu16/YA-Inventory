namespace YAInventory.Models
{
    /// <summary>
    /// One line within a completed Sale. Stored as JSON in the Sale record.
    /// </summary>
    public class SaleItem
    {
        public string ProductId      { get; set; } = string.Empty;
        public string Barcode        { get; set; } = string.Empty;
        public string Name           { get; set; } = string.Empty;

        /// <summary>Original MRP / list price per unit.</summary>
        public decimal OriginalPrice { get; set; }

        /// <summary>Effective sale price per unit.</summary>
        public decimal UnitPrice     { get; set; }

        public int Quantity          { get; set; }

        /// <summary>Per-unit discount (OriginalPrice − UnitPrice).</summary>
        public decimal UnitDiscount  { get; set; }

        /// <summary>Total discount for this line (UnitDiscount × Qty).</summary>
        public decimal DiscountAmount { get; set; }

        /// <summary>Line total (UnitPrice × Qty).</summary>
        public decimal Total         { get; set; }
    }
}
