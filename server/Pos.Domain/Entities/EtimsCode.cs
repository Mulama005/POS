using Pos.Domain.Common;

namespace Pos.Domain.Entities;

/// <summary>
/// An individual reference code within an EtimsCodeClass — e.g. within Tax Type ("04"),
/// the codes "A"/"B"/"C"/"D"/"E" map to the different VAT rate bands. Populated by
/// syncing against /code/selectCodes (VSCU spec section 3.3.2.1) — never created
/// manually.
/// </summary>
public class EtimsCode : BaseEntity
{
    public Guid EtimsCodeClassId { get; set; }
    public EtimsCodeClass EtimsCodeClass { get; set; } = null!;

    /// <summary>KRA's own code value within its parent classification, e.g. "B" for the
    /// 16% VAT band under Tax Type. Combined with EtimsCodeClassId this is the natural
    /// key from KRA's side — unique per class, and what FetchCodesAsync's upsert matches
    /// on.</summary>
    public string Cd { get; set; } = string.Empty;

    public string CdNm { get; set; } = string.Empty;
    public string? CdDesc { get; set; }
    public int SrtOrd { get; set; }

    public string? UserDfnCd1 { get; set; }
    public string? UserDfnCd2 { get; set; }
    public string? UserDfnCd3 { get; set; }

    public bool UseYn { get; set; } = true;
}