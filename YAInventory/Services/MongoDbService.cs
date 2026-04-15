using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YAInventory.Models;

namespace YAInventory.Services
{
    /// <summary>
    /// Wraps MongoDB Atlas operations. All methods handle exceptions gracefully —
    /// the app must never crash if Mongo is unavailable.
    ///
    /// MongoDB auto-manages _id (ObjectId). We use Barcode / SaleId as
    /// business keys for upsert filters. The Product and Sale models have
    /// [BsonIgnore] on MongoId, so _id is never included in replacement
    /// documents — MongoDB preserves existing _id on replace, and
    /// auto-generates on insert.
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
                await _db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

                // Ensure unique indexes on business keys so upserts work correctly
                await EnsureIndexesAsync();

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

        /// <summary>
        /// Creates unique indexes on Barcode (products) and SaleId (sales)
        /// so upsert-by-business-key is safe against race conditions.
        /// </summary>
        private async Task EnsureIndexesAsync()
        {
            try
            {
                if (ProductsCollection != null)
                {
                    var prodIdx = new CreateIndexModel<Product>(
                        Builders<Product>.IndexKeys.Ascending(p => p.Barcode),
                        new CreateIndexOptions { Unique = true, Name = "barcode_unique" });
                    await ProductsCollection.Indexes.CreateOneAsync(prodIdx);
                }

                if (SalesCollection != null)
                {
                    var saleIdx = new CreateIndexModel<Sale>(
                        Builders<Sale>.IndexKeys.Ascending(s => s.SaleId),
                        new CreateIndexOptions { Unique = true, Name = "saleid_unique" });
                    await SalesCollection.Indexes.CreateOneAsync(saleIdx);
                }
            }
            catch { /* Non-critical — indexes may already exist */ }
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

                var productList = products.ToList();
                if (productList.Count == 0) return true;

                var writes = new List<WriteModel<Product>>();
                foreach (var p in productList)
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

        public async Task<bool> DeleteProductAsync(string barcode)
        {
            try
            {
                if (ProductsCollection is null) return false;
                var filter  = Builders<Product>.Filter.Eq(p => p.Barcode, barcode);
                await ProductsCollection.DeleteOneAsync(filter);
                return true;
            }
            catch { return false; }
        }

        public async Task<bool> DeleteManyProductsAsync(IEnumerable<string> barcodes)
        {
            try
            {
                if (ProductsCollection is null) return false;
                var list = barcodes.ToList();
                if (list.Count == 0) return true;
                
                var filter = Builders<Product>.Filter.In(p => p.Barcode, list);
                await ProductsCollection.DeleteManyAsync(filter);
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
                // Use upsert by SaleId to be idempotent
                var filter  = Builders<Sale>.Filter.Eq(s => s.SaleId, sale.SaleId);
                var options = new ReplaceOptions { IsUpsert = true };
                await SalesCollection.ReplaceOneAsync(filter, sale, options);
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
                await _db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
                return true;
            }
            catch { return false; }
        }

        public async Task<List<Sale>> GetAllSalesAsync()
        {
            try
            {
                if (SalesCollection is null) return new();
                return await SalesCollection
                    .Find(Builders<Sale>.Filter.Empty)
                    .ToListAsync();
            }
            catch { return new(); }
        }

        public async Task<bool> UpsertManySalesAsync(IEnumerable<Sale> sales)
        {
            try
            {
                if (SalesCollection is null) return false;

                var salesList = sales.ToList();
                if (salesList.Count == 0) return true;

                var writes = new List<WriteModel<Sale>>();
                foreach (var s in salesList)
                {
                    var filter = Builders<Sale>.Filter.Eq(x => x.SaleId, s.SaleId);
                    writes.Add(new ReplaceOneModel<Sale>(filter, s) { IsUpsert = true });
                }
                if (writes.Count > 0)
                    await SalesCollection.BulkWriteAsync(writes);
                return true;
            }
            catch { return false; }
        }
    }
}
