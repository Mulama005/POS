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
        var services = new List<object>();

        // 1. Database connectivity
        try
        {
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
        }
        catch
        {
            services.Add(new
            {
                name = "db",
                label = "Database Connectivity",
                status = "error",
                detail = "Disconnected",
                meta = "Connection failed",
                latency = ""
            });
        }

        // 2. Supabase Storage – check configuration
        var storageUrl = _config["Supabase:Url"];
        var storageKey = _config["Supabase:Key"];
        var storageOk = !string.IsNullOrEmpty(storageUrl) && !string.IsNullOrEmpty(storageKey);
        services.Add(new
        {
            name = "storage",
            label = "Supabase Storage",
            status = storageOk ? "ok" : "error",
            detail = storageOk ? "Available" : "Not configured",
            meta = storageOk ? "Configured" : "Missing credentials",
            latency = ""
        });

        // 3. eTIMS Integration – no real check yet, but we can later implement a ping
        services.Add(new
        {
            name = "etims",
            label = "eTIMS Integration",
            status = "warn",
            detail = "Not implemented",
            meta = "Placeholder",
            latency = ""
        });

        // 4. M-Pesa Integration – placeholder
        services.Add(new
        {
            name = "mpesa",
            label = "M-Pesa Integration",
            status = "warn",
            detail = "Not implemented",
            meta = "Placeholder",
            latency = ""
        });

        // 5. Pesapal Integration – placeholder
        services.Add(new
        {
            name = "pesapal",
            label = "Pesapal Integration",
            status = "warn",
            detail = "Not implemented",
            meta = "Placeholder",
            latency = ""
        });

        return Ok(services);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAudit([FromQuery] int limit = 20)
    {
        // Query real audit logs
        var entries = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .Select(a => new
            {
                id = a.Id,
                ts = a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                user = a.UserId.ToString(), // you can join with DomainUsers to get name later
                action = a.ActionType,
                details = a.Details ?? "",
                level = a.ActionType == "ERROR" ? "error" : "info" // simplistic mapping
            })
            .ToListAsync();

        // If no audit logs exist, return empty array
        return Ok(entries);
    }
}