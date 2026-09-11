using Pos.Domain.Common;

namespace Pos.Domain.Entities;

/// <summary>
/// A KRA VSCU "code classification" — a category of reference codes, e.g. Tax Type,
/// Packaging Unit, Unit of Quantity, Currency (see VSCU Specification Document v2.0,
/// section 4 for the full catalogue of classification types KRA maintains). Populated
/// by syncing against /code/selectCodes (section 3.3.2.1) — never created manually,
/// since these values are dictated by KRA, not chosen locally.
/// </summary>
public class EtimsCodeClass : BaseEntity
{
    /// <summary>KRA's own 2-character classification code, e.g. "04" for Tax Type. This
    /// is the natural key from KRA's side — unique, and what FetchCodesAsync's upsert
    /// matches on, NOT the local Guid Id.</summary>
    public string CdCls { get; set; } = string.Empty;

    public string CdClsNm { get; set; } = string.Empty;
    public string? CdClsDesc { get; set; }

    public string? UserDfnNm1 { get; set; }
    public string? UserDfnNm2 { get; set; }
    public string? UserDfnNm3 { get; set; }

    /// <summary>Whether KRA currently considers this classification active ("Y"/"N" in
    /// their API, translated to bool at the service boundary — see EtimsService).</summary>
    public bool UseYn { get; set; } = true;

    public ICollection<EtimsCode> Codes { get; set; } = new List<EtimsCode>();
}