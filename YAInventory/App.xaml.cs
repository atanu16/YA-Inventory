using System;
using System.Windows;
using System.Windows.Threading;

namespace YAInventory
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global unhandled-exception handler — prevents silent crashes
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        }

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n" +
                "The application will continue running.",
                "YA Inventory — Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;   // keep app alive
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                MessageBox.Show($"Fatal error: {ex.Message}", "YA Inventory", MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Stop sync service cleanly on exit
            if (Current.MainWindow?.DataContext is ViewModels.MainViewModel vm)
                vm.Sync.Stop();

            base.OnExit(e);
        }
    }
}
