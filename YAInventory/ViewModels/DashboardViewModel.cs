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

        // ── Lists ──────────────────────────────────────────────────────────
        public ObservableCollection<Product> LowStockProducts  { get; } = new();
        public ObservableCollection<Sale>    RecentSales        { get; } = new();
        public ObservableCollection<SalesBarData> WeeklySalesChart { get; } = new();

        // ── Commands ───────────────────────────────────────────────────────
        public ICommand RefreshCommand       { get; }
        public ICommand GoToInventoryCommand { get; }
        public ICommand GoToBillingCommand   { get; }

        public string CurrencySymbol => _main.Settings.CurrencySymbol;

        public DashboardViewModel(MainViewModel main)
        {
            _main = main;
            RefreshCommand       = new AsyncRelayCommand(LoadAsync);
            GoToInventoryCommand = new RelayCommand(_ => _main.Navigate("Inventory"));
            GoToBillingCommand   = new RelayCommand(_ => _main.Navigate("Billing"));

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
                TodaySales    = sales.Where(s => s.SaleDate.Date == today).Sum(s => s.Total);
                MonthlySales  = sales.Where(s => s.SaleDate.Year  == today.Year &&
                                                  s.SaleDate.Month == today.Month)
                                     .Sum(s => s.Total);

                // Low stock list (top 10)
                LowStockProducts.Clear();
                foreach (var p in active.Where(p => p.StockStatus is StockStatus.LowStock or StockStatus.OutOfStock)
                                         .OrderBy(p => p.Quantity).Take(10))
                    LowStockProducts.Add(p);

                // Recent sales (last 8)
                RecentSales.Clear();
                foreach (var s in sales.OrderByDescending(s => s.SaleDate).Take(8))
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
