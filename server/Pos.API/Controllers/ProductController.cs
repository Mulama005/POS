using System.Globalization;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Features.Products;
using Pos.Domain.Entities;
using Pos.Infrastructure.Persistence;
using Pos.Api.Controllers;
using Pos.Application.Common.Interfaces;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly PosDbContext _context;
    private readonly IStorageService _storageService;

    public ProductsController(PosDbContext context, IStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, int pageSize = 20, string? search = null, Guid? category = null)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .Include(p => p.StockUnits)
            .Where(p => p.IsActive);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p =>
                p.Name.Contains(search) ||
                p.Sku.Contains(search) ||
                (p.Barcode != null && p.Barcode.Contains(search)));
        if (category.HasValue)
            query = query.Where(p => p.CategoryId == category.Value);

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
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                CostPrice = p.CostPrice,
                SalePrice = p.SalePrice,
                TaxClass = p.TaxClass,
                ImageUrl = p.ImageUrl,
                ReorderThreshold = p.ReorderThreshold,
                WarrantyMonths = p.WarrantyMonths,
                IsActive = p.IsActive,
                StockCount = p.StockUnits.Count(u => u.Status == "InStock")
            })
            .ToListAsync();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.StockUnits)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product == null) return NotFound();

        var dto = MapToDto(product);
        return Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Create([FromForm] CreateProductRequest request, IFormFile? image)
    {
        // Validate category exists
        var category = await _context.Categories.FindAsync(request.CategoryId);
        if (category == null) return BadRequest("Invalid category");

        // Check SKU uniqueness
        if (await _context.Products.AnyAsync(p => p.Sku == request.Sku))
            return BadRequest("SKU already exists");

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

        // Handle image upload (optional)
        if (image != null)
        {
            using var stream = image.OpenReadStream();
			var fileName = $"{product.Id}_{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
			product.ImageUrl = await _storageService.UploadFileAsync(stream, fileName);
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var createdProduct = await _context.Products
        .Include(p => p.Category)
        .Include(p => p.StockUnits)
        .FirstOrDefaultAsync(p => p.Id == product.Id);

    	var dto = MapToDto(createdProduct!);
    	return CreatedAtAction(nameof(Get), new { id = product.Id }, dto);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Update(Guid id, [FromForm] CreateProductRequest request)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        // Update fields (skip image for brevity)
        product.Name = request.Name;
        product.Description = request.Description;
        product.CategoryId = request.CategoryId;
        product.CostPrice = request.CostPrice;
        product.SalePrice = request.SalePrice;
        product.TaxClass = request.TaxClass;
        product.ReorderThreshold = request.ReorderThreshold;
        product.WarrantyMonths = request.WarrantyMonths;
        product.UpdatedAt = DateTime.UtcNow;

        // Handle image replacement if needed

        await _context.SaveChangesAsync();
        return Ok(MapToDto(product));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private ProductDto MapToDto(Product p)
    {
        return new ProductDto
        {
            Id = p.Id,
            Sku = p.Sku,
            Barcode = p.Barcode,
            Name = p.Name,
            Description = p.Description,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            CostPrice = p.CostPrice,
            SalePrice = p.SalePrice,
            TaxClass = p.TaxClass,
            ImageUrl = p.ImageUrl,
            ReorderThreshold = p.ReorderThreshold,
            WarrantyMonths = p.WarrantyMonths,
            IsActive = p.IsActive,
            StockCount = p.StockUnits?.Count(u => u.Status == "InStock") ?? 0
        };
    }

	/// <summary>
	/// Free-text search across name, SKU, and barcode for the checkout search box.
	/// Inactive products are excluded.
	/// </summary>
	[HttpGet("search")]
	public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
	{
    	if (string.IsNullOrWhiteSpace(q))
    	{
        return Ok(Array.Empty<object>());
    	}

    	var term = q.Trim();

    	var results = await _context.Products
        	.AsNoTracking()
        	.Where(p => p.IsActive && (
          	  	EF.Functions.ILike(p.Name ?? "", $"%{term}%") ||
            	EF.Functions.ILike(p.Sku ?? "", $"%{term}%") ||
            	EF.Functions.ILike(p.Barcode ?? "", $"%{term}%")))
        	.OrderBy(p => p.Name)
        	.Take(25)
        	.Select(p => new ProductDtos
        	{
            	Id = p.Id,
            	Sku = p.Sku,
            	Barcode = p.Barcode ?? string.Empty,
            	Name = p.Name,
            	CategoryId = p.CategoryId,
            	CategoryName = p.Category != null ? p.Category.Name : string.Empty,
            	SalePrice = p.SalePrice,
            	TaxClass = p.TaxClass,
            	StockQuantity = p.StockUnits.Count(u => u.Status == "InStock"), // compute stock count
            	ImageUrl = p.ImageUrl
        	})
        	.ToListAsync(cancellationToken);

    	return Ok(results);
	}

	/// <summary>
	/// Exact barcode match for a physical scanner input.
	/// </summary>
	[HttpGet("lookup")]
	public async Task<IActionResult> LookupByBarcode([FromQuery] string barcode, CancellationToken cancellationToken)
	{
    	if (string.IsNullOrWhiteSpace(barcode))
    	{
        	return BadRequest("barcode is required.");
    	}

    	var product = await _context.Products
        	.AsNoTracking()
        	.Where(p => p.IsActive && p.Barcode == barcode.Trim())
        	.Select(p => new ProductDtos
        	{
            	Id = p.Id,
            	Sku = p.Sku,
            	Barcode = p.Barcode ?? string.Empty, 
            	Name = p.Name,
            	CategoryId = p.CategoryId,
            	CategoryName = p.Category != null ? p.Category.Name : string.Empty,
            	SalePrice = p.SalePrice,
            	TaxClass = p.TaxClass,
            	StockQuantity = p.StockUnits.Count(u => u.Status == "InStock"),
            	ImageUrl = p.ImageUrl
        	})
        	.FirstOrDefaultAsync(cancellationToken);

    	if (product is null)
    	{
        	return NotFound(new { message = $"No active product with barcode '{barcode}'." });
    	}

    	return Ok(product);
	}

    [HttpPost("import-csv/preview")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> PreviewCsv(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded");

        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<ProductImportDto>().ToList();

        var existingSkus = await _context.Products.Select(p => p.Sku).ToHashSetAsync();

        var preview = records.Select(r =>
        {
            var isDuplicate = existingSkus.Contains(r.Sku);
            var action = isDuplicate ? "Skip" : "Create";
            return new ProductPreview
            {
                Row = r,
                IsDuplicate = isDuplicate,
                Action = action
            };
        }).ToList();

        return Ok(new { preview, totalRows = preview.Count });
    }

    [HttpPost("import-csv/commit")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> CommitCsv([FromBody] List<ProductImportDto> rows)
    {
        var categories = await _context.Categories.ToDictionaryAsync(c => c.Name, c => c.Id);
        var products = new List<Product>();

        foreach (var row in rows)
        {
            if (!categories.TryGetValue(row.CategoryName, out var catId))
                return BadRequest($"Category '{row.CategoryName}' not found");

            if (await _context.Products.AnyAsync(p => p.Sku == row.Sku))
                continue; // skip duplicates

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Sku = row.Sku,
                Barcode = row.Barcode,
                Name = row.Name,
                CategoryId = catId,
                CostPrice = row.CostPrice,
                SalePrice = row.SalePrice,
                TaxClass = row.TaxClass ?? "standard",
                ReorderThreshold = row.ReorderThreshold,
                WarrantyMonths = row.WarrantyMonths,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            products.Add(product);
        }

        await _context.Products.AddRangeAsync(products);
        await _context.SaveChangesAsync();
        return Ok(new { created = products.Count });
    }

    [HttpGet("{productId}/price")]
    [Authorize]
    public async Task<IActionResult> GetPrice(Guid productId, [FromQuery] Guid? customerId = null)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound();

        decimal finalPrice = product.SalePrice;

        if (customerId.HasValue)
        {
            var customer = await _context.Customers.FindAsync(customerId.Value);
            if (!string.IsNullOrEmpty(customer?.PricingTier))
            {
                var tierPrice = await _context.ProductTierPrices
                    .Include(tp => tp.Tier)
                    .FirstOrDefaultAsync(tp => tp.ProductId == productId && tp.Tier.Name == customer.PricingTier);
                if (tierPrice != null)
                    finalPrice = tierPrice.Price;
            }
        }

        return Ok(new { productId, price = finalPrice });
    }
	
}