using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using YAInventory.Models;

namespace YAInventory.Services
{
    /// <summary>
    /// Manages all local file storage:
    ///   C:\YA Inventory Management\
    ///     config.json      — app settings
    ///     products.csv     — product catalogue
    ///     sales.csv        — transaction history
    ///     images\          — logos / product images
    /// All methods are thread-safe via SemaphoreSlim.
    /// </summary>
    public class LocalStorageService
    {
        public static readonly string RootFolder =
            Path.Combine(@"C:\YA Inventory Management");

        private static readonly string ConfigFile   = Path.Combine(RootFolder, "config.json");
        private static readonly string ProductsFile = Path.Combine(RootFolder, "products.csv");
        private static readonly string SalesFile    = Path.Combine(RootFolder, "sales.csv");
        public  static readonly string ImagesFolder = Path.Combine(RootFolder, "images");

        private readonly SemaphoreSlim _lock = new(1, 1);

        // ── Initialisation ─────────────────────────────────────────────────
        public void EnsureFolderStructure()
        {
            Directory.CreateDirectory(RootFolder);
            Directory.CreateDirectory(ImagesFolder);

            if (!File.Exists(ConfigFile))
                SaveSettings(new AppSettings());

            if (!File.Exists(ProductsFile))
                File.WriteAllText(ProductsFile, string.Empty);

            if (!File.Exists(SalesFile))
                File.WriteAllText(SalesFile, string.Empty);
        }

        // ── Settings ───────────────────────────────────────────────────────
        public AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(ConfigFile)) return new AppSettings();
                var json = File.ReadAllText(ConfigFile);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        public void SaveSettings(AppSettings settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(ConfigFile, json);
        }

        // ── Products ───────────────────────────────────────────────────────
        public async Task<List<Product>> LoadProductsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(ProductsFile) || new FileInfo(ProductsFile).Length == 0)
                    return new List<Product>();

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null
                };

                using var reader = new StreamReader(ProductsFile);
                using var csv    = new CsvReader(reader, config);
                return csv.GetRecords<Product>().ToList();
            }
            catch { return new List<Product>(); }
            finally { _lock.Release(); }
        }

        public async Task SaveProductsAsync(IEnumerable<Product> products)
        {
            await _lock.WaitAsync();
            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                };

                using var writer = new StreamWriter(ProductsFile, append: false);
                using var csv    = new CsvWriter(writer, config);
                await csv.WriteRecordsAsync(products);
            }
            finally { _lock.Release(); }
        }

        public async Task UpsertProductAsync(Product product)
        {
            var products = await LoadProductsAsync();
            var idx = products.FindIndex(p => p.Barcode == product.Barcode);

            product.UpdatedAt = DateTime.UtcNow;

            if (idx >= 0)
                products[idx] = product;
            else
                products.Add(product);

            await SaveProductsAsync(products);
        }

        public async Task DeleteProductAsync(string barcode)
        {
            var products = await LoadProductsAsync();
            var product  = products.FirstOrDefault(p => p.Barcode == barcode);
            if (product is null) return;

            // Soft delete — keep record, zero quantity
            product.IsDeleted  = true;
            product.UpdatedAt  = DateTime.UtcNow;
            await SaveProductsAsync(products);
        }

        // ── Sales ──────────────────────────────────────────────────────────
        public async Task<List<Sale>> LoadSalesAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(SalesFile) || new FileInfo(SalesFile).Length == 0)
                    return new List<Sale>();

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null
                };

                using var reader = new StreamReader(SalesFile);
                using var csv    = new CsvReader(reader, config);
                var sales = csv.GetRecords<Sale>().ToList();

                // Deserialise items JSON
                foreach (var s in sales)
                {
                    try
                    {
                        s.Items = JsonConvert.DeserializeObject<List<SaleItem>>(s.ItemsJson)
                                  ?? new List<SaleItem>();
                    }
                    catch { s.Items = new List<SaleItem>(); }
                }

                return sales;
            }
            catch { return new List<Sale>(); }
            finally { _lock.Release(); }
        }

        public async Task AppendSaleAsync(Sale sale)
        {
            await _lock.WaitAsync();
            try
            {
                sale.ItemsJson = JsonConvert.SerializeObject(sale.Items);
                bool fileHasContent = File.Exists(SalesFile) && new FileInfo(SalesFile).Length > 0;

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = !fileHasContent
                };

                using var writer = new StreamWriter(SalesFile, append: true);
                using var csv    = new CsvWriter(writer, config);

                if (!fileHasContent)
                {
                    // First record ever — write header row then the data row
                    csv.WriteHeader<Sale>();
                    await csv.NextRecordAsync();
                }
                else
                {
                    // File already has rows — start a new line before appending
                    await writer.WriteLineAsync();
                }

                csv.WriteRecord(sale);
                await csv.FlushAsync();
            }
            finally { _lock.Release(); }
        }
    }
}
