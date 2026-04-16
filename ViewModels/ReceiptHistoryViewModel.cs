using Newtonsoft.Json;
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
    public class ReceiptHistoryViewModel : BaseViewModel
    {
        private readonly MainViewModel _main;

        // ── Data ───────────────────────────────────────────────────────────
        private List<Sale> _allSales = new();

        public ObservableCollection<Sale> FilteredSales { get; } = new();

        // ── Selected receipt and its items ─────────────────────────────────
        private Sale? _selectedReceipt;
        public Sale? SelectedReceipt
        {
            get => _selectedReceipt;
            set
            {
                if (SetProperty(ref _selectedReceipt, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    
                    SelectedItems.Clear();
                    if (_selectedReceipt != null && _selectedReceipt.Items != null)
                    {
                        foreach (var item in _selectedReceipt.Items)
                        {
                            SelectedItems.Add(item);
                        }
                    }
                }
            }
        }

        public bool HasSelection => SelectedReceipt != null;

        // ── Search ─────────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }

        // ── Date-range filter ──────────────────────────────────────────────
        private DateTime _fromDate = DateTime.Today.AddMonths(-1);
        private DateTime _toDate   = DateTime.Today;

        public DateTime FromDate
        {
            get => _fromDate;
            set { SetProperty(ref _fromDate, value); ApplyFilter(); }
        }
        public DateTime ToDate
        {
            get => _toDate;
            set { SetProperty(ref _toDate, value); ApplyFilter(); }
        }

        // ── Summary for selected receipt ───────────────────────────────────
        public ObservableCollection<SaleItem> SelectedItems { get; } = new();

        // ── Commands ────────────────────────────────────────────────────────
        public ICommand RefreshCommand      { get; }
        public ICommand SelectReceiptCommand { get; }
        public ICommand PrintReceiptCommand  { get; }
        public ICommand CloseDetailCommand   { get; }
        public ICommand ClearSearchCommand   { get; }
        public ICommand SetTodayCommand      { get; }
        public ICommand SetWeekCommand       { get; }
        public ICommand SetMonthCommand      { get; }

        public ReceiptHistoryViewModel(MainViewModel main)
        {
            _main = main;

            RefreshCommand       = new AsyncRelayCommand(_ => LoadAsync());
            SelectReceiptCommand = new RelayCommand(s => SelectReceipt(s as Sale));
            PrintReceiptCommand  = new RelayCommand(_ => PrintSelected());
            CloseDetailCommand   = new RelayCommand(_ => { SelectedReceipt = null; SelectedItems.Clear(); });
            ClearSearchCommand   = new RelayCommand(_ => SearchText = string.Empty);

            SetTodayCommand = new RelayCommand(_ => { FromDate = DateTime.Today; ToDate = DateTime.Today; });
            SetWeekCommand  = new RelayCommand(_ => { FromDate = DateTime.Today.AddDays(-6); ToDate = DateTime.Today; });
            SetMonthCommand = new RelayCommand(_ => { FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); ToDate = DateTime.Today; });

            _ = LoadAsync();
        }

        // ── Load ────────────────────────────────────────────────────────────
        private async Task LoadAsync(object? _ = null)
        {
            SetBusy("Loading receipt history…");
            try
            {
                _allSales = await _main.Storage.LoadSalesAsync();
                // Ensure items are deserialised
                foreach (var s in _allSales.Where(x => x.Items.Count == 0 && !string.IsNullOrEmpty(x.ItemsJson)))
                {
                    try { s.Items = JsonConvert.DeserializeObject<List<SaleItem>>(s.ItemsJson) ?? new(); }
                    catch { s.Items = new(); }
                }
                ApplyFilter();
            }
            finally { ClearBusy(); }
        }

        // ── Filter ──────────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            FilteredSales.Clear();
            var q = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
            var toEnd = ToDate.Date.AddDays(1).AddTicks(-1);   // include full ToDate day

            var matches = _allSales
                .Where(s => s.SaleDate.Date >= FromDate.Date && s.SaleDate <= toEnd)
                .Where(s => string.IsNullOrEmpty(q)
                            || s.SaleId.ToLowerInvariant().Contains(q)
                            || s.PaymentMethod.ToLowerInvariant().Contains(q)
                            || s.Items.Any(i => i.Name.ToLowerInvariant().Contains(q)
                                             || i.Barcode.Contains(q)))
                .OrderByDescending(s => s.SaleDate);

            foreach (var s in matches)
                FilteredSales.Add(s);

            OnPropertyChanged(nameof(FilteredSales));
        }

        // ── Select receipt ──────────────────────────────────────────────────
        private void SelectReceipt(Sale? sale)
        {
            if (sale is null) return;
            SelectedReceipt = sale;
            SelectedItems.Clear();
            foreach (var item in sale.Items)
                SelectedItems.Add(item);
        }

        // ── Print ───────────────────────────────────────────────────────────
        private void PrintSelected()
        {
            if (SelectedReceipt is null)
            {
                _main.Notify("Select a receipt first", "Warning");
                return;
            }
            try
            {
                _main.Printer.PrintReceipt(SelectedReceipt, _main.Settings,
                    string.IsNullOrWhiteSpace(_main.Settings.PrinterName) ? null : _main.Settings.PrinterName);
                _main.Notify($"Printing {SelectedReceipt.SaleId}…", "Success");
            }
            catch (Exception ex)
            {
                _main.Notify($"Print error: {ex.Message}", "Error");
            }
        }
    }
}
