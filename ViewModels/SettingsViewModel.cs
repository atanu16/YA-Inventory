using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using YAInventory.Helpers;
using YAInventory.Models;
using YAInventory.Services;

namespace YAInventory.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly MainViewModel _main;

        // ── Working copy of settings ───────────────────────────────────────
        private string  _shopName            = string.Empty;
        private string  _address             = string.Empty;
        private string  _phone               = string.Empty;
        private string  _email               = string.Empty;
        private string? _logoPath;
        private decimal _defaultDiscount;
        private decimal _taxPercent;
        private string  _mongoUri            = string.Empty;
        private string  _mongoDb             = string.Empty;
        private int     _syncInterval        = 30;
        private string  _printerName         = string.Empty;
        private int     _lowStockThreshold   = 5;
        private string  _currencySymbol      = "₹";

        public string  ShopName            { get => _shopName;          set => SetProperty(ref _shopName, value); }
        public string  Address             { get => _address;           set => SetProperty(ref _address, value); }
        public string  Phone               { get => _phone;             set => SetProperty(ref _phone, value); }
        public string  Email               { get => _email;             set => SetProperty(ref _email, value); }
        public string? LogoPath            { get => _logoPath;          set => SetProperty(ref _logoPath, value); }
        public decimal DefaultDiscount     { get => _defaultDiscount;   set => SetProperty(ref _defaultDiscount, value); }
        public decimal TaxPercent          { get => _taxPercent;        set => SetProperty(ref _taxPercent, value); }
        public string  MongoUri            { get => _mongoUri;          set => SetProperty(ref _mongoUri, value); }
        public string  MongoDbName         { get => _mongoDb;           set => SetProperty(ref _mongoDb, value); }
        public int     SyncInterval        { get => _syncInterval;      set => SetProperty(ref _syncInterval, value); }
        public string  PrinterName         { get => _printerName;       set => SetProperty(ref _printerName, value); }
        public int     LowStockThreshold   { get => _lowStockThreshold; set => SetProperty(ref _lowStockThreshold, value); }
        public string  CurrencySymbol      { get => _currencySymbol;    set => SetProperty(ref _currencySymbol, value); }

        // ── Connection test ────────────────────────────────────────────────
        private string _connectionStatus = "Not tested";
        public  string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        // ── Printer list ───────────────────────────────────────────────────
        public ObservableCollection<string> AvailablePrinters { get; } = new();

        // ── Commands ───────────────────────────────────────────────────────
        public ICommand SaveCommand          { get; }
        public ICommand TestMongoCommand     { get; }
        public ICommand BrowseLogoCommand    { get; }
        public ICommand OpenDataFolderCommand{ get; }
        public ICommand BackupCommand        { get; }
        public ICommand RefreshPrintersCommand { get; }

        public string DataFolder => LocalStorageService.RootFolder;

        public SettingsViewModel(MainViewModel main)
        {
            _main = main;
            SaveCommand           = new AsyncRelayCommand(SaveAsync);
            TestMongoCommand      = new AsyncRelayCommand(TestMongoAsync);
            BrowseLogoCommand     = new RelayCommand(_ => BrowseLogo());
            OpenDataFolderCommand = new RelayCommand(_ => OpenDataFolder());
            BackupCommand         = new AsyncRelayCommand(BackupAsync);
            RefreshPrintersCommand= new RelayCommand(_ => LoadPrinters());

            LoadFromSettings();
            LoadPrinters();
        }

        private void LoadFromSettings()
        {
            var s = _main.Settings;
            ShopName          = s.ShopName;
            Address           = s.Address;
            Phone             = s.Phone;
            Email             = s.Email;
            LogoPath          = s.LogoPath;
            DefaultDiscount   = s.DefaultDiscountPercent;
            TaxPercent        = s.TaxPercent;
            MongoUri          = s.MongoConnectionString;
            MongoDbName       = s.MongoDatabaseName;
            SyncInterval      = s.SyncIntervalSeconds;
            PrinterName       = s.PrinterName;
            LowStockThreshold = s.LowStockThreshold;
            CurrencySymbol    = s.CurrencySymbol;
        }

        private async Task SaveAsync(object? _)
        {
            SetBusy("Saving settings…");
            try
            {
                var s = _main.Settings;
                s.ShopName               = ShopName;
                s.Address                = Address;
                s.Phone                  = Phone;
                s.Email                  = Email;
                s.LogoPath               = LogoPath;
                s.DefaultDiscountPercent = DefaultDiscount;
                s.TaxPercent             = TaxPercent;
                s.MongoConnectionString  = MongoUri;
                s.MongoDatabaseName      = MongoDbName;
                s.SyncIntervalSeconds    = SyncInterval;
                s.PrinterName            = PrinterName;
                s.LowStockThreshold      = LowStockThreshold;
                s.CurrencySymbol         = CurrencySymbol;

                _main.Storage.SaveSettings(s);
                _main.Settings = s;

                // Restart sync with new settings
                _main.Sync.Stop();
                if (!string.IsNullOrWhiteSpace(MongoUri))
                    _main.Sync.Start(s, SyncInterval);

                await Task.Delay(200);
                _main.Notify("Settings saved!", "Success");
            }
            finally { ClearBusy(); }
        }

        private async Task TestMongoAsync(object? _)
        {
            if (string.IsNullOrWhiteSpace(MongoUri))
            {
                ConnectionStatus = "No connection string entered";
                return;
            }

            ConnectionStatus = "Testing…";
            SetBusy("Connecting to MongoDB…");
            try
            {
                var ok = await _main.Mongo.ConnectAsync(MongoUri, MongoDbName);
                ConnectionStatus = ok
                    ? "Connected successfully!"
                    : "Connection failed — check URI and network";
                _main.Notify(ConnectionStatus, ok ? "Success" : "Error");
            }
            finally { ClearBusy(); }
        }

        private void BrowseLogo()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };
            if (dialog.ShowDialog() != true) return;

            // Copy to images folder
            var dest = Path.Combine(LocalStorageService.ImagesFolder, "logo" + Path.GetExtension(dialog.FileName));
            File.Copy(dialog.FileName, dest, overwrite: true);
            LogoPath = dest;
        }

        private static void OpenDataFolder()
        {
            if (Directory.Exists(LocalStorageService.RootFolder))
                System.Diagnostics.Process.Start("explorer.exe", LocalStorageService.RootFolder);
        }

        private async Task BackupAsync(object? _)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter   = "Zip Files (*.zip)|*.zip",
                FileName = $"YAInventory_Backup_{DateTime.Today:yyyy-MM-dd}.zip"
            };
            if (dialog.ShowDialog() != true) return;

            SetBusy("Creating backup…");
            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(dialog.FileName)) File.Delete(dialog.FileName);
                    System.IO.Compression.ZipFile.CreateFromDirectory(
                        LocalStorageService.RootFolder, dialog.FileName);
                });
                _main.Notify("Backup created!", "Success");
            }
            catch (Exception ex) { _main.Notify($"Backup failed: {ex.Message}", "Error"); }
            finally { ClearBusy(); }
        }

        private void LoadPrinters()
        {
            AvailablePrinters.Clear();
            AvailablePrinters.Add("(Default)");
            foreach (var p in _main.Printer.GetAvailablePrinters())
                AvailablePrinters.Add(p);
        }
    }
}
