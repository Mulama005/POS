using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Authorization;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

/// <summary>
/// Read-only product lookup — built as the minimum surface Step 24 (checkout) needs to
/// scan/search for items. Step 18 (full product CRUD, image upload, CSV bulk import) is
/// expected to extend this controller with write endpoints, not replace it — the two GET
/// endpoints here are the contract checkout depends on, so keep their shapes stable.
/// Any authenticated register-capable role can read the catalog; only Step 18's write
/// endpoints need to be Manager/Admin-restricted.
/// </summary>
[ApiController]
[Route("api/products")]
[Authorize(Roles = RoleGroups.RegisterCapableRoles)]
public sealed class ProductsController : ControllerBase
{
    private readonly PosDbContext _db;

    public ProductsController(PosDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Free-text search across name, SKU, and barcode for the checkout search box.
    /// Inactive products are excluded — nothing they cover should be sellable.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<object>());
        }

        var term = q.Trim();

        var results = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && (
                EF.Functions.ILike(p.Name, $"%{term}%") ||
                EF.Functions.ILike(p.Sku, $"%{term}%") ||
                EF.Functions.ILike(p.Barcode, $"%{term}%")))
            .OrderBy(p => p.Name)
            .Take(25)
            .Select(ProductSummaryProjection())
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    /// <summary>
    /// Exact barcode match for a physical scanner input — the checkout screen calls this
    /// (not /search) whenever the scan input receives a full barcode, since a scan should
    /// add the item immediately rather than show a results list.
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> LookupByBarcode([FromQuery] string barcode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest("barcode is required.");
        }

        var product = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Barcode == barcode.Trim())
            .Select(ProductSummaryProjection())
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return NotFound(new { message = $"No active product with barcode '{barcode}'." });
        }

        return Ok(product);
    }

    private static System.Linq.Expressions.Expression<Func<Product, ProductSummaryDto>> ProductSummaryProjection()
    {
        return p => new ProductSummaryDto(
            p.Id,
            p.Sku,
            p.Barcode,
            p.Name,
            p.CategoryId,
            p.Category.Name,
            p.SalePrice,
            p.TaxClass.ToString(),
            p.StockQuantity,
            p.ImageUrl);
    }
}