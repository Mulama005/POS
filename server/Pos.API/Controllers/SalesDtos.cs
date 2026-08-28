namespace Pos.Api.Controllers;

/// <summary>
/// A Manager/Admin re-enters their own credentials to step-up-approve a discount above
/// the configured threshold, without switching the active session. Mirrors how MFA's
/// challenge token works — see IDiscountApprovalStore.
/// </summary>
public sealed record ApproveDiscountRequest(string Email, string Password);

public sealed record SaleItemRequest(
    Guid ProductId,
    Guid? StockUnitId,
    int Quantity,
    /// <summary>Manager-applied markdown on this specific line, in KES. Server-validated —
    /// never trust a client-submitted price directly.</summary>
    decimal DiscountAmount);

public sealed record PaymentRequest(
    /// <summary>"Cash", "Mpesa", or "Card" — see PaymentMethod. "Credit" (Deni) is rejected
    /// for now; that needs Step 32's credit-ledger module to track what's owed.</summary>
    string Method,
    decimal Amount,
    string? MpesaPhoneNumber);

public sealed record CompleteSaleRequest(
    Guid RegisterId,
    Guid? CustomerId,
    IReadOnlyList<SaleItemRequest> Items,
    /// <summary>Additional discount applied across the whole cart (on top of any per-line
    /// discounts), in KES. Distributed proportionally across lines before tax is computed.</summary>
    decimal CartDiscountAmount,
    /// <summary>Required only when total discount (line + cart) exceeds the configured
    /// threshold — obtained from POST /api/sales/approve-discount.</summary>
    string? DiscountApprovalToken,
    IReadOnlyList<PaymentRequest> Payments);

public sealed record SaleItemResponse(
    Guid ProductId,
    string ProductName,
    Guid? UnitId,
    int Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal LineTotal);

public sealed record PaymentResponse(
    string Method,
    decimal Amount,
    string Status,
    string? ExternalReference);

public sealed record CompleteSaleResponse(
    Guid SaleId,
    DateTime SaleDate,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal Total,
    string Status,
    IReadOnlyList<SaleItemResponse> Items,
    IReadOnlyList<PaymentResponse> Payments);