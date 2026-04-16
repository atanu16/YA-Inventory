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
        // No hardcoded paper width — we derive it from the actual printer's margin bounds at runtime.
        private const int Margin = 8;

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
            // Let the printer's own paper size determine the width; just shrink margins.
            pd.DefaultPageSettings.Margins = new Margins(Margin, Margin, Margin, Margin);

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

            // Derive printable area from the actual printer at runtime — works for
            // any paper size (58 mm, 76 mm, 80 mm, A4, etc.).
            int x = e.MarginBounds.Left;
            int w = e.MarginBounds.Width;
            _printY = e.MarginBounds.Top;

            // Scale fonts proportionally so small paper (58 mm ≈ 164 px) still fits.
            // Reference width is 80 mm ≈ 227 px at 72 dpi; we base sizes on that.
            float scale      = Math.Max(0.75f, Math.Min(1.25f, w / 210f));
            float titlePt    = (float)Math.Round(11f * scale);
            float normalPt   = (float)Math.Round(7.5f * scale);
            float smallPt    = (float)Math.Round(6.5f * scale);

            var fontTitle = new Font("Courier New", titlePt, FontStyle.Bold);
            _fontBold   = new Font("Courier New", normalPt, FontStyle.Bold);
            _fontNormal = new Font("Courier New", normalPt, FontStyle.Regular);
            _fontSmall  = new Font("Courier New", smallPt,  FontStyle.Regular);

            var black = Brushes.Black;

            // ── Shop header ────────────────────────────────────────────────
            DrawCentered(g, _settings.ShopName, fontTitle, black, w, x);
            if (!string.IsNullOrWhiteSpace(_settings.Address))
                DrawCentered(g, _settings.Address, _fontSmall, black, w, x);
            if (!string.IsNullOrWhiteSpace(_settings.Phone))
                DrawCentered(g, $"Tel: {_settings.Phone}", _fontSmall, black, w, x);

            DrawDivider(g, w, x);

            // ── Invoice info ───────────────────────────────────────────────
            DrawLine(g, $"Invoice : {_currentSale.SaleId}", _fontNormal, black, x);
            DrawLine(g, $"Date    : {_currentSale.SaleDate:dd-MMM-yyyy hh:mm tt}", _fontNormal, black, x);
            DrawLine(g, $"Payment : {_currentSale.PaymentMethod}", _fontNormal, black, x);

            DrawDivider(g, w, x);

            // ── Column headers ─────────────────────────────────────────────
            DrawItemRow5(g, "Item", "Qty", "MRP", "Disc MRP", "Total", _fontBold, black, w, x);
            DrawDivider(g, w, x);

            // ── Items ────────────────────────────────────────────────────────
            string sym = _settings.CurrencySymbol;
            decimal totalSavings = 0;

            foreach (var item in _currentSale.Items)
            {
                string discMrpCol = item.UnitDiscount > 0
                    ? $"{sym}{item.UnitPrice:N2}"
                    : "—";

                // Compute max name chars dynamically from available col-1 space
                int nameColPx  = (int)(w * 0.32);
                float charW    = g.MeasureString("W", _fontNormal!).Width;
                int maxChars   = Math.Max(6, (int)(nameColPx / charW));

                DrawItemRow5(g,
                    TruncateName(item.Name, maxChars),
                    item.Quantity.ToString(),
                    $"{sym}{item.OriginalPrice:N2}",
                    discMrpCol,
                    $"{sym}{item.Total:N2}",
                    _fontNormal, black, w, x);

                if (item.UnitDiscount > 0)
                    totalSavings += item.DiscountAmount;
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

            // ── "You Saved" banner (when there are savings) ─────────
            if (totalSavings > 0)
            {
                var fontSaved = new Font("Courier New", 9, FontStyle.Bold);
                DrawCentered(g, $"★ YOU SAVED {sym}{totalSavings:N2}! ★", fontSaved, black, w, x);
                _printY += 2;
                fontSaved.Dispose();
            }

            DrawCentered(g, "Thank you for your purchase!", _fontSmall, black, w, x);
            DrawCentered(g, "Powered by YA Inventory", _fontSmall, Brushes.Gray, w, x);

            fontTitle.Dispose();
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

        // ── 5-column item row drawing ─────────────────────────────────────
        private void DrawItemRow5(Graphics g, string item, string qty, string price, string sale, string total, Font font, Brush brush, int w, int x)
        {
            // Col 1: Item name (Left)
            g.DrawString(item, font, brush, x, _printY);

            // Col 2: Qty (centered at ~33%)
            int c2X = x + (int)(w * 0.33);
            var c2S = g.MeasureString(qty, font);
            g.DrawString(qty, font, brush, c2X - c2S.Width / 2f, _printY);

            // Col 3: Price (right-aligned at ~52%)
            int c3X = x + (int)(w * 0.52);
            var c3S = g.MeasureString(price, font);
            g.DrawString(price, font, brush, c3X - c3S.Width, _printY);

            // Col 4: Sale Price (right-aligned at ~72%)
            int c4X = x + (int)(w * 0.75);
            var c4S = g.MeasureString(sale, font);
            g.DrawString(sale, font, brush, c4X - c4S.Width, _printY);

            // Col 5: Total (right edge)
            var c5S = g.MeasureString(total, font);
            g.DrawString(total, font, brush, x + w - c5S.Width, _printY);

            _printY += (int)Math.Max(c2S.Height, c5S.Height) + 1;
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
            Str($"Date   : {sale.SaleDate:dd-MMM-yyyy hh:mm tt}"); Nl();
            Str(new string('-', 32)); Nl();

            string sym = settings.CurrencySymbol;
            decimal totalSavings = 0;

            foreach (var item in sale.Items)
            {
                Str($"{TruncateName(item.Name, 16),-16} x{item.Quantity}"); Nl();
                if (item.UnitDiscount > 0)
                {
                    Str($"  MRP:{sym}{item.OriginalPrice:N2} Disc:{sym}{item.UnitPrice:N2}"); Nl();
                    totalSavings += item.DiscountAmount;
                }
                else
                    Str($"  MRP:{sym}{item.OriginalPrice:N2}"); Nl();
                Str($"  Total: {sym}{item.Total:N2}"); Nl();
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

            // ── "You Saved" banner ─────────────────
            if (totalSavings > 0)
            {
                Center(); Bold(true);
                Str($"* YOU SAVED {sym}{totalSavings:N2}! *"); Nl();
                Bold(false);
            }

            Center(); Str("Thank you!"); Nl();
            Nl(); Nl(); Nl();
            CutPaper();

            return buf.ToArray();
        }
    }
}
