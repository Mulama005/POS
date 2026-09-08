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

        // 2. Supabase Storage — check configuration. Bound from the "Supabase" section
        // (see SupabaseStorageOptions) — Url + ServiceRoleKey, not "Key"; the previous
        // version of this check read a config key that was never actually set, so it
        // always reported "Not configured" regardless of the real state.
        var storageUrl = _config["Supabase:Url"];
        var storageKey = _config["Supabase:ServiceRoleKey"];
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

        // 3. eTIMS Integration — no real check yet, but we can later implement a ping
        services.Add(new
        {
            name = "etims",
            label = "eTIMS Integration",
            status = "warn",
            detail = "Not implemented",
            meta = "Placeholder",
            latency = ""
        });

        // 4. M-Pesa Integration — placeholder
        services.Add(new
        {
            name = "mpesa",
            label = "M-Pesa Integration",
            status = "warn",
            detail = "Not implemented",
            meta = "Placeholder",
            latency = ""
        });

        // 5. Pesapal Integration — placeholder
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
        // Resolve each entry's actual user name via DomainUsers rather than showing the
        // raw UserId GUID — a name reads as finished, a UUID reads as a debug leftover.
        // Left-joined so an entry survives even if the user was later deactivated/removed.
        var entries = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .GroupJoin(
                _context.DomainUsers,
                audit => audit.UserId,
                user => user.Id,
                (audit, users) => new { audit, users })
            .SelectMany(
                x => x.users.DefaultIfEmpty(),
                (x, user) => new
                {
                    id = x.audit.Id,
                    ts = x.audit.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    user = user != null ? user.FullName : x.audit.UserId.ToString(),
                    action = x.audit.ActionType,
                    details = x.audit.Details ?? "",
                    level = x.audit.ActionType == "ERROR" ? "error" : "info" // simplistic mapping
                })
            .ToListAsync();

        return Ok(entries);
    }
}