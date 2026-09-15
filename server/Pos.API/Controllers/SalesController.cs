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
    private readonly IEtimsService _etimsService;


    public SalesController(
        PosDbContext db,
        IAuthorizationService authorizationService,
        IDiscountApprovalStore approvalStore,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration config,
        IAuditService auditService,
        ILogger<SalesController> logger,
        IEtimsService etimsService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _approvalStore = approvalStore;
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _logger = logger;
        _auditService = auditService;
        _etimsService = etimsService;
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
        if (request.ClientTransactionId == Guid.Empty)
        {
            return BadRequest("ClientTransactionId is required and must be a UUID.");
        }

        var existingSale = await _db.Sales
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == request.ClientTransactionId, cancellationToken);

        if (existingSale is not null)
        {
            if (existingSale.EtimsResultCode == "000" && existingSale.IsSynced)
            {
                return Ok(MapCompleteSaleResponse(existingSale));
            }

            var retryResult = await SubmitEtimsSaleAsync(existingSale, cancellationToken);
            if (!retryResult.Success)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = retryResult.ErrorMessage ?? retryResult.ResultMessage ?? "eTIMS sale submission failed.",
                    resultCode = retryResult.ResultCode,
                    saleId = existingSale.Id,
                    invoiceNumber = existingSale.EtimsInvoiceNumber
                });
            }

            return Ok(MapCompleteSaleResponse(existingSale));
        }

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
        // Category is needed to tell bulk vs serialized products apart; StockUnits is
        // needed both for the stock-sufficiency check (Product.StockQuantity reads it)
        // and to actually select which unit(s) get marked Sold below.
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.StockUnits)
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

        var etimsUnregistered = request.Items
            .Select(i => products[i.ProductId])
            .Where(p => string.IsNullOrWhiteSpace(p.EtimsItemCode) ||
                        p.EtimsRegisteredAt is null ||
                        string.IsNullOrWhiteSpace(p.EtimsItemClassificationCode))
            .Select(p => new { p.Id, p.Sku, p.Name })
            .Distinct()
            .ToList();

        if (etimsUnregistered.Count > 0)
        {
            return BadRequest(new
            {
                message = "One or more products are not registered with eTIMS. Register every product before selling it.",
                products = etimsUnregistered
            });
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

            // Serialized products (phones, and anything else in a category with
            // RequiresSerialTracking = true) are each an individually identified unit —
            // "3 of this phone" on one line doesn't mean anything, since each one needs
            // its own StockUnit marked Sold. The cashier should scan/add each unit as its
            // own line instead.
            var lineProduct = products[item.ProductId];
            if (lineProduct.Category.RequiresSerialTracking && item.Quantity != 1)
            {
                return BadRequest(
                    $"'{lineProduct.Name}' is serial-tracked — add each unit as its own line " +
                    $"(quantity must be 1) instead of a quantity of {item.Quantity}.");
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

            var lineTax = product.TaxClass == TaxClass.Standard
                ? Math.Round(finalLineAmount - (finalLineAmount / (1 + StandardVatRate)), 2, MidpointRounding.AwayFromZero)
                : 0m;

            var totalLineDiscount = rawAmount - finalLineAmount;

            // Consume stock now, inside the same transaction the sale itself is saved in.
            // Server picks which unit is sold rather than trusting itemRequest.StockUnitId —
            // the client can't be relied on to pick a unit that's actually still InStock.
            Guid? consumedStockUnitId = null;
            if (product.Category.RequiresSerialTracking)
            {
                // Validated above to be exactly 1 for serialized lines.
                var unitToSell = product.StockUnits
                    .Where(u => u.Status == "InStock")
                    .OrderBy(u => u.PurchaseDate ?? DateTime.MaxValue)
                    .ThenBy(u => u.CreatedAt)
                    .FirstOrDefault();

                if (unitToSell is null)
                {
                    // The aggregate stock check above already confirmed enough units
                    // exist — this only happens if two sales raced for the last unit of
                    // the same product between that check and here.
                    return Conflict(new
                    {
                        message = $"'{product.Name}' just sold out — no available unit left to sell.",
                        productId = product.Id,
                    });
                }

                unitToSell.Status = "Sold";
                unitToSell.SaleDate = DateTime.UtcNow;
                unitToSell.SalePrice = product.SalePrice;
                consumedStockUnitId = unitToSell.Id;
            }
            else
            {
                product.BulkQuantityOnHand -= itemRequest.Quantity;
            }

            var saleItem = new SaleItem
            {
                ProductId = product.Id,
                StockUnitId = consumedStockUnitId,
                Quantity = itemRequest.Quantity,
                UnitPrice = product.SalePrice,
                DiscountAmount = totalLineDiscount,
                TaxAmount = lineTax,
                LineTotal = finalLineAmount,
            };
            saleItems.Add(saleItem);

            itemResponses.Add(new SaleItemResponse(
                product.Id, product.Name, consumedStockUnitId, itemRequest.Quantity,
                product.SalePrice, totalLineDiscount, lineTax, finalLineAmount));

            saleTaxTotal += lineTax;
            saleTotal += finalLineAmount;
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

        var invoiceNumber = await _db.Database
            .SqlQueryRaw<long>(
                "SELECT nextval('\"EtimsInvoiceNumberSequence\"') AS \"Value\"")
            .SingleAsync(cancellationToken);

        var sale = new Sale
        {
            Id = request.ClientTransactionId,
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
            EtimsInvoiceNumber = invoiceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            IsSynced = false,
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
            paymentResponses.Add(new PaymentResponse(payment.Id, method.ToString(), payment.Amount, payment.Status.ToString(), payment.ExternalReference));
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
            "Sale {SaleId} completed locally at register {RegisterId} by cashier {CashierId} — total {Total}; submitting to eTIMS.",
            sale.Id, register.Id, cashierId, sale.Total);
        
        await _auditService.LogAsync(
            userId: cashierId,
            actionType: "SALE_CREATED",
            entityName: "Sale",
            entityId: sale.Id,
            details: $"Sale {sale.Id} total: {sale.Total}"
        );

        var etimsResult = await SubmitEtimsSaleAsync(sale, cancellationToken);
        if (!etimsResult.Success)
        {
            await _auditService.LogAsync(
                userId: cashierId,
                actionType: "ETIMS_SALE_SUBMISSION_FAILED",
                entityName: "Sale",
                entityId: sale.Id,
                details: etimsResult.ErrorMessage ?? etimsResult.ResultMessage ?? "Unknown eTIMS sales submission error.");

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = etimsResult.ErrorMessage ?? etimsResult.ResultMessage ?? "eTIMS sale submission failed.",
                resultCode = etimsResult.ResultCode,
                saleId = sale.Id,
                invoiceNumber = sale.EtimsInvoiceNumber
            });
        }

        await _auditService.LogAsync(
            userId: cashierId,
            actionType: "ETIMS_SALE_REGISTERED",
            entityName: "Sale",
            entityId: sale.Id,
            details: $"eTIMS accepted sale {sale.Id}; receipt {sale.EtimsReceiptNumber}; invoice {sale.EtimsInvoiceNumber}."
        );

        return Ok(MapCompleteSaleResponse(sale));
    }

    private async Task<EtimsSaleSaveResult> SubmitEtimsSaleAsync(
        Sale sale,
        CancellationToken cancellationToken)
    {
        var items = await _db.SaleItems
            .Where(i => i.SaleId == sale.Id)
            .Include(i => i.Product)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return new EtimsSaleSaveResult(false, null, null, null,
                long.TryParse(sale.EtimsInvoiceNumber, out var no) ? no : 0,
                null, null, null, null, null, null, null,
                "Cannot submit an eTIMS sale with no sale items.");
        }

        var customer = sale.CustomerId is null
            ? null
            : await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value, cancellationToken);

        if (!long.TryParse(sale.EtimsInvoiceNumber, out var invoiceNumber))
        {
            return new EtimsSaleSaveResult(false, null, null, null, 0, null, null, null, null, null, null, null,
                "The sale does not have a valid local eTIMS invoice number.");
        }

        var operatorId =
            User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.Identity?.Name
            ?? "AyiyaPOS";
        operatorId = operatorId.Trim();
        if (operatorId.Length > 20) operatorId = operatorId[..20];
        if (operatorId.Length == 0) operatorId = "AyiyaPOS";

        var operatorName =
            User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? "AyiyaPOS";
        operatorName = operatorName.Trim();
        if (operatorName.Length > 60) operatorName = operatorName[..60];
        if (operatorName.Length == 0) operatorName = "AyiyaPOS";

        var taxblA = 0m;
        var taxblB = 0m;
        var taxblC = 0m;
        var taxblD = 0m;
        var taxblE = 0m;
        var taxAmtA = 0m;
        var taxAmtB = 0m;
        var taxAmtC = 0m;
        var taxAmtD = 0m;
        var taxAmtE = 0m;

        var etimsItems = new List<EtimsSaleSaveItemRequest>();

        foreach (var (item, index) in items.Select((item, index) => (item, index)))
        {
            var product = item.Product;
            if (string.IsNullOrWhiteSpace(product.EtimsItemCode) ||
                product.EtimsRegisteredAt is null ||
                string.IsNullOrWhiteSpace(product.EtimsItemClassificationCode))
            {
                return new EtimsSaleSaveResult(false, null, null, null, invoiceNumber, null, null, null, null, null, null, null,
                    $"Product '{product.Name}' is not registered with eTIMS.");
            }

            var taxTypeCode = product.TaxClass switch
            {
                TaxClass.Standard => "B",
                TaxClass.ZeroRated => "C",
                TaxClass.Exempt => "A",
                _ => null
            };

            if (taxTypeCode is null)
            {
                return new EtimsSaleSaveResult(false, null, null, null, invoiceNumber, null, null, null, null, null, null, null,
                    $"Product '{product.Name}' has an unsupported tax class.");
            }

            var pkgUnitCode = product.EtimsPackagingUnitCode ?? "NT";
            var qtyUnitCode = product.EtimsQuantityUnitCode ?? "U";

            var taxableAmount = Math.Round(item.LineTotal - item.TaxAmount, 2, MidpointRounding.AwayFromZero);
            if (taxableAmount < 0) taxableAmount = 0;

            switch (taxTypeCode)
            {
                case "A": taxblA += taxableAmount; taxAmtA += item.TaxAmount; break;
                case "B": taxblB += taxableAmount; taxAmtB += item.TaxAmount; break;
                case "C": taxblC += taxableAmount; taxAmtC += item.TaxAmount; break;
                case "D": taxblD += taxableAmount; taxAmtD += item.TaxAmount; break;
                case "E": taxblE += taxableAmount; taxAmtE += item.TaxAmount; break;
            }

            var grossBeforeDiscount = item.UnitPrice * item.Quantity;
            var discountRate = grossBeforeDiscount > 0
                ? Math.Round((item.DiscountAmount / grossBeforeDiscount) * 100m, 2, MidpointRounding.AwayFromZero)
                : 0m;

            etimsItems.Add(new EtimsSaleSaveItemRequest(
                index + 1,
                product.EtimsItemClassificationCode,
                product.EtimsItemCode,
                product.Name,
                string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode,
                pkgUnitCode,
                item.Quantity,
                qtyUnitCode,
                item.Quantity,
                item.UnitPrice,
                Math.Round(item.UnitPrice * item.Quantity, 2, MidpointRounding.AwayFromZero),
                discountRate,
                item.DiscountAmount,
                taxTypeCode,
                taxableAmount,
                item.TaxAmount,
                item.LineTotal));
        }

        var paymentTypeCodes = sale.Payments
            .Select(p => p.Method switch
            {
                PaymentMethod.Cash => "01",
                PaymentMethod.Card => "05",
                PaymentMethod.Mpesa => "06",
                PaymentMethod.Credit => "02",
                _ => "07"
            })
            .Distinct()
            .ToList();

        var paymentTypeCode = paymentTypeCodes.Count == 1
            ? paymentTypeCodes[0]
            : paymentTypeCodes.Count > 1 ? "07" : null;

        var saleDate = sale.SaleDate;
        var taxableTotal = Math.Round(taxblA + taxblB + taxblC + taxblD + taxblE, 2, MidpointRounding.AwayFromZero);
        var taxTotal = Math.Round(taxAmtA + taxAmtB + taxAmtC + taxAmtD + taxAmtE, 2, MidpointRounding.AwayFromZero);

        var request = new EtimsSaleSaveRequest(
            sale.Id.ToString("N"),
            invoiceNumber,
            0,
            null,
            customer?.FullName,
            "N",
            "S",
            paymentTypeCode,
            "02",
            saleDate,
            saleDate,
            saleDate,
            taxblA, taxblB, taxblC, taxblD, taxblE,
            0m, 16m, 0m, 0m, 0m,
            taxAmtA, taxAmtB, taxAmtC, taxAmtD, taxAmtE,
            taxableTotal,
            taxTotal,
            sale.Total,
            operatorId, operatorName, operatorId, operatorName,
            customer?.Phone,
            invoiceNumber,
            null, null, null, null,
            "N",
            etimsItems);

        var result = await _etimsService.SaveSaleAsync(request, cancellationToken);

        if (!result.Success)
        {
            sale.IsSynced = false;
            sale.EtimsResultCode = result.ResultCode;
            await _db.SaveChangesAsync(cancellationToken);
            return result;
        }

        sale.EtimsInvoiceNumber = invoiceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        sale.EtimsReceiptNumber = result.ReceiptNumber;
        sale.EtimsTotalReceiptNumber = result.TotalReceiptNumber;
        sale.EtimsInternalData = result.InternalData;
        sale.EtimsReceiptSignature = result.ReceiptSignature;
        sale.EtimsReceiptPublishedDate = result.ReceiptPublishedDate;
        sale.EtimsSdcId = result.SdcId;
        sale.EtimsMrcNo = result.MrcNo;
        sale.EtimsResultCode = result.ResultCode;
        sale.EtimsControlNumber = result.ReceiptNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        sale.EtimsQrCodeData = result.InternalData;
        sale.IsSynced = true;
        sale.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static CompleteSaleResponse MapCompleteSaleResponse(Sale sale)
    {
        var items = sale.Items.Select(i => new SaleItemResponse(
            i.ProductId,
            i.Product?.Name ?? string.Empty,
            i.StockUnitId,
            i.Quantity,
            i.UnitPrice,
            i.DiscountAmount,
            i.TaxAmount,
            i.LineTotal)).ToList();

        var payments = sale.Payments.Select(p => new PaymentResponse(
            p.Id, p.Method.ToString(), p.Amount, p.Status.ToString(), p.ExternalReference)).ToList();

        return new CompleteSaleResponse(
            sale.Id, sale.SaleDate, sale.Subtotal, sale.DiscountTotal, sale.TaxTotal, sale.Total,
            sale.Status.ToString(), items, payments,
            sale.EtimsInvoiceNumber, sale.EtimsReceiptNumber, sale.EtimsTotalReceiptNumber,
            sale.EtimsInternalData, sale.EtimsReceiptSignature, sale.EtimsReceiptPublishedDate,
            sale.EtimsSdcId, sale.EtimsMrcNo, sale.EtimsResultCode, sale.IsSynced);
    }
}