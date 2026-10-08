using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Interfaces;
using Pos.Application.Features.Products;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.Controllers;

/// <summary>
/// Bulk product onboarding from a CSV. Two steps so nothing is written until the manager has
/// seen the result: /preview validates every row and reports a per-row outcome, /commit
/// re-validates (the client is never trusted) and writes inside one transaction.
/// Replaces the older import-csv endpoints, which are left in place but no longer used.
/// </summary>
[ApiController]
[Route("api/products/import")]
[Authorize(Roles = "Manager,Admin")]
public sealed class ProductImportController : ControllerBase
{
    private const int MaxRows = 5000;
    private readonly PosDbContext _db;
    private readonly IAuditService _audit;

    public ProductImportController(PosDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet("template")]
    public IActionResult Template()
    {
        const string csv =
            "Sku,Barcode,Name,CategoryName,CostPrice,SalePrice,TaxClass,ReorderThreshold,WarrantyMonths,OpeningQuantity,SerialTracked\r\n" +
            "PHN-0001,6001234567890,Samsung Galaxy A15 128GB,Phones,17500,21999,Standard,3,12,0,yes\r\n" +
            ",,USB-C Cable 1m,Accessories,150,350,Standard,20,6,50,no\r\n";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "products-import-template.csv");
    }

    [HttpPost("preview")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Preview(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "No file uploaded." });

        List<ProductImportRow> rows;
        try { rows = ReadCsv(file); }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Couldn't read that file as a CSV: {ex.Message}" });
        }
        if (rows.Count == 0) return BadRequest(new { message = "The file has no data rows." });
        if (rows.Count > MaxRows) return BadRequest(new { message = $"Too many rows ({rows.Count}). Split the file into batches of {MaxRows} or fewer." });

        var results = await ValidateAsync(rows, ct);
        return Ok(Summarise(results));
    }

    [HttpPost("commit")]
    public async Task<IActionResult> Commit([FromBody] List<ProductImportRow> rows, CancellationToken ct)
    {
        if (rows is null || rows.Count == 0) return BadRequest(new { message = "Nothing to import." });
        if (rows.Count > MaxRows) return BadRequest(new { message = $"Too many rows ({rows.Count})." });

        var results = await ValidateAsync(rows, ct);
        var toCreate = results.Where(r => r.Action == "Create").ToList();
        if (toCreate.Count == 0)
            return BadRequest(new { message = "No importable rows - fix the errors and preview again.", summary = Summarise(results) });

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        // Categories (matched case-insensitively; created when missing).
        var categories = (await _db.Categories.ToListAsync(ct))
            .GroupBy(c => c.Name.ToLower()).ToDictionary(g => g.Key, g => g.First());
        var categoriesCreated = 0;
        foreach (var r in toCreate)
        {
            var name = r.Row.CategoryName!.Trim();
            if (categories.ContainsKey(name.ToLower())) continue;
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = name,
                IsActive = true,
                RequiresSerialTracking = ParseBool(r.Row.SerialTracked),
                CreatedAt = now,
            };
            _db.Categories.Add(category);
            categories[name.ToLower()] = category;
            categoriesCreated++;
        }

        var skuCounters = await LoadSkuCountersAsync(ct);
        var openingStockApplied = 0;
        foreach (var r in toCreate)
        {
            var category = categories[r.Row.CategoryName!.Trim().ToLower()];
            var sku = string.IsNullOrWhiteSpace(r.Row.Sku)
                ? NextSku(category.Name, skuCounters)
                : r.Row.Sku.Trim();

            var qty = category.RequiresSerialTracking ? 0 : ParseInt(r.Row.OpeningQuantity) ?? 0;
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Sku = sku,
                Barcode = string.IsNullOrWhiteSpace(r.Row.Barcode) ? null : r.Row.Barcode.Trim(),
                Name = r.Row.Name!.Trim(),
                CategoryId = category.Id,
                CostPrice = ParseMoney(r.Row.CostPrice) ?? 0m,
                SalePrice = ParseMoney(r.Row.SalePrice)!.Value,
                TaxClass = ParseTaxClass(r.Row.TaxClass),
                ReorderThreshold = ParseInt(r.Row.ReorderThreshold) ?? 5,
                WarrantyMonths = ParseInt(r.Row.WarrantyMonths) ?? category.DefaultWarrantyMonths,
                BulkQuantityOnHand = qty,
                IsActive = true,
                CreatedAt = now,
            };
            if (qty > 0) openingStockApplied++;
            _db.Products.Add(product);
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _audit.LogAsync(userId, "PRODUCT_IMPORT", "Product", Guid.NewGuid(),
            $"Imported {toCreate.Count} products ({categoriesCreated} new categories, {openingStockApplied} with opening stock); " +
            $"{results.Count(r => r.Action == "Skip")} skipped, {results.Count(r => r.Action == "Error")} errors.");

        return Ok(new
        {
            created = toCreate.Count,
            categoriesCreated,
            openingStockApplied,
            skipped = results.Count(r => r.Action == "Skip"),
            errors = results.Count(r => r.Action == "Error"),
        });
    }

    // ---------------------------------------------------------------- validation

    private async Task<List<ProductImportRowResult>> ValidateAsync(List<ProductImportRow> rows, CancellationToken ct)
    {
        var existingSkus = (await _db.Products.Select(p => p.Sku).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingBarcodes = (await _db.Products
                .Where(p => p.Barcode != null && p.Barcode != "").Select(p => p.Barcode!).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categories = (await _db.Categories.ToListAsync(ct))
            .GroupBy(c => c.Name.ToLower()).ToDictionary(g => g.Key, g => g.First());

        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ProductImportRowResult>();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var res = new ProductImportRowResult { RowNumber = i + 2, Row = row }; // +2: header is line 1

            var sku = row.Sku?.Trim();
            var barcode = row.Barcode?.Trim();
            var name = row.Name?.Trim();
            var categoryName = row.CategoryName?.Trim();

            if (string.IsNullOrEmpty(name)) res.Errors.Add("Name is required.");
            if (string.IsNullOrEmpty(categoryName)) res.Errors.Add("CategoryName is required.");

            var sale = ParseMoney(row.SalePrice);
            if (sale is null || sale <= 0) res.Errors.Add("SalePrice must be a number greater than 0.");
            var cost = string.IsNullOrWhiteSpace(row.CostPrice) ? 0m : ParseMoney(row.CostPrice);
            if (cost is null || cost < 0) res.Errors.Add("CostPrice must be a number (or blank).");
            else if (cost == 0) res.Warnings.Add("No CostPrice - the 'never sell below cost' check won't protect this item.");
            if (sale is > 0 && cost is > 0 && sale < cost)
                res.Errors.Add("SalePrice is below CostPrice - checkout would refuse to sell it.");

            if (!string.IsNullOrWhiteSpace(row.ReorderThreshold) && ParseInt(row.ReorderThreshold) is null or < 0)
                res.Errors.Add("ReorderThreshold must be a whole number.");
            if (!string.IsNullOrWhiteSpace(row.WarrantyMonths) && ParseInt(row.WarrantyMonths) is null or < 0)
                res.Errors.Add("WarrantyMonths must be a whole number.");
            if (!string.IsNullOrWhiteSpace(row.OpeningQuantity) && ParseInt(row.OpeningQuantity) is null or < 0)
                res.Errors.Add("OpeningQuantity must be a whole number.");

            if (!string.IsNullOrWhiteSpace(row.TaxClass) && !TryParseTaxClass(row.TaxClass, out _))
                res.Warnings.Add($"Unknown TaxClass '{row.TaxClass}' - will use Standard.");

            if (!string.IsNullOrEmpty(sku) && sku.Length > 100) res.Errors.Add("Sku is too long (max 100).");

            // Duplicate handling
            var skipped = false;
            if (!string.IsNullOrEmpty(sku))
            {
                if (existingSkus.Contains(sku)) { skipped = true; res.Warnings.Add("SKU already exists - row skipped."); }
                else if (!seenSkus.Add(sku)) res.Errors.Add("SKU appears more than once in this file.");
            }
            if (!string.IsNullOrEmpty(barcode))
            {
                if (existingBarcodes.Contains(barcode) && !skipped) res.Errors.Add("Barcode already belongs to another product.");
                else if (!seenBarcodes.Add(barcode) && !skipped) res.Errors.Add("Barcode appears more than once in this file.");
            }

            // Category
            if (!string.IsNullOrEmpty(categoryName))
            {
                if (categories.TryGetValue(categoryName.ToLower(), out var cat))
                {
                    var qty = ParseInt(row.OpeningQuantity) ?? 0;
                    if (cat.RequiresSerialTracking && qty > 0)
                        res.Warnings.Add("Serial-tracked category - OpeningQuantity ignored. Receive these units with serial numbers under Receive Stock.");
                }
                else
                {
                    res.WillCreateCategory = true;
                    var serial = ParseBool(row.SerialTracked);
                    res.Warnings.Add($"New category '{categoryName}' will be created ({(serial ? "serial-tracked" : "bulk quantity")}).");
                    if (serial && (ParseInt(row.OpeningQuantity) ?? 0) > 0)
                        res.Warnings.Add("Serial-tracked category - OpeningQuantity ignored. Receive these units under Receive Stock.");
                }
            }

            res.ResolvedSku = string.IsNullOrEmpty(sku) ? "(auto)" : sku;
            res.Action = res.Errors.Count > 0 ? "Error" : skipped ? "Skip" : "Create";
            results.Add(res);
        }
        return results;
    }

    private static object Summarise(List<ProductImportRowResult> results) => new
    {
        rows = results,
        totalRows = results.Count,
        willCreate = results.Count(r => r.Action == "Create"),
        willSkip = results.Count(r => r.Action == "Skip"),
        errors = results.Count(r => r.Action == "Error"),
        newCategories = results.Where(r => r.WillCreateCategory && r.Action == "Create")
            .Select(r => r.Row.CategoryName!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
    };

    // ---------------------------------------------------------------- SKU generation

    /// <summary>Highest existing numeric suffix per prefix ("PHO-0007" -> PHO: 7).</summary>
    private async Task<Dictionary<string, int>> LoadSkuCountersAsync(CancellationToken ct)
    {
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var skus = await _db.Products.Select(p => p.Sku).ToListAsync(ct);
        foreach (var sku in skus)
        {
            var m = Regex.Match(sku, @"^([A-Za-z]{3})-(\d+)$");
            if (!m.Success) continue;
            var n = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var prefix = m.Groups[1].Value.ToUpperInvariant();
            if (!counters.TryGetValue(prefix, out var cur) || n > cur) counters[prefix] = n;
        }
        return counters;
    }

    private static string NextSku(string categoryName, Dictionary<string, int> counters)
    {
        var letters = new string(categoryName.Where(char.IsLetter).ToArray()).ToUpperInvariant();
        var prefix = (letters + "XXX")[..3];
        counters.TryGetValue(prefix, out var cur);
        counters[prefix] = ++cur;
        return $"{prefix}-{cur:D4}";
    }

    // ---------------------------------------------------------------- parsing helpers

    private static List<ProductImportRow> ReadCsv(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream(), detectEncodingFromByteOrderMarks: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null,
            BadDataFound = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => Regex.Replace(args.Header ?? "", @"[\s_\-]", "").ToLowerInvariant(),
        };
        using var csv = new CsvReader(reader, config);
        return csv.GetRecords<ProductImportRow>()
            .Where(r => !string.IsNullOrWhiteSpace(r.Name) || !string.IsNullOrWhiteSpace(r.Sku) || !string.IsNullOrWhiteSpace(r.SalePrice))
            .ToList();
    }

    private static decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = Regex.Replace(value, @"(?i)kes|ksh|[\s,]", "");
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? Math.Round(d, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value.Replace(",", "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static bool ParseBool(string? value) =>
        value?.Trim().ToLowerInvariant() is "yes" or "y" or "true" or "1";

    private static bool TryParseTaxClass(string? value, out TaxClass tax)
    {
        tax = TaxClass.Standard;
        if (string.IsNullOrWhiteSpace(value)) return true;
        var normalized = value.Trim().Replace("-", "").Replace(" ", "");
        return Enum.TryParse(normalized, ignoreCase: true, out tax);
    }

    private static TaxClass ParseTaxClass(string? value) =>
        TryParseTaxClass(value, out var t) ? t : TaxClass.Standard;
}