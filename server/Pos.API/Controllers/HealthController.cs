using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class HealthController : ControllerBase
{
    private readonly PosDbContext _context;
    private readonly IConfiguration _config;

    public HealthController(PosDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        // In a real implementation, you'd check each service.
        // For now, we return mock statuses with realistic checks.
        var services = new List<object>();

        // 1. Database connectivity
        var dbOk = await _context.Database.CanConnectAsync();
        services.Add(new
        {
            name = "db",
            label = "Database Connectivity",
            status = dbOk ? "ok" : "error",
            detail = dbOk ? "Connected" : "Disconnected",
            meta = dbOk ? "Latency" : "Last check failed",
            latency = dbOk ? "4 ms" : ""
        });

        // 2. Supabase Storage (we can check if the bucket exists or just assume)
        // For demonstration, we'll check if Supabase URL is configured.
        var storageUrl = _config["Supabase:Url"];
        var storageOk = !string.IsNullOrEmpty(storageUrl);
        services.Add(new
        {
            name = "storage",
            label = "Supabase Storage",
            status = storageOk ? "ok" : "error",
            detail = storageOk ? "Available" : "Not configured",
            meta = storageOk ? "Last checked 12s ago" : "Check configuration",
            latency = storageOk ? "N/A" : ""
        });

        // 3. eTIMS Integration (mock)
        services.Add(new
        {
            name = "etims",
            label = "eTIMS Integration",
            status = "warn",
            detail = "Degraded",
            meta = "Last sync 09:41:03",
            latency = ""
        });

        // 4. M-Pesa Integration (mock)
        services.Add(new
        {
            name = "mpesa",
            label = "M-Pesa Integration",
            status = "ok",
            detail = "Connected",
            meta = "Last txn 09:58:22",
            latency = ""
        });

        // 5. Pesapal Integration (mock)
        services.Add(new
        {
            name = "pesapal",
            label = "Pesapal Integration",
            status = "error",
            detail = "Disconnected",
            meta = "Last sync 08:03:11",
            latency = ""
        });

        return Ok(services);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit([FromQuery] int limit = 20)
    {
        // In a real app, you'd query an AuditLog table.
        // For now, we'll generate some sample data.
        var entries = Enumerable.Range(1, limit).Select(i => new
        {
            id = i,
            ts = DateTime.UtcNow.AddMinutes(-i * 5).ToString("yyyy-MM-dd HH:mm:ss"),
            user = i % 2 == 0 ? "system" : "admin",
            action = i % 3 == 0 ? "INVOICE_CREATED" : i % 3 == 1 ? "USER_LOGIN" : "ETIMS_RETRY",
            details = $"Sample detail {i}",
            level = i % 4 == 0 ? "error" : i % 4 == 1 ? "warn" : "info"
        }).ToList();

        return Ok(entries);
    }
}