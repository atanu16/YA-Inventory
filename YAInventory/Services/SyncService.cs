using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YAInventory.Models;

namespace YAInventory.Services
{
    /// <summary>
    /// Background service that periodically synchronises local CSV data with MongoDB Atlas.
    ///
    /// Data-flow contract:
    ///   1. WRITE local first (always, even offline)
    ///   2. PUSH local → Mongo   (if online, on timer)
    ///   3. PULL Mongo → local   (merge, latest UpdatedAt wins)
    ///
    /// Conflict resolution: whichever record has the newer UpdatedAt wins.
    /// No data is ever permanently deleted — soft deletes are used.
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

        // ── Sync cycle ─────────────────────────────────────────────────────
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

                UpdateStatus(SyncState.Syncing, "Syncing products…");
                await SyncProductsAsync();

                var settings = _local.LoadSettings();
                settings.LastSyncUtc = DateTime.UtcNow;
                _local.SaveSettings(settings);
                _status.LastSync = DateTime.UtcNow;

                UpdateStatus(SyncState.Success, "Sync complete");
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

        private async Task SyncProductsAsync()
        {
            // 1. Load local products
            var localProducts = await _local.LoadProductsAsync();

            // 2. Load remote products
            var remoteProducts = await _mongo.GetAllProductsAsync();

            // 3. Merge: build a dictionary keyed by barcode
            var merged = localProducts.ToDictionary(p => p.Barcode);

            foreach (var remote in remoteProducts)
            {
                if (merged.TryGetValue(remote.Barcode, out var local))
                {
                    // Conflict resolution: latest UpdatedAt wins
                    if (remote.UpdatedAt > local.UpdatedAt)
                        merged[remote.Barcode] = remote;
                }
                else
                {
                    merged[remote.Barcode] = remote;
                }
            }

            var finalList = merged.Values.ToList();

            // 4. Push merged set back to local
            await _local.SaveProductsAsync(finalList);

            // 5. Push merged set back to Mongo (upsert)
            await _mongo.UpsertManyProductsAsync(finalList);
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
