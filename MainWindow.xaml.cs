using System.Windows;
using System.Windows.Input;
using YAInventory.ViewModels;

namespace YAInventory
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var vm = new MainViewModel();
            DataContext = vm;

            Loaded += async (_, _) => await vm.InitialiseAsync();

            // Allow window resize by dragging edges (since WindowStyle=None)
            SourceInitialized += (_, _) =>
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var src    = System.Windows.Interop.HwndSource.FromHwnd(handle);
                src?.AddHook(WndProc);
            };
        }

        // Drag-move via top bar / sidebar header
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // Edge-resize hook for borderless window
        private static System.IntPtr WndProc(System.IntPtr hwnd, int msg,
            System.IntPtr wParam, System.IntPtr lParam, ref bool handled)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTBOTTOMRIGHT = 17;

            if (msg == WM_NCHITTEST)
            {
                var pos    = new System.Windows.Point(
                    (lParam.ToInt32() & 0xFFFF),
                    (lParam.ToInt32() >> 16) & 0xFFFF);

                var window = Application.Current.MainWindow;
                if (window == null) return System.IntPtr.Zero;

                double right  = window.Left + window.Width;
                double bottom = window.Top  + window.Height;

                if (pos.X >= right - 16 && pos.Y >= bottom - 16)
                {
                    handled = true;
                    return new System.IntPtr(HTBOTTOMRIGHT);
                }
            }
            return System.IntPtr.Zero;
        }

        // Force a data refresh if the user clicks a nav button that is ALREADY active.
        // (WPF RadioButtons normally don't fire their Command if IsChecked is already true).
        private void NavButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && DataContext is MainViewModel vm)
            {
                var pageName = rb.CommandParameter?.ToString();
                if (!string.IsNullOrWhiteSpace(pageName) && rb.IsChecked == true)
                {
                    vm.Navigate(pageName);
                }
            }
        }
    }
}
