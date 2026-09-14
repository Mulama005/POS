using Pos.Domain.Common;

namespace Pos.Domain.Entities;

/// <summary>
/// Tracks the incremental-sync watermark for a KRA reference dataset (codes,
/// item classes, etc). Per the VSCU spec (section 2.2, point 3): "To retrieve the
/// latest data from eTIMS API server, VSCU shall send the 'Date and Time' on which the
/// kind of data was finally received... So, the 'Date and Time' of final request should
/// be managed by TIS, and once successfully retrieved, TIS shall update the latest 'Date
/// and Time' to the related data for the next update." This table is that management —
/// one row per synced dataset, holding the timestamp to send as `lastReqDt` on the next
/// call.
/// </summary>
public class EtimsSyncState : BaseEntity
{
    /// <summary>Which dataset this row tracks — see EtimsSyncKeys for the fixed set of
    /// valid values. A plain string (not an enum) so a future dataset can be added
    /// without a schema change, but only EtimsSyncKeys' constants should ever be used as
    /// real values.</summary>
    public string SyncKey { get; set; } = string.Empty;

    /// <summary>The `lastReqDt` to send on the NEXT sync — only advanced after a sync's
    /// fetched records have been fully upserted and saved, never just after the HTTP call
    /// succeeds. Null means "never successfully synced" — FetchCodesAsync/
    /// FetchItemClassesAsync callers should fall back to EtimsSyncKeys.EpochLastReqDt in
    /// that case to request KRA's entire dataset.</summary>
    public DateTime? LastSuccessfulSyncAt { get; set; }

    /// <summary>When a sync was last attempted, successful or not — separate from
    /// LastSuccessfulSyncAt so a failed attempt doesn't silently look like "never run" on
    /// an admin diagnostics screen.</summary>
    public DateTime? LastAttemptAt { get; set; }

    public bool LastAttemptSucceeded { get; set; }
    public string? LastAttemptMessage { get; set; }
    public int LastAttemptRecordCount { get; set; }
}

/// <summary>Fixed set of valid EtimsSyncState.SyncKey values, and the shared "give me
/// everything" sentinel used as lastReqDt on a dataset's very first sync.</summary>
public static class EtimsSyncKeys
{
    public const string Codes = "Codes";
    public const string ItemClasses = "ItemClasses";

    /// <summary>Deliberately far enough in the past to predate any real KRA record —
    /// the spec's own worked examples use dates as early as 2018, so 2015 leaves margin
    /// without being so extreme it looks like a bug.</summary>
    public static readonly DateTime EpochLastReqDt = new(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}