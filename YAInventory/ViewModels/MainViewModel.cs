using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YAInventory.Helpers;
using YAInventory.Models;
using YAInventory.Services;

namespace YAInventory.ViewModels
{
    /// <summary>
    /// Root ViewModel — owns navigation, settings, sync status, and the welcome popup.
    /// DataContext of MainWindow.
    /// </summary>
    public class MainViewModel : BaseViewModel
    {
        // ── Services (shared singletons) — must be PROPERTIES (not fields)
        // WPF data binding uses TypeDescriptor.GetProperties() which skips fields.
        public LocalStorageService Storage { get; }
        public MongoDbService      Mongo   { get; }
        public SyncService         Sync    { get; }
        public PrintService        Printer { get; }
        public NavigationService   Nav     { get; }

        // ── State ──────────────────────────────────────────────────────────
        private AppSettings _settings = new();
        public  AppSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public SyncStatus SyncStatus { get; } = new();

        // ── Notification popup ─────────────────────────────────────────────
        private bool   _showNotification;
        private string _notificationMessage = string.Empty;
        private string _notificationType    = "Info";   // Info | Success | Warning | Error

        public bool ShowNotification
        {
            get => _showNotification;
            set => SetProperty(ref _showNotification, value);
        }
        public string NotificationMessage
        {
            get => _notificationMessage;
            set => SetProperty(ref _notificationMessage, value);
        }
        public string NotificationType
        {
            get => _notificationType;
            set => SetProperty(ref _notificationType, value);
        }

        // ── Welcome splash ─────────────────────────────────────────────────
        private bool _showWelcome = true;
        public bool ShowWelcome
        {
            get => _showWelcome;
            set => SetProperty(ref _showWelcome, value);
        }

        // ── Window controls ────────────────────────────────────────────────
        public ICommand MinimizeCommand    { get; }
        public ICommand MaximizeCommand    { get; }
        public ICommand CloseCommand       { get; }
        public ICommand NavigateCommand    { get; }
        public ICommand DismissNotification{ get; }

        // ── Current page label ─────────────────────────────────────────────
        public string CurrentPage => Nav.CurrentPage;

        public MainViewModel()
        {
            Storage = new LocalStorageService();
            Mongo   = new MongoDbService();
            Printer = new PrintService();
            Nav     = new NavigationService();
            Sync    = new SyncService(Storage, Mongo, SyncStatus);

            MinimizeCommand     = new RelayCommand(_ => Application.Current.MainWindow.WindowState = WindowState.Minimized);
            MaximizeCommand     = new RelayCommand(_ => ToggleMaximize());
            CloseCommand        = new RelayCommand(_ => Application.Current.Shutdown());
            NavigateCommand     = new RelayCommand(page => Navigate(page?.ToString() ?? "Dashboard"));
            DismissNotification = new RelayCommand(_ => ShowNotification = false);

            Nav.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NavigationService.CurrentPage))
                    OnPropertyChanged(nameof(CurrentPage));
            };
        }

        // ── Initialisation (called from App.xaml.cs) ───────────────────────
        public async Task InitialiseAsync()
        {
            SetBusy("Initialising…");
            try
            {
                Storage.EnsureFolderStructure();
                Settings = Storage.LoadSettings();

                // Register pages
                Nav.Register("Dashboard",       () => new DashboardViewModel(this));
                Nav.Register("Inventory",        () => new InventoryViewModel(this));
                Nav.Register("Billing",          () => new BillingViewModel(this));
                Nav.Register("Reports",          () => new ReportsViewModel(this));
                Nav.Register("ReceiptHistory",   () => new ReceiptHistoryViewModel(this));
                Nav.Register("Settings",         () => new SettingsViewModel(this));

                Nav.NavigateTo("Dashboard");

                // Start sync if configured
                if (!string.IsNullOrWhiteSpace(Settings.MongoConnectionString))
                    Sync.Start(Settings, Settings.SyncIntervalSeconds);

                // Welcome splash auto-dismiss after 3 s
                await Task.Delay(3000);
                ShowWelcome = false;

                Notify($"Welcome to {Settings.ShopName}!", "Info");
            }
            finally { ClearBusy(); }
        }

        // ── Navigation ─────────────────────────────────────────────────────
        public void Navigate(string page) => Nav.NavigateTo(page);

        // ── Notifications ──────────────────────────────────────────────────
        public async void Notify(string message, string type = "Info", int autoHideMs = 3000)
        {
            NotificationMessage = message;
            NotificationType    = type;
            ShowNotification    = true;

            if (autoHideMs > 0)
            {
                await Task.Delay(autoHideMs);
                ShowNotification = false;
            }
        }

        // ── Window helpers ─────────────────────────────────────────────────
        private static void ToggleMaximize()
        {
            var w = Application.Current.MainWindow;
            w.WindowState = w.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}
