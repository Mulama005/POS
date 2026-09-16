using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pos.Application.Features.Products;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Etims;
using Pos.Application.Common.Interfaces;
using System.Security.Claims;
namespace Pos.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly PosDbContext _context;
    private readonly IStorageService _storageService;
    private readonly IAuditService _auditService;
    private readonly IEtimsService _etimsService;
    private readonly EtimsOptions _etimsOptions;

    public ProductsController(
    PosDbContext context,
    IAuditService auditService,
    IStorageService storageService,
    IEtimsService etimsService,
    IOptions<EtimsOptions> etimsOptions)
{
    _context = context;
    _storageService = storageService;
    _auditService = auditService;
    _etimsService = etimsService;
    _etimsOptions = etimsOptions.Value;
}

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? category = null,
        [FromQuery] bool? etimsClassified = null)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.StockUnits)
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                p.Name.Contains(search) ||
                p.Sku.Contains(search) ||
                (p.Barcode != null && p.Barcode.Contains(search)));
        }

        if (category.HasValue)
        {
            query = query.Where(p => p.CategoryId == category.Value);
        }

        if (etimsClassified.HasValue)
        {
            query = etimsClassified.Value
                ? query.Where(p => p.EtimsItemClassificationCode != null)
                : query.Where(p => p.EtimsItemClassificationCode == null);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Barcode = p.Barcode,
                Name = p.Name,
                Description = p.Description,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null
                    ? p.Category.Name
                    : string.Empty,
                CostPrice = p.CostPrice,
                SalePrice = p.SalePrice,
                TaxClass = p.TaxClass,
                ImageUrl = p.ImageUrl,
                ReorderThreshold = p.ReorderThreshold,
                WarrantyMonths = p.WarrantyMonths,
                IsActive = p.IsActive,
                StockCount =
                    p.BulkQuantityOnHand +
                    p.StockUnits.Count(u => u.Status == "InStock"),

                EtimsItemClassificationCode =
                    p.EtimsItemClassificationCode,

                EtimsItemClassificationName =
                    _context.EtimsItemClasses
                        .Where(e =>
                            e.ItemClsCd ==
                            p.EtimsItemClassificationCode)
                        .Select(e => e.ItemClsNm)
                        .FirstOrDefault(),

                EtimsTaxTypeCode = p.EtimsTaxTypeCode,
                EtimsClassifiedAt = p.EtimsClassifiedAt
            })
            .ToListAsync();

        return Ok(new
        {
            items,
            total,
            page,
            pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.StockUnits)
            .FirstOrDefaultAsync(p =>
                p.Id == id &&
                p.IsActive);

        if (product == null)
            return NotFound();

        string? etimsClassificationName = null;

        if (product.EtimsItemClassificationCode is not null)
        {
            etimsClassificationName =
                await _context.EtimsItemClasses
                    .Where(e =>
                        e.ItemClsCd ==
                        product.EtimsItemClassificationCode)
                    .Select(e => e.ItemClsNm)
                    .FirstOrDefaultAsync();
        }

        var dto = MapToDto(
            product,
            etimsClassificationName);

        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Create(
        [FromForm] CreateProductRequest request,
        IFormFile? image)
    {
        var category =
            await _context.Categories.FindAsync(request.CategoryId);

        if (category == null)
            return BadRequest("Invalid category");

        if (await _context.Products.AnyAsync(
                p => p.Sku == request.Sku))
        {
            return BadRequest("SKU already exists");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = request.Sku,
            Barcode = request.Barcode,
            Name = request.Name,
            Description = request.Description,
            CategoryId = request.CategoryId,
            CostPrice = request.CostPrice,
            SalePrice = request.SalePrice,
            TaxClass = request.TaxClass,
            ReorderThreshold = request.ReorderThreshold,
            WarrantyMonths = request.WarrantyMonths,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        if (image != null)
        {
            using var stream = image.OpenReadStream();

            var fileName =
                $"{product.Id}_{Guid.NewGuid()}" +
                $"{Path.GetExtension(image.FileName)}";

            product.ImageUrl =
                await _storageService.UploadFileAsync(
                    stream,
                    fileName,
                    string.IsNullOrWhiteSpace(image.ContentType)
                        ? "application/octet-stream"
                        : image.ContentType);
        }

        _context.Products.Add(product);

        await _context.SaveChangesAsync();

        var createdProduct =
            await _context.Products
                .Include(p => p.Category)
                .Include(p => p.StockUnits)
                .FirstOrDefaultAsync(p =>
                    p.Id == product.Id);

        var userId =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
            return Unauthorized();

        var currentUserId = Guid.Parse(userId);

        await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "PRODUCT_CREATED",
            entityName: "Product",
            entityId: product.Id,
            details:
                $"Created product {product.Sku} - {product.Name}"
        );

        var dto = MapToDto(createdProduct!);

        return CreatedAtAction(
            nameof(Get),
            new { id = product.Id },
            dto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromForm] CreateProductRequest request)
    {
        var product =
            await _context.Products.FindAsync(id);

        if (product == null)
            return NotFound();

        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.CostPrice = request.CostPrice;
        product.SalePrice = request.SalePrice;
        product.TaxClass = request.TaxClass;
        product.ReorderThreshold = request.ReorderThreshold;
        product.WarrantyMonths = request.WarrantyMonths;
        product.UpdatedAt = DateTime.UtcNow;

        var userId =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
            return Unauthorized();

        var currentUserId = Guid.Parse(userId);

        await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "PRODUCT_UPDATED",
            entityName: "Product",
            entityId: product.Id,
            details:
                $"Updated product {product.Sku} at {product.UpdatedAt}"
        );

        await _context.SaveChangesAsync();

        return Ok(MapToDto(product));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product =
            await _context.Products.FindAsync(id);

        if (product == null)
            return NotFound();

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var userId =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
            return Unauthorized();

        var currentUserId = Guid.Parse(userId);

        await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "PRODUCT_DELETED",
            entityName: "Product",
            entityId: product.Id,
            details:
                $"Deactivated product {product.Sku} - {product.Name}"
        );

        return NoContent();
    }

    /// <summary>
    /// Assigns one KRA eTIMS classification to one or more products.
    ///
    /// A classification is considered assignable when it has no child
    /// classification in the synced KRA taxonomy.
    ///
    /// TaxTyCd is deliberately NOT used to determine whether a node is
    /// a leaf because KRA leaf classifications can legitimately have
    /// a null TaxTyCd.
    /// </summary>
    [HttpPost("assign-etims-classification")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> AssignEtimsClassification(
        [FromBody] AssignEtimsClassificationRequest request)
    {
        if (request.ProductIds is null ||
            request.ProductIds.Count == 0)
        {
            return BadRequest(new
            {
                message =
                    "At least one product must be selected."
            });
        }

        if (string.IsNullOrWhiteSpace(request.ItemClsCd))
        {
            return BadRequest(new
            {
                message =
                    "ItemClsCd is required."
            });
        }

        var itemClsCd = request.ItemClsCd.Trim();

        var classification =
            await _context.EtimsItemClasses
                .FirstOrDefaultAsync(e =>
                    e.ItemClsCd == itemClsCd);

        if (classification is null)
        {
            return BadRequest(new
            {
                message =
                    $"'{itemClsCd}' was not found in the synced " +
                    "KRA taxonomy. Run a code sync if this is " +
                    "a recently-added classification."
            });
        }

        /*
         * Determine whether this classification has children.
         *
         * KRA's synced hierarchy observed in the database:
         *
         * Level 1 -> Level 2 : first 2 digits
         * Level 2 -> Level 3 : first 4 digits
         * Level 3 -> Level 4 : first 6 digits
         * Level 4 -> Level 5 : first 8 digits
         * Level 5            : no children
         *
         * Do NOT use TaxTyCd as a leaf indicator.
         */
        var hasChildren = classification.ItemClsLvl switch
        {
            1 => await _context.EtimsItemClasses
                .AnyAsync(e =>
                    e.ItemClsLvl == 2 &&
                    e.ItemClsCd.StartsWith(
                        classification.ItemClsCd.Substring(0, 2))),

            2 => await _context.EtimsItemClasses
                .AnyAsync(e =>
                    e.ItemClsLvl == 3 &&
                    e.ItemClsCd.StartsWith(
                        classification.ItemClsCd.Substring(0, 4))),

            3 => await _context.EtimsItemClasses
                .AnyAsync(e =>
                    e.ItemClsLvl == 4 &&
                    e.ItemClsCd.StartsWith(
                        classification.ItemClsCd.Substring(0, 6))),

            4 => await _context.EtimsItemClasses
                .AnyAsync(e =>
                    e.ItemClsLvl == 5 &&
                    e.ItemClsCd.StartsWith(
                        classification.ItemClsCd.Substring(0, 8))),

            5 => false,

            _ => false
        };

        if (hasChildren)
        {
            return BadRequest(new
            {
                message =
                    $"'{itemClsCd}' ({classification.ItemClsNm}) " +
                    "is a parent classification and cannot be " +
                    "assigned to a product. Pick the most specific " +
                    "classification available."
            });
        }

        var products =
            await _context.Products
                .Where(p =>
                    request.ProductIds.Contains(p.Id) &&
                    p.IsActive)
                .ToListAsync();

        var missingIds =
            request.ProductIds
                .Except(products.Select(p => p.Id))
                .ToList();

        if (missingIds.Count > 0)
        {
            return BadRequest(new
            {
                message =
                    $"{missingIds.Count} product id(s) were not " +
                    "found or are inactive.",
                missingIds
            });
        }

        var userId =
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId == null)
            return Unauthorized();

        var currentUserId = Guid.Parse(userId);
        var now = DateTime.UtcNow;

        var reclassified =
            products
                .Where(p =>
                    p.EtimsItemClassificationCode is not null &&
                    p.EtimsItemClassificationCode !=
                        classification.ItemClsCd)
                .Select(p => p.Sku)
                .ToList();

        foreach (var product in products)
        {
            product.EtimsItemClassificationCode =
                classification.ItemClsCd;

            /*
             * TaxTyCd is classification metadata.
             * It is allowed to be null.
             */
            product.EtimsTaxTypeCode =
                classification.TaxTyCd;

            product.EtimsClassifiedAt = now;
            product.EtimsClassifiedByUserId =
                currentUserId;

            product.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();

        var skuList =
            string.Join(
                ", ",
                products
                    .Select(p => p.Sku)
                    .Take(20));

        var truncated =
            products.Count > 20
                ? $" (+{products.Count - 20} more)"
                : "";

        var details =
            $"Assigned {classification.ItemClsCd} " +
            $"({classification.ItemClsNm}) to " +
            $"{products.Count} product(s): " +
            $"{skuList}{truncated}.";

        if (reclassified.Count > 0)
        {
            details +=
                $" Reclassified (had a different code before): " +
                $"{string.Join(", ", reclassified)}.";
        }

        await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "PRODUCTS_ETIMS_CLASSIFIED",
            entityName: "Product",
            entityId: Guid.Empty,
            details: details
        );

        return Ok(new
        {
            updatedProductIds =
                products.Select(p => p.Id),

            itemClsCd =
                classification.ItemClsCd,

            itemClsNm =
                classification.ItemClsNm,

            taxTyCd =
                classification.TaxTyCd,

            reclassifiedCount =
                reclassified.Count
        });
    }

    private ProductDto MapToDto(
        Product p,
        string? etimsItemClassificationName = null)
    {
        return new ProductDto
        {
            Id = p.Id,
            Sku = p.Sku,
            Barcode = p.Barcode,
            Name = p.Name,
            Description = p.Description,
            CategoryId = p.CategoryId,
            CategoryName =
                p.Category?.Name ?? string.Empty,
            CostPrice = p.CostPrice,
            SalePrice = p.SalePrice,
            TaxClass = p.TaxClass,
            ImageUrl = p.ImageUrl,
            ReorderThreshold = p.ReorderThreshold,
            WarrantyMonths = p.WarrantyMonths,
            IsActive = p.IsActive,
            StockCount = p.StockQuantity,

            EtimsItemClassificationCode =
                p.EtimsItemClassificationCode,

            EtimsItemClassificationName =
                etimsItemClassificationName,

            EtimsTaxTypeCode =
                p.EtimsTaxTypeCode,

            EtimsClassifiedAt =
                p.EtimsClassifiedAt
        };
    }

    /// <summary>
    /// Free-text search across name, SKU, and barcode
    /// for the checkout search box.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<object>());
        }

        var term = q.Trim();

        var results =
            await _context.Products
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    (
                        EF.Functions.ILike(
                            p.Name ?? "",
                            $"%{term}%") ||

                        EF.Functions.ILike(
                            p.Sku ?? "",
                            $"%{term}%") ||

                        EF.Functions.ILike(
                            p.Barcode ?? "",
                            $"%{term}%")
                    ))
                .OrderBy(p => p.Name)
                .Take(25)
                .Select(p => new ProductDtos
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Barcode = p.Barcode ?? string.Empty,
                    Name = p.Name,
                    CategoryId = p.CategoryId,
                    CategoryName =
                        p.Category != null
                            ? p.Category.Name
                            : string.Empty,
                    SalePrice = p.SalePrice,
                    TaxClass = p.TaxClass,
                    StockQuantity =
                        p.BulkQuantityOnHand +
                        p.StockUnits.Count(
                            u => u.Status == "InStock"),
                    ImageUrl = p.ImageUrl
                })
                .ToListAsync(cancellationToken);

        return Ok(results);
    }

    /// <summary>
    /// Exact barcode match for a physical scanner input.
    /// </summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> LookupByBarcode(
        [FromQuery] string barcode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest(
                "barcode is required.");
        }

        var product =
            await _context.Products
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.Barcode == barcode.Trim())
                .Select(p => new ProductDtos
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Barcode =
                        p.Barcode ?? string.Empty,
                    Name = p.Name,
                    CategoryId = p.CategoryId,
                    CategoryName =
                        p.Category != null
                            ? p.Category.Name
                            : string.Empty,
                    SalePrice = p.SalePrice,
                    TaxClass = p.TaxClass,
                    StockQuantity =
                        p.BulkQuantityOnHand +
                        p.StockUnits.Count(
                            u => u.Status == "InStock"),
                    ImageUrl = p.ImageUrl
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (product is null)
        {
            return NotFound(new
            {
                message =
                    $"No active product with barcode " +
                    $"'{barcode}'."
            });
        }

        return Ok(product);
    }

    [HttpPost("import-csv/preview")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> PreviewCsv(
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(
                "No file uploaded");
        }

        using var reader =
            new StreamReader(file.OpenReadStream());

        using var csv =
            new CsvReader(
                reader,
                CultureInfo.InvariantCulture);

        var records =
            csv.GetRecords<ProductImportDto>()
                .ToList();

        var existingSkus =
            await _context.Products
                .Select(p => p.Sku)
                .ToHashSetAsync();

        var preview =
            records
                .Select(r =>
                {
                    var isDuplicate =
                        existingSkus.Contains(r.Sku);

                    var action =
                        isDuplicate
                            ? "Skip"
                            : "Create";

                    return new ProductPreview
                    {
                        Row = r,
                        IsDuplicate = isDuplicate,
                        Action = action
                    };
                })
                .ToList();

        return Ok(new
        {
            preview,
            totalRows = preview.Count
        });
    }

    [HttpPost("import-csv/commit")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> CommitCsv(
        [FromBody] List<ProductImportDto> rows)
    {
        var categories =
            await _context.Categories
                .ToDictionaryAsync(
                    c => c.Name,
                    c => c.Id);

        var products =
            new List<Product>();

        foreach (var row in rows)
        {
            if (!categories.TryGetValue(
                    row.CategoryName,
                    out var catId))
            {
                return BadRequest(
                    $"Category '{row.CategoryName}' not found");
            }

            if (await _context.Products.AnyAsync(
                    p => p.Sku == row.Sku))
            {
                continue;
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Sku = row.Sku,
                Barcode = row.Barcode,
                Name = row.Name,
                CategoryId = catId,
                CostPrice = row.CostPrice,
                SalePrice = row.SalePrice,
                TaxClass =
                    ParseTaxClass(row.TaxClass),
                ReorderThreshold =
                    row.ReorderThreshold,
                WarrantyMonths =
                    row.WarrantyMonths,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            products.Add(product);
        }

        await _context.Products
            .AddRangeAsync(products);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            created = products.Count
        });
    }

    /// <summary>
    /// CSV cells are plain text. Matches the enum by name
    /// (case-insensitive) or underlying number and falls
    /// back to Standard for blank/unrecognised values.
    /// </summary>
    private static TaxClass ParseTaxClass(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TaxClass.Standard;
        }

        var normalized =
            value
                .Trim()
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty);

        return Enum.TryParse<TaxClass>(
            normalized,
            ignoreCase: true,
            out var parsed)
                ? parsed
                : TaxClass.Standard;
    }

    [HttpGet("{productId}/price")]
    [Authorize]
    public async Task<IActionResult> GetPrice(
        Guid productId,
        [FromQuery] Guid? customerId = null)
    {
        var product =
            await _context.Products
                .FindAsync(productId);

        if (product == null)
            return NotFound();

        decimal finalPrice =
            product.SalePrice;

        if (customerId.HasValue)
        {
            var customer =
                await _context.Customers
                    .FindAsync(customerId.Value);

            if (!string.IsNullOrEmpty(
                    customer?.PricingTier))
            {
                var tierPrice =
                    await _context.ProductTierPrices
                        .Include(tp => tp.Tier)
                        .FirstOrDefaultAsync(tp =>
                            tp.ProductId == productId &&
                            tp.Tier.Name ==
                                customer.PricingTier);

                if (tierPrice != null)
                {
                    finalPrice =
                        tierPrice.Price;
                }
            }
        }

        return Ok(new
        {
            productId,
            price = finalPrice
        });
    }
}