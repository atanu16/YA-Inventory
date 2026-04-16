using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YAInventory.Models;

namespace YAInventory.Services
{
    /// <summary>
    /// Background service that synchronises local CSV data with MongoDB Atlas.
    ///
    /// Data-flow contract:
    ///   • Local CSV is the source of truth (AES-256 encrypted on disk).
    ///   • If CSV files don't exist on startup → bootstrap from MongoDB.
    ///   • Every local change → save to CSV first, then push to MongoDB.
    ///   • Background timer → pushes local changes to MongoDB (one-way).
    ///   • Manual refresh → pushes ALL local data to MongoDB.
    ///   • MongoDB stores plain data (no encryption needed there).
    ///
    /// No bidirectional merge — local always wins.
    /// </summary>
    public class SyncService : IDisposable
    {
        private readonly LocalStorageService _local;
        private readonly MongoDbService      _mongo;
        private readonly SyncStatus          _status;

        private Timer?  _timer;
        private bool    _isSyncing;
        private string  _connectionString = string.Empty;
        private string  _databaseName     = string.Empty;

        public SyncStatus Status => _status;
        public event Action<string>? SyncMessageChanged;
        public event Action?         SyncCompleted;

        public SyncService(LocalStorageService local, MongoDbService mongo, SyncStatus status)
        {
            _local  = local;
            _mongo  = mongo;
            _status = status;
        }

        // ── Start / Stop ───────────────────────────────────────────────────
        public void Start(AppSettings settings, int intervalSeconds = 30)
        {
            _connectionString = settings.MongoConnectionString;
            _databaseName     = settings.MongoDatabaseName;

            var interval = TimeSpan.FromSeconds(Math.Max(10, intervalSeconds));
            _timer?.Dispose();
            _timer = new Timer(_ => _ = RunSyncCycleAsync(), null, TimeSpan.Zero, interval);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            _mongo.Disconnect();
            UpdateStatus(SyncState.Offline, "Sync stopped");
        }

        // ── Bootstrap: pull from MongoDB when local CSV is missing ─────────
        /// <summary>
        /// Called once at startup. If local CSV files don't exist,
        /// connects to MongoDB and downloads the full catalogue/sales
        /// to create the initial encrypted local files.
        /// </summary>
        public async Task BootstrapFromCloudAsync()
        {
            bool needProducts = !_local.HasLocalProductsFile();
            bool needSales    = !_local.HasLocalSalesFile();

            if (!needProducts && !needSales) return;

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                UpdateStatus(SyncState.Offline, "No MongoDB configured — starting with empty data");
                return;
            }

            UpdateStatus(SyncState.Syncing, "Local data not found — downloading from cloud…");

            try
            {
                if (!IsInternetAvailable())
                {
                    UpdateStatus(SyncState.Offline, "No internet — starting with empty data");
                    return;
                }

                if (!_mongo.IsConnected)
                {
                    var ok = await _mongo.ConnectAsync(_connectionString, _databaseName);
                    if (!ok)
                    {
                        UpdateStatus(SyncState.Failed, "Cannot reach MongoDB — starting with empty data");
                        return;
                    }
                }

                // Pull products from MongoDB → save to encrypted CSV
                if (needProducts)
                {
                    UpdateStatus(SyncState.Syncing, "Downloading products from cloud…");
                    var products = await _mongo.GetAllProductsAsync();
                    if (products.Count > 0)
                    {
                        await _local.SaveProductsAsync(products);
                        UpdateStatus(SyncState.Syncing, $"Downloaded {products.Count} products");
                    }
                }

                // Pull sales from MongoDB → save to encrypted CSV
                if (needSales)
                {
                    UpdateStatus(SyncState.Syncing, "Downloading sales from cloud…");
                    var sales = await _mongo.GetAllSalesAsync();

                    // Hydrate Items from ItemsJson for CSV storage
                    foreach (var s in sales)
                    {
                        try
                        {
                            if (s.Items == null || s.Items.Count == 0)
                                s.Items = JsonConvert.DeserializeObject<List<SaleItem>>(s.ItemsJson ?? "[]")
                                          ?? new List<SaleItem>();
                        }
                        catch { s.Items = new List<SaleItem>(); }

                        if (string.IsNullOrEmpty(s.ItemsJson) || s.ItemsJson == "[]")
                            s.ItemsJson = JsonConvert.SerializeObject(s.Items);
                    }

                    if (sales.Count > 0)
                    {
                        await _local.SaveSalesAsync(sales);
                        UpdateStatus(SyncState.Syncing, $"Downloaded {sales.Count} sales");
                    }
                }

                // Mark first sync as done
                var settings = _local.LoadSettings();
                settings.LastSyncUtc = DateTime.UtcNow;
                _local.SaveSettings(settings);
                _status.LastSync = DateTime.UtcNow;

                UpdateStatus(SyncState.Success, "Cloud data downloaded successfully!");
            }
            catch (Exception ex)
            {
                UpdateStatus(SyncState.Failed, $"Bootstrap error: {ex.Message}");
            }
        }

        // ── Sync cycle: one-way local → cloud push ─────────────────────────
        public async Task RunSyncCycleAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                if (!IsInternetAvailable())
                {
                    UpdateStatus(SyncState.Offline, "No internet connection");
                    return;
                }

                UpdateStatus(SyncState.Syncing, "Connecting to cloud…");

                if (!_mongo.IsConnected)
                {
                    UpdateStatus(SyncState.Syncing, "Connecting to MongoDB Atlas…");
                    var ok = await _mongo.ConnectAsync(_connectionString, _databaseName);
                    if (!ok)
                    {
                        UpdateStatus(SyncState.Failed, "Cannot reach MongoDB Atlas — retrying next cycle");
                        return;
                    }
                }

                var pingOk = await _mongo.PingAsync();
                if (!pingOk)
                {
                    _mongo.Disconnect();
                    UpdateStatus(SyncState.Failed, "Lost connection to Mongo");
                    return;
                }

                var settings = _local.LoadSettings();
                var lastSync = settings.LastSyncUtc ?? DateTime.MinValue;

                // ── Push products local → cloud ────────────────────────────
                UpdateStatus(SyncState.Syncing, "Pushing products to cloud…");
                var localProducts = await _local.LoadProductsAsync();
                
                // 1. Push updates
                var productsToSync = localProducts
                    .Where(p => p.UpdatedAt > lastSync)
                    .ToList();

                if (productsToSync.Count > 0)
                    await _mongo.UpsertManyProductsAsync(productsToSync);

                // 2. Propagate deletions (Hard Deletes)
                var allCloudProducts = await _mongo.GetAllProductsAsync();
                var localBarcodes    = new HashSet<string>(localProducts.Select(p => p.Barcode));
                var barcodesToDelete = allCloudProducts
                    .Select(p => p.Barcode)
                    .Where(b => !localBarcodes.Contains(b))
                    .ToList();

                if (barcodesToDelete.Count > 0)
                {
                    UpdateStatus(SyncState.Syncing, "Removing deleted products from cloud…");
                    await _mongo.DeleteManyProductsAsync(barcodesToDelete);
                }

                // ── Push sales local → cloud ───────────────────────────────
                UpdateStatus(SyncState.Syncing, "Pushing sales to cloud…");
                var localSales = await _local.LoadSalesAsync();
                var salesToSync = localSales
                    .Where(s => s.UpdatedAt > lastSync)
                    .ToList();

                if (salesToSync.Count > 0)
                    await _mongo.UpsertManySalesAsync(salesToSync);

                // ── Finalise ───────────────────────────────────────────────
                settings.LastSyncUtc = DateTime.UtcNow;
                _local.SaveSettings(settings);
                _status.LastSync = DateTime.UtcNow;

                UpdateStatus(SyncState.Success, "Sync complete");
                SyncCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                UpdateStatus(SyncState.Failed, $"Sync error: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        // ── Manual refresh: push ALL local data to cloud ───────────────────
        /// <summary>
        /// Pushes the ENTIRE local dataset to MongoDB, regardless of LastSyncUtc.
        /// Called when the user explicitly refreshes / syncs.
        /// </summary>
        public async Task PushAllToCloudAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                if (string.IsNullOrWhiteSpace(_connectionString))
                {
                    UpdateStatus(SyncState.Offline, "MongoDB not configured");
                    return;
                }

                if (!IsInternetAvailable())
                {
                    UpdateStatus(SyncState.Offline, "No internet connection");
                    return;
                }

                UpdateStatus(SyncState.Syncing, "Connecting to cloud…");

                if (!_mongo.IsConnected)
                {
                    var ok = await _mongo.ConnectAsync(_connectionString, _databaseName);
                    if (!ok)
                    {
                        UpdateStatus(SyncState.Failed, "Cannot reach MongoDB Atlas");
                        return;
                    }
                }

                // Push ALL products
                UpdateStatus(SyncState.Syncing, "Pushing all products to cloud…");
                var allProducts = await _local.LoadProductsAsync();
                if (allProducts.Count > 0)
                    await _mongo.UpsertManyProductsAsync(allProducts);

                // Propagate deletions (Hard Deletes) during manual sync
                var allCloudProducts = await _mongo.GetAllProductsAsync();
                var localBarcodes    = new HashSet<string>(allProducts.Select(p => p.Barcode));
                var barcodesToDelete = allCloudProducts
                    .Select(p => p.Barcode)
                    .Where(b => !localBarcodes.Contains(b))
                    .ToList();

                if (barcodesToDelete.Count > 0)
                {
                    UpdateStatus(SyncState.Syncing, "Removing deleted products from cloud…");
                    await _mongo.DeleteManyProductsAsync(barcodesToDelete);
                }

                // Push ALL sales
                UpdateStatus(SyncState.Syncing, "Pushing all sales to cloud…");
                var allSales = await _local.LoadSalesAsync();
                if (allSales.Count > 0)
                    await _mongo.UpsertManySalesAsync(allSales);

                // Update last sync
                var settings = _local.LoadSettings();
                settings.LastSyncUtc = DateTime.UtcNow;
                _local.SaveSettings(settings);
                _status.LastSync = DateTime.UtcNow;

                UpdateStatus(SyncState.Success, $"All data synced to cloud ({allProducts.Count} products, {allSales.Count} sales)");
                SyncCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                UpdateStatus(SyncState.Failed, $"Full sync error: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────
        private static bool IsInternetAvailable()
        {
            try
            {
                using var ping  = new Ping();
                var reply = ping.Send("8.8.8.8", 1000);
                return reply.Status == IPStatus.Success;
            }
            catch { return false; }
        }

        private void UpdateStatus(SyncState state, string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _status.State   = state;
                _status.Message = message;
            });
            SyncMessageChanged?.Invoke(message);
        }

        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
