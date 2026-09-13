using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

/// <summary>
/// Step 26 — KRA reference-data sync (codes + product classification taxonomy) and
/// browse endpoints over what's been synced. Admin-only: this touches compliance
/// configuration, not day-to-day operations, and the item-class dataset is large enough
/// that triggering a sync isn't something to expose to every role.
/// </summary>
[ApiController]
[Route("api/admin/etims")]
[Authorize(Roles = RoleGroups.AdminOnly)]
public sealed class EtimsController : ControllerBase
{
    private readonly IEtimsCodeSyncService _syncService;
    private readonly IAuditService _auditService;
    private readonly PosDbContext _db;

    public EtimsController(IEtimsCodeSyncService syncService, IAuditService auditService, PosDbContext db)
    {
        _syncService = syncService;
        _auditService = auditService;
        _db = db;
    }

    /// <summary>Triggers a code-list sync against /code/selectCodes. Safe to call
    /// repeatedly — incremental via lastReqDt, upserts rather than replacing.</summary>
    [HttpPost("sync/codes")]
    public async Task<IActionResult> SyncCodes(CancellationToken cancellationToken)
    {
        var summary = await _syncService.SyncCodesAsync(cancellationToken);

        var userId = GetUserId();
        if (userId is not null)
        {
            await _auditService.LogAsync(
                userId: userId.Value,
                actionType: summary.Success ? "ETIMS_CODES_SYNCED" : "ETIMS_CODES_SYNC_FAILED",
                entityName: "EtimsCodeClass",
                entityId: Guid.Empty,
                details: summary.Message);
        }

        return summary.Success ? Ok(summary) : StatusCode(502, summary);
    }

    /// <summary>Triggers an item-classification sync against
    /// /itemClass/selectItemsClass. This is the large dataset (KRA's full product
    /// taxonomy) — expect a first (full) sync to take noticeably longer than a codes
    /// sync.</summary>
    [HttpPost("sync/item-classes")]
    public async Task<IActionResult> SyncItemClasses(CancellationToken cancellationToken)
    {
        var summary = await _syncService.SyncItemClassesAsync(cancellationToken);

        var userId = GetUserId();
        if (userId is not null)
        {
            await _auditService.LogAsync(
                userId: userId.Value,
                actionType: summary.Success ? "ETIMS_ITEM_CLASSES_SYNCED" : "ETIMS_ITEM_CLASSES_SYNC_FAILED",
                entityName: "EtimsItemClass",
                entityId: Guid.Empty,
                details: summary.Message);
        }

        return summary.Success ? Ok(summary) : StatusCode(502, summary);
    }

    /// <summary>Current sync watermark/status for both datasets — when each last
    /// succeeded, what the last attempt (successful or not) actually said. Meant for an
    /// admin diagnostics view, same spirit as /api/admin/health.</summary>
    [HttpGet("sync/status")]
    public async Task<IActionResult> GetSyncStatus(CancellationToken cancellationToken)
    {
        var states = await _db.EtimsSyncStates.ToListAsync(cancellationToken);

        var result = states.Select(s => new
        {
            syncKey = s.SyncKey,
            lastSuccessfulSyncAt = s.LastSuccessfulSyncAt,
            lastAttemptAt = s.LastAttemptAt,
            lastAttemptSucceeded = s.LastAttemptSucceeded,
            lastAttemptMessage = s.LastAttemptMessage,
            lastAttemptRecordCount = s.LastAttemptRecordCount
        });

        return Ok(result);
    }

    /// <summary>Browse the locally-synced product classification taxonomy — needed for
    /// the next slice of Step 26 (assigning each Product a KRA classification code), and
    /// standalone useful for sanity-checking what actually came back from a sync. `q`
    /// searches ItemClsNm (case-insensitive contains) and exact ItemClsCd; `level`
    /// filters to a specific taxonomy depth when provided.</summary>
    [HttpGet("item-classes")]
    public async Task<IActionResult> SearchItemClasses(
        [FromQuery] string? q,
        [FromQuery] int? level,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.EtimsItemClasses.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(i => i.ItemClsNm.Contains(term) || i.ItemClsCd == term);
        }

        if (level.HasValue)
        {
            query = query.Where(i => i.ItemClsLvl == level.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(i => i.ItemClsCd)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.ItemClsCd,
                i.ItemClsNm,
                i.ItemClsLvl,
                i.TaxTyCd,
                i.MjrTgYn,
                i.UseYn
            })
            .ToListAsync(cancellationToken);

        return Ok(new { total, page, pageSize, items });
    }

    /// <summary>Browse the locally-synced general reference codes (Tax Type, Packaging
    /// Unit, etc), grouped by classification. `cdCls` filters to one classification
    /// (e.g. "04" for Tax Type) when provided; omitted returns all classes with their
    /// codes nested — the full set is small enough that pagination isn't needed here the
    /// way it is for item-classes.</summary>
    [HttpGet("codes")]
    public async Task<IActionResult> GetCodes([FromQuery] string? cdCls, CancellationToken cancellationToken)
    {
        var query = _db.EtimsCodeClasses.Include(c => c.Codes).AsQueryable();

        if (!string.IsNullOrWhiteSpace(cdCls))
        {
            query = query.Where(c => c.CdCls == cdCls.Trim());
        }

        var classes = await query
            .OrderBy(c => c.CdCls)
            .Select(c => new
            {
                c.CdCls,
                c.CdClsNm,
                c.CdClsDesc,
                c.UseYn,
                Codes = c.Codes
                    .OrderBy(d => d.SrtOrd)
                    .Select(d => new { d.Cd, d.CdNm, d.CdDesc, d.UseYn })
            })
            .ToListAsync(cancellationToken);

        return Ok(classes);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}