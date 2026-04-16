using Newtonsoft.Json;

namespace YAInventory.Models
{
    /// <summary>
    /// Application settings persisted to config.json in the data folder.
    /// </summary>
    public class AppSettings
    {
        // ── Shop info ─────────────────────────────────────────────────────
        [JsonProperty("shopName")]
        public string ShopName { get; set; } = "YA Inventory";

        [JsonProperty("address")]
        public string Address { get; set; } = string.Empty;

        [JsonProperty("phone")]
        public string Phone { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("logoPath")]
        public string? LogoPath { get; set; }

        // ── Discount defaults ─────────────────────────────────────────────
        [JsonProperty("defaultDiscountPercent")]
        public decimal DefaultDiscountPercent { get; set; }

        [JsonProperty("taxPercent")]
        public decimal TaxPercent { get; set; } = 0m;

        // ── MongoDB ───────────────────────────────────────────────────────
        [JsonProperty("mongoConnectionString")]
        public string MongoConnectionString { get; set; } = string.Empty;

        [JsonProperty("mongoDatabaseName")]
        public string MongoDatabaseName { get; set; } = "YAInventory";

        [JsonProperty("syncIntervalSeconds")]
        public int SyncIntervalSeconds { get; set; } = 30;

        // ── Printer ───────────────────────────────────────────────────────
        [JsonProperty("printerName")]
        public string PrinterName { get; set; } = string.Empty;

        [JsonProperty("paperWidthMm")]
        public int PaperWidthMm { get; set; } = 80;

        // ── App preferences ───────────────────────────────────────────────
        [JsonProperty("lowStockThreshold")]
        public int LowStockThreshold { get; set; } = 5;

        [JsonProperty("currencySymbol")]
        public string CurrencySymbol { get; set; } = "₹";

        [JsonProperty("lastSyncUtc")]
        public DateTime? LastSyncUtc { get; set; }

        [JsonProperty("appVersion")]
        public string AppVersion { get; set; } = "1.0.0";
    }
}
