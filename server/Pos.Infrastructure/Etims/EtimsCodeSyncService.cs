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
        var fetch = await _etimsService.FetchItemClassesAsync(lastReqDt, cancellationToken);

        if (!fetch.Success)
        {
            state.LastAttemptAt = requestStartedAt;
            state.LastAttemptSucceeded = false;
            state.LastAttemptMessage = fetch.ErrorMessage ?? $"Failed with result code {fetch.ResultCode}";
            state.LastAttemptRecordCount = 0;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("eTIMS item-class sync failed: {Message}", state.LastAttemptMessage);
            return new EtimsSyncSummary(false, 0, state.LastAttemptMessage, null);
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
        foreach (var dto in fetch.ItemClasses)
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

        state.LastSuccessfulSyncAt = requestStartedAt;
        state.LastAttemptAt = requestStartedAt;
        state.LastAttemptSucceeded = true;
        state.LastAttemptMessage = $"Synced {fetch.ItemClasses.Count} item classes.";
        state.LastAttemptRecordCount = upserted;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        _logger.LogInformation("eTIMS item-class sync succeeded: {Upserted} rows upserted.", upserted);

        return new EtimsSyncSummary(true, upserted, state.LastAttemptMessage, requestStartedAt);
    }
}