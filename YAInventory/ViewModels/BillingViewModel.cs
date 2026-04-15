using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using YAInventory.Helpers;
using YAInventory.Models;

namespace YAInventory.ViewModels
{
    public class BillingViewModel : BaseViewModel
    {
        private readonly MainViewModel _main;

        // ── Cart ───────────────────────────────────────────────────────────
        public ObservableCollection<CartItem> CartItems { get; } = new();

        // ── Barcode input ──────────────────────────────────────────────────
        private string _barcodeInput = string.Empty;
        public string BarcodeInput
        {
            get => _barcodeInput;
            set => SetProperty(ref _barcodeInput, value);
        }

        // ── Cart-level discount ────────────────────────────────────────────
        private decimal _cartDiscountPercent;
        private decimal _cartDiscountFlat;
        private string  _paymentMethod = "Cash";

        public decimal CartDiscountPercent
        {
            get => _cartDiscountPercent;
            set { SetProperty(ref _cartDiscountPercent, value); RecalcTotals(); }
        }
        public decimal CartDiscountFlat
        {
            get => _cartDiscountFlat;
            set { SetProperty(ref _cartDiscountFlat, value); RecalcTotals(); }
        }
        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        public IEnumerable<string> PaymentMethods { get; } =
            new[] { "Cash", "Card", "UPI", "Wallet", "Other" };

        // ── Totals ─────────────────────────────────────────────────────────
        private decimal _subtotal;
        private decimal _discountAmount;
        private decimal _taxAmount;
        private decimal _finalTotal;

        public decimal Subtotal       { get => _subtotal;       private set => SetProperty(ref _subtotal, value); }
        public decimal DiscountAmount { get => _discountAmount; private set => SetProperty(ref _discountAmount, value); }
        public decimal TaxAmount      { get => _taxAmount;      private set => SetProperty(ref _taxAmount, value); }
        public decimal FinalTotal     { get => _finalTotal;     private set => SetProperty(ref _finalTotal, value); }

        public string CurrencySymbol => _main.Settings.CurrencySymbol;

        // ── Search-as-you-type product list ────────────────────────────────
        private List<Product> _allProducts = new();
        public ObservableCollection<Product> SearchResults { get; } = new();
        public bool HasSearchResults => SearchResults.Count > 0;

        private string _productSearch = string.Empty;
        public string ProductSearch
        {
            get => _productSearch;
            set { SetProperty(ref _productSearch, value); FilterProducts(); }
        }

        // ── Commands ───────────────────────────────────────────────────────
        public ICommand ScanBarcodeCommand     { get; }
        public ICommand AddToCartCommand       { get; }
        public ICommand RemoveFromCartCommand  { get; }
        public ICommand IncrQtyCommand         { get; }
        public ICommand DecrQtyCommand         { get; }
        public ICommand ClearCartCommand       { get; }
        public ICommand CheckoutCommand        { get; }
        public ICommand PrintLastReceiptCommand{ get; }

        private Sale? _lastSale;

        public BillingViewModel(MainViewModel main)
        {
            _main = main;

            ScanBarcodeCommand      = new AsyncRelayCommand(HandleScanAsync);
            AddToCartCommand        = new RelayCommand(p => AddToCart(p as Product));
            RemoveFromCartCommand   = new RelayCommand(p => RemoveFromCart(p as CartItem));
            IncrQtyCommand          = new RelayCommand(p => ChangeQty(p as CartItem, +1));
            DecrQtyCommand          = new RelayCommand(p => ChangeQty(p as CartItem, -1));
            ClearCartCommand        = new RelayCommand(_ => ClearCart());
            CheckoutCommand         = new AsyncRelayCommand(CheckoutAsync);
            PrintLastReceiptCommand = new RelayCommand(_ => PrintLastReceipt());

            CartItems.CollectionChanged       += (_, _) => RecalcTotals();
            SearchResults.CollectionChanged   += (_, _) => OnPropertyChanged(nameof(HasSearchResults));

            _ = LoadProductsAsync();
        }

        private async Task LoadProductsAsync()
        {
            _allProducts = await _main.Storage.LoadProductsAsync();
            _allProducts = _allProducts.Where(p => !p.IsDeleted).ToList();
        }

        // ── Barcode scan ───────────────────────────────────────────────────
        public async Task HandleScanAsync(object? _)
        {
            var code = BarcodeInput.Trim();
            BarcodeInput = string.Empty;
            if (string.IsNullOrEmpty(code)) return;

            var product = _allProducts.FirstOrDefault(p => p.Barcode == code);
            if (product is null)
            {
                await LoadProductsAsync();   // refresh cache
                product = _allProducts.FirstOrDefault(p => p.Barcode == code);
            }

            if (product is null)
            {
                _main.Notify($"Barcode '{code}' not found. Add it in Inventory first.", "Warning");
                return;
            }

            AddToCart(product);
        }

        // ── Cart operations ────────────────────────────────────────────────
        public void AddToCart(Product? product)
        {
            if (product is null) return;

            var existing = CartItems.FirstOrDefault(c => c.Barcode == product.Barcode);
            if (existing != null)
            {
                if (existing.Quantity >= product.Quantity)
                {
                    _main.Notify($"Cannot add more. Only {product.Quantity} in stock.", "Warning");
                    return;
                }
                existing.Quantity++;
            }
            else
            {
                if (product.Quantity <= 0)
                {
                    _main.Notify("Out of stock.", "Warning");
                    return;
                }

                CartItems.Add(new CartItem
                {
                    ProductId       = product.ProductId,
                    Barcode         = product.Barcode,
                    Name            = product.Name,
                    UnitPrice       = product.Price,
                    Quantity        = 1,
                    DiscountPercent = product.DefaultDiscount
                });
            }

            // Clear search field after successful add
            ProductSearch = string.Empty;
            
            RecalcTotals();
        }

        private void RemoveFromCart(CartItem? item)
        {
            if (item is null) return;
            CartItems.Remove(item);
            RecalcTotals();
        }

        private void ChangeQty(CartItem? item, int delta)
        {
            if (item is null) return;

            // Enforce stock limits when increasing quantity
            if (delta > 0)
            {
                var product = _allProducts.FirstOrDefault(p => p.Barcode == item.Barcode);
                if (product != null && item.Quantity + delta > product.Quantity)
                {
                    _main.Notify($"Cannot add more. Only {product.Quantity} in stock.", "Warning");
                    return;
                }
            }

            int newQty = item.Quantity + delta;
            if (newQty <= 0)
                CartItems.Remove(item);
            else
                item.Quantity = newQty;
                
            RecalcTotals();
        }

        private void ClearCart()
        {
            CartItems.Clear();
            CartDiscountPercent = 0;
            CartDiscountFlat    = 0;
            ProductSearch       = string.Empty;
            RecalcTotals();
        }

        // ── Totals calculation ─────────────────────────────────────────────
        private void RecalcTotals()
        {
            decimal gross = CartItems.Sum(c => c.Total);   // already item-discounted
            decimal cartOff = gross * (CartDiscountPercent / 100m) + CartDiscountFlat;
            decimal discounted = Math.Max(0, gross - cartOff);
            decimal tax = discounted * (_main.Settings.TaxPercent / 100m);

            Subtotal       = gross;
            DiscountAmount = cartOff;
            TaxAmount      = Math.Round(tax, 2);
            FinalTotal     = Math.Round(discounted + TaxAmount, 2);
        }

        // ── Checkout ───────────────────────────────────────────────────────
        private async Task CheckoutAsync(object? _)
        {
            if (CartItems.Count == 0)
            {
                _main.Notify("Cart is empty", "Warning");
                return;
            }

            SetBusy("Processing sale…");
            try
            {
                var sale = new Sale
                {
                    SaleDate      = DateTime.Now,
                    Subtotal      = Subtotal,
                    Discount      = DiscountAmount,
                    TaxPercent    = _main.Settings.TaxPercent,
                    TaxAmount     = TaxAmount,
                    Total         = FinalTotal,
                    PaymentMethod = PaymentMethod,
                    Items         = CartItems.Select(c => new SaleItem
                    {
                        ProductId       = c.ProductId,
                        Barcode         = c.Barcode,
                        Name            = c.Name,
                        UnitPrice       = c.UnitPrice,
                        Quantity        = c.Quantity,
                        DiscountPercent = c.DiscountPercent,
                        DiscountFlat    = c.DiscountFlat,
                        Total           = c.Total
                    }).ToList()
                };

                // 1. Save locally
                await _main.Storage.AppendSaleAsync(sale);

                // 2. Deduct stock from inventory
                var products = await _main.Storage.LoadProductsAsync();
                foreach (var item in sale.Items)
                {
                    var p = products.FirstOrDefault(x => x.Barcode == item.Barcode);
                    if (p != null)
                    {
                        p.Quantity  = Math.Max(0, p.Quantity - item.Quantity);
                        p.UpdatedAt = DateTime.UtcNow;
                    }
                }
                await _main.Storage.SaveProductsAsync(products);

                // 3. Push to Mongo if online
                if (_main.Mongo.IsConnected)
                {
                    await _main.Mongo.InsertSaleAsync(sale);
                    foreach (var item in sale.Items)
                    {
                        var p = products.FirstOrDefault(x => x.Barcode == item.Barcode);
                        if (p != null) await _main.Mongo.UpsertProductAsync(p);
                    }
                }

                _lastSale = sale;

                // Print receipt
                PrintReceipt(sale);

                ClearCart();
                await LoadProductsAsync();
                _main.Notify($"Sale {sale.SaleId} — {_main.Settings.CurrencySymbol}{sale.Total:N2}", "Success");
            }
            finally { ClearBusy(); }
        }

        // ── Printing ───────────────────────────────────────────────────────
        private void PrintReceipt(Sale sale)
        {
            try
            {
                _main.Printer.PrintReceipt(sale, _main.Settings,
                    string.IsNullOrWhiteSpace(_main.Settings.PrinterName) ? null : _main.Settings.PrinterName);
            }
            catch (Exception ex)
            {
                _main.Notify($"Print error: {ex.Message}", "Warning");
            }
        }

        private void PrintLastReceipt()
        {
            if (_lastSale is null)
            {
                _main.Notify("No receipt to print", "Warning");
                return;
            }
            PrintReceipt(_lastSale);
        }

        // ── Product search ─────────────────────────────────────────────────
        private void FilterProducts()
        {
            SearchResults.Clear();
            if (string.IsNullOrWhiteSpace(ProductSearch)) return;

            var q = ProductSearch.ToLowerInvariant();
            foreach (var p in _allProducts.Where(p =>
                p.Name.ToLowerInvariant().Contains(q) || p.Barcode.Contains(q)).Take(8))
                SearchResults.Add(p);
        }
    }
}
