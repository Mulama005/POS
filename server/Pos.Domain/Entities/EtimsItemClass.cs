using Pos.Domain.Common;

namespace Pos.Domain.Entities;

/// <summary>
/// A single node in KRA's item classification taxonomy (UNSPSC-like — see the
/// VSCU Specification Document v2.0, section 3.3.2.2 response sample: entries like
/// "Paper products no use" at level 3 and "Non metallic sonic welded structural
/// assemblies" at level 5). Every Product will eventually need one of these assigned
/// (the deepest/most specific applicable level) before it can be sold compliantly —
/// that wiring is Step 26's next slice, not this one. This table is populated by syncing
/// against /itemClass/selectItemsClass; expect this to be a large, mostly-leaf-level
/// table (KRA's full taxonomy runs into the tens of thousands of codes), which is why
/// it's synced and stored locally rather than queried live per product.
/// </summary>
public class EtimsItemClass : BaseEntity
{
    /// <summary>KRA's classification code, e.g. "5022110801". This is the natural key
    /// from KRA's side — unique, and what FetchItemClassesAsync's upsert matches on.</summary>
    public string ItemClsCd { get; set; } = string.Empty;

    public string ItemClsNm { get; set; } = string.Empty;

    /// <summary>Depth in KRA's classification hierarchy — higher numbers are more
    /// specific. The spec's own sample shows levels 3, 4, and 5 for genuinely different
    /// codes, so this is a real hierarchy depth, not a fixed schema level to filter on
    /// blindly.</summary>
    public int ItemClsLvl { get; set; }

    /// <summary>The VAT tax type this classification implies, when KRA provides one —
    /// see EtimsCodeClass for "04"/Tax Type's own code list (A/B/C/D/E). Null in the
    /// spec's own sample for higher (less specific) levels — only fully leaf-level
    /// classifications reliably carry this.</summary>
    public string? TaxTyCd { get; set; }

    /// <summary>Whether KRA flags this as a frequently-used/major-target item class.
    /// Informational only — not required for a valid submission.</summary>
    public bool? MjrTgYn { get; set; }

    public bool UseYn { get; set; } = true;
}