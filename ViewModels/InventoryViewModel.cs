using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YAInventory.Helpers;
using YAInventory.Models;

namespace YAInventory.ViewModels
{
    public class InventoryViewModel : BaseViewModel
    {
        private readonly MainViewModel _main;

        // ── All products (master list) ─────────────────────────────────────
        private List<Product> _allProducts = new();

        // ── Filtered / displayed list ──────────────────────────────────────
        public ObservableCollection<Product> Products { get; } = new();

        // ── Search & filter ────────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _filterCategory = "All";
        private string _filterStock = "All";

        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }
        public string FilterCategory
        {
            get => _filterCategory;
            set { SetProperty(ref _filterCategory, value); ApplyFilter(); }
        }
        public string FilterStock
        {
            get => _filterStock;
            set { SetProperty(ref _filterStock, value); ApplyFilter(); }
        }

        public ObservableCollection<string> Categories { get; } = new() { "All" };
        public ObservableCollection<string> StockFilters { get; } =
            new() { "All", "In Stock", "Low Stock", "Out of Stock" };

        // ── Selected product ───────────────────────────────────────────────
        private Product? _selectedProduct;
        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }

        // ── Barcode scanner buffer ─────────────────────────────────────────
        private string _barcodeBuffer = string.Empty;
        public string BarcodeBuffer
        {
            get => _barcodeBuffer;
            set => SetProperty(ref _barcodeBuffer, value);
        }

        // ── Dialog state ───────────────────────────────────────────────────
        private bool    _showDialog;
        private bool    _isEditMode;
        private Product _dialogProduct = new();

        public bool    ShowDialog    { get => _showDialog;    set => SetProperty(ref _showDialog, value); }
        public bool    IsEditMode    { get => _isEditMode;    set => SetProperty(ref _isEditMode, value); }
        public Product DialogProduct { get => _dialogProduct; set => SetProperty(ref _dialogProduct, value); }

        // ── Qty-update dialog state ────────────────────────────────────────
        private bool    _showQtyDialog;
        private Product _qtyDialogProduct = new();
        private string  _newQuantity = string.Empty;

        public bool    ShowQtyDialog     { get => _showQtyDialog;     set => SetProperty(ref _showQtyDialog, value); }
        public Product QtyDialogProduct  { get => _qtyDialogProduct;  set { SetProperty(ref _qtyDialogProduct, value); OnPropertyChanged(nameof(QtyPreviewTotal)); } }
        public string  NewQuantity       { get => _newQuantity;       set { SetProperty(ref _newQuantity, value); OnPropertyChanged(nameof(QtyPreviewTotal)); } }

        /// <summary>Live preview: current qty + what the user typed.</summary>
        public string QtyPreviewTotal
        {
            get
            {
                if (int.TryParse(_newQuantity?.Trim(), out int added) && added >= 0)
                    return (_qtyDialogProduct.Quantity + added).ToString();
                return "—";
            }
        }

        // ── Stats ──────────────────────────────────────────────────────────
        public int TotalCount      => _allProducts.Count(p => !p.IsDeleted);
        public int LowStockCount   => _allProducts.Count(p => !p.IsDeleted && p.StockStatus == StockStatus.LowStock);
        public int OutOfStockCount => _allProducts.Count(p => !p.IsDeleted && p.StockStatus == StockStatus.OutOfStock);
        public string CurrencySymbol => _main.Settings.CurrencySymbol;

        // ── Commands ───────────────────────────────────────────────────────
        public ICommand LoadCommand          { get; }
        public ICommand AddProductCommand    { get; }
        public ICommand EditProductCommand   { get; }
        public ICommand DeleteProductCommand { get; }
        public ICommand SaveProductCommand   { get; }
        public ICommand CancelDialogCommand  { get; }
        public ICommand BarcodeScannedCommand{ get; }
        public ICommand ExportCommand        { get; }
        public ICommand RefreshCommand       { get; }
        public ICommand UpdateQtyCommand     { get; }
        public ICommand SaveQtyCommand       { get; }
        public ICommand CancelQtyDialogCommand { get; }

        public InventoryViewModel(MainViewModel main)
        {
            _main = main;

            LoadCommand           = new AsyncRelayCommand(LoadProductsAsync);
            AddProductCommand     = new RelayCommand(_ => OpenAddDialog());
            EditProductCommand    = new RelayCommand(p => OpenEditDialog(p as Product ?? SelectedProduct));
            DeleteProductCommand  = new AsyncRelayCommand(p => DeleteAsync(p as Product ?? SelectedProduct));
            SaveProductCommand    = new AsyncRelayCommand(SaveProductAsync);
            CancelDialogCommand   = new RelayCommand(_ => ShowDialog = false);
            BarcodeScannedCommand = new AsyncRelayCommand(HandleBarcodeAsync);
            ExportCommand         = new AsyncRelayCommand(ExportToExcelAsync);
            RefreshCommand        = new AsyncRelayCommand(RefreshAndSyncAsync);
            UpdateQtyCommand      = new RelayCommand(p => OpenQtyDialog(p as Product ?? SelectedProduct));
            SaveQtyCommand        = new AsyncRelayCommand(SaveQtyAsync);
            CancelQtyDialogCommand= new RelayCommand(_ => ShowQtyDialog = false);

            _ = LoadProductsAsync();
        }

        // ── Load ───────────────────────────────────────────────────────────
        public async Task LoadProductsAsync()
        {
            SetBusy("Loading inventory…");
            try
            {
                _allProducts = await _main.Storage.LoadProductsAsync();
                _allProducts = _allProducts.Where(p => !p.IsDeleted).ToList();

                // Rebuild category list while preserving selection
                var currentCat = FilterCategory;
                var cats = _allProducts.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();
                
                Categories.Clear();
                Categories.Add("All");
                foreach (var c in cats) Categories.Add(c);

                // Restore selection if it still exists
                if (Categories.Contains(currentCat))
                    FilterCategory = currentCat;
                else
                    FilterCategory = "All";

                ApplyFilter();
                RaiseStatsChanged();
            }
            finally { ClearBusy(); }
        }

        // ── Refresh + push all to cloud ────────────────────────────────────
        private async Task RefreshAndSyncAsync(object? _)
        {
            await LoadProductsAsync();

            // Push ALL local data to MongoDB on manual refresh
            _main.Notify("Syncing all data to cloud…", "Info");
            await _main.Sync.PushAllToCloudAsync();
        }

        // ── Filter ─────────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var filtered = _allProducts.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var q = SearchText.ToLowerInvariant();
                filtered = filtered.Where(p =>
                    p.Name.ToLowerInvariant().Contains(q) ||
                    p.Barcode.Contains(q) ||
                    p.Category.ToLowerInvariant().Contains(q));
            }

            if (!string.IsNullOrEmpty(FilterCategory) && FilterCategory != "All")
                filtered = filtered.Where(p => p.Category == FilterCategory);

            if (!string.IsNullOrEmpty(FilterStock) && FilterStock != "All")
                filtered = FilterStock switch
                {
                    "In Stock"     => filtered.Where(p => p.StockStatus == StockStatus.InStock),
                    "Low Stock"    => filtered.Where(p => p.StockStatus == StockStatus.LowStock),
                    "Out of Stock" => filtered.Where(p => p.StockStatus == StockStatus.OutOfStock),
                    _              => filtered
                };

            Products.Clear();
            foreach (var p in filtered.OrderBy(p => p.Name))
                Products.Add(p);
        }

        // ── Barcode scanner ────────────────────────────────────────────────
        public async Task HandleBarcodeAsync(object? barcode)
        {
            var code = (barcode as string ?? BarcodeBuffer).Trim();
            if (string.IsNullOrEmpty(code)) return;

            BarcodeBuffer = string.Empty;

            var existing = _allProducts.FirstOrDefault(p => p.Barcode == code);
            if (existing != null)
            {
                // Directly open the edit view for the existing product
                _main.Notify($"Found: {existing.Name}", "Info");
                OpenEditDialog(existing);
            }
            else
            {
                // New product — open add dialog pre-filled with barcode
                var newProduct = new Product
                {
                    Barcode  = code
                };
                OpenAddDialog(newProduct);
                _main.Notify($"New barcode {code} — fill in product details", "Warning");
            }
        }

        // ── Dialog helpers ─────────────────────────────────────────────────
        private void OpenAddDialog(Product? template = null)
        {
            DialogProduct = template ?? new Product();
            IsEditMode = false;
            ShowDialog = true;
        }

        private void OpenEditDialog(Product? product)
        {
            if (product is null) return;
            // Clone to avoid editing in-place before save
            DialogProduct = new Product
            {
                ProductId       = product.ProductId,
                Name            = product.Name,
                Barcode         = product.Barcode,
                Price           = product.Price,
                Quantity        = product.Quantity,
                Category        = product.Category,
                SalePrice       = product.SalePrice,
                ImagePath       = product.ImagePath,
                CreatedAt       = product.CreatedAt,
                UpdatedAt       = product.UpdatedAt,
                MongoId         = product.MongoId
            };
            IsEditMode = true;
            ShowDialog = true;
        }

        // ── Save ───────────────────────────────────────────────────────────
        private async Task SaveProductAsync(object? _)
        {
            if (string.IsNullOrWhiteSpace(DialogProduct.Name))
            {
                _main.Notify("Product name is required", "Error");
                return;
            }
            if (string.IsNullOrWhiteSpace(DialogProduct.Barcode))
            {
                _main.Notify("Barcode is required", "Error");
                return;
            }

            SetBusy("Saving product…");
            try
            {
                DialogProduct.UpdatedAt = DateTime.UtcNow;
                bool isNew = !IsEditMode;
                if (isNew) DialogProduct.CreatedAt = DateTime.UtcNow;

                await _main.Storage.UpsertProductAsync(DialogProduct);

                // Push to Mongo immediately if connected
                bool pushedToCloud = false;
                if (_main.Mongo.IsConnected)
                {
                    pushedToCloud = await _main.Mongo.UpsertProductAsync(DialogProduct);
                }

                ShowDialog = false;

                if (isNew)
                {
                    // Clear search and filter so the newly added product is definitely visible
                    SearchText = string.Empty;
                    FilterStock = "All";
                    FilterCategory = "All";
                }

                await LoadProductsAsync();
                
                // Highlight the newly added/edited product
                SelectedProduct = Products.FirstOrDefault(p => p.Barcode == DialogProduct.Barcode);

                if (pushedToCloud)
                    _main.Notify(isNew ? "Product added & synced to cloud!" : "Product updated & synced!", "Success");
                else
                {
                    _main.Notify(isNew ? "Product added locally — will sync when online" : "Product updated locally — will sync when online", "Warning");
                    // Trigger a sync cycle so it picks up the change soon
                    _ = _main.Sync.RunSyncCycleAsync();
                }
            }
            finally { ClearBusy(); }
        }

        // ── Update Quantity ────────────────────────────────────────────────
        private void OpenQtyDialog(Product? product)
        {
            if (product is null) return;
            QtyDialogProduct = product;
            NewQuantity      = "0";
            ShowQtyDialog    = true;
        }

        private async Task SaveQtyAsync(object? _)
        {
            if (!int.TryParse(NewQuantity?.Trim(), out int addQty) || addQty < 0)
            {
                _main.Notify("Please enter a valid non-negative quantity to add.", "Error");
                return;
            }

            SetBusy("Updating quantity…");
            try
            {
                var p        = QtyDialogProduct;
                p.Quantity  += addQty;
                p.UpdatedAt  = DateTime.UtcNow;
                await _main.Storage.UpsertProductAsync(p);
                if (_main.Mongo.IsConnected)
                    await _main.Mongo.UpsertProductAsync(p);
                _main.Notify($"{p.Name}: +{addQty} → now {p.Quantity}.", "Success");

                ShowQtyDialog = false;
                await LoadProductsAsync();

                if (!_main.Mongo.IsConnected)
                    _ = _main.Sync.RunSyncCycleAsync();
            }
            finally { ClearBusy(); }
        }

        // ── Delete ─────────────────────────────────────────────────────────
        private async Task DeleteAsync(Product? product)
        {
            if (product is null) return;

            var result = MessageBox.Show(
                $"Delete '{product.Name}'?\n\nThis will permanently delete the product from local storage and the cloud.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            SetBusy("Deleting…");
            try
            {
                await _main.Storage.DeleteProductAsync(product.Barcode);

                // Push hard-delete to Mongo so cloud stays in sync
                if (_main.Mongo.IsConnected)
                {
                    await _main.Mongo.DeleteProductAsync(product.Barcode);
                }

                await LoadProductsAsync();
                _main.Notify("Product deleted", "Success");
            }
            finally { ClearBusy(); }
        }

        // ── Export ─────────────────────────────────────────────────────────
        private async Task ExportToExcelAsync(object? _)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter   = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"Inventory_{DateTime.Today:yyyy-MM-dd}.xlsx"
            };
            if (dialog.ShowDialog() != true) return;

            SetBusy("Exporting…");
            try
            {
                await Task.Run(() => ExcelExportHelper.ExportProducts(_allProducts, dialog.FileName));
                _main.Notify("Exported successfully!", "Success");
            }
            catch (Exception ex)
            {
                _main.Notify($"Export failed: {ex.Message}", "Error");
            }
            finally { ClearBusy(); }
        }

        private void RaiseStatsChanged()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(LowStockCount));
            OnPropertyChanged(nameof(OutOfStockCount));
        }
    }
}
