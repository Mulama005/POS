namespace Pos.Api.Controllers;

/// <summary>
/// Shape returned by both /api/products/search and /api/products/lookup — this is the
/// contract Step 18's full product module should keep stable when it extends this
/// controller, since the checkout screen (Step 24) is built against it.
/// </summary>
public sealed record ProductSummaryDto(
    Guid Id,
    string Sku,
    string Barcode,
    string Name,
    Guid CategoryId,
    string CategoryName,
    decimal SalePrice,
    string TaxClass,
    int StockQuantity,
    string? ImageUrl);