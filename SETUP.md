# YA Inventory Management — Setup & Usage Guide

## Quick Start

### Prerequisites
- .NET 8 SDK (https://dotnet.microsoft.com/download)
- Visual Studio 2022 (or VS Code with C# extension)

### Build & Run
```bash
# Open terminal in: C:\Projects\YA Inventory\
dotnet build YAInventory/YAInventory.csproj
dotnet run --project YAInventory/YAInventory.csproj
```

Or open `YAInventory.sln` in Visual Studio and press **F5**.

---

## Folder Structure

```
YA Inventory/
├── YAInventory.sln
└── YAInventory/
    ├── YAInventory.csproj
    ├── App.xaml / .cs            ← Application startup
    ├── MainWindow.xaml / .cs     ← Shell window (sidebar + content)
    ├── Models/
    │   ├── Product.cs            ← Inventory item
    │   ├── CartItem.cs           ← POS cart item
    │   ├── Sale.cs / SaleItem.cs ← Completed transaction
    │   ├── AppSettings.cs        ← JSON config model
    │   └── SyncStatus.cs        ← Observable sync state
    ├── ViewModels/
    │   ├── BaseViewModel.cs      ← INotifyPropertyChanged base
    │   ├── MainViewModel.cs      ← Root: navigation + services
    │   ├── DashboardViewModel.cs ← KPIs and charts
    │   ├── InventoryViewModel.cs ← CRUD + barcode scanner
    │   ├── BillingViewModel.cs   ← POS + checkout
    │   ├── ReportsViewModel.cs   ← Analytics + export
    │   └── SettingsViewModel.cs  ← Config + MongoDB test
    ├── Views/
    │   ├── DashboardView.xaml    ← KPI cards + chart
    │   ├── InventoryView.xaml    ← DataGrid + add/edit dialog
    │   ├── BillingView.xaml      ← Scan + cart + totals
    │   ├── ReportsView.xaml      ← Date range + tables
    │   └── SettingsView.xaml     ← All settings panels
    ├── Services/
    │   ├── LocalStorageService.cs ← CSV + JSON file I/O
    │   ├── MongoDbService.cs      ← MongoDB Atlas CRUD
    │   ├── SyncService.cs         ← Background bidirectional sync
    │   ├── PrintService.cs        ← GDI + ESC/POS thermal print
    │   └── NavigationService.cs   ← Page routing
    ├── Helpers/
    │   ├── RelayCommand.cs        ← ICommand
    │   └── AsyncRelayCommand.cs   ← async ICommand
    ├── Converters/
    │   ├── BoolToVisibilityConverter.cs
    │   ├── StockStatusToColorConverter.cs
    │   ├── NumberFormatConverter.cs
    │   ├── StringEqualsConverter.cs
    │   └── BarHeightConverter.cs
    └── Resources/
        ├── Colors.xaml
        └── Styles.xaml
```

---

## Local Data Storage

On first launch the app creates:
```
C:\YA Inventory Management\
├── config.json       ← Shop name, MongoDB URI, settings
├── products.csv      ← Inventory catalogue
├── sales.csv         ← Transaction history
└── images\           ← Logos, product images
```

**No internet required.** All operations write local first.

---

## Cloud Sync (MongoDB Atlas — Optional)

1. Create a free cluster at https://cloud.mongodb.com
2. Create a user and get your connection string:
   `mongodb+srv://<user>:<pass>@cluster0.xxxxx.mongodb.net/`
3. Open **Settings → Cloud Sync** and paste the string
4. Click **Test Connection** → should say "Connected successfully!"
5. Click **Save Settings**
6. The app will auto-sync every 30 seconds (configurable)

### Sync Logic
```
WRITE  → Local CSV  (always, even offline)
PUSH   → MongoDB    (if internet available, on timer)
PULL   → Local CSV  (merge: latest UpdatedAt wins)
```
No data is ever permanently deleted (soft deletes only).

---

## Thermal Printer Setup

1. Connect your 80mm thermal printer via USB
2. Install the printer driver (Windows should auto-detect)
3. Go to **Settings → Thermal Printer** and select the printer from the dropdown
4. Click **Save Settings**
5. Receipts will print automatically at checkout

For direct ESC/POS (no driver): use `PrintService.BuildEscPosReceipt()` and send raw bytes via `SerialPort`.

---

## Barcode Scanner

Plug in any USB HID barcode scanner. It works as a keyboard:
- **Inventory page**: scan in the "Scan barcode…" box — finds product or opens Add dialog
- **Billing page**: scan in the "BARCODE SCANNER" box — adds to cart
- The scanner typically sends the code followed by Enter (CR/LF) — this is auto-handled

---

## Advanced Features

| Feature | Location |
|---------|----------|
| Low stock alerts | Dashboard → Alerts card + Inventory filter |
| Daily/monthly reports | Reports page |
| Export to Excel | Reports page buttons / Inventory toolbar |
| Backup & Restore | Settings → Data Management |
| Default discount | Settings → Pricing & Discount |
| Per-product discount | Inventory → Edit product |
| Cart-level discount | Billing → Order Summary panel |

---

## NuGet Packages Used

| Package | Purpose |
|---------|---------|
| MongoDB.Driver 2.27 | Atlas cloud sync |
| CsvHelper 33 | Local inventory CSV |
| ClosedXML 0.102 | Excel export |
| Newtonsoft.Json 13 | Settings JSON |
| System.Drawing.Common 8 | GDI print |
| Microsoft.Extensions.Http 8 | Connectivity check |
