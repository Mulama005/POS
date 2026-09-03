using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "Manager,Admin")]
public class NotificationsController : ControllerBase
{
    private readonly PosDbContext _context;

    public NotificationsController(PosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        var notifications = new List<NotificationDto>();

        // 1. Low stock alerts
        var lowStock = await _context.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                Stock = p.StockUnits.Count(u => u.Status == "InStock"),
                p.ReorderThreshold
            })
            .Where(p => p.Stock <= p.ReorderThreshold)
            .ToListAsync();

        foreach (var item in lowStock)
        {
            notifications.Add(new NotificationDto
            {
                Id = $"lowstock-{item.Id}",
                Type = "LowStock",
                Title = "Low Stock Alert",
                Message = $"{item.Name} ({item.Sku}) has {item.Stock} units remaining (threshold: {item.ReorderThreshold}).",
                Priority = item.Stock == 0 ? "high" : "medium",
                Link = "/inventory",
                Timestamp = DateTime.UtcNow
            });
        }

        // 2. Warranty expiring soon (within 30 days)
        var expiringWarranties = await _context.StockUnits
            .Include(u => u.Product)
            .Where(u => u.Status == "Sold" && u.SaleDate.HasValue)
            .ToListAsync();

        var expiring = expiringWarranties
            .Where(u =>
            {
                var expiry = u.SaleDate.Value.AddMonths(u.Product.WarrantyMonths);
                var daysLeft = (expiry - DateTime.UtcNow).Days;
                return daysLeft >= 0 && daysLeft <= 30;
            })
            .Select(u => new
            {
                u.Id,
                u.SerialNumber,
                u.Product.Name,
                DaysLeft = (int)(u.SaleDate.Value.AddMonths(u.Product.WarrantyMonths) - DateTime.UtcNow).TotalDays
            })
            .ToList();

        foreach (var item in expiring)
        {
            notifications.Add(new NotificationDto
            {
                Id = $"warranty-{item.Id}",
                Type = "WarrantyExpiry",
                Title = "Warranty Expiring Soon",
                Message = $"Unit {item.SerialNumber} ({item.Name}) warranty expires in {item.DaysLeft} days.",
                Priority = item.DaysLeft <= 7 ? "high" : "medium",
                Link = "/inventory",
                Timestamp = DateTime.UtcNow
            });
        }

        // 3. Pending approvals 
        var pendingApprovals = await _context.Sales
            .Where(s => s.Status == SaleStatus.PendingApproval)
            .Select(s => new
            {
                s.Id,
                s.Total,
                CashierName = s.Cashier.FullName,
                s.SaleDate
            })
            .ToListAsync();

        foreach (var item in pendingApprovals)
        {
            notifications.Add(new NotificationDto
            {
                Id = $"pending-{item.Id}",
                Type = "PendingApproval",
                Title = "Pending Approval",
                Message = $"Sale {item.Id} for KES {item.Total:F2} by {item.CashierName} needs approval.",
                Priority = "high",
                Link = "/sales/pending",
                Timestamp = item.SaleDate
            });
        }

        // 4. High discounts (discount > 20%)
        var highDiscounts = await _context.Sales
            .Where(s => s.Status == SaleStatus.Completed && s.DiscountTotal / s.Subtotal > 0.20m)
            .Select(s => new
            {
                s.Id,
                s.Total,
                s.DiscountTotal,
                DiscountPercent = (s.DiscountTotal / s.Subtotal) * 100,
                CashierName = s.Cashier.FullName,
                s.SaleDate
            })
            .ToListAsync();

        foreach (var item in highDiscounts)
        {
            notifications.Add(new NotificationDto
            {
                Id = $"discount-{item.Id}",
                Type = "HighDiscount",
                Title = "High Discount Applied",
                Message = $"Sale {item.Id} has {item.DiscountPercent:F1}% discount (KES {item.DiscountTotal:F2}) by {item.CashierName}.",
                Priority = item.DiscountPercent > 30 ? "high" : "medium",
                Link = "/sales",
                Timestamp = item.SaleDate
            });
        }

        return Ok(notifications.OrderByDescending(n => n.Priority == "high")
            .ThenByDescending(n => n.Timestamp));
    }
}

// ── DTO ──────────────────────────────────────────────────────────────
public class NotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}