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

        // ── Stats ──────────────────────────────────────────────────────────
        public int TotalCount      => _allProducts.Count(p => !p.IsDeleted);
        public int LowStockCount   => _allProducts.Count(p => !p.IsDeleted && p.StockStatus == StockStatus.LowStock);
        public int OutOfStockCount => _allProducts.Count(p => !p.IsDeleted && p.StockStatus == StockStatus.OutOfStock);
        public string CurrencySymbol => _main.Settings.CurrencySymbol;

        // ── Commands ───────────────────────────────────────────────────────
        public ICommand LoadCommand         { get; }
        public ICommand AddProductCommand   { get; }
        public ICommand EditProductCommand  { get; }
        public ICommand DeleteProductCommand{ get; }
        public ICommand SaveProductCommand  { get; }
        public ICommand CancelDialogCommand { get; }
        public ICommand BarcodeScannedCommand { get; }
        public ICommand ExportCommand       { get; }
        public ICommand RefreshCommand      { get; }

        public InventoryViewModel(MainViewModel main)
        {
            _main = main;

            LoadCommand          = new AsyncRelayCommand(LoadProductsAsync);
            AddProductCommand    = new RelayCommand(_ => OpenAddDialog());
            EditProductCommand   = new RelayCommand(p => OpenEditDialog(p as Product ?? SelectedProduct));
            DeleteProductCommand = new AsyncRelayCommand(p => DeleteAsync(p as Product ?? SelectedProduct));
            SaveProductCommand   = new AsyncRelayCommand(SaveProductAsync);
            CancelDialogCommand  = new RelayCommand(_ => ShowDialog = false);
            BarcodeScannedCommand= new AsyncRelayCommand(HandleBarcodeAsync);
            ExportCommand        = new AsyncRelayCommand(ExportToExcelAsync);
            RefreshCommand       = new AsyncRelayCommand(LoadProductsAsync);

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

                // Rebuild category list
                var cats = _allProducts.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();
                Categories.Clear();
                Categories.Add("All");
                foreach (var c in cats) Categories.Add(c);

                ApplyFilter();
                RaiseStatsChanged();
            }
            finally { ClearBusy(); }
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

            if (FilterCategory != "All")
                filtered = filtered.Where(p => p.Category == FilterCategory);

            if (FilterStock != "All")
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
                // Jump to product in list
                SearchText      = code;
                SelectedProduct = existing;
                _main.Notify($"Found: {existing.Name}", "Info");
            }
            else
            {
                // New product — open add dialog pre-filled with barcode
                var newProduct = new Product
                {
                    Barcode          = code,
                    DefaultDiscount  = _main.Settings.DefaultDiscountPercent
                };
                OpenAddDialog(newProduct);
                _main.Notify($"New barcode {code} — fill in product details", "Warning");
            }
        }

        // ── Dialog helpers ─────────────────────────────────────────────────
        private void OpenAddDialog(Product? template = null)
        {
            DialogProduct = template ?? new Product
            {
                DefaultDiscount = _main.Settings.DefaultDiscountPercent
            };
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
                DefaultDiscount = product.DefaultDiscount,
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

                // Also push to Mongo if connected
                if (_main.Mongo.IsConnected)
                    await _main.Mongo.UpsertProductAsync(DialogProduct);

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

                _main.Notify(isNew ? "Product added!" : "Product updated!", "Success");
            }
            finally { ClearBusy(); }
        }

        // ── Delete ─────────────────────────────────────────────────────────
        private async Task DeleteAsync(Product? product)
        {
            if (product is null) return;

            var result = MessageBox.Show(
                $"Delete '{product.Name}'?\n\nProduct will be soft-deleted (quantity zeroed).",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            SetBusy("Deleting…");
            try
            {
                await _main.Storage.DeleteProductAsync(product.Barcode);
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
