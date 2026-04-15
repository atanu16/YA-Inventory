using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using YAInventory.Models;

namespace YAInventory.Services
{
    /// <summary>
    /// Generates and prints 80mm thermal receipts using Windows GDI printing.
    /// Also generates ESC/POS raw byte sequences for direct serial/USB printers.
    /// </summary>
    public class PrintService
    {
        private const int PaperWidthPx = 560; // ~80mm at 72dpi
        private const int Margin       = 10;

        private AppSettings? _settings;
        private Sale?         _currentSale;
        private int           _printY;
        private Font?         _fontBold;
        private Font?         _fontNormal;
        private Font?         _fontSmall;

        // ── Public API ─────────────────────────────────────────────────────
        public void PrintReceipt(Sale sale, AppSettings settings, string? printerName = null)
        {
            _settings    = settings;
            _currentSale = sale;

            var pd = new PrintDocument();
            pd.DefaultPageSettings.PaperSize = new PaperSize("Custom", PaperWidthPx, 1400);
            pd.DefaultPageSettings.Margins   = new Margins(Margin, Margin, Margin, Margin);

            if (!string.IsNullOrWhiteSpace(printerName))
                pd.PrinterSettings.PrinterName = printerName;

            pd.PrintPage += OnPrintPage;
            pd.Print();
        }

        public List<string> GetAvailablePrinters()
        {
            var printers = new List<string>();
            foreach (string p in PrinterSettings.InstalledPrinters)
                printers.Add(p);
            return printers;
        }

        // ── GDI Print page ─────────────────────────────────────────────────
        private void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            if (_currentSale is null || _settings is null || e.Graphics is null) return;

            var g = e.Graphics;
            int x = Margin;
            int w = PaperWidthPx - Margin * 2;
            _printY = Margin;

            _fontBold   = new Font("Courier New", 9,  FontStyle.Bold);
            _fontNormal = new Font("Courier New", 8,  FontStyle.Regular);
            _fontSmall  = new Font("Courier New", 7,  FontStyle.Regular);

            var black = Brushes.Black;

            // ── Shop header ────────────────────────────────────────────────
            DrawCentered(g, _settings.ShopName, _fontBold, black, w, x);
            if (!string.IsNullOrWhiteSpace(_settings.Address))
                DrawCentered(g, _settings.Address, _fontSmall, black, w, x);
            if (!string.IsNullOrWhiteSpace(_settings.Phone))
                DrawCentered(g, $"Tel: {_settings.Phone}", _fontSmall, black, w, x);

            DrawDivider(g, w, x);

            // ── Invoice info ───────────────────────────────────────────────
            DrawLine(g, $"Invoice : {_currentSale.SaleId}", _fontNormal, black, x);
            DrawLine(g, $"Date    : {_currentSale.SaleDate:dd-MMM-yyyy HH:mm}", _fontNormal, black, x);
            DrawLine(g, $"Payment : {_currentSale.PaymentMethod}", _fontNormal, black, x);

            DrawDivider(g, w, x);

            // ── Column headers ─────────────────────────────────────────────
            var col = BuildItemLine("Item", "Qty", "Price", "Total");
            DrawLine(g, col, _fontBold, black, x);
            DrawDivider(g, w, x);

            // ── Items ──────────────────────────────────────────────────────
            string sym = _settings.CurrencySymbol;
            foreach (var item in _currentSale.Items)
            {
                var line = BuildItemLine(
                    TruncateName(item.Name, 18),
                    item.Quantity.ToString(),
                    $"{sym}{item.UnitPrice:N2}",
                    $"{sym}{item.Total:N2}");

                DrawLine(g, line, _fontNormal, black, x);

                if (item.DiscountPercent > 0 || item.DiscountFlat > 0)
                {
                    string discStr = item.DiscountPercent > 0
                        ? $"  Discount: -{item.DiscountPercent}%"
                        : $"  Discount: -{sym}{item.DiscountFlat:N2}";
                    DrawLine(g, discStr, _fontSmall, black, x);
                }
            }

            DrawDivider(g, w, x);

            // ── Totals ─────────────────────────────────────────────────────
            DrawTwoCol(g, "Subtotal:", $"{sym}{_currentSale.Subtotal:N2}", w, x);
            if (_currentSale.Discount > 0)
                DrawTwoCol(g, $"Discount:", $"-{sym}{_currentSale.Discount:N2}", w, x);
            if (_currentSale.TaxAmount > 0)
                DrawTwoCol(g, $"Tax ({_currentSale.TaxPercent}%):", $"{sym}{_currentSale.TaxAmount:N2}", w, x);

            DrawDivider(g, w, x);
            DrawTwoCol(g, "TOTAL:", $"{sym}{_currentSale.Total:N2}", w, x, _fontBold);
            DrawDivider(g, w, x);

            // ── Footer ─────────────────────────────────────────────────────
            _printY += 4;
            DrawCentered(g, "Thank you for your purchase!", _fontSmall, black, w, x);
            DrawCentered(g, "Powered by YA Inventory", _fontSmall, Brushes.Gray, w, x);

            _fontBold.Dispose();
            _fontNormal.Dispose();
            _fontSmall.Dispose();

            e.HasMorePages = false;
        }

        // ── Drawing helpers ────────────────────────────────────────────────
        private void DrawLine(Graphics g, string text, Font font, Brush brush, int x)
        {
            g.DrawString(text, font, brush, x, _printY);
            _printY += (int)g.MeasureString(text, font).Height + 1;
        }

        private void DrawCentered(Graphics g, string text, Font font, Brush brush, int w, int x)
        {
            var size  = g.MeasureString(text, font);
            float cx  = x + (w - size.Width) / 2f;
            g.DrawString(text, font, brush, cx, _printY);
            _printY += (int)size.Height + 1;
        }

        private void DrawDivider(Graphics g, int w, int x)
        {
            g.DrawLine(Pens.Black, x, _printY, x + w, _printY);
            _printY += 4;
        }

        private void DrawTwoCol(Graphics g, string left, string right, int w, int x, Font? font = null)
        {
            font ??= _fontNormal!;
            g.DrawString(left, font, Brushes.Black, x, _printY);
            var rSize = g.MeasureString(right, font);
            g.DrawString(right, font, Brushes.Black, x + w - rSize.Width, _printY);
            _printY += (int)rSize.Height + 1;
        }

        private static string BuildItemLine(string name, string qty, string price, string total)
        {
            return $"{name,-18}{qty,3} {price,9} {total,9}";
        }

        private static string TruncateName(string name, int max) =>
            name.Length > max ? name[..max] : name;

        // ── ESC/POS raw bytes (for direct USB/serial printers) ─────────────
        public byte[] BuildEscPosReceipt(Sale sale, AppSettings settings)
        {
            var buf = new List<byte>();

            void Add(byte[] b) => buf.AddRange(b);
            void Str(string s) => buf.AddRange(Encoding.ASCII.GetBytes(s));
            void Nl()          => buf.Add(0x0A);
            void Bold(bool on) => Add(on ? new byte[]{0x1B,0x45,0x01} : new byte[]{0x1B,0x45,0x00});
            void Center()      => Add(new byte[]{0x1B,0x61,0x01});
            void Left()        => Add(new byte[]{0x1B,0x61,0x00});
            void BigFont(bool on) => Add(on ? new byte[]{0x1D,0x21,0x11} : new byte[]{0x1D,0x21,0x00});
            void CutPaper()    => Add(new byte[]{0x1D,0x56,0x00});

            // Initialize
            Add(new byte[]{0x1B,0x40});

            Center(); Bold(true); BigFont(true);
            Str(settings.ShopName); Nl();
            BigFont(false); Bold(false);

            if (!string.IsNullOrEmpty(settings.Address)) { Str(settings.Address); Nl(); }
            if (!string.IsNullOrEmpty(settings.Phone))   { Str($"Tel: {settings.Phone}"); Nl(); }

            Str(new string('-', 32)); Nl();
            Left();
            Str($"Invoice: {sale.SaleId}"); Nl();
            Str($"Date   : {sale.SaleDate:dd-MMM-yyyy HH:mm}"); Nl();
            Str(new string('-', 32)); Nl();

            string sym = settings.CurrencySymbol;
            foreach (var item in sale.Items)
            {
                Str($"{TruncateName(item.Name, 18),-18}{item.Quantity,3}"); Nl();
                Str($"  {sym}{item.UnitPrice:N2} x{item.Quantity} = {sym}{item.Total:N2}"); Nl();
            }

            Str(new string('-', 32)); Nl();
            Str($"{"Subtotal:",-16}{sym}{sale.Subtotal,10:N2}"); Nl();
            if (sale.Discount > 0) { Str($"{"Discount:",-16}-{sym}{sale.Discount,9:N2}"); Nl(); }
            if (sale.TaxAmount > 0){ Str($"{"Tax:",-16}{sym}{sale.TaxAmount,10:N2}"); Nl(); }
            Str(new string('=', 32)); Nl();
            Bold(true);
            Str($"{"TOTAL:",-16}{sym}{sale.Total,10:N2}"); Nl();
            Bold(false);
            Str(new string('-', 32)); Nl();

            Center(); Str("Thank you!"); Nl();
            Nl(); Nl(); Nl();
            CutPaper();

            return buf.ToArray();
        }
    }
}
