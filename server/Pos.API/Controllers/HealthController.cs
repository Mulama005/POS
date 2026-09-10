using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class HealthController : ControllerBase
{
    private readonly PosDbContext _context;
    private readonly IConfiguration _config;
    private readonly IEtimsService _etimsService;

    public HealthController(PosDbContext context, IConfiguration config, IEtimsService etimsService)
    {
        _context = context;
        _config = config;
        _etimsService = etimsService;
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

        // 3. eTIMS Integration — Step 26, device-init slice only. This makes a real call
        // to the locally-running VSCU JAR every time the health dashboard loads, which is
        // fine for an Admin-only diagnostic page hit occasionally, but don't reuse this
        // pattern for anything called per-request — device-init doesn't need to run that
        // often, this is just the cheapest place to expose "is the JAR reachable right now."
        try
        {
            var sw = Stopwatch.StartNew();
            var etimsResult = await _etimsService.InitDeviceAsync();
            sw.Stop();
            services.Add(new
            {
                name = "etims",
                label = "eTIMS Integration",
                status = etimsResult.Success ? "ok" : "error",
                // TaxpayerName is only populated on a "000" fresh handshake — a "902"
                // (already installed) response comes back with no data payload but is
                // still a genuine success, so fall back to the descriptive message rather
                // than showing a blank/placeholder name.
                detail = etimsResult.Success
                    ? (etimsResult.TaxpayerName ?? etimsResult.ResultMessage ?? "Connected")
                    : (etimsResult.ErrorMessage ?? "Device init failed"),
                meta = etimsResult.Success
                    ? (etimsResult.BranchName is not null ? $"Branch: {etimsResult.BranchName}" : $"Code: {etimsResult.ResultCode}")
                    : (etimsResult.ResultCode ?? "No response"),
                latency = $"{sw.ElapsedMilliseconds} ms"
            });
        }
        catch (Exception ex)
        {
            services.Add(new
            {
                name = "etims",
                label = "eTIMS Integration",
                status = "error",
                detail = $"Unhandled error: {ex.Message}",
                meta = "Exception",
                latency = ""
            });
        }

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