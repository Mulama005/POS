/*using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/manager")]
[Authorize(Roles = "Manager,Admin")]
public class ManagerController : ControllerBase
{
    private readonly PosDbContext _context;

    public ManagerController(PosDbContext context)
    {
        _context = context;
    }

    // ── KPI Summary ─────────────────────────────────────────────────────
    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> GetSummary()
    {
        // In a real app, you'd sum sales from today.
        // For now, we'll generate plausible values.
        var today = DateTime.UtcNow.Date;
        var ordersCount = await _context.Sales.CountAsync(s => s.CreatedAt.Date == today); // if Sales table exists
        var totalSales = await _context.Sales.Where(s => s.CreatedAt.Date == today).SumAsync(s => s.TotalAmount); // if exists

        // If Sales doesn't exist yet, use mock.
        if (!await _context.Sales.AnyAsync())
        {
            totalSales = 14832.40m;
            ordersCount = 318;
        }

        var avgOrder = ordersCount > 0 ? totalSales / ordersCount : 0;
        var activeRegisters = await _context.Registers.CountAsync(r => r.IsActive && r.IsTillOpen);

        return Ok(new
        {
            todaySales = totalSales,
            totalOrders = ordersCount,
            avgOrderValue = avgOrder,
            activeRegisters = activeRegisters,
            totalRegisters = await _context.Registers.CountAsync()
        });
    }

    // ── Registers ────────────────────────────────────────────────────────
    [HttpGet("dashboard/registers")]
    public async Task<IActionResult> GetRegisters()
    {
        var registers = await _context.Registers
            .Where(r => r.IsActive)
            .Select(r => new
            {
                id = r.Id,
                name = r.Name,
                cashier = r.CashierName ?? "—", // assumes a CashierName field; if not, you might need to join
                status = r.IsTillOpen ? "Open" : "Closed",
                expected = r.ExpectedCashAtOpen,
                counted = r.CountedCash ?? r.ExpectedCashAtOpen // placeholder
            })
            .ToListAsync();

        // If no registers, return mock data
        if (!registers.Any())
        {
            registers = new List<object>
            {
                new { id = "r1", name = "Register 01", cashier = "Maria Santos", status = "Open", expected = 850.00m, counted = 853.50m },
                new { id = "r2", name = "Register 02", cashier = "James Okafor", status = "Open", expected = 1240.00m, counted = 1238.25m },
                // ... include the rest of your mock data
            };
        }

        return Ok(registers);
    }

    // ── Low Stock Alerts ────────────────────────────────────────────────
    [HttpGet("dashboard/stock-alerts")]
    public async Task<IActionResult> GetStockAlerts()
    {
        var alerts = await _context.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                sku = p.Sku,
                current = p.StockUnits.Count(u => u.Status == "InStock"),
                threshold = p.ReorderThreshold
            })
            .Where(p => p.current <= p.threshold)
            .OrderBy(p => p.current)
            .ToListAsync();

        if (!alerts.Any())
        {
            // Return mock data if no products or no alerts
            alerts = new List<object>
            {
                new { id = "s1", name = "Organic Whole Milk 1L", sku = "DRY-1042", current = 3, threshold = 12 },
                new { id = "s2", name = "AA Batteries 8-Pack", sku = "ELC-0088", current = 1, threshold = 10 },
                new { id = "s3", name = "Sourdough Bread Loaf", sku = "BAK-2201", current = 5, threshold = 15 },
                new { id = "s4", name = "Hand Sanitiser 500ml", sku = "HYG-3310", current = 0, threshold = 8 },
                new { id = "s5", name = "Laundry Pods 30ct", sku = "CLN-4415", current = 4, threshold = 6 },
                new { id = "s6", name = "Free-Range Eggs 12pk", sku = "DRY-1009", current = 2, threshold = 20 }
            };
        }

        return Ok(alerts);
    }

    // ── Pending Approvals ──────────────────────────────────────────────
    [HttpGet("dashboard/pending-approvals")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        // If you have a Refund/Void table, query it. Otherwise mock.
        // For now, return mock data with a realistic shape.
        var pending = new List<object>
        {
            new { id = "p1", transactionId = "TXN-20849", amount = 84.99m, reason = "Defective product", time = DateTime.UtcNow.AddMinutes(-90).ToString("hh:mm tt"), type = "Refund" },
            new { id = "p2", transactionId = "TXN-20851", amount = 12.50m, reason = "Customer changed mind", time = DateTime.UtcNow.AddMinutes(-75).ToString("hh:mm tt"), type = "Void" },
            new { id = "p3", transactionId = "TXN-20860", amount = 229.00m, reason = "Wrong item scanned", time = DateTime.UtcNow.AddMinutes(-60).ToString("hh:mm tt"), type = "Refund" },
            new { id = "p4", transactionId = "TXN-20874", amount = 45.00m, reason = "Price discrepancy", time = DateTime.UtcNow.AddMinutes(-45).ToString("hh:mm tt"), type = "Void" },
            new { id = "p5", transactionId = "TXN-20889", amount = 9.99m, reason = "Expired item sold", time = DateTime.UtcNow.AddMinutes(-20).ToString("hh:mm tt"), type = "Refund" }
        };

        return Ok(pending);
    }

    // ── Approve/Reject endpoint (if needed) ────────────────────────────
    [HttpPost("dashboard/approvals/{id}/approve")]
    public async Task<IActionResult> ApproveApproval(string id)
    {
        // In reality, you'd update the database record.
        // Here we just return success.
        return Ok(new { success = true });
    }

    [HttpPost("dashboard/approvals/{id}/reject")]
    public async Task<IActionResult> RejectApproval(string id)
    {
        return Ok(new { success = true });
    }
}*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/manager")]
[Authorize(Roles = "Manager,Admin")]
public class ManagerController : ControllerBase
{
    // ── KPI Summary ─────────────────────────────────────────────────────
    [HttpGet("dashboard/summary")]
    public IActionResult GetSummary()
    {
        // Return mock data that matches the frontend structure
        return Ok(new
        {
            todaySales = 14832.40m,
            totalOrders = 318,
            avgOrderValue = 14832.40m / 318, // ~46.64
            activeRegisters = 4,
            totalRegisters = 5
        });
    }

    // ── Registers ────────────────────────────────────────────────────────
    [HttpGet("dashboard/registers")]
    public IActionResult GetRegisters()
    {
        var registers = new[]
        {
            new { id = "r1", name = "Register 01", cashier = "Maria Santos", status = "Open", expected = 850.00m, counted = 853.50m },
            new { id = "r2", name = "Register 02", cashier = "James Okafor", status = "Open", expected = 1240.00m, counted = 1238.25m },
            new { id = "r3", name = "Register 03", cashier = "Priya Nair", status = "Open", expected = 675.50m, counted = 665.00m },
            new { id = "r4", name = "Register 04", cashier = "—", status = "Closed", expected = 320.00m, counted = 320.00m },
            new { id = "r5", name = "Register 05", cashier = "Tom Eklund", status = "Open", expected = 990.75m, counted = 993.00m }
        };
        return Ok(registers);
    }

    // ── Low Stock Alerts ────────────────────────────────────────────────
    [HttpGet("dashboard/stock-alerts")]
    public IActionResult GetStockAlerts()
    {
        var alerts = new[]
        {
            new { id = "s1", name = "Organic Whole Milk 1L", sku = "DRY-1042", current = 3, threshold = 12 },
            new { id = "s2", name = "AA Batteries 8-Pack", sku = "ELC-0088", current = 1, threshold = 10 },
            new { id = "s3", name = "Sourdough Bread Loaf", sku = "BAK-2201", current = 5, threshold = 15 },
            new { id = "s4", name = "Hand Sanitiser 500ml", sku = "HYG-3310", current = 0, threshold = 8 },
            new { id = "s5", name = "Laundry Pods 30ct", sku = "CLN-4415", current = 4, threshold = 6 },
            new { id = "s6", name = "Free-Range Eggs 12pk", sku = "DRY-1009", current = 2, threshold = 20 }
        };
        return Ok(alerts);
    }

    // ── Pending Approvals ──────────────────────────────────────────────
    [HttpGet("dashboard/pending-approvals")]
    public IActionResult GetPendingApprovals()
    {
        var pending = new[]
        {
            new { id = "p1", transactionId = "TXN-20849", amount = 84.99m, reason = "Defective product", time = DateTime.UtcNow.AddMinutes(-90).ToString("hh:mm tt"), type = "Refund" },
            new { id = "p2", transactionId = "TXN-20851", amount = 12.50m, reason = "Customer changed mind", time = DateTime.UtcNow.AddMinutes(-75).ToString("hh:mm tt"), type = "Void" },
            new { id = "p3", transactionId = "TXN-20860", amount = 229.00m, reason = "Wrong item scanned", time = DateTime.UtcNow.AddMinutes(-60).ToString("hh:mm tt"), type = "Refund" },
            new { id = "p4", transactionId = "TXN-20874", amount = 45.00m, reason = "Price discrepancy", time = DateTime.UtcNow.AddMinutes(-45).ToString("hh:mm tt"), type = "Void" },
            new { id = "p5", transactionId = "TXN-20889", amount = 9.99m, reason = "Expired item sold", time = DateTime.UtcNow.AddMinutes(-20).ToString("hh:mm tt"), type = "Refund" }
        };
        return Ok(pending);
    }

    // ── Approve/Reject endpoints (mock) ────────────────────────────────
    [HttpPost("dashboard/approvals/{id}/approve")]
    public IActionResult ApproveApproval(string id) => Ok(new { success = true });

    [HttpPost("dashboard/approvals/{id}/reject")]
    public IActionResult RejectApproval(string id) => Ok(new { success = true });
}