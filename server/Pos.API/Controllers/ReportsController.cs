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

    [HttpGet("sales")]
    public async Task<IActionResult> SalesReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        // Total sales
        var sales = await _context.Sales
            .Where(s => s.SaleDate.Date >= from.Date && s.SaleDate.Date <= to.Date && s.Status == SaleStatus.Completed)
            .ToListAsync();

        var totalSales = sales.Sum(s => s.Total);
        var totalOrders = sales.Count;
        var avgOrder = totalOrders > 0 ? totalSales / totalOrders : 0;

        // Top selling products
        var topProducts = await _context.SaleItems
            .Where(si => si.Sale.SaleDate.Date >= from.Date && si.Sale.SaleDate.Date <= to.Date)
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

        // Daily trend (last 30 days)
        var trend = Enumerable.Range(0, (to - from).Days + 1)
            .Select(i => from.Date.AddDays(i))
            .Select(d => new
            {
                Date = d,
                Total = sales.Where(s => s.SaleDate.Date == d).Sum(s => s.Total),
                Orders = sales.Count(s => s.SaleDate.Date == d)
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
        // Inventory valuation
        var products = await _context.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Name,
                p.Sku,
                Stock = p.StockUnits.Count(u => u.Status == "InStock"),
                CostPrice = p.CostPrice,
                SalePrice = p.SalePrice,
                p.ReorderThreshold,
                p.WarrantyMonths,
                Units = p.StockUnits.Select(u => new { u.SerialNumber, u.Status, u.SaleDate })
            })
            .ToListAsync();

        // Low stock items
        var lowStock = products
            .Where(p => p.Stock <= p.ReorderThreshold)
            .ToList();

        // Warranty status distribution
        var warrantyStatus = products
            .SelectMany(p => p.Units.Where(u => u.Status == "Sold" && u.SaleDate.HasValue))
            .GroupBy(u =>
            {
                var expiry = u.SaleDate.Value.AddMonths(products.First(p => p.Units.Contains(u)).WarrantyMonths);
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
    public async Task<IActionResult> FinancialReport()
    {
        // Revenue
        var completedSales = await _context.Sales
            .Where(s => s.Status == SaleStatus.Completed)
            .ToListAsync();

        var totalRevenue = completedSales.Sum(s => s.Total);
        var totalTax = completedSales.Sum(s => s.TaxTotal);
        var totalDiscounts = completedSales.Sum(s => s.DiscountTotal);

        // Credit ledger – outstanding balances
        var outstandingCredit = await _context.Customers
            .SumAsync(c => c.CurrentCreditBalance);

        return Ok(new
        {
            totalRevenue,
            totalTax,
            totalDiscounts,
            outstandingCredit,
            netRevenue = totalRevenue - totalTax
        });
    }

    [HttpGet("staff")]
    public async Task<IActionResult> StaffReport([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        var staffPerformance = await _context.Sales
            .Where(s => s.SaleDate.Date >= from.Date && s.SaleDate.Date <= to.Date && s.Status == SaleStatus.Completed)
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
    
    // ── Export endpoints ──────────────────────────────────────────────────────

     [HttpGet("export/sales")]
    public async Task<IActionResult> ExportSalesReport(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        await LogExportAsync("Sales", from, to, format);

        var data = await _context.Sales
            .Where(s => s.SaleDate.Date >= from.Date && s.SaleDate.Date <= to.Date && s.Status == SaleStatus.Completed)
            .Select(s => new
            {
                s.Id,
                s.SaleDate,
                s.Total,
                s.Subtotal,
                s.DiscountTotal,
                s.TaxTotal,
                CashierName = s.Cashier.FullName,
                RegisterName = s.Register.Name,
                CustomerName = s.Customer == null ? "Walk-in" : s.Customer.FullName
            })
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        return ExportData(data, format, $"Sales_Report_{from:yyyyMMdd}_{to:yyyyMMdd}");
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
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        await LogExportAsync("Staff", from, to, format);

        var data = await _context.Sales
            .Where(s => s.SaleDate.Date >= from.Date && s.SaleDate.Date <= to.Date && s.Status == SaleStatus.Completed)
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

        return ExportData(data, format, $"Staff_Report_{from:yyyyMMdd}_{to:yyyyMMdd}");
    }
    
    [HttpGet("export/financial")]
    public async Task<IActionResult> ExportFinancialReport(
        [FromQuery] string format = "csv")
    {
        // 🔐 Audit log
        await LogExportAsync("Financial", null, null, format);

        var completedSales = await _context.Sales
            .Where(s => s.Status == SaleStatus.Completed)
            .ToListAsync();

        var totalRevenue = completedSales.Sum(s => s.Total);
        var totalTax = completedSales.Sum(s => s.TaxTotal);
        var totalDiscounts = completedSales.Sum(s => s.DiscountTotal);
        var outstandingCredit = await _context.Customers.SumAsync(c => c.CurrentCreditBalance);

        // Create a single-row data source for the report
        var data = new[]
        {
            new
            {
                Metric = "Total Revenue",
                Value = totalRevenue
            },
            new
            {
                Metric = "Total Tax",
                Value = totalTax
            },
            new
            {
                Metric = "Total Discounts",
                Value = totalDiscounts
            },
            new
            {
                Metric = "Outstanding Credit",
                Value = outstandingCredit
            },
            new
            {
                Metric = "Net Revenue",
                Value = totalRevenue - totalTax
            }
        };

        return ExportData(data, format, $"Financial_Report_{DateTime.Now:yyyyMMdd}");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task LogExportAsync(string reportType, DateTime? from, DateTime? to, string format)
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
            details: details
        );
    }

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

                        // Define columns (one per property)
                        table.ColumnsDefinition(columns =>
                        {
                            foreach (var _ in props)
                                columns.RelativeColumn();
                        });

                        // Header row
                        table.Header(header =>
                        {
                            foreach (var prop in props)
                            {
                                // Apply styling to the cell container, not the text
                                header.Cell()
                                    .Background(Colors.Grey.Lighten3)
                                    .Padding(4)
                                    .Border(1)
                                    .Text(prop.Name)
                                    .SemiBold();
                            }
                        });

                        // Data rows
                        foreach (var item in data)
                        {
                            foreach (var prop in props)
                            {
                                var value = prop.GetValue(item)?.ToString() ?? "";
                                // Apply styling to the cell container
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
}