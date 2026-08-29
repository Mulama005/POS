using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Features.Products;
using Pos.Infrastructure.Persistence;
using Pos.Domain.Entities;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly PosDbContext _context;

    public StockController(PosDbContext context)
    {
        _context = context;
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