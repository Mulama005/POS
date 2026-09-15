using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Etims;
using Microsoft.Extensions.Options;

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
    private readonly IEtimsService _etimsService;
    private readonly EtimsOptions _etimsOptions;
    private readonly IAuditService _auditService;
    private readonly PosDbContext _db;

    public EtimsController(
        IEtimsCodeSyncService syncService,
        IEtimsService etimsService,
        IAuditService auditService,
        PosDbContext db,
        IOptions<EtimsOptions> etimsOptions)
    {
        _syncService = syncService;
        _etimsService = etimsService;
        _auditService = auditService;
        _db = db;
        _etimsOptions = etimsOptions.Value;
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
    /// Registers one classified product as an eTIMS item through the local VSCU JAR.
    ///
    /// EtimsItemCode is allocated locally before the external call so a failed or
    /// interrupted registration can be retried with the same KRA item code.
    ///
    /// EtimsRegisteredAt is the authoritative local flag indicating that the
    /// registration call was accepted successfully.
    /// </summary>
    [HttpPost("items/{productId:guid}/register")]
    public async Task<IActionResult> RegisterItem(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(
                p => p.Id == productId && p.IsActive,
                cancellationToken);

        if (product is null)
        {
            return NotFound(new
            {
                message = "Product was not found or is inactive."
            });
        }

        if (string.IsNullOrWhiteSpace(product.EtimsItemClassificationCode))
        {
            return BadRequest(new
            {
                message =
                    "Assign a leaf-level eTIMS classification to the product before registering it."
            });
        }

        // IMPORTANT:
        // EtimsItemCode is allocated locally before calling VSCU.
        // Therefore EtimsItemCode != null does NOT necessarily mean KRA
        // accepted the registration.
        //
        // Only EtimsRegisteredAt means the registration completed successfully.
        if (product.EtimsRegisteredAt is not null)
        {
            return Conflict(new
            {
                message = "Product is already registered with eTIMS.",
                itemCd = product.EtimsItemCode
            });
        }

        var classification = await _db.EtimsItemClasses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.ItemClsCd == product.EtimsItemClassificationCode &&
                    x.UseYn,
                cancellationToken);

        if (classification is null)
        {
            return BadRequest(new
            {
                message =
                    "The product's eTIMS classification no longer exists or is inactive."
            });
        }

        // Product.TaxClass is the POS source of truth for the tax type.
        //
        // KRA's classification feed can legitimately contain a null TaxTyCd
        // even for a leaf classification, so registration derives the
        // mandatory KRA tax type from the product's TaxClass.
        var taxTypeCode = product.TaxClass switch
        {
            Pos.Domain.Enums.TaxClass.Standard => "B",
            Pos.Domain.Enums.TaxClass.ZeroRated => "C",
            Pos.Domain.Enums.TaxClass.Exempt => "A",
            _ => null
        };

        if (taxTypeCode is null)
        {
            return BadRequest(new
            {
                message =
                    "The product has an unsupported tax class for eTIMS item registration."
            });
        }

        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var userName =
            User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.Identity?.Name
            ?? "AyiyaPOS";

        userName = userName.Trim();

        if (userName.Length == 0)
        {
            userName = "AyiyaPOS";
        }

        // KRA regrId/modrId are limited to 20 characters.
        // Do not send the 36-character POS user GUID.
        var operatorId =
            User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.Identity?.Name
            ?? "AyiyaPOS";

        operatorId = operatorId.Trim();

        if (operatorId.Length > 20)
        {
            operatorId = operatorId[..20];
        }

        if (operatorId.Length == 0)
        {
            operatorId = "AyiyaPOS";
        }

        // ------------------------------------------------------------
        // ITEM CODE
        // ------------------------------------------------------------
        //
        // If an earlier attempt already allocated an item code but did not
        // complete registration, reuse it.
        //
        // Example:
        //
        //   EtimsItemCode       = KE2NTU0000001
        //   EtimsRegisteredAt   = NULL
        //
        // The request below will reuse KE2NTU0000001 rather than generating
        // KE2NTU0000002.
        //
        if (string.IsNullOrWhiteSpace(product.EtimsItemCode))
        {
            var sequence = await _db.Database
                .SqlQueryRaw<long>(
                    "SELECT nextval('\"EtimsItemCodeSequence\"') AS \"Value\"")
                .SingleAsync(cancellationToken);

            var origin =
                _etimsOptions.OriginCountryCode
                    .Trim()
                    .ToUpperInvariant();

            var itemType =
                _etimsOptions.ItemTypeCode
                    .Trim()
                    .ToUpperInvariant();

            var pkg =
                _etimsOptions.DefaultPackagingUnitCode
                    .Trim()
                    .ToUpperInvariant();

            var qty =
                _etimsOptions.DefaultQuantityUnitCode
                    .Trim()
                    .ToUpperInvariant();

            if (origin.Length != 2 ||
                itemType.Length != 1 ||
                pkg.Length != 2 ||
                qty.Length != 1)
            {
                return BadRequest(new
                {
                    message =
                        "eTIMS item-code configuration is invalid. Expected country=2 characters, item type=1, packaging unit=2, quantity unit=1."
                });
            }

            product.EtimsItemCode =
                $"{origin}{itemType}{pkg}{qty}{sequence.ToString(
                    "D7",
                    System.Globalization.CultureInfo.InvariantCulture)}";

            product.EtimsItemTypeCode = itemType;
            product.EtimsOriginCountryCode = origin;
            product.EtimsPackagingUnitCode = pkg;
            product.EtimsQuantityUnitCode = qty;
            product.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }

        // If this is a retry, the fields below should already exist from the
        // original allocation. The fallbacks also make the request robust if
        // an older locally-created item code is missing these fields.
        var itemTypeCode =
            string.IsNullOrWhiteSpace(product.EtimsItemTypeCode)
                ? _etimsOptions.ItemTypeCode.Trim().ToUpperInvariant()
                : product.EtimsItemTypeCode.Trim().ToUpperInvariant();

        var originCountryCode =
            string.IsNullOrWhiteSpace(product.EtimsOriginCountryCode)
                ? _etimsOptions.OriginCountryCode.Trim().ToUpperInvariant()
                : product.EtimsOriginCountryCode.Trim().ToUpperInvariant();

        var packagingUnitCode =
            string.IsNullOrWhiteSpace(product.EtimsPackagingUnitCode)
                ? _etimsOptions.DefaultPackagingUnitCode.Trim().ToUpperInvariant()
                : product.EtimsPackagingUnitCode.Trim().ToUpperInvariant();

        var quantityUnitCode =
            string.IsNullOrWhiteSpace(product.EtimsQuantityUnitCode)
                ? _etimsOptions.DefaultQuantityUnitCode.Trim().ToUpperInvariant()
                : product.EtimsQuantityUnitCode.Trim().ToUpperInvariant();

        if (itemTypeCode.Length != 1 ||
            originCountryCode.Length != 2 ||
            packagingUnitCode.Length != 2 ||
            quantityUnitCode.Length != 1)
        {
            return BadRequest(new
            {
                message =
                    "eTIMS item-code configuration is invalid. Expected country=2 characters, item type=1, packaging unit=2, quantity unit=1."
            });
        }

        var request = new EtimsItemSaveRequest(
            classification.ItemClsCd,
            product.EtimsItemCode!,
            itemTypeCode,
            product.Name,
            originCountryCode,
            packagingUnitCode,
            quantityUnitCode,
            taxTypeCode,
            product.SalePrice,
            "N",
            "Y",
            operatorId,
            userName,
            operatorId,
            userName,
            product.Name,
            null,
            string.IsNullOrWhiteSpace(product.Barcode)
                ? null
                : product.Barcode);

        // ------------------------------------------------------------
        // KRA / VSCU ITEM REGISTRATION
        // ------------------------------------------------------------

        var result = await _etimsService.SaveItemAsync(
            request,
            cancellationToken);

        if (!result.Success)
        {
            await _auditService.LogAsync(
                userId.Value,
                "ETIMS_ITEM_REGISTRATION_FAILED",
                "Product",
                product.Id,
                result.ErrorMessage
                    ?? result.ResultMessage
                    ?? "Unknown eTIMS item registration error.");

            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message =
                        result.ErrorMessage
                        ?? result.ResultMessage
                        ?? "eTIMS item registration failed.",

                    resultCode = result.ResultCode,

                    itemCd = result.ItemCd
                        ?? product.EtimsItemCode,

                    itemClsCd =
                        product.EtimsItemClassificationCode
                });
        }

        // ------------------------------------------------------------
        // MARK AS SUCCESSFULLY REGISTERED
        // ------------------------------------------------------------

        product.EtimsRegisteredAt =
            result.ResultDate ?? DateTime.UtcNow;

        product.EtimsRegisteredByUserId =
            userId.Value;

        product.EtimsTaxTypeCode =
            taxTypeCode;

        product.EtimsItemTypeCode =
            itemTypeCode;

        product.EtimsOriginCountryCode =
            originCountryCode;

        product.EtimsPackagingUnitCode =
            packagingUnitCode;

        product.EtimsQuantityUnitCode =
            quantityUnitCode;

        product.UpdatedAt =
            DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            userId.Value,
            "ETIMS_ITEM_REGISTERED",
            "Product",
            product.Id,
            $"Registered item {product.EtimsItemCode} ({product.Name}) with KRA eTIMS.");

        return Ok(new
        {
            productId = product.Id,

            itemCd =
                product.EtimsItemCode,

            itemClsCd =
                classification.ItemClsCd,

            itemClsNm =
                classification.ItemClsNm,

            taxTypeCode,

            resultCode =
                result.ResultCode,

            resultDate =
                result.ResultDate,

            registeredAt =
                product.EtimsRegisteredAt
        });
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

        var prefixLength =
            GetHierarchyPrefixLength(parent.ItemClsLvl);

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