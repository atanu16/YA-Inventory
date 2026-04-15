using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;
using YAInventory.Models;

namespace YAInventory.Helpers
{
    /// <summary>Exports inventory and sales data to .xlsx files.</summary>
    public static class ExcelExportHelper
    {
        public static void ExportProducts(IEnumerable<Product> products, string filePath)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Inventory");

            // Header row
            var headers = new[] { "Product ID", "Name", "Barcode", "Price", "Quantity", "Category", "Discount %", "Stock Status", "Last Updated" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#6366F1");
                cell.Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var p in products)
            {
                ws.Cell(row, 1).Value = p.ProductId;
                ws.Cell(row, 2).Value = p.Name;
                ws.Cell(row, 3).Value = p.Barcode;
                ws.Cell(row, 4).Value = (double)p.Price;
                ws.Cell(row, 5).Value = p.Quantity;
                ws.Cell(row, 6).Value = p.Category;
                ws.Cell(row, 7).Value = (double)p.DefaultDiscount;
                ws.Cell(row, 8).Value = p.StockStatusLabel;
                ws.Cell(row, 9).Value = p.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                // Colour-code stock status
                var statusColor = p.StockStatus switch
                {
                    StockStatus.InStock    => XLColor.FromHtml("#D1FAE5"),
                    StockStatus.LowStock   => XLColor.FromHtml("#FEF3C7"),
                    StockStatus.OutOfStock => XLColor.FromHtml("#FEE2E2"),
                    _                      => XLColor.White
                };
                ws.Row(row).Style.Fill.BackgroundColor = statusColor;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
        }

        public static void ExportSales(IEnumerable<Sale> sales, string filePath)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Sales");

            var headers = new[] { "Invoice ID", "Date", "Subtotal", "Discount", "Tax", "Total", "Payment", "Items" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#6366F1");
                cell.Style.Font.FontColor = XLColor.White;
            }

            int row = 2;
            foreach (var s in sales)
            {
                ws.Cell(row, 1).Value = s.SaleId;
                ws.Cell(row, 2).Value = s.SaleDate.ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row, 3).Value = (double)s.Subtotal;
                ws.Cell(row, 4).Value = (double)s.Discount;
                ws.Cell(row, 5).Value = (double)s.TaxAmount;
                ws.Cell(row, 6).Value = (double)s.Total;
                ws.Cell(row, 7).Value = s.PaymentMethod;
                ws.Cell(row, 8).Value = s.Items.Count;
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(filePath);
        }
    }
}
