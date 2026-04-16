using CsvHelper.Configuration.Attributes;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace YAInventory.Models
{
    /// <summary>
    /// Represents a completed sale / billing transaction.
    /// MongoDB auto-manages _id — we use SaleId as the upsert key.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Sale
    {
        [BsonIgnore]
        [Ignore]
        public string? MongoId { get; set; }

        [Index(0)] public string SaleId      { get; set; } = GenerateId();
        [Index(1)] public DateTime SaleDate  { get; set; } = DateTime.Now;
        [Index(2)] public decimal Subtotal   { get; set; }
        [Index(3)] public decimal Discount   { get; set; }
        [Index(4)] public decimal TaxPercent { get; set; }
        [Index(5)] public decimal TaxAmount  { get; set; }
        [Index(6)] public decimal Total      { get; set; }
        [Index(7)] public string? CashierName { get; set; }
        [Index(8)] public string PaymentMethod { get; set; } = "Cash";
        [Index(9)] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Serialised JSON of items — flattened for CSV, nested for Mongo.</summary>
        [Index(10)] public string ItemsJson  { get; set; } = "[]";

        [Ignore]
        [BsonIgnore]
        public List<SaleItem> Items { get; set; } = new();

        private static string GenerateId() =>
            "RCT-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" +
            Guid.NewGuid().ToString("N")[..4].ToUpper();
    }
}
