using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Features.Products;
using Pos.Infrastructure.Persistence;
using Pos.Domain.Entities;
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