using CsvHelper.Configuration.Attributes;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace YAInventory.Models
{
    /// <summary>
    /// Represents a product in inventory. Barcode is the unique business key.
    /// Same barcode → same product (no duplication, quantity increases instead).
    /// MongoDB auto-manages _id — we use Barcode as the upsert key.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Product
    {
        // ── MongoDB _id: fully ignored — let MongoDB auto-manage it ───────
        [BsonIgnore]
        [Ignore]   // CsvHelper: skip this column
        public string? MongoId { get; set; }

        // ── Core fields ───────────────────────────────────────────────────
        [Index(0)]
        public string ProductId { get; set; } = GenerateId();

        [Index(1)]
        public string Name { get; set; } = string.Empty;

        [Index(2)]
        public string Barcode { get; set; } = string.Empty;

        [Index(3)]
        public decimal Price { get; set; }

        [Index(4)]
        public int Quantity { get; set; }

        [Index(5)]
        public string Category { get; set; } = "General";

        /// <summary>Product-level default discount percentage (0-100).</summary>
        [Index(6)]
        public decimal DefaultDiscount { get; set; }

        [Index(7)]
        public string? ImagePath { get; set; }

        // ── Sync metadata ─────────────────────────────────────────────────
        [Index(8)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Index(9)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Soft-delete: product stays in DB with Quantity=0.</summary>
        [Index(10)]
        public bool IsDeleted { get; set; }

        // ── Computed (not persisted) ──────────────────────────────────────
        [Ignore]
        [BsonIgnore]
        public StockStatus StockStatus =>
            IsDeleted ? StockStatus.Deleted :
            Quantity == 0 ? StockStatus.OutOfStock :
            Quantity <= 5 ? StockStatus.LowStock :
            StockStatus.InStock;

        [Ignore]
        [BsonIgnore]
        public string StockStatusLabel => StockStatus switch
        {
            StockStatus.InStock    => "In Stock",
            StockStatus.LowStock   => "Low Stock",
            StockStatus.OutOfStock => "Out of Stock",
            _                      => "Deleted"
        };

        private static string GenerateId() =>
            Guid.NewGuid().ToString("N")[..8].ToUpper();
    }

    public enum StockStatus
    {
        InStock,
        LowStock,
        OutOfStock,
        Deleted
    }
}
