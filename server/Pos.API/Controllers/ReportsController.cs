using System.Security.Claims;
using CsvHelper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;
using System.Globalization;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Manager,Admin")]
public class ReportsController : ControllerBase
{
    private readonly PosDbContext _context;
    private readonly IAuditService _auditService;

    public ReportsController(IAuditService auditService, PosDbContext context)
    {
        _context = context;
        _auditService = auditService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  READ ENDPOINTS
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("sales")]
    public async Task<IActionResult> SalesReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = DateTime.SpecifyKind(
            (fromDate ?? DateTime.UtcNow.AddDays(-30)).Date,
            DateTimeKind.Utc);

        var toExclusive = DateTime.SpecifyKind(
            (toDate ?? DateTime.UtcNow).Date.AddDays(1),
            DateTimeKind.Utc);

        // Total sales
        var sales = await _context.Sales
            .Where(s => s.SaleDate >= from
                     && s.SaleDate < toExclusive
                     && s.Status == SaleStatus.Completed)
            .ToListAsync();

        var totalSales = sales.Sum(s => s.Total);
        var totalOrders = sales.Count;
        var avgOrder = totalOrders > 0 ? totalSales / totalOrders : 0;

        // Top selling products
        var topProducts = await _context.SaleItems
            .Where(si => si.Sale.SaleDate >= from
                      && si.Sale.SaleDate < toExclusive)
            .GroupBy(si => si.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                ProductName = g.First().Product.Name,
                TotalSold = g.Sum(si => si.Quantity),
                TotalRevenue = g.Sum(si => si.LineTotal)
            })
            .OrderByDescending(g => g.TotalSold)
            .Take(10)
            .ToListAsync();

        // Daily trend — group once, then project the range
        var salesByDay = sales
            .GroupBy(s => s.SaleDate.Date)
            .ToDictionary(
                g => g.Key,
                g => new { Total = g.Sum(s => s.Total), Orders = g.Count() });

        var lastDay = toExclusive.AddDays(-1).Date;

        var trend = Enumerable
            .Range(0, (lastDay - from.Date).Days + 1)
            .Select(i => from.Date.AddDays(i))
            .Select(d => new
            {
                Date = d,
                Total = salesByDay.TryGetValue(d, out var v) ? v.Total : 0m,
                Orders = salesByDay.TryGetValue(d, out var v2) ? v2.Orders : 0
            })
            .ToList();

        return Ok(new
        {
            totalSales,
            totalOrders,
            avgOrderValue = avgOrder,
            topProducts,
            trend
        });
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> InventoryReport()
    {
        var products = await _context.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Name,
                p.Sku,
                Stock = p.StockUnits.Count(u => u.Status == "InStock"),
                p.CostPrice,
                p.SalePrice,
                p.ReorderThreshold,
                p.WarrantyMonths,
                Units = p.StockUnits.Select(u => new { u.SerialNumber, u.Status, u.SaleDate })
            })
            .ToListAsync();

        var lowStock = products
            .Where(p => p.Stock <= p.ReorderThreshold)
            .ToList();

        var warrantyStatus = products
            .SelectMany(p => p.Units
                .Where(u => u.Status == "Sold" && u.SaleDate.HasValue)
                .Select(u => new { u.SaleDate, p.WarrantyMonths }))
            .GroupBy(u =>
            {
                var expiry = u.SaleDate!.Value.AddMonths(u.WarrantyMonths);
                if (expiry < DateTime.UtcNow) return "Expired";
                if (expiry < DateTime.UtcNow.AddMonths(3)) return "Expiring Soon";
                return "Under Warranty";
            })
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToList();

        return Ok(new
        {
            totalItems = products.Sum(p => p.Stock),
            totalValue = products.Sum(p => p.Stock * p.CostPrice),
            lowStock,
            warrantyStatus
        });
    }

    [HttpGet("financial")]
    public async Task<IActionResult> FinancialReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = DateTime.SpecifyKind(
            (fromDate ?? DateTime.UtcNow.AddDays(-30)).Date,
            DateTimeKind.Utc);

        var toExclusive = DateTime.SpecifyKind(
            (toDate ?? DateTime.UtcNow).Date.AddDays(1),
            DateTimeKind.Utc);

        var completedSales = await _context.Sales
            .Where(s => s.SaleDate >= from
                     && s.SaleDate < toExclusive
                     && s.Status == SaleStatus.Completed)
            .ToListAsync();

        var totalRevenue = completedSales.Sum(s => s.Total);
        var totalTax = completedSales.Sum(s => s.TaxTotal);
        var totalDiscounts = completedSales.Sum(s => s.DiscountTotal);

        // NOTE: this must stay identical to the export calculation below.
        // Verify against your data model: if Sale.Total is post-discount,
        // remove the discount subtraction from both places.
        var netRevenue = totalRevenue - totalDiscounts - totalTax;

        var outstandingCredit = await _context.Customers
            .SumAsync(c => c.CurrentCreditBalance);

        return Ok(new
        {
            totalRevenue,
            totalTax,
            totalDiscounts,
            outstandingCredit,
            netRevenue
        });
    }

    [HttpGet("staff")]
    public async Task<IActionResult> StaffReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = DateTime.SpecifyKind(
            (fromDate ?? DateTime.UtcNow.AddDays(-30)).Date,
            DateTimeKind.Utc);

        var toExclusive = DateTime.SpecifyKind(
            (toDate ?? DateTime.UtcNow).Date.AddDays(1),
            DateTimeKind.Utc);

        var staffPerformance = await _context.Sales
            .Where(s => s.SaleDate >= from
                     && s.SaleDate < toExclusive
                     && s.Status == SaleStatus.Completed)
            .GroupBy(s => s.CashierId)
            .Select(g => new
            {
                CashierId = g.Key,
                CashierName = g.First().Cashier.FullName,
                TotalSales = g.Sum(s => s.Total),
                TotalOrders = g.Count(),
                AvgOrder = g.Average(s => s.Total),
                TotalItems = g.SelectMany(s => s.Items).Sum(si => si.Quantity)
            })
            .OrderByDescending(g => g.TotalSales)
            .ToListAsync();

        return Ok(staffPerformance);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  EXPORT ENDPOINTS
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("export/sales")]
    public async Task<IActionResult> ExportSalesReport(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = DateTime.SpecifyKind(
            (fromDate ?? DateTime.UtcNow.AddDays(-30)).Date,
            DateTimeKind.Utc);

        var toExclusive = DateTime.SpecifyKind(
            (toDate ?? DateTime.UtcNow).Date.AddDays(1),
            DateTimeKind.Utc);

        var displayTo = toExclusive.AddDays(-1);

        await LogExportAsync("Sales", from, displayTo, format);

        // 1. Load sales with line items, cashier, register
        var sales = await _context.Sales
            .Where(s => s.SaleDate >= from
                     && s.SaleDate < toExclusive
                     && s.Status == SaleStatus.Completed)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Cashier)
            .Include(s => s.Register)
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        // 2. Load payments for those sales
        var saleIds = sales.Select(s => s.Id).ToList();
        var payments = await _context.Payments
            .Where(p => saleIds.Contains(p.SaleId))
            .ToListAsync();

        var paymentBySale = payments
            .GroupBy(p => p.SaleId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.ProcessedAt).First());

        // 3. Flatten to one row per line item
        var rows = new List<SalesReportRow>();
        foreach (var sale in sales)
        {
            paymentBySale.TryGetValue(sale.Id, out var payment);

            foreach (var item in sale.Items)
            {
                rows.Add(new SalesReportRow
                {
                    SaleDate = sale.SaleDate,
                    ProductDescription = item.Product?.Name ?? "(deleted product)",
                    Quantity = item.Quantity,
                    Subtotal = item.LineTotal - item.TaxAmount,
                    Tax = item.TaxAmount,
                    Total = item.LineTotal,
                    PayMode = FormatPayMode(payment?.Method),
                    MpesaRef = payment?.ExternalReference,
                    MpesaPhone = MaskPhone(payment?.MpesaPhoneNumber),
                    EtimsCuNumber = sale.EtimsInvoiceNumber,
                    EtimsStatus = sale.IsSynced ? "Success" : "Pending",
                    CashierName = sale.Cashier?.FullName ?? "—"
                });
            }
        }

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            return ExportSalesCsv(rows, from, displayTo);

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return ExportSalesPdf(rows, from, displayTo);

        return BadRequest("Unsupported format. Use 'csv' or 'pdf'.");
    }

    [HttpGet("export/inventory")]
    public async Task<IActionResult> ExportInventoryReport(
        [FromQuery] string format = "csv")
    {
        await LogExportAsync("Inventory", null, null, format);

        var data = await _context.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Name,
                p.Sku,
                p.Barcode,
                CategoryName = p.Category.Name,
                Stock = p.StockUnits.Count(u => u.Status == "InStock"),
                p.CostPrice,
                p.SalePrice,
                p.ReorderThreshold,
                p.WarrantyMonths,
                TotalValue = p.StockUnits.Count(u => u.Status == "InStock") * p.CostPrice
            })
            .OrderBy(p => p.Name)
            .ToListAsync();

        return ExportData(data, format, $"Inventory_Report_{DateTime.Now:yyyyMMdd}");
    }

    [HttpGet("export/staff")]
    public async Task<IActionResult> ExportStaffReport(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = DateTime.SpecifyKind(
            (fromDate ?? DateTime.UtcNow.AddDays(-30)).Date,
            DateTimeKind.Utc);

        var toExclusive = DateTime.SpecifyKind(
            (toDate ?? DateTime.UtcNow).Date.AddDays(1),
            DateTimeKind.Utc);

        var displayTo = toExclusive.AddDays(-1);

        await LogExportAsync("Staff", from, displayTo, format);

        var data = await _context.Sales
            .Where(s => s.SaleDate >= from
                     && s.SaleDate < toExclusive
                     && s.Status == SaleStatus.Completed)
            .GroupBy(s => s.CashierId)
            .Select(g => new
            {
                CashierName = g.First().Cashier.FullName,
                TotalSales = g.Sum(s => s.Total),
                TotalOrders = g.Count(),
                AvgOrder = g.Average(s => s.Total),
                TotalItems = g.SelectMany(s => s.Items).Sum(si => si.Quantity)
            })
            .OrderByDescending(x => x.TotalSales)
            .ToListAsync();

        return ExportData(data, format, $"Staff_Report_{from:yyyyMMdd}_{displayTo:yyyyMMdd}");
    }

    [HttpGet("export/financial")]
    public async Task<IActionResult> ExportFinancialReport(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = DateTime.SpecifyKind(
            (fromDate ?? DateTime.UtcNow.AddDays(-30)).Date,
            DateTimeKind.Utc);

        var toExclusive = DateTime.SpecifyKind(
            (toDate ?? DateTime.UtcNow).Date.AddDays(1),
            DateTimeKind.Utc);

        var displayTo = toExclusive.AddDays(-1);

        await LogExportAsync("Financial", from, displayTo, format);

        var completedSales = await _context.Sales
            .Where(s => s.SaleDate >= from
                     && s.SaleDate < toExclusive
                     && s.Status == SaleStatus.Completed)
            .ToListAsync();

        var totalRevenue = completedSales.Sum(s => s.Total);
        var totalTax = completedSales.Sum(s => s.TaxTotal);
        var totalDiscounts = completedSales.Sum(s => s.DiscountTotal);

        // Keep identical to /financial GET above.
        var netRevenue = totalRevenue - totalDiscounts - totalTax;

        var outstandingCredit = await _context.Customers
            .SumAsync(c => c.CurrentCreditBalance);

        var dto = new FinancialReportDto
        {
            PeriodFrom = from,
            PeriodTo = displayTo,
            TotalRevenue = totalRevenue,
            TotalTax = totalTax,
            TotalDiscounts = totalDiscounts,
            NetRevenue = netRevenue,
            OutstandingCredit = outstandingCredit
        };

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            return ExportFinancialCsv(dto);

        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            return ExportFinancialPdf(dto);

        return BadRequest("Unsupported format. Use 'csv' or 'pdf'.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AUDIT HELPER
    // ═══════════════════════════════════════════════════════════════════════

    private async Task LogExportAsync(
        string reportType,
        DateTime? from,
        DateTime? to,
        string format)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null) return;

        var details = from.HasValue && to.HasValue
            ? $"Exported {reportType} report from {from:yyyy-MM-dd} to {to:yyyy-MM-dd} as {format.ToUpper()}"
            : $"Exported {reportType} report as {format.ToUpper()}";

        await _auditService.LogAsync(
            userId: Guid.Parse(userIdClaim),
            actionType: "REPORT_EXPORTED",
            entityName: "Report",
            entityId: Guid.Parse(userIdClaim),
            details: details);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  GENERIC EXPORT HELPERS (used by inventory and staff)
    // ═══════════════════════════════════════════════════════════════════════

    private IActionResult ExportData<T>(IEnumerable<T> data, string format, string baseFileName)
    {
        if (format.ToLower() == "csv")
            return ExportCsv(data, $"{baseFileName}.csv");
        else if (format.ToLower() == "pdf")
            return ExportPdf(data, $"{baseFileName}.pdf");
        else
            return BadRequest("Unsupported format. Use 'csv' or 'pdf'.");
    }

    private IActionResult ExportCsv<T>(IEnumerable<T> data, string fileName)
    {
        var memoryStream = new MemoryStream();
        var writer = new StreamWriter(memoryStream);
        var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(data);
        writer.Flush();
        memoryStream.Position = 0;
        return File(memoryStream, "text/csv", fileName);
    }

    private IActionResult ExportPdf<T>(IEnumerable<T> data, string fileName)
    {
        if (!data.Any())
        {
            var emptyDocument = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Content().Text("No data available for this report.");
                });
            });
            var emptyPdfBytes = emptyDocument.GeneratePdf();
            return File(emptyPdfBytes, "application/pdf", fileName);
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header()
                    .Text(fileName.Replace(".pdf", ""))
                    .SemiBold().FontSize(18).FontColor(Colors.Blue.Darken2);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Table(table =>
                    {
                        var props = typeof(T).GetProperties();

                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in props)
                                columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            foreach (var prop in props)
                            {
                                header.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(4)
                                    .Border(1)
                                    .Text(prop.Name)
                                    .SemiBold();
                            }
                        });

                        foreach (var item in data)
                        {
                            foreach (var prop in props)
                            {
                                var value = prop.GetValue(item)?.ToString() ?? "";
                                table.Cell()
                                    .Padding(4)
                                    .Border(1)
                                    .Text(value);
                            }
                        }
                    });

                page.Footer()
                    .AlignRight()
                    .Text(text =>
                    {
                        text.Span("Generated on: ");
                        text.Span($"{DateTime.Now:yyyy-MM-dd HH:mm}").FontColor(Colors.Grey.Medium);
                    });
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", fileName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FINANCIAL PDF (bespoke accountant layout)
    // ═══════════════════════════════════════════════════════════════════════

    private IActionResult ExportFinancialPdf(FinancialReportDto dto)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t
                    .FontFamily("Helvetica")
                    .FontSize(10)
                    .FontColor(Colors.Black));

                page.Header().Element(c => ComposeFinancialHeader(c, dto));
                page.Content().Element(c => ComposeFinancialBody(c, dto));
                page.Footer().Element(ComposeFinancialFooter);
            });
        });

        var pdfBytes = document.GeneratePdf();
        var fileName = $"Financial_Report_{dto.PeriodFrom:yyyyMMdd}_{dto.PeriodTo:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    private void ComposeFinancialHeader(IContainer container, FinancialReportDto dto)
    {
        container.Column(col =>
        {
            col.Item().Text("FINANCIAL REPORT")
                .FontSize(20).Bold();

            col.Item().PaddingTop(4)
                .Text($"Trading Period: {dto.PeriodFrom:dd MMM yyyy} – {dto.PeriodTo:dd MMM yyyy}")
                .FontSize(10)
                .FontColor(Colors.Grey.Darken1);

            col.Item().PaddingTop(14).LineHorizontal(1.5f).LineColor(Colors.Black);
            col.Item().PaddingTop(24);
        });
    }

    private void ComposeFinancialBody(IContainer container, FinancialReportDto dto)
    {
        container.Column(col =>
        {
            ComposeSection(col, "1. REVENUE & TRADING METRICS", new List<FinancialLine>
            {
                new("Gross Register Revenue (Sales Performance Log)",
                    dto.TotalRevenue, LineStyle.Amount),
                new("Less: Total Customer Discounts Offered",
                    dto.TotalDiscounts, LineStyle.Deduct),
                new("Less: VAT Output Tax Obligation (16% statutory band)",
                    dto.TotalTax, LineStyle.Deduct),
                new("NET TRADING REVENUE",
                    dto.NetRevenue, LineStyle.Subtotal),
            });

            col.Item().PaddingTop(28);

            ComposeSection(col, "2. MEMORANDUM — CREDIT POSITION", new List<FinancialLine>
            {
                new("Outstanding Customer Credit (Deni)",
                    dto.OutstandingCredit, LineStyle.Amount),
            });
        });
    }

    private void ComposeSection(ColumnDescriptor col, string title, List<FinancialLine> lines)
    {
        col.Item().PaddingBottom(14)
            .Text(title)
            .FontSize(11).Bold();

        foreach (var line in lines)
        {
            if (line.Style == LineStyle.Subtotal)
            {
                col.Item().PaddingTop(8).PaddingBottom(4)
                    .LineHorizontal(1f).LineColor(Colors.Black);
            }

            col.Item().PaddingVertical(3).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    if (line.Style == LineStyle.Subtotal)
                        t.Span(line.Label).Bold();
                    else
                        t.Span(line.Label);
                });

                row.ConstantItem(130).AlignRight().Text(t =>
                {
                    var display = line.Style == LineStyle.Deduct
                        ? $"({line.Amount:N2})"
                        : $"{line.Amount:N2}";

                    if (line.Style == LineStyle.Subtotal)
                        t.Span(display).Bold();
                    else
                        t.Span(display);
                });
            });
        }

        col.Item().PaddingBottom(16);
    }

    private void ComposeFinancialFooter(IContainer container)
    {
        container.AlignRight().Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1));
            t.Span($"Generated {DateTime.Now:dd MMM yyyy HH:mm} · AyiyaPOS");
        });
    }

    private IActionResult ExportFinancialCsv(FinancialReportDto dto)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Section,Metric,Amount");

        sb.AppendLine($"1. Revenue & Trading Metrics,Gross Register Revenue,{dto.TotalRevenue:F2}");
        sb.AppendLine($",Less: Total Customer Discounts,{dto.TotalDiscounts:F2}");
        sb.AppendLine($",Less: VAT Output Tax (16%),{dto.TotalTax:F2}");
        sb.AppendLine($",NET TRADING REVENUE,{dto.NetRevenue:F2}");
        sb.AppendLine();
        sb.AppendLine($"2. Memorandum — Credit Position,Outstanding Customer Credit,{dto.OutstandingCredit:F2}");

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"Financial_Report_{dto.PeriodFrom:yyyyMMdd}_{dto.PeriodTo:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SALES PDF (bespoke line-item layout)
    // ═══════════════════════════════════════════════════════════════════════

    private enum TextAlign { Left, Right, Center }

    private record SalesColumn(string Title, float Width, bool IsRelative, TextAlign Align);

    private static readonly SalesColumn[] SalesColumns = new[]
    {
        new SalesColumn("Sale Date/Time",      74,   false, TextAlign.Left),
        new SalesColumn("Product Description", 2.5f, true,  TextAlign.Left),
        new SalesColumn("Qty",                 26,   false, TextAlign.Right),
        new SalesColumn("Subtotal",            56,   false, TextAlign.Right),
        new SalesColumn("VAT (16%)",           56,   false, TextAlign.Right),
        new SalesColumn("Total (KES)",         62,   false, TextAlign.Right),
        new SalesColumn("Pay Mode",            54,   false, TextAlign.Left),
        new SalesColumn("M-Pesa Ref",          62,   false, TextAlign.Left),
        new SalesColumn("M-Pesa Phone",        64,   false, TextAlign.Left),
        new SalesColumn("eTIMS CU",            76,   false, TextAlign.Left),
        new SalesColumn("Status",              40,   false, TextAlign.Left),
        new SalesColumn("Cashier",             1.0f, true,  TextAlign.Left),
    };

    private IActionResult ExportSalesPdf(
        List<SalesReportRow> rows, DateTime from, DateTime to)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(t => t
                    .FontFamily("Helvetica")
                    .FontSize(7.5f)
                    .FontColor(Colors.Black));

                page.Header().Element(c => ComposeSalesHeader(c, from, to));
                page.Content().Element(c => ComposeSalesTable(c, rows));
                page.Footer().Element(ComposeSalesFooter);
            });
        });

        var fileName = $"Sales_Report_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return File(document.GeneratePdf(), "application/pdf", fileName);
    }

    private void ComposeSalesHeader(IContainer container, DateTime from, DateTime to)
    {
        container.Column(col =>
        {
            col.Item().Text("Enhanced Retail Sales Report")
                .FontSize(15).Bold();

            col.Item().PaddingTop(4).Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken2));
                t.Span($"Period: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");
                t.Span("   ·   ");
                t.Span($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
            });

            col.Item().PaddingTop(8).LineHorizontal(1f).LineColor(Colors.Black);
            col.Item().PaddingTop(6);
        });
    }

    private void ComposeSalesTable(IContainer container, List<SalesReportRow> rows)
    {
        if (rows.Count == 0)
        {
            container.PaddingTop(40).AlignCenter()
                .Text("No completed sales in this period.")
                .FontSize(10).FontColor(Colors.Grey.Darken1);
            return;
        }

        container.Table(table =>
        {
            // 1. Columns — widths defined once from the shared metadata
            table.ColumnsDefinition(cols =>
            {
                foreach (var c in SalesColumns)
                {
                    if (c.IsRelative) cols.RelativeColumn(c.Width);
                    else              cols.ConstantColumn(c.Width);
                }
            });

            // 2. Header — alignment taken from the same metadata
            table.Header(header =>
            {
                foreach (var c in SalesColumns)
                {
                    var cell = header.Cell()
                        .BorderBottom(1f).BorderColor(Colors.Black)
                        .PaddingVertical(4)
                        .PaddingHorizontal(3);

                    var aligned = c.Align switch
                    {
                        TextAlign.Right  => cell.AlignRight(),
                        TextAlign.Center => cell.AlignCenter(),
                        _                => cell.AlignLeft()
                    };

                    aligned.Text(c.Title).FontSize(7).Bold();
                }
            });

            // 3. Body rows
            foreach (var row in rows)
            {
                var values = new (string Text, TextAlign Align)[]
                {
                    (row.SaleDate.ToString("dd/MM/yyyy HH:mm:ss"), TextAlign.Left),
                    (row.ProductDescription,                        TextAlign.Left),
                    (row.Quantity.ToString(),                       TextAlign.Right),
                    (row.Subtotal.ToString("N2"),                   TextAlign.Right),
                    (row.Tax.ToString("N2"),                        TextAlign.Right),
                    (row.Total.ToString("N2"),                      TextAlign.Right),
                    (row.PayMode       ?? "—",                      TextAlign.Left),
                    (row.MpesaRef      ?? "—",                      TextAlign.Left),
                    (row.MpesaPhone    ?? "—",                      TextAlign.Left),
                    (row.EtimsCuNumber ?? "—",                      TextAlign.Left),
                    (row.EtimsStatus   ?? "—",                      TextAlign.Left),
                    (row.CashierName,                               TextAlign.Left),
                };

                for (int i = 0; i < values.Length; i++)
                {
                    var (text, align) = values[i];
                    var cell = table.Cell()
                        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .PaddingVertical(3)
                        .PaddingHorizontal(3);

                    var aligned = align switch
                    {
                        TextAlign.Right  => cell.AlignRight(),
                        TextAlign.Center => cell.AlignCenter(),
                        _                => cell.AlignLeft()
                    };

                    aligned.Text(text).FontSize(7.5f);
                }
            }

            // 4. Total row
            var totalQty = rows.Sum(r => r.Quantity);
            var totalSubtotal = rows.Sum(r => r.Subtotal);
            var totalTax = rows.Sum(r => r.Tax);
            var totalTotal = rows.Sum(r => r.Total);

            table.Cell().ColumnSpan(2)
                .BorderTop(1.5f).BorderColor(Colors.Black)
                .PaddingTop(8).PaddingBottom(2)
                .Text("Total").Bold().FontSize(8);

            table.Cell().BorderTop(1.5f).BorderColor(Colors.Black)
                .PaddingTop(8).PaddingBottom(2).AlignRight()
                .Text(totalQty.ToString()).Bold().FontSize(8);

            table.Cell().BorderTop(1.5f).BorderColor(Colors.Black)
                .PaddingTop(8).PaddingBottom(2).AlignRight()
                .Text(totalSubtotal.ToString("N2")).Bold().FontSize(8);

            table.Cell().BorderTop(1.5f).BorderColor(Colors.Black)
                .PaddingTop(8).PaddingBottom(2).AlignRight()
                .Text(totalTax.ToString("N2")).Bold().FontSize(8);

            table.Cell().BorderTop(1.5f).BorderColor(Colors.Black)
                .PaddingTop(8).PaddingBottom(2).AlignRight()
                .Text(totalTotal.ToString("N2")).Bold().FontSize(8);

            for (int i = 0; i < 6; i++)
            {
                table.Cell()
                    .BorderTop(1.5f).BorderColor(Colors.Black)
                    .PaddingTop(8).PaddingBottom(2);
            }
        });
    }

    private void ComposeSalesFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span("Generated by AyiyaPOS");
            });

            row.ConstantItem(120).AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });
    }

    private IActionResult ExportSalesCsv(
        List<SalesReportRow> rows, DateTime from, DateTime to)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(
            "Sale Date/Time,Product Description,Qty,Subtotal,VAT (16%),Total (KES)," +
            "Pay Mode,M-Pesa Ref,M-Pesa Phone,KRA eTIMS CU,eTIMS Status,Cashier");

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Escape(row.SaleDate.ToString("dd/MM/yyyy HH:mm:ss")),
                Escape(row.ProductDescription),
                row.Quantity,
                row.Subtotal.ToString("F2", CultureInfo.InvariantCulture),
                row.Tax.ToString("F2", CultureInfo.InvariantCulture),
                row.Total.ToString("F2", CultureInfo.InvariantCulture),
                Escape(row.PayMode ?? ""),
                Escape(row.MpesaRef ?? ""),
                Escape(row.MpesaPhone ?? ""),
                Escape(row.EtimsCuNumber ?? ""),
                Escape(row.EtimsStatus ?? ""),
                Escape(row.CashierName)));
        }

        var fileName = $"Sales_Report_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";
        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", fileName);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FORMATTING HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static string FormatPayMode(PaymentMethod? method)
    {
        return method switch
        {
            PaymentMethod.Cash => "Cash",
            PaymentMethod.Mpesa => "Mpesa",
            PaymentMethod.Card => "Card",
            null => "—",
            _ => method.ToString() ?? "—"
        };
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "—";
        var p = phone.Trim();
        if (p.Length < 8) return p;
        return p[..4] + "****" + p[^3..];
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  DTOs
// ═══════════════════════════════════════════════════════════════════════════

public class SalesReportRow
{
    public DateTime SaleDate { get; set; }
    public string ProductDescription { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? PayMode { get; set; }
    public string? MpesaRef { get; set; }
    public string? MpesaPhone { get; set; }
    public string? EtimsCuNumber { get; set; }
    public string? EtimsStatus { get; set; }
    public string CashierName { get; set; } = "";
}

public class FinancialReportDto
{
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal OutstandingCredit { get; set; }
}

public enum LineStyle
{
    Amount,
    Deduct,
    Subtotal
}

public record FinancialLine(string Label, decimal Amount, LineStyle Style);