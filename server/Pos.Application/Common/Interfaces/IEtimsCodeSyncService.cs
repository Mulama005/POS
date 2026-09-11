namespace Pos.Application.Common.Interfaces;

public sealed record EtimsSyncSummary(
    bool Success,
    int RecordsUpserted,
    string? Message,
    DateTime? NewWatermark);

/// <summary>
/// Persistence layer over IEtimsService's raw code/item-class fetches — reads/writes
/// EtimsSyncState to manage the incremental `lastReqDt` watermark, and upserts fetched
/// records into EtimsCodeClass/EtimsCode/EtimsItemClass. IEtimsService itself never
/// touches the database (see its own doc comments); this is deliberately the layer that
/// does, kept separate so the HTTP-wrapper and the persistence/upsert logic can be
/// reasoned about (and tested) independently.
/// </summary>
public interface IEtimsCodeSyncService
{
    /// <summary>Syncs KRA's general reference code lists (Tax Type, Packaging Unit,
    /// etc) into EtimsCodeClass/EtimsCode.</summary>
    Task<EtimsSyncSummary> SyncCodesAsync(CancellationToken cancellationToken = default);

    /// <summary>Syncs KRA's product classification taxonomy into EtimsItemClass. This is
    /// the large dataset — expect this to take noticeably longer than SyncCodesAsync on
    /// a first (full) run.</summary>
    Task<EtimsSyncSummary> SyncItemClassesAsync(CancellationToken cancellationToken = default);
}