namespace Pos.Application.Features.Products;

/// <summary>
/// Request body for POST /api/products/assign-etims-classification — the write side of
/// the Step 26 classification picker. One classification code applied to a batch of
/// product ids; see ProductsController.AssignEtimsClassification for why this stays
/// "one code per batch" rather than "one code per category".
/// </summary>
public class AssignEtimsClassificationRequest
{
    public List<Guid> ProductIds { get; set; } = new();

    /// <summary>Must match an EtimsItemClass.ItemClsCd with a non-null TaxTyCd (i.e. a
    /// leaf-level classification) — validated server-side, not trusted from the client.</summary>
    public string ItemClsCd { get; set; } = string.Empty;
}