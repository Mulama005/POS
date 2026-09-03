using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Manager,Admin")]
public class ReportsController : ControllerBase
{
    private readonly PosDbContext _context;

    public ReportsController(PosDbContext context)
    {
        _context = context;
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
}