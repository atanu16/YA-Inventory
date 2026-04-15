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
        public decimal UnitPrice     { get; set; }
        public int Quantity          { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountFlat  { get; set; }
        public decimal Total         { get; set; }
    }
}
