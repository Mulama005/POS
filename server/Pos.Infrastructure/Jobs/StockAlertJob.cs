using Pos.Infrastructure.Persistence;
using Pos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Pos.Infrastructure.Jobs;

public class StockAlertJob
{
    private readonly PosDbContext _db;
    private readonly ILogger<StockAlertJob> _logger;

    public StockAlertJob(PosDbContext db, ILogger<StockAlertJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CheckLowStock()
    {
        var products = await _db.Products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.ReorderThreshold,
                Stock = p.StockUnits.Count(u => u.Status == "InStock")
            })
            .ToListAsync();

        var alerts = products.Where(p => p.Stock <= p.ReorderThreshold).ToList();
        if (!alerts.Any())
        {
            _logger.LogInformation("No low stock items.");
            return;
        }

        // Send notification – implement email/WhatsApp later
        var message = "Low stock alert:\n" + string.Join("\n", alerts.Select(a => $"{a.Name}: {a.Stock} left (threshold {a.ReorderThreshold})"));
        _logger.LogWarning(message);

        // Save alerts to a table for dashboard display
        // await _db.LowStockAlerts.AddRangeAsync(...);
        await _db.SaveChangesAsync();
    }
}