using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using YAInventory.Helpers;
using YAInventory.Models;

namespace YAInventory.ViewModels
{
    public class ReportsViewModel : BaseViewModel
    {
        private readonly MainViewModel _main;

        // ── Date range ─────────────────────────────────────────────────────
        private DateTime _fromDate = DateTime.Today.AddDays(-30);
        private DateTime _toDate   = DateTime.Today;

        public DateTime FromDate
        {
            get => _fromDate;
            set { SetProperty(ref _fromDate, value); _ = LoadAsync(); }
        }
        public DateTime ToDate
        {
            get => _toDate;
            set { SetProperty(ref _toDate, value); _ = LoadAsync(); }
        }

        // ── Summaries ──────────────────────────────────────────────────────
        private decimal _totalRevenue;
        private decimal _totalDiscount;
        private decimal _totalTax;
        private int     _totalTransactions;
        private decimal _avgOrderValue;

        public decimal TotalRevenue      { get => _totalRevenue;       set => SetProperty(ref _totalRevenue, value); }
        public decimal TotalDiscount     { get => _totalDiscount;      set => SetProperty(ref _totalDiscount, value); }
        public decimal TotalTax          { get => _totalTax;           set => SetProperty(ref _totalTax, value); }
        public int     TotalTransactions { get => _totalTransactions;  set => SetProperty(ref _totalTransactions, value); }
        public decimal AvgOrderValue     { get => _avgOrderValue;      set => SetProperty(ref _avgOrderValue, value); }

        public string CurrencySymbol => _main.Settings.CurrencySymbol;

        // ── Tables ─────────────────────────────────────────────────────────
        public ObservableCollection<Sale>              SalesList      { get; } = new();
        public ObservableCollection<DailySummaryRow>   DailySummary   { get; } = new();
        public ObservableCollection<TopProductRow>     TopProducts    { get; } = new();
        public ObservableCollection<SalesBarData>      ChartData      { get; } = new();

        // ── Dialog ─────────────────────────────────────────────────────────
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
        public ICommand RefreshCommand        { get; }
        public ICommand ExportSalesCommand    { get; }
        public ICommand ExportInventoryCommand{ get; }
        public ICommand SetTodayCommand       { get; }
        public ICommand SetWeekCommand        { get; }
        public ICommand SetMonthCommand       { get; }
        public ICommand ViewInvoiceCommand    { get; }
        public ICommand CloseInvoiceCommand   { get; }

        public ReportsViewModel(MainViewModel main)
        {
            _main = main;
            RefreshCommand         = new AsyncRelayCommand(LoadAsync);
            ExportSalesCommand     = new AsyncRelayCommand(ExportSalesAsync);
            ExportInventoryCommand = new AsyncRelayCommand(ExportInventoryAsync);
            SetTodayCommand = new RelayCommand(_ => { FromDate = DateTime.Today; ToDate = DateTime.Today; });
            SetWeekCommand  = new RelayCommand(_ => { FromDate = DateTime.Today.AddDays(-6); ToDate = DateTime.Today; });
            SetMonthCommand = new RelayCommand(_ => { FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); ToDate = DateTime.Today; });

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
            SetBusy("Loading reports…");
            try
            {
                var allSales = await _main.Storage.LoadSalesAsync();
                var filtered = allSales
                    .Where(s => s.SaleDate.Date >= FromDate.Date && s.SaleDate.Date <= ToDate.Date)
                    .OrderByDescending(s => s.SaleDate)
                    .ToList();

                TotalRevenue      = filtered.Sum(s => s.Total);
                TotalDiscount     = filtered.Sum(s => s.Discount);
                TotalTax          = filtered.Sum(s => s.TaxAmount);
                TotalTransactions = filtered.Count;
                AvgOrderValue     = filtered.Count > 0 ? TotalRevenue / filtered.Count : 0;

                SalesList.Clear();
                foreach (var s in filtered) SalesList.Add(s);

                // Daily summary
                DailySummary.Clear();
                var daily = filtered
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new DailySummaryRow
                    {
                        Date         = g.Key,
                        Transactions = g.Count(),
                        Revenue      = g.Sum(s => s.Total),
                        Discount     = g.Sum(s => s.Discount)
                    })
                    .OrderByDescending(d => d.Date);

                foreach (var d in daily) DailySummary.Add(d);

                // Top products
                TopProducts.Clear();
                var topProds = filtered
                    .SelectMany(s => s.Items)
                    .GroupBy(i => i.Name)
                    .Select(g => new TopProductRow
                    {
                        Name     = g.Key,
                        Quantity = g.Sum(i => i.Quantity),
                        Revenue  = g.Sum(i => i.Total)
                    })
                    .OrderByDescending(t => t.Revenue)
                    .Take(10);

                foreach (var t in topProds) TopProducts.Add(t);

                // Chart data (show last 14 days or selected range, capped at 14)
                ChartData.Clear();
                int days = Math.Min(14, (ToDate - FromDate).Days + 1);
                for (int i = days - 1; i >= 0; i--)
                {
                    var day = ToDate.AddDays(-i);
                    var amt = filtered.Where(s => s.SaleDate.Date == day.Date).Sum(s => s.Total);
                    ChartData.Add(new SalesBarData { Label = day.ToString("MM/dd"), Amount = amt });
                }
            }
            finally { ClearBusy(); }
        }

        private async Task ExportSalesAsync(object? _)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter   = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"Sales_{FromDate:yyyy-MM-dd}_to_{ToDate:yyyy-MM-dd}.xlsx"
            };
            if (dialog.ShowDialog() != true) return;

            SetBusy("Exporting…");
            try
            {
                await Task.Run(() => ExcelExportHelper.ExportSales(SalesList, dialog.FileName));
                _main.Notify("Sales exported!", "Success");
            }
            catch (Exception ex) { _main.Notify($"Export failed: {ex.Message}", "Error"); }
            finally { ClearBusy(); }
        }

        private async Task ExportInventoryAsync(object? _)
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
                var products = await _main.Storage.LoadProductsAsync();
                await Task.Run(() => ExcelExportHelper.ExportProducts(products, dialog.FileName));
                _main.Notify("Inventory exported!", "Success");
            }
            catch (Exception ex) { _main.Notify($"Export failed: {ex.Message}", "Error"); }
            finally { ClearBusy(); }
        }
    }

    public class DailySummaryRow
    {
        public DateTime Date         { get; set; }
        public int      Transactions { get; set; }
        public decimal  Revenue      { get; set; }
        public decimal  Discount     { get; set; }
        public string   DateLabel    => Date.ToString("dd MMM yyyy");
    }

    public class TopProductRow
    {
        public string  Name     { get; set; } = string.Empty;
        public int     Quantity { get; set; }
        public decimal Revenue  { get; set; }
    }
}
