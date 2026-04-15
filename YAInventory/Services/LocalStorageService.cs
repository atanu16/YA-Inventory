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
    ///     products.csv     — product catalogue  (AES-256 encrypted)
    ///     sales.csv        — transaction history (AES-256 encrypted)
    ///     images\          — logos / product images
    /// All methods are thread-safe via SemaphoreSlim.
    /// CSV files are encrypted on disk with a fixed application password.
    /// </summary>
    public class LocalStorageService
    {
        public static readonly string RootFolder =
            Path.Combine(@"C:\YA Inventory Management");

        private static readonly string ConfigFile   = Path.Combine(RootFolder, "config.json");
        private static readonly string ProductsFile = Path.Combine(RootFolder, "products.csv");
        private static readonly string SalesFile    = Path.Combine(RootFolder, "sales.csv");
        public  static readonly string ImagesFolder = Path.Combine(RootFolder, "images");

        /// <summary>Application-level encryption password for local CSV files.</summary>
        private const string EncryptionPassword = "801697";

        private readonly SemaphoreSlim _lock = new(1, 1);

        // ── Initialisation ─────────────────────────────────────────────────
        public void EnsureFolderStructure()
        {
            Directory.CreateDirectory(RootFolder);
            Directory.CreateDirectory(ImagesFolder);

            if (!File.Exists(ConfigFile))
                SaveSettings(new AppSettings());

            // CSV files are NOT created empty here.
            // If they don't exist, the app will bootstrap from MongoDB.
            // If MongoDB is not configured either, empty files are created
            // on the first local save.

            // Migrate any existing plain-text CSV files to encrypted format
            MigratePlainTextIfNeeded(ProductsFile);
            MigratePlainTextIfNeeded(SalesFile);
        }

        /// <summary>Returns true if the local products CSV exists and has data.</summary>
        public bool HasLocalProductsFile() =>
            File.Exists(ProductsFile) && new FileInfo(ProductsFile).Length > 0;

        /// <summary>Returns true if the local sales CSV exists and has data.</summary>
        public bool HasLocalSalesFile() =>
            File.Exists(SalesFile) && new FileInfo(SalesFile).Length > 0;

        /// <summary>
        /// Detects whether a CSV file is still in plain-text format (pre-encryption era)
        /// and migrates it to encrypted format. Keeps a .bak backup of the original.
        /// </summary>
        private void MigratePlainTextIfNeeded(string filePath)
        {
            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                return;

            // Try to decrypt — if it works, the file is already encrypted
            try
            {
                CryptoHelper.DecryptFromFile(filePath, EncryptionPassword);
                return; // Already encrypted, nothing to do
            }
            catch
            {
                // Decryption failed → file is probably plain text
            }

            // Read the plain-text content, back it up, and re-save encrypted
            try
            {
                string plainCsv = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(plainCsv))
                    return;

                // Keep a backup just in case
                string backupPath = filePath + ".bak";
                File.Copy(filePath, backupPath, overwrite: true);

                // Encrypt and overwrite
                CryptoHelper.EncryptToFile(filePath, plainCsv, EncryptionPassword);
            }
            catch
            {
                // If migration fails, leave the file untouched
            }
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

                // Decrypt file → plain CSV text
                string csvText = CryptoHelper.DecryptFromFile(ProductsFile, EncryptionPassword);
                if (string.IsNullOrWhiteSpace(csvText))
                    return new List<Product>();

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null
                };

                using var reader = new StringReader(csvText);
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

                // Write CSV to in-memory string
                using var stringWriter = new StringWriter();
                using var csv          = new CsvWriter(stringWriter, config);
                await csv.WriteRecordsAsync(products);
                await csv.FlushAsync();

                string csvText = stringWriter.ToString();

                // Encrypt and write to disk
                CryptoHelper.EncryptToFile(ProductsFile, csvText, EncryptionPassword);
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
            var initialCount = products.Count;
            
            // Hard delete — actually remove it from the list
            products.RemoveAll(p => p.Barcode == barcode);
            
            if (products.Count < initialCount)
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

                // Decrypt file → plain CSV text
                string csvText = CryptoHelper.DecryptFromFile(SalesFile, EncryptionPassword);
                if (string.IsNullOrWhiteSpace(csvText))
                    return new List<Sale>();

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    HeaderValidated = null
                };

                using var reader = new StringReader(csvText);
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
            // Load all existing sales, add the new one, re-encrypt the entire file.
            // This is necessary because you cannot append to an encrypted stream.
            var allSales = await LoadSalesAsync();

            sale.ItemsJson = JsonConvert.SerializeObject(sale.Items);
            allSales.Add(sale);

            await _lock.WaitAsync();
            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                };

                using var stringWriter = new StringWriter();
                using var csv          = new CsvWriter(stringWriter, config);

                // Serialise ItemsJson for every sale before writing
                foreach (var s in allSales)
                    s.ItemsJson = JsonConvert.SerializeObject(s.Items);

                await csv.WriteRecordsAsync(allSales);
                await csv.FlushAsync();

                string csvText = stringWriter.ToString();
                CryptoHelper.EncryptToFile(SalesFile, csvText, EncryptionPassword);
            }
            finally { _lock.Release(); }
        }

        /// <summary>
        /// Overwrites the entire sales CSV with the supplied list (used by sync merge).
        /// </summary>
        public async Task SaveSalesAsync(IEnumerable<Sale> sales)
        {
            await _lock.WaitAsync();
            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true
                };

                var allSales = sales.ToList();

                using var stringWriter = new StringWriter();
                using var csv          = new CsvWriter(stringWriter, config);

                // Serialise ItemsJson for every sale before writing
                foreach (var s in allSales)
                    s.ItemsJson = JsonConvert.SerializeObject(s.Items);

                await csv.WriteRecordsAsync(allSales);
                await csv.FlushAsync();

                string csvText = stringWriter.ToString();
                CryptoHelper.EncryptToFile(SalesFile, csvText, EncryptionPassword);
            }
            finally { _lock.Release(); }
        }
    }
}
