using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Authorization;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Identity;
using Pos.Infrastructure.Persistence;
using System.Security.Claims;

namespace Pos.Api.Controllers;

/// <summary>
/// Step 24 — checkout/cart completion. The cart itself lives entirely client-side (offline
/// store) until the cashier hits "Complete sale"; this controller is only ever called at
/// that final moment, which is deliberate — it's what lets a sale in progress survive a
/// lost connection.
///
/// Pricing/tax are always computed here from the current Product row, never trusted from
/// the client, so a tampered request can't change what a customer is actually charged.
///
/// VAT assumption: Product.SalePrice is treated as VAT-inclusive (the shelf/receipt price a
/// customer actually pays), matching standard Kenyan retail practice. The 16% component is
/// backed out of that price for Standard-rated items. Confirm this matches how the shop
/// prices its shelf tags before Step 26 (eTIMS) locks in the same assumption.
/// </summary>
[ApiController]
[Route("api/sales")]
[Authorize(Roles = RoleGroups.RegisterCapableRoles)]
public sealed class SalesController : ControllerBase
{
    private const decimal StandardVatRate = 0.16m;
    private static readonly HashSet<string> AcceptedPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cash", "Mpesa", "Card",
    };

    private readonly PosDbContext _db;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDiscountApprovalStore _approvalStore;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _config;
    private readonly ILogger<SalesController> _logger;
    private readonly IAuditService _auditService;

    public SalesController(
        PosDbContext db,
        IAuthorizationService authorizationService,
        IDiscountApprovalStore approvalStore,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration config,
        IAuditService auditService,
        ILogger<SalesController> logger)
    {
        _db = db;
        _authorizationService = authorizationService;
        _approvalStore = approvalStore;
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _logger = logger;
        _auditService = auditService;
    }

    /// <summary>
    /// A Manager/Admin re-enters their own credentials here to approve a discount above the
    /// configured threshold. Returns a short-lived token the checkout screen then includes
    /// in the /api/sales completion request. Does not touch the caller's own session.
    /// </summary>
    [HttpPost("approve-discount")]
    public async Task<IActionResult> ApproveDiscount([FromBody] ApproveDiscountRequest request)
    {
        const string genericError = "Invalid email or password.";

        var appUser = await _userManager.FindByEmailAsync(request.Email);
        if (appUser is null)
        {
            return Unauthorized(new { message = genericError });
        }

        var passwordCheck = await _signInManager.CheckPasswordSignInAsync(appUser, request.Password, lockoutOnFailure: true);
        if (!passwordCheck.Succeeded)
        {
            return Unauthorized(new
            {
                message = passwordCheck.IsLockedOut
                    ? "Account locked due to repeated failed attempts."
                    : genericError,
            });
        }

        var domainUser = await _db.DomainUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == appUser.Id);
        if (domainUser is null || !domainUser.IsActive)
        {
            return Unauthorized(new { message = genericError });
        }

        if (domainUser.Role is not (RegisterUserRole.Manager or RegisterUserRole.Admin))
        {
            return Forbid();
        }

        var token = _approvalStore.CreateApproval(domainUser.Id);
        
        /*await _auditService.LogAsync(
            userId: domainUser.Id,
            actionType: "SALE_APPROVED",
            entityName: "Sale",
            entityId: sale.Id,
            details: $"Approved discount for sale {sale.Id}"
        );*/
        
        return Ok(new { approvalToken = token });
    }

    /// <summary>Completes a sale: validates stock and register/till state, computes pricing
    /// and tax server-side, enforces the discount-approval threshold, records payments, and
    /// decrements stock — all in one transaction.</summary>
    [HttpPost]
    public async Task<IActionResult> Complete([FromBody] CompleteSaleRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("A sale must have at least one item.");
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, request.RegisterId, PolicyNames.RegisterScoped);
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var cashierIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(cashierIdClaim, out var cashierId))
        {
            return Unauthorized();
        }

        var register = await _db.Registers.FirstOrDefaultAsync(r => r.Id == request.RegisterId, cancellationToken);
        if (register is null || !register.IsActive)
        {
            return BadRequest("Register not found or inactive.");
        }

        var openTillSession = await _db.TillSessions
            .FirstOrDefaultAsync(t => t.RegisterId == request.RegisterId && t.Status == TillSessionStatus.Open, cancellationToken);
        if (openTillSession is null)
        {
            return BadRequest("This register's till is closed. Open the till before completing a sale.");
        }

        if (request.CustomerId is not null &&
            !await _db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            return BadRequest("Customer not found.");
        }

        // Load every requested product in one round trip; also catches duplicate-line
        // requests naturally since we key by ProductId below.
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var missing = productIds.Where(id => !products.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            return BadRequest(new { message = "One or more products were not found.", productIds = missing });
        }

        var inactive = request.Items.Where(i => !products[i.ProductId].IsActive).Select(i => i.ProductId).ToList();
        if (inactive.Count > 0)
        {
            return BadRequest(new { message = "One or more products are no longer active.", productIds = inactive });
        }

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                return BadRequest($"Quantity must be positive for product {item.ProductId}.");
            }
            if (item.DiscountAmount < 0)
            {
                return BadRequest($"Discount cannot be negative for product {item.ProductId}.");
            }
        }

        // Sum requested quantity per product (a client could split the same product across
        // two lines) so the stock check is accurate against the total being sold.
        var requestedQtyByProduct = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var insufficientStock = requestedQtyByProduct
            .Where(kv => products[kv.Key].StockQuantity < kv.Value)
            .Select(kv => new { productId = kv.Key, requested = kv.Value, available = products[kv.Key].StockQuantity })
            .ToList();
        if (insufficientStock.Count > 0)
        {
            return Conflict(new { message = "Insufficient stock for one or more items.", items = insufficientStock });
        }

        if (request.CartDiscountAmount < 0)
        {
            return BadRequest("Cart discount cannot be negative.");
        }

        // --- Pricing: server is the sole source of truth for price and tax. ---
        var lines = new List<(SaleItemRequest Request, Product Product, decimal RawAmount, decimal AfterLineDiscount)>();
        decimal rawSubtotal = 0m;
        decimal afterLineDiscountsTotal = 0m;

        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];
            var rawAmount = product.SalePrice * item.Quantity;
            var lineDiscount = Math.Min(item.DiscountAmount, rawAmount); // can't discount below zero
            var afterLineDiscount = rawAmount - lineDiscount;

            lines.Add((item, product, rawAmount, afterLineDiscount));
            rawSubtotal += rawAmount;
            afterLineDiscountsTotal += afterLineDiscount;
        }

        var cartDiscount = Math.Min(request.CartDiscountAmount, afterLineDiscountsTotal);
        var totalDiscount = (rawSubtotal - afterLineDiscountsTotal) + cartDiscount;

        var thresholdKes = _config.GetValue<decimal?>("Sales:DiscountApprovalThresholdKes") ?? 500m;
        Guid? discountApprovedByUserId = null;
        if (totalDiscount > thresholdKes)
        {
            if (string.IsNullOrWhiteSpace(request.DiscountApprovalToken) ||
                !_approvalStore.TryConsumeApproval(request.DiscountApprovalToken, out var approverId))
            {
                return StatusCode(StatusCodes.Status428PreconditionRequired, new
                {
                    message = $"Discount of {totalDiscount:F2} exceeds the {thresholdKes:F2} threshold and needs Manager/Admin approval.",
                    requiresDiscountApproval = true,
                });
            }
            discountApprovedByUserId = approverId;
            
            
        }

        // Distribute the cart-level discount proportionally across lines (by each line's
        // share of the post-line-discount total) so tax is computed on the true taxable
        // amount per line, not just the aggregate.
        decimal saleTaxTotal = 0m;
        decimal saleTotal = 0m;
        var saleItems = new List<SaleItem>();
        var itemResponses = new List<SaleItemResponse>();

        foreach (var (itemRequest, product, rawAmount, afterLineDiscount) in lines)
        {
            var shareOfCartDiscount = afterLineDiscountsTotal > 0
                ? cartDiscount * (afterLineDiscount / afterLineDiscountsTotal)
                : 0m;
            var finalLineAmount = Math.Round(afterLineDiscount - shareOfCartDiscount, 2, MidpointRounding.AwayFromZero);

            var lineTax = product.TaxClass == "standard"
                ? Math.Round(finalLineAmount - (finalLineAmount / (1 + StandardVatRate)), 2, MidpointRounding.AwayFromZero)
                : 0m;

            var totalLineDiscount = rawAmount - finalLineAmount;

            var saleItem = new SaleItem
            {
                ProductId = product.Id,
                StockUnitId = itemRequest.StockUnitId,
                Quantity = itemRequest.Quantity,
                UnitPrice = product.SalePrice,
                DiscountAmount = totalLineDiscount,
                TaxAmount = lineTax,
                LineTotal = finalLineAmount,
            };
            saleItems.Add(saleItem);

            itemResponses.Add(new SaleItemResponse(
                product.Id, product.Name, itemRequest.StockUnitId, itemRequest.Quantity,
                product.SalePrice, totalLineDiscount, lineTax, finalLineAmount));

            saleTaxTotal += lineTax;
            saleTotal += finalLineAmount;

            //product.StockQuantity -= itemRequest.Quantity;
        }

        // --- Payments ---
        if (request.Payments is null || request.Payments.Count == 0)
        {
            return BadRequest("At least one payment is required.");
        }

        foreach (var payment in request.Payments)
        {
            if (payment.Method.Equals("Credit", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Credit (Deni) payments aren't available yet — that needs Step 32's credit-ledger module.");
            }
            if (!AcceptedPaymentMethods.Contains(payment.Method))
            {
                return BadRequest($"Unknown payment method '{payment.Method}'.");
            }
            if (payment.Amount <= 0)
            {
                return BadRequest("Payment amount must be positive.");
            }
        }

        var paidTotal = request.Payments.Sum(p => p.Amount);
        if (Math.Abs(paidTotal - saleTotal) > 0.01m)
        {
            return BadRequest(new
            {
                message = "Payment total does not match the sale total.",
                saleTotal,
                paidTotal,
            });
        }

        var sale = new Sale
        {
            RegisterId = register.Id,
            TillSessionId = openTillSession.Id,
            CashierId = cashierId,
            CustomerId = request.CustomerId,
            SaleDate = DateTime.UtcNow,
            Subtotal = rawSubtotal,
            DiscountTotal = totalDiscount,
            TaxTotal = saleTaxTotal,
            Total = saleTotal,
            Status = SaleStatus.Completed,
            DiscountApprovedByUserId = discountApprovedByUserId,
            IsSynced = true,
        };
        
        await _auditService.LogAsync(
            userId: cashierId,
            actionType: "DISCOUNT_APPLIED",
            entityName: "Sale",
            entityId: sale.Id,
            details: $"Discount of {totalDiscount/rawSubtotal}% applied to sale {sale.Id} by {discountApprovedByUserId}, amount: {totalDiscount}"
        );
        
        sale.Items = saleItems;
        foreach (var item in saleItems)
        {
            item.SaleId = sale.Id;
        }

        var paymentResponses = new List<PaymentResponse>();
        foreach (var paymentRequest in request.Payments)
        {
            var method = Enum.Parse<PaymentMethod>(paymentRequest.Method, ignoreCase: true);

            // Cash is settled the instant it's counted at the register. Mpesa/Card start
            // Pending — Steps 27/28 wire up the real Daraja/Pesapal callbacks that flip
            // these to Success/Failed asynchronously.
            var status = method == PaymentMethod.Cash ? PaymentStatus.Success : PaymentStatus.Pending;

            var payment = new Payment
            {
                SaleId = sale.Id,
                Method = method,
                Amount = paymentRequest.Amount,
                Status = status,
                MpesaPhoneNumber = paymentRequest.MpesaPhoneNumber,
                ProcessedAt = status == PaymentStatus.Success ? DateTime.UtcNow : null,
            };
            sale.Payments.Add(payment);
            paymentResponses.Add(new PaymentResponse(method.ToString(), payment.Amount, payment.Status.ToString(), payment.ExternalReference));
            
            await _auditService.LogAsync(
                userId: cashierId,
                actionType: "PAYMENT_RECEIVED",
                entityName: "Payment",
                entityId: payment.Id,
                details: $"Payment of {payment.Amount} via {method} for sale {sale.Id}"
            );
        }

        _db.Sales.Add(sale);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "Sale {SaleId} completed at register {RegisterId} by cashier {CashierId} — total {Total}",
            sale.Id, register.Id, cashierId, sale.Total);
        
        await _auditService.LogAsync(
            userId: cashierId,
            actionType: "SALE_CREATED",
            entityName: "Sale",
            entityId: sale.Id,
            details: $"Sale {sale.Id} total: {sale.Total}"
        );

        return Ok(new CompleteSaleResponse(
            sale.Id, sale.SaleDate, sale.Subtotal, sale.DiscountTotal, sale.TaxTotal, sale.Total,
            sale.Status.ToString(), itemResponses, paymentResponses));
    }
}