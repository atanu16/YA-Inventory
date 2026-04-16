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
    public class DashboardViewModel : BaseViewModel
    {
        private readonly MainViewModel _main;

        // ── KPI cards ──────────────────────────────────────────────────────
        private int     _totalProducts;
        private int     _totalStock;
        private decimal _todaySales;
        private decimal _monthlySales;
        private int     _lowStockCount;
        private int     _outOfStockCount;

        public int     TotalProducts   { get => _totalProducts;   set => SetProperty(ref _totalProducts, value); }
        public int     TotalStock      { get => _totalStock;       set => SetProperty(ref _totalStock, value); }
        public decimal TodaySales      { get => _todaySales;       set => SetProperty(ref _todaySales, value); }
        public decimal MonthlySales    { get => _monthlySales;     set => SetProperty(ref _monthlySales, value); }
        public int     LowStockCount   { get => _lowStockCount;    set => SetProperty(ref _lowStockCount, value); }
        public int     OutOfStockCount { get => _outOfStockCount;  set => SetProperty(ref _outOfStockCount, value); }

        // ── Today's Payments ───────────────────────────────────────────────
        private decimal _cashPaymentsToday;
        private decimal _upiPaymentsToday;
        private decimal _otherPaymentsToday;

        public decimal CashPaymentsToday  { get => _cashPaymentsToday;  set => SetProperty(ref _cashPaymentsToday, value); }
        public decimal UpiPaymentsToday   { get => _upiPaymentsToday;   set => SetProperty(ref _upiPaymentsToday, value); }
        public decimal OtherPaymentsToday { get => _otherPaymentsToday; set => SetProperty(ref _otherPaymentsToday, value); }

        // ── Lists ──────────────────────────────────────────────────────────
        public ObservableCollection<Product> LowStockProducts  { get; } = new();
        public ObservableCollection<Sale>    RecentSales        { get; } = new();
        public ObservableCollection<SalesBarData> WeeklySalesChart { get; } = new();

        // ── Dialog state ───────────────────────────────────────────────────
        private bool _showInvoiceDialog;
        public bool ShowInvoiceDialog
        {
            get => _showInvoiceDialog;
            set => SetProperty(ref _showInvoiceDialog, value);
        }

        private Sale? _selectedInvoice;
        public Sale? SelectedInvoice
        {
            get => _selectedInvoice;
            set => SetProperty(ref _selectedInvoice, value);
        }

        // ── Commands ───────────────────────────────────────────────────────
        public ICommand RefreshCommand       { get; }
        public ICommand GoToInventoryCommand { get; }
        public ICommand GoToBillingCommand   { get; }
        public ICommand GoToReportsCommand   { get; }
        public ICommand ViewInvoiceCommand   { get; }
        public ICommand CloseInvoiceCommand  { get; }

        public string CurrencySymbol => _main.Settings.CurrencySymbol;

        public DashboardViewModel(MainViewModel main)
        {
            _main = main;
            RefreshCommand       = new AsyncRelayCommand(LoadAsync);
            GoToInventoryCommand = new RelayCommand(_ => _main.Navigate("Inventory"));
            GoToBillingCommand   = new RelayCommand(_ => _main.Navigate("Billing"));
            GoToReportsCommand   = new RelayCommand(_ => _main.Navigate("Reports"));
            
            ViewInvoiceCommand = new RelayCommand(s => {
                if (s is Sale sale)
                {
                    SelectedInvoice = sale;
                    ShowInvoiceDialog = true;
                }
            });

            CloseInvoiceCommand = new RelayCommand(_ => {
                ShowInvoiceDialog = false;
                SelectedInvoice = null;
            });

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            SetBusy("Loading dashboard…");
            try
            {
                var products = await _main.Storage.LoadProductsAsync();
                var active   = products.Where(p => !p.IsDeleted).ToList();

                TotalProducts   = active.Count;
                TotalStock      = active.Sum(p => p.Quantity);
                LowStockCount   = active.Count(p => p.StockStatus == StockStatus.LowStock);
                OutOfStockCount = active.Count(p => p.StockStatus == StockStatus.OutOfStock);

                var sales  = await _main.Storage.LoadSalesAsync();
                var today  = DateTime.Today;
                var todaysSalesList = sales.Where(s => s.SaleDate.Date == today).ToList();
                TodaySales    = todaysSalesList.Sum(s => s.Total);
                MonthlySales  = sales.Where(s => s.SaleDate.Year  == today.Year &&
                                                  s.SaleDate.Month == today.Month)
                                     .Sum(s => s.Total);

                // Today's Payment breakdown
                CashPaymentsToday  = todaysSalesList.Where(s => string.Equals(s.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase)).Sum(s => s.Total);
                UpiPaymentsToday   = todaysSalesList.Where(s => string.Equals(s.PaymentMethod, "UPI", StringComparison.OrdinalIgnoreCase)).Sum(s => s.Total);
                OtherPaymentsToday = todaysSalesList.Where(s => !string.Equals(s.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase) 
                                                             && !string.Equals(s.PaymentMethod, "UPI", StringComparison.OrdinalIgnoreCase)).Sum(s => s.Total);

                // Low stock list (top 10)
                LowStockProducts.Clear();
                foreach (var p in active.Where(p => p.StockStatus is StockStatus.LowStock or StockStatus.OutOfStock)
                                         .OrderBy(p => p.Quantity).Take(10))
                    LowStockProducts.Add(p);

                // Recent sales (last 15)
                RecentSales.Clear();
                foreach (var s in sales.OrderByDescending(s => s.SaleDate).Take(15))
                    RecentSales.Add(s);

                // Weekly chart data (last 7 days)
                WeeklySalesChart.Clear();
                for (int i = 6; i >= 0; i--)
                {
                    var day    = today.AddDays(-i);
                    var amount = sales.Where(s => s.SaleDate.Date == day).Sum(s => s.Total);
                    WeeklySalesChart.Add(new SalesBarData
                    {
                        Label  = day.ToString("ddd"),
                        Amount = amount
                    });
                }
            }
            finally { ClearBusy(); }
        }
    }

    public class SalesBarData
    {
        public string  Label  { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
