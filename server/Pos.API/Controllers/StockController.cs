using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Features.Products;
using Pos.Infrastructure.Persistence;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Application.Common.Interfaces;
using System.Security.Claims;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly PosDbContext _context;
    private readonly IAuditService _auditService;

    public StockController(IAuditService auditService, PosDbContext context)
    {
        _context = context;
        _auditService = auditService;
    }

    /// <summary>
    /// Serial-tracked receiving — one StockUnit row per physical item, each with its own
    /// serial number (and optionally IMEI). Only valid for categories with
    /// RequiresSerialTracking = true; bulk categories use POST /api/stock/receive-bulk
    /// instead, since tracking individual serials for a box of cables isn't meaningful.
    /// </summary>
    [HttpPost("receive")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> ReceiveStock([FromBody] ReceiveStockRequest request)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId);
        if (product == null) return NotFound("Product not found");

        if (!product.Category.RequiresSerialTracking)
        {
            return BadRequest(
                $"'{product.Name}' is in a bulk-tracked category ({product.Category.Name}) — " +
                "use POST /api/stock/receive-bulk instead of receiving individual serial numbers.");
        }

        if (request.SerialNumbers.Count == 0)
        {
            return BadRequest("At least one serial number is required.");
        }

        var units = request.SerialNumbers.Select((sn, idx) => new StockUnit
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            SerialNumber = sn,
            Imei = request.Imei != null && idx < request.Imei.Count ? request.Imei[idx] : null,
            PurchaseDate = request.PurchaseDate,
            Status = "InStock",
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.StockUnits.AddRange(units);
        await _context.SaveChangesAsync();
        
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();
        var currentUserId = Guid.Parse(userId);
        
        /*await _auditService.LogAsync(
            userId: currentUserId,
            actionType: "STOCK_RECEIVED",
            entityName: "Product",
            entityId: units.ProductId,
            details: $"Received {serialNumbers.Count} units for product {units.ProductId}"
        );*/
        
        return Ok(new { added = units.Count });
    }

    /// <summary>
    /// Bulk receiving for non-serialized categories (cables, chargers, and similar) — just
    /// adds to Product.BulkQuantityOnHand. Only valid for categories with
    /// RequiresSerialTracking = false; serialized categories use POST /api/stock/receive.
    /// </summary>
    [HttpPost("receive-bulk")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> ReceiveBulkStock([FromBody] ReceiveBulkStockRequest request)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId);
        if (product == null) return NotFound("Product not found");

        if (product.Category.RequiresSerialTracking)
        {
            return BadRequest(
                $"'{product.Name}' is in a serial-tracked category ({product.Category.Name}) — " +
                "use POST /api/stock/receive with per-unit serial numbers instead.");
        }

        if (request.Quantity <= 0)
        {
            return BadRequest("Quantity must be greater than zero.");
        }

        product.BulkQuantityOnHand += request.Quantity;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { productId = product.Id, newQuantity = product.BulkQuantityOnHand });
    }

    /// <summary>Records a purchase from a neighboring shop and receives it into stock.</summary>
    [HttpPost("source")]
    [Authorize(Roles = "Cashier,Manager,Admin")]
    public async Task<IActionResult> SourceStock([FromBody] SourceStockRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientTransactionId == Guid.Empty)
            return BadRequest("A purchase transaction ID is required.");

        var existing = await _context.SupplierSourcedPurchases.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ClientTransactionId, cancellationToken);
        if (existing is not null)
        {
            var inventory = await _context.Products.AsNoTracking()
                .Where(x => x.Id == existing.ProductId)
                .Select(x => new { x.Name, x.CostPrice, Quantity = x.BulkQuantityOnHand + x.StockUnits.Count(unit => unit.Status == "InStock") })
                .FirstAsync(cancellationToken);
            return Ok(new
            {
                purchaseId = existing.Id, productId = existing.ProductId, productName = inventory.Name,
                quantity = existing.Quantity, totalCost = existing.TotalCost,
                newQuantity = inventory.Quantity, averageCost = inventory.CostPrice, replayed = true
            });
        }

        var supplierName = request.SupplierName?.Trim();
        if (string.IsNullOrWhiteSpace(supplierName) || supplierName.Length > 160)
            return BadRequest("Enter the supplier shop name (up to 160 characters).");
        if (request.Quantity <= 0 || request.Quantity > 10000)
            return BadRequest("Quantity must be between 1 and 10,000.");
        if (request.UnitCost < 0 || request.UnitCost > 100_000_000m)
            return BadRequest("Enter a valid unit purchase cost.");
        var unitCost = Math.Round(request.UnitCost, 2, MidpointRounding.AwayFromZero);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var product = await _context.Products.Include(x => x.Category).Include(x => x.StockUnits)
            .FirstOrDefaultAsync(x => x.Id == request.ProductId && x.IsActive, cancellationToken);
        if (product is null) return NotFound("Active product not found.");

        var serialNumbers = (request.SerialNumbers ?? new List<string>()).Select(x => x.Trim()).ToList();
        var imeiNumbers = request.Imei ?? new List<string>();
        if (product.Category.RequiresSerialTracking)
        {
            if (serialNumbers.Count != request.Quantity || serialNumbers.Any(string.IsNullOrWhiteSpace) || serialNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serialNumbers.Count)
                return BadRequest("Add a unique serial number for every unit being sourced.");
            var normalizedSerials = serialNumbers.Select(x => x.ToUpperInvariant()).ToList();
            var existingSerial = await _context.StockUnits.AnyAsync(x => normalizedSerials.Contains(x.SerialNumber.ToUpper()), cancellationToken);
            if (existingSerial) return Conflict("One or more serial numbers are already recorded in stock.");
            if (imeiNumbers.Count > 0 && (imeiNumbers.Count != request.Quantity || imeiNumbers.Any(string.IsNullOrWhiteSpace) || imeiNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != imeiNumbers.Count))
                return BadRequest("If supplied, add a unique IMEI for every sourced unit.");
        }
        else if (serialNumbers.Count > 0 || imeiNumbers.Count > 0)
        {
            return BadRequest("This product is bulk-tracked; serial numbers and IMEIs are not used.");
        }

        var recordedAt = DateTime.UtcNow;
        var currentStock = product.BulkQuantityOnHand + product.StockUnits.Count(x => x.Status == "InStock");
        var currentInventoryCost = product.BulkQuantityOnHand * product.CostPrice + product.StockUnits
            .Where(x => x.Status == "InStock")
            .Sum(x => x.PurchaseCost ?? product.CostPrice);
        var totalCost = Math.Round(unitCost * request.Quantity, 2, MidpointRounding.AwayFromZero);
        product.CostPrice = currentStock == 0
            ? unitCost
            : Math.Round((currentInventoryCost + totalCost) / (currentStock + request.Quantity), 2, MidpointRounding.AwayFromZero);
        product.UpdatedAt = recordedAt;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (product.Category.RequiresSerialTracking)
            {
                var units = serialNumbers.Select((serial, index) => new StockUnit
                {
                    Id = Guid.NewGuid(), ProductId = product.Id, SerialNumber = serial,
                    Imei = imeiNumbers.Count > index ? imeiNumbers[index].Trim() : null,
                    PurchaseDate = recordedAt, PurchaseCost = unitCost,
                    Status = "InStock", CreatedAt = recordedAt
                });
                _context.StockUnits.AddRange(units);
            }
            else
            {
                product.BulkQuantityOnHand += request.Quantity;
            }

            _context.SupplierSourcedPurchases.Add(new SupplierSourcedPurchase
            {
                Id = request.ClientTransactionId,
                ProductId = product.Id,
                SupplierName = supplierName,
                Quantity = request.Quantity,
                UnitCost = unitCost,
                TotalCost = totalCost,
                Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
                PurchasedAt = recordedAt,
                RecordedByUserId = userId,
                CreatedAt = recordedAt
            });
            _context.InventoryAdjustments.Add(new InventoryAdjustment
            {
                ProductId = product.Id,
                AdjustmentType = InventoryAdjustmentType.Increase,
                Quantity = request.Quantity,
                Reason = $"Sourced from {supplierName}",
                AdjustedByUserId = userId,
                Timestamp = recordedAt,
                CreatedAt = recordedAt
            });

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await _auditService.LogAsync(userId, "SUPPLIER_STOCK_SOURCED", "SupplierSourcedPurchase", request.ClientTransactionId,
            $"Received {request.Quantity} × {product.Name} from {supplierName} at KES {unitCost:N2} each.");

        return Ok(new
        {
            purchaseId = request.ClientTransactionId,
            productId = product.Id,
            productName = product.Name,
            quantity = request.Quantity,
            totalCost,
            newQuantity = currentStock + request.Quantity,
            averageCost = product.CostPrice,
            replayed = false
        });
    }

    [HttpGet("warranty/{serialNumber}")]
    [Authorize]
    public async Task<IActionResult> WarrantyLookup(string serialNumber)
    {
        var unit = await _context.StockUnits
            .Include(u => u.Product)
            .FirstOrDefaultAsync(u => u.SerialNumber == serialNumber || (u.Imei != null && u.Imei == serialNumber));

        if (unit == null) return NotFound("Unit not found");

        var saleDate = unit.SaleDate ?? unit.PurchaseDate; // fallback if not yet sold
        var expiry = saleDate?.AddMonths(unit.Product.WarrantyMonths);
        var isUnder = saleDate.HasValue && expiry.HasValue && expiry.Value > DateTime.UtcNow;

        var warrantyInfo = new
        {
            name = unit.Product.Name,
            serial = unit.SerialNumber,
            saleDate = saleDate,
            warrantyMonths = unit.Product.WarrantyMonths,
            expiryDate = expiry,
            status = unit.Status,
            isUnderWarranty = isUnder
        };
        return Ok(warrantyInfo);
    }
}

public sealed record SourceStockRequest(
    Guid ClientTransactionId,
    Guid ProductId,
    string SupplierName,
    int Quantity,
    decimal UnitCost,
    IReadOnlyList<string>? SerialNumbers = null,
    IReadOnlyList<string>? Imei = null,
    string? Reference = null);
