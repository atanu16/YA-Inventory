using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YAInventory.Models;

namespace YAInventory.Services
{
    /// <summary>
    /// Wraps MongoDB Atlas operations. All methods handle exceptions gracefully —
    /// the app must never crash if Mongo is unavailable.
    /// </summary>
    public class MongoDbService
    {
        private IMongoDatabase? _db;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        // ── Connection ─────────────────────────────────────────────────────
        public async Task<bool> ConnectAsync(string connectionString, string databaseName)
        {
            try
            {
                var settings = MongoClientSettings.FromConnectionString(connectionString);

                // Generous timeouts for Atlas SRV resolution (first connect can be slow)
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
                settings.ConnectTimeout         = TimeSpan.FromSeconds(10);
                settings.SocketTimeout          = TimeSpan.FromSeconds(10);

                // Atlas requires TLS; ensure it is on for SRV URIs
                if (connectionString.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
                    settings.UseTls = true;

                var client = new MongoClient(settings);
                _db = client.GetDatabase(databaseName);

                // Ping to verify connectivity
                await _db.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1));

                _isConnected = true;
                return true;
            }
            catch
            {
                _isConnected = false;
                return false;
            }
        }

        public void Disconnect()
        {
            _db          = null;
            _isConnected = false;
        }

        // ── Products ───────────────────────────────────────────────────────
        private IMongoCollection<Product>? ProductsCollection =>
            _db?.GetCollection<Product>("products");

        public async Task<List<Product>> GetAllProductsAsync()
        {
            try
            {
                if (ProductsCollection is null) return new();
                return await ProductsCollection
                    .Find(Builders<Product>.Filter.Empty)
                    .ToListAsync();
            }
            catch { return new(); }
        }

        public async Task<List<Product>> GetProductsUpdatedAfterAsync(DateTime after)
        {
            try
            {
                if (ProductsCollection is null) return new();
                var filter = Builders<Product>.Filter.Gt(p => p.UpdatedAt, after);
                return await ProductsCollection.Find(filter).ToListAsync();
            }
            catch { return new(); }
        }

        public async Task<bool> UpsertProductAsync(Product product)
        {
            try
            {
                if (ProductsCollection is null) return false;
                var filter  = Builders<Product>.Filter.Eq(p => p.Barcode, product.Barcode);
                var options = new ReplaceOptions { IsUpsert = true };
                await ProductsCollection.ReplaceOneAsync(filter, product, options);
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> UpsertManyProductsAsync(IEnumerable<Product> products)
        {
            try
            {
                if (ProductsCollection is null) return false;
                var writes = new List<WriteModel<Product>>();
                foreach (var p in products)
                {
                    var filter = Builders<Product>.Filter.Eq(x => x.Barcode, p.Barcode);
                    writes.Add(new ReplaceOneModel<Product>(filter, p) { IsUpsert = true });
                }
                if (writes.Count > 0)
                    await ProductsCollection.BulkWriteAsync(writes);
                return true;
            }
            catch { return false; }
        }

        // ── Sales ──────────────────────────────────────────────────────────
        private IMongoCollection<Sale>? SalesCollection =>
            _db?.GetCollection<Sale>("sales");

        public async Task<bool> InsertSaleAsync(Sale sale)
        {
            try
            {
                if (SalesCollection is null) return false;
                await SalesCollection.InsertOneAsync(sale);
                return true;
            }
            catch { return false; }
        }

        public async Task<List<Sale>> GetSalesAfterAsync(DateTime after)
        {
            try
            {
                if (SalesCollection is null) return new();
                var filter = Builders<Sale>.Filter.Gt(s => s.SaleDate, after);
                return await SalesCollection.Find(filter).ToListAsync();
            }
            catch { return new(); }
        }

        public async Task<bool> PingAsync()
        {
            try
            {
                if (_db is null) return false;
                await _db.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1));
                return true;
            }
            catch { return false; }
        }
    }
}
