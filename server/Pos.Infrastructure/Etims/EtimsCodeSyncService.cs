using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Etims;

/// <summary>
/// See IEtimsCodeSyncService for the split rationale: this is the persistence/upsert
/// layer over IEtimsService's raw HTTP fetches. Both sync methods share the same shape —
/// read the watermark, fetch, upsert in a transaction, only advance the watermark once
/// the upsert has actually been saved.
/// </summary>
public sealed class EtimsCodeSyncService : IEtimsCodeSyncService
{
    private readonly PosDbContext _context;
    private readonly IEtimsService _etimsService;
    private readonly ILogger<EtimsCodeSyncService> _logger;

    public EtimsCodeSyncService(PosDbContext context, IEtimsService etimsService, ILogger<EtimsCodeSyncService> logger)
    {
        _context = context;
        _etimsService = etimsService;
        _logger = logger;
    }

    public async Task<EtimsSyncSummary> SyncCodesAsync(CancellationToken cancellationToken = default)
    {
        // Captured BEFORE the fetch, not after — this becomes the new watermark on
        // success. Using the fetch's start time (not completion time) means the next
        // sync's window slightly overlaps this one rather than risking a gap if
        // something changed on KRA's side mid-request; the upsert logic is idempotent
        // (matched on KRA's own natural keys), so re-processing an overlap is harmless.
        var requestStartedAt = DateTime.UtcNow;

        var state = await _context.EtimsSyncStates
            .FirstOrDefaultAsync(s => s.SyncKey == EtimsSyncKeys.Codes, cancellationToken);
        if (state is null)
        {
            state = new EtimsSyncState { SyncKey = EtimsSyncKeys.Codes };
            _context.EtimsSyncStates.Add(state);
        }

        var lastReqDt = state.LastSuccessfulSyncAt ?? EtimsSyncKeys.EpochLastReqDt;
        var fetch = await _etimsService.FetchCodesAsync(lastReqDt, cancellationToken);

        if (!fetch.Success)
        {
            state.LastAttemptAt = requestStartedAt;
            state.LastAttemptSucceeded = false;
            state.LastAttemptMessage = fetch.ErrorMessage ?? $"Failed with result code {fetch.ResultCode}";
            state.LastAttemptRecordCount = 0;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("eTIMS code sync failed: {Message}", state.LastAttemptMessage);
            return new EtimsSyncSummary(false, 0, state.LastAttemptMessage, null);
        }

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Load the full existing set up front rather than one query per class — the
        // code-list dataset is small (a few dozen classes, at most a few hundred codes
        // total per the spec's own scope), so this is cheap and avoids N+1 queries.
        var existingClasses = await _context.EtimsCodeClasses
            .Include(c => c.Codes)
            .ToDictionaryAsync(c => c.CdCls, cancellationToken);

        var upserted = 0;
        foreach (var clsDto in fetch.CodeClasses)
        {
            if (!existingClasses.TryGetValue(clsDto.CdCls, out var cls))
            {
                cls = new EtimsCodeClass { CdCls = clsDto.CdCls };
                _context.EtimsCodeClasses.Add(cls);
                existingClasses[clsDto.CdCls] = cls;
            }

            cls.CdClsNm = clsDto.CdClsNm;
            cls.CdClsDesc = clsDto.CdClsDesc;
            cls.UserDfnNm1 = clsDto.UserDfnNm1;
            cls.UserDfnNm2 = clsDto.UserDfnNm2;
            cls.UserDfnNm3 = clsDto.UserDfnNm3;
            cls.UseYn = clsDto.UseYn;
            cls.UpdatedAt = requestStartedAt;
            upserted++;

            var existingCodes = cls.Codes.ToDictionary(c => c.Cd);
            foreach (var codeDto in clsDto.Codes)
            {
                if (!existingCodes.TryGetValue(codeDto.Cd, out var code))
                {
                    code = new EtimsCode { Cd = codeDto.Cd, EtimsCodeClass = cls };
                    cls.Codes.Add(code);
                    existingCodes[codeDto.Cd] = code;
                }

                code.CdNm = codeDto.CdNm;
                code.CdDesc = codeDto.CdDesc;
                code.SrtOrd = codeDto.SrtOrd;
                code.UseYn = codeDto.UseYn;
                code.UpdatedAt = requestStartedAt;
                upserted++;
            }
        }

        // Watermark only advances here, inside the same transaction as the data it
        // describes — if the upsert fails or the process dies before commit, the next
        // sync re-requests from the old watermark instead of silently skipping records.
        state.LastSuccessfulSyncAt = requestStartedAt;
        state.LastAttemptAt = requestStartedAt;
        state.LastAttemptSucceeded = true;
        state.LastAttemptMessage = $"Synced {fetch.CodeClasses.Count} code classes.";
        state.LastAttemptRecordCount = upserted;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation("eTIMS code sync succeeded: {Upserted} rows upserted across {Classes} classes.",
            upserted, fetch.CodeClasses.Count);

        return new EtimsSyncSummary(true, upserted, state.LastAttemptMessage, requestStartedAt);
    }

    public async Task<EtimsSyncSummary> SyncItemClassesAsync(CancellationToken cancellationToken = default)
    {
        var requestStartedAt = DateTime.UtcNow;

        var state = await _context.EtimsSyncStates
            .FirstOrDefaultAsync(s => s.SyncKey == EtimsSyncKeys.ItemClasses, cancellationToken);
        if (state is null)
        {
            state = new EtimsSyncState { SyncKey = EtimsSyncKeys.ItemClasses };
            _context.EtimsSyncStates.Add(state);
        }

        var lastReqDt = state.LastSuccessfulSyncAt ?? EtimsSyncKeys.EpochLastReqDt;

        // /itemClass/selectItemsClass caps each response at 1000 records — confirmed
        // against a sibling tax authority's spec for the same underlying platform, since
        // KRA's own PDF doesn't document the limit. There's no documented offset/page
        // parameter, only lastReqDt, so paging works by re-calling with each response's
        // own resultDt as the next lastReqDt. Because this dataset is bulk reference
        // data (likely with many records sharing the same original timestamp), this
        // approach isn't guaranteed to reach the true end — the loop below detects and
        // reports that rather than assuming success. iterationCap is a hard safety
        // limit, not an expected real count.
        const int pageSize = 1000;
        const int iterationCap = 200; // 200k records — comfortably above any plausible real count
        var seenCodes = new HashSet<string>();
        var allItems = new List<EtimsItemClassDto>();
        var iterations = 0;
        var stoppedReason = "complete";

        while (iterations < iterationCap)
        {
            iterations++;
            var fetch = await _etimsService.FetchItemClassesAsync(lastReqDt, cancellationToken);

            if (!fetch.Success)
            {
                // A failure partway through a multi-page pull still has earlier pages'
                // worth of real data sitting in allItems — but persisting a partial
                // dataset silently would be worse than persisting nothing, since a
                // caller can't tell "fully synced" from "stopped halfway" just by
                // looking at the table. Bail out without writing anything this run;
                // the watermark stays where it was, so the next attempt starts over
                // from the same point rather than resuming from a half-known state.
                state.LastAttemptAt = requestStartedAt;
                state.LastAttemptSucceeded = false;
                state.LastAttemptMessage = $"Failed on page {iterations} ({allItems.Count} items fetched before failure): " +
                    (fetch.ErrorMessage ?? $"result code {fetch.ResultCode}");
                state.LastAttemptRecordCount = 0;
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogWarning("eTIMS item-class sync failed: {Message}", state.LastAttemptMessage);
                return new EtimsSyncSummary(false, 0, state.LastAttemptMessage, null);
            }

            var newInThisPage = fetch.ItemClasses.Count(i => seenCodes.Add(i.ItemClsCd));
            allItems.AddRange(fetch.ItemClasses);

            _logger.LogInformation(
                "eTIMS item-class page {Page}: {Count} returned, {New} new, {Total} total so far.",
                iterations, fetch.ItemClasses.Count, newInThisPage, allItems.Count);

            if (fetch.ItemClasses.Count < pageSize)
            {
                // Fewer than a full page came back — the documented end-of-data signal.
                break;
            }

            if (newInThisPage == 0)
            {
                // Got a full page, but every record was one we already have — advancing
                // lastReqDt via resultDt isn't making progress. This is the "date-based
                // paging can't slice a same-timestamp backlog" scenario flagged above;
                // stop rather than loop until iterationCap for no reason.
                stoppedReason = "no-progress";
                _logger.LogWarning(
                    "eTIMS item-class sync stopped after {Total} records: got a full page with no new codes. " +
                    "The dataset may have more than this endpoint's lastReqDt-based paging can reach — see the " +
                    "comment on SyncItemClassesAsync.", allItems.Count);
                break;
            }

            if (string.IsNullOrWhiteSpace(fetch.ResultDt) ||
                !DateTime.TryParseExact(fetch.ResultDt, "yyyyMMddHHmmss", null,
                    System.Globalization.DateTimeStyles.None, out var nextReqDt))
            {
                // Can't page further without a valid resultDt to advance from — same
                // "stop and report clearly" approach as the no-progress case.
                stoppedReason = "no-resultDt";
                _logger.LogWarning(
                    "eTIMS item-class sync stopped after {Total} records: response had no usable resultDt to page from.",
                    allItems.Count);
                break;
            }

            lastReqDt = nextReqDt;
        }

        if (iterations >= iterationCap)
        {
            stoppedReason = "iteration-cap";
            _logger.LogWarning(
                "eTIMS item-class sync stopped after hitting the {Cap}-page safety cap ({Total} records) — " +
                "this almost certainly means something is wrong (e.g. resultDt not actually advancing), not that " +
                "the real dataset is this large.", iterationCap, allItems.Count);
        }

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        // This dataset is KRA's full product taxonomy — potentially tens of thousands of
        // rows. Loading the whole existing table into memory as one query is still far
        // cheaper than one round-trip per row, but if this table eventually grows large
        // enough to make that materialization itself a problem, the fix is chunked/
        // batched upserts (e.g. via a raw bulk-upsert), not per-row querying — flagging
        // here rather than pre-building that complexity before it's actually needed.
        var existing = await _context.EtimsItemClasses.ToDictionaryAsync(i => i.ItemClsCd, cancellationToken);

        var upserted = 0;
        foreach (var dto in allItems)
        {
            if (!existing.TryGetValue(dto.ItemClsCd, out var entity))
            {
                entity = new EtimsItemClass { ItemClsCd = dto.ItemClsCd };
                _context.EtimsItemClasses.Add(entity);
                existing[dto.ItemClsCd] = entity;
            }

            entity.ItemClsNm = dto.ItemClsNm;
            entity.ItemClsLvl = dto.ItemClsLvl;
            entity.TaxTyCd = dto.TaxTyCd;
            entity.MjrTgYn = dto.MjrTgYn;
            entity.UseYn = dto.UseYn;
            entity.UpdatedAt = requestStartedAt;
            upserted++;
        }

        // Only advance the watermark on a clean "complete" stop. A no-progress or
        // resultDt-failure stop means we genuinely don't know if everything was
        // retrieved — advancing the watermark in that case would make the gap
        // permanent (a later sync would only ever ask for records after this point,
        // never going back to find whatever was missed). Leaving it where it was means
        // the next sync attempt starts over and gets a fresh chance.
        var advanceWatermark = stoppedReason == "complete";

        state.LastSuccessfulSyncAt = advanceWatermark ? requestStartedAt : state.LastSuccessfulSyncAt;
        state.LastAttemptAt = requestStartedAt;
        state.LastAttemptSucceeded = true;
        state.LastAttemptMessage = advanceWatermark
            ? $"Synced {allItems.Count} item classes across {iterations} page(s)."
            : $"Synced {allItems.Count} item classes across {iterations} page(s), but stopped early ({stoppedReason}) — " +
              "watermark NOT advanced, next sync will retry from the same point. See server logs for details.";
        state.LastAttemptRecordCount = upserted;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation("eTIMS item-class sync finished ({StoppedReason}): {Upserted} rows upserted across {Pages} page(s).",
            stoppedReason, upserted, iterations);

        return new EtimsSyncSummary(advanceWatermark, upserted, state.LastAttemptMessage, advanceWatermark ? requestStartedAt : null);
    }
}