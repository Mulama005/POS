using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

/// <summary>
/// Step 26 — KRA reference-data sync (codes + product classification taxonomy) and
/// browse/search endpoints over what's been synced.
/// Admin-only because this touches compliance configuration.
/// </summary>
[ApiController]
[Route("api/admin/etims")]
[Authorize(Roles = RoleGroups.AdminOnly)]
public sealed class EtimsController : ControllerBase
{
    private readonly IEtimsCodeSyncService _syncService;
    private readonly IAuditService _auditService;
    private readonly PosDbContext _db;

    public EtimsController(
        IEtimsCodeSyncService syncService,
        IAuditService auditService,
        PosDbContext db)
    {
        _syncService = syncService;
        _auditService = auditService;
        _db = db;
    }

    /// <summary>
    /// Triggers a code-list sync against /code/selectCodes.
    /// Safe to call repeatedly — incremental via lastReqDt, upserts rather than replacing.
    /// </summary>
    [HttpPost("sync/codes")]
    public async Task<IActionResult> SyncCodes(
        CancellationToken cancellationToken)
    {
        var summary = await _syncService.SyncCodesAsync(cancellationToken);

        var userId = GetUserId();

        if (userId is not null)
        {
            await _auditService.LogAsync(
                userId: userId.Value,
                actionType: summary.Success
                    ? "ETIMS_CODES_SYNCED"
                    : "ETIMS_CODES_SYNC_FAILED",
                entityName: "EtimsCodeClass",
                entityId: Guid.Empty,
                details: summary.Message);
        }

        return summary.Success
            ? Ok(summary)
            : StatusCode(502, summary);
    }

    /// <summary>
    /// Triggers an item-classification sync against
    /// /itemClass/selectItemsClass.
    /// </summary>
    [HttpPost("sync/item-classes")]
    public async Task<IActionResult> SyncItemClasses(
        CancellationToken cancellationToken)
    {
        var summary =
            await _syncService.SyncItemClassesAsync(cancellationToken);

        var userId = GetUserId();

        if (userId is not null)
        {
            await _auditService.LogAsync(
                userId: userId.Value,
                actionType: summary.Success
                    ? "ETIMS_ITEM_CLASSES_SYNCED"
                    : "ETIMS_ITEM_CLASSES_SYNC_FAILED",
                entityName: "EtimsItemClass",
                entityId: Guid.Empty,
                details: summary.Message);
        }

        return summary.Success
            ? Ok(summary)
            : StatusCode(502, summary);
    }

    /// <summary>
    /// Current sync watermark/status for both datasets.
    /// </summary>
    [HttpGet("sync/status")]
    public async Task<IActionResult> GetSyncStatus(
        CancellationToken cancellationToken)
    {
        var states = await _db.EtimsSyncStates
            .AsNoTracking()
            .ToListAsync(cancellationToken);

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

    /// <summary>
    /// Searches the locally synced KRA item-classification taxonomy.
    ///
    /// q:
    ///   Searches ItemClsNm using contains matching and ItemClsCd using exact matching.
    ///
    /// level:
    ///   Restricts results to a specific taxonomy level.
    ///
    /// leafOnly:
    ///   When true, returns only classifications that have no child classification.
    ///
    /// The leaf decision is based on the actual hierarchy, not TaxTyCd.
    /// </summary>
    [HttpGet("item-classes")]
    public async Task<IActionResult> SearchItemClasses(
        [FromQuery] string? q,
        [FromQuery] int? level,
        [FromQuery] bool leafOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.EtimsItemClasses
            .AsNoTracking()
            .AsQueryable();

        var term = q?.Trim();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(i =>
                i.ItemClsNm.Contains(term) ||
                i.ItemClsCd == term);
        }

        if (level.HasValue)
        {
            query = query.Where(i =>
                i.ItemClsLvl == level.Value);
        }

        /*
         * We intentionally do not use:
         *
         *     TaxTyCd != null
         *
         * as the definition of a leaf.
         *
         * TaxTyCd is tax metadata. A classification is a leaf when there
         * is no classification at the next level beneath it.
         *
         * For search results, we first retrieve the matching records and
         * calculate hierarchy metadata after materialization.
         */
        var candidates = await query
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

        var hierarchy = await BuildHierarchyMetadataAsync(
            candidates.Select(i => new EtimsHierarchyNode(
                i.ItemClsCd,
                i.ItemClsLvl)),
            cancellationToken);

        var enriched = candidates
            .Select(i =>
            {
                var hasChildren =
                    hierarchy.TryGetValue(i.ItemClsCd, out var value) &&
                    value;

                return new
                {
                    i.ItemClsCd,
                    i.ItemClsNm,
                    i.ItemClsLvl,
                    i.TaxTyCd,
                    i.MjrTgYn,
                    i.UseYn,
                    hasChildren,
                    selectable = !hasChildren
                };
            });

        if (leafOnly)
        {
            enriched = enriched.Where(i => !i.hasChildren);
        }

        var total = enriched.Count();

        IEnumerable<object> ordered;

        if (!string.IsNullOrWhiteSpace(term))
        {
            var termLower = term.ToLowerInvariant();

            ordered = enriched
                .OrderByDescending(i =>
                    i.ItemClsNm.ToLower().StartsWith(termLower))
                .ThenBy(i => i.ItemClsNm.Length)
                .ThenBy(i => i.ItemClsNm)
                .ThenBy(i => i.ItemClsCd)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Cast<object>();
        }
        else
        {
            ordered = enriched
                .OrderBy(i => i.ItemClsCd)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Cast<object>();
        }

        var items = ordered.ToList();

        return Ok(new
        {
            total,
            page,
            pageSize,
            items
        });
    }

    /// <summary>
    /// Returns the direct children of a KRA item classification.
    ///
    /// Omitting parentCode returns the root Segment-level classifications.
    ///
    /// Hierarchy:
    ///   Level 1 -> Level 2: first 2 digits
    ///   Level 2 -> Level 3: first 4 digits
    ///   Level 3 -> Level 4: first 6 digits
    ///   Level 4 -> Level 5: first 8 digits
    /// </summary>
    [HttpGet("item-classes/children")]
    public async Task<IActionResult> GetItemClassChildren(
        [FromQuery] string? parentCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedParentCode = parentCode?.Trim();

        // ------------------------------------------------------------
        // ROOT
        // ------------------------------------------------------------

        if (string.IsNullOrWhiteSpace(normalizedParentCode))
        {
            var roots = await _db.EtimsItemClasses
                .AsNoTracking()
                .Where(i =>
                    i.ItemClsLvl == 1 &&
                    i.UseYn)
                .OrderBy(i => i.ItemClsCd)
                .Select(i => new
                {
                    i.ItemClsCd,
                    i.ItemClsNm,
                    i.ItemClsLvl,
                    i.TaxTyCd,
                    i.MjrTgYn,
                    i.UseYn,
                    hasChildren = true,
                    selectable = false
                })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                parent = (object?)null,
                items = roots
            });
        }

        // ------------------------------------------------------------
        // FIND PARENT
        // ------------------------------------------------------------

        var parent = await _db.EtimsItemClasses
            .AsNoTracking()
            .Where(i =>
                i.ItemClsCd == normalizedParentCode)
            .Select(i => new
            {
                i.ItemClsCd,
                i.ItemClsNm,
                i.ItemClsLvl
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (parent is null)
        {
            return NotFound(new
            {
                message =
                    $"Item classification '{normalizedParentCode}' was not found."
            });
        }

        // ------------------------------------------------------------
        // DEEPEST LEVEL
        // ------------------------------------------------------------

        if (parent.ItemClsLvl >= 5)
        {
            return Ok(new
            {
                parent = new
                {
                    parent.ItemClsCd,
                    parent.ItemClsNm,
                    parent.ItemClsLvl
                },
                items = Array.Empty<object>()
            });
        }

        // ------------------------------------------------------------
        // DETERMINE CHILD PREFIX
        // ------------------------------------------------------------

        var prefixLength = GetHierarchyPrefixLength(
            parent.ItemClsLvl);

        if (prefixLength <= 0 ||
            parent.ItemClsCd.Length < prefixLength)
        {
            return BadRequest(new
            {
                message =
                    $"Invalid classification hierarchy for '{parent.ItemClsCd}'."
            });
        }

        var prefix =
            parent.ItemClsCd[..prefixLength];

        var childLevel =
            parent.ItemClsLvl + 1;

        // ------------------------------------------------------------
        // FETCH DIRECT CHILDREN
        // ------------------------------------------------------------

        var children = await _db.EtimsItemClasses
            .AsNoTracking()
            .Where(i =>
                i.ItemClsLvl == childLevel &&
                i.UseYn &&
                i.ItemClsCd.StartsWith(prefix))
            .OrderBy(i => i.ItemClsCd)
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

        // ------------------------------------------------------------
        // DETERMINE WHICH CHILDREN HAVE CHILDREN
        // ------------------------------------------------------------

        var childPrefixes = children
            .Select(child =>
            {
                var length =
                    GetHierarchyPrefixLength(child.ItemClsLvl);

                return length > 0 &&
                       child.ItemClsCd.Length >= length
                    ? child.ItemClsCd[..length]
                    : null;
            })
            .Where(prefix => prefix is not null)
            .Distinct()
            .ToList();

        var nextLevelCodes = new HashSet<string>();

        if (childPrefixes.Count > 0 &&
            childLevel < 5)
        {
            var nextLevel =
                childLevel + 1;

            var nextLevelRows = await _db.EtimsItemClasses
                .AsNoTracking()
                .Where(i =>
                    i.ItemClsLvl == nextLevel &&
                    i.UseYn)
                .Select(i => i.ItemClsCd)
                .ToListAsync(cancellationToken);

            foreach (var code in nextLevelRows)
            {
                foreach (var childPrefix in childPrefixes)
                {
                    if (code.StartsWith(childPrefix))
                    {
                        nextLevelCodes.Add(code);
                        break;
                    }
                }
            }
        }

        var result = children.Select(child =>
        {
            var childPrefixLength =
                GetHierarchyPrefixLength(child.ItemClsLvl);

            var childPrefix =
                childPrefixLength > 0 &&
                child.ItemClsCd.Length >= childPrefixLength
                    ? child.ItemClsCd[..childPrefixLength]
                    : null;

            var hasChildren =
                childPrefix is not null &&
                nextLevelCodes.Any(code =>
                    code.StartsWith(childPrefix));

            return new
            {
                child.ItemClsCd,
                child.ItemClsNm,
                child.ItemClsLvl,
                child.TaxTyCd,
                child.MjrTgYn,
                child.UseYn,
                hasChildren,

                // The existing product-assignment flow requires a tax
                // type on the final classification.
                selectable =
                    !hasChildren
            };
        }).ToList();

        return Ok(new
        {
            parent = new
            {
                parent.ItemClsCd,
                parent.ItemClsNm,
                parent.ItemClsLvl
            },
            items = result
        });
    }

    /// <summary>
    /// Browse the locally-synced general reference codes
    /// (Tax Type, Packaging Unit, etc.).
    /// </summary>
    [HttpGet("codes")]
    public async Task<IActionResult> GetCodes(
        [FromQuery] string? cdCls,
        CancellationToken cancellationToken)
    {
        var query = _db.EtimsCodeClasses
            .Include(c => c.Codes)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(cdCls))
        {
            query = query.Where(c =>
                c.CdCls == cdCls.Trim());
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
                    .Select(d => new
                    {
                        d.Cd,
                        d.CdNm,
                        d.CdDesc,
                        d.UseYn
                    })
            })
            .ToListAsync(cancellationToken);

        return Ok(classes);
    }

    // ========================================================================
    // HIERARCHY HELPERS
    // ========================================================================

    private static int GetHierarchyPrefixLength(int level)
    {
        return level switch
        {
            1 => 2,
            2 => 4,
            3 => 6,
            4 => 8,
            _ => 0
        };
    }

    private sealed record EtimsHierarchyNode(
        string ItemClsCd,
        int ItemClsLvl);

    private async Task<Dictionary<string, bool>> BuildHierarchyMetadataAsync(
        IEnumerable<EtimsHierarchyNode> nodes,
        CancellationToken cancellationToken)
    {
        var nodeList = nodes.ToList();

        var result = nodeList
            .ToDictionary(
                n => n.ItemClsCd,
                _ => false);

        if (nodeList.Count == 0)
        {
            return result;
        }

        var nodesByLevel = nodeList
            .GroupBy(n => n.ItemClsLvl)
            .ToList();

        foreach (var group in nodesByLevel)
        {
            var level = group.Key;

            if (level >= 5)
            {
                continue;
            }

            var prefixLength =
                GetHierarchyPrefixLength(level);

            if (prefixLength == 0)
            {
                continue;
            }

            var prefixes = group
                .Where(n =>
                    n.ItemClsCd.Length >= prefixLength)
                .Select(n =>
                    n.ItemClsCd[..prefixLength])
                .Distinct()
                .ToList();

            if (prefixes.Count == 0)
            {
                continue;
            }

            var nextLevel = level + 1;

            var nextLevelCodes = await _db.EtimsItemClasses
                .AsNoTracking()
                .Where(i =>
                    i.ItemClsLvl == nextLevel &&
                    i.UseYn)
                .Select(i => i.ItemClsCd)
                .ToListAsync(cancellationToken);

            var matchingPrefixes = new HashSet<string>(
                prefixes.Where(prefix =>
                    nextLevelCodes.Any(code =>
                        code.StartsWith(prefix))));

            foreach (var node in group)
            {
                if (node.ItemClsCd.Length < prefixLength)
                {
                    continue;
                }

                var prefix =
                    node.ItemClsCd[..prefixLength];

                result[node.ItemClsCd] =
                    matchingPrefixes.Contains(prefix);
            }
        }

        return result;
    }

    private Guid? GetUserId()
    {
        var claim =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var id)
            ? id
            : null;
    }
}