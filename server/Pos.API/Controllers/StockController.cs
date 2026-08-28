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

    [HttpPost("receive")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> ReceiveStock([FromBody] ReceiveStockRequest request)
    {
        var product = await _context.Products.FindAsync(request.ProductId);
        if (product == null) return NotFound("Product not found");

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