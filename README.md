<div align="center">
  <img src="YAInventory/YA.png" alt="YA Inventory Logo" width="120" />
</div>

# YA Inventory POS

A modern, high-performance Point of Sale (POS) and Inventory Management system built with **.NET 8 (WPF)**. Designed with a stunning dark-themed glassmorphism UI, YA Inventory POS offers a seamless, distraction-free environment for managing products, processing sales, and reviewing historical receipts.

## ✨ Features

- **Modern UI/UX**: Premium dark theme featuring smooth transitions, glassmorphism aesthetics, rounded data grids, and responsive layouts.
- **Billing & POS**: Complete Point of Sale interface with barcode scanning support, real-time discounting, flexible tax calculation, and direct thermal receipt printing.
- **Inventory Management**: Track stock levels, categorize products, and receive low-stock alerts. Rapidly add, edit, or delete items.
- **Receipt History**: Powerful search and filter functions to review past transactions. View full line-item details, filter by date ranges, and effortlessly reprint historical sales.
- **Dashboard Analytics**: Visualise your daily sales volume, monitor recent transactions, and investigate critical low-stock warnings at a single glance.
- **Data Persistence**: Offline-first architecture using rapid local CSV storage, combined with robust background synchronization to **MongoDB Atlas Cloud** for enterprise reliability and backups.
- **Hardware Integrations**: Natively integrates with standard POS thermal ESC/POS printers to instantly generate professional customer receipts.

## 🚀 Tech Stack

- **Framework**: .NET 8.0 / C#
- **UI Architecture**: Windows Presentation Foundation (WPF) 
- **Pattern**: Strict MVVM (Model-View-ViewModel)
- **Database**: Local CSV (`CsvHelper`) + Cloud NoSQL (`MongoDB.Driver`)

## 📦 Installation & Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/ya-inventory.git
   ```
2. **Open the solution:**
   Open `YAInventory.sln` using Visual Studio 2022 (or higher).
3. **Build the project:**
   Restore NuGet packages and build the project targeting `.NET 8.0-windows`.
4. **Run the application:**
   Press `F5` or `Ctrl+F5` to start the app. 

> **Note:** The application operates in isolated data folders. Asset files and data stores like `products.csv` and `sales.csv` are automatically provisioned under `C:\YA Inventory Management` upon first launch, requiring zero manual database installation to get started!

## ⚙️ Configuration

Operational application settings can be configured directly inside the **Settings** view in the application's runtime interface. This includes:
- Shop Name and Business details
- Target Thermal Printer Name string
- Default Tax and Discount percentages
- Cloud Synchronization (MongoDB Connection Strings)

## 🤝 Contributing

Contributions, issues, and feature requests are highly welcome! Feel free to check the [issues page](https://github.com/yourusername/ya-inventory/issues) if you want to contribute.

## 📄 License

This project is licensed under the MIT License.
