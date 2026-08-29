using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
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

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> GetSummary()
    {
        var today = DateTime.UtcNow.Date;

        // Direct enum comparison – no cast needed
        var todaySales = await _context.Sales
            .Where(s => s.SaleDate.Date == today && s.Status == SaleStatus.Completed)
            .SumAsync(s => s.Total);

        var totalOrders = await _context.Sales
            .CountAsync(s => s.SaleDate.Date == today && s.Status == SaleStatus.Completed);

        var activeRegisters = await _context.TillSessions
            .Where(ts => ts.Status == TillSessionStatus.Open)
            .Select(ts => ts.RegisterId)
            .Distinct()
            .CountAsync();

        var totalRegisters = await _context.Registers
            .CountAsync(r => r.IsActive);

        var avgOrder = totalOrders > 0 ? todaySales / totalOrders : 0;

        return Ok(new
        {
            todaySales,
            totalOrders,
            avgOrderValue = avgOrder,
            activeRegisters,
            totalRegisters
        });
    }

    [HttpGet("dashboard/registers")]
    public async Task<IActionResult> GetRegisters()
    {
        var registers = await _context.Registers
            .Where(r => r.IsActive)
            .Select(r => new
            {
                id = r.Id,
                name = r.Name,
                latestSession = _context.TillSessions
                    .Where(ts => ts.RegisterId == r.Id)
                    .OrderByDescending(ts => ts.OpenedAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var result = registers.Select(r => new
        {
            id = r.id,
            name = r.name,
            cashier = "—",
            status = r.latestSession != null && r.latestSession.Status == TillSessionStatus.Open ? "Open" : "Closed",
            expected = r.latestSession?.ExpectedCashAtClose ?? 0,
            counted = r.latestSession?.CountedCashAtClose ?? 0
        });

        return Ok(result);
    }

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

        return Ok(alerts);
    }

    [HttpGet("dashboard/pending-approvals")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        // Placeholder – return empty list for now
        var pending = new List<object>();
        return Ok(pending);
    }

    [HttpPost("dashboard/approvals/{id}/approve")]
    public async Task<IActionResult> ApproveApproval(string id)
    {
        return Ok(new { success = true });
    }

    [HttpPost("dashboard/approvals/{id}/reject")]
    public async Task<IActionResult> RejectApproval(string id)
    {
        return Ok(new { success = true });
    }
}