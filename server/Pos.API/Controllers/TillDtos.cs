namespace Pos.Api.Controllers;

public sealed record OpenTillRequest(Guid RegisterId, decimal OpeningFloat);

public sealed record CloseTillRequest(Guid RegisterId, decimal CountedCashAtClose);

public sealed record TillSessionResponse(
    Guid Id,
    Guid RegisterId,
    string RegisterName,
    Guid OpenedByUserId,
    string OpenedByName,
    DateTime OpenedAt,
    decimal OpeningFloat,
    string Status);

public sealed record TillReconciliationResponse(
    Guid Id,
    Guid RegisterId,
    DateTime OpenedAt,
    DateTime ClosedAt,
    decimal OpeningFloat,
    decimal CashSalesTotal,
    decimal ExpectedCashAtClose,
    decimal CountedCashAtClose,
    decimal Variance,
    /// <summary>Non-cash totals shown for context only — they don't affect the physical
    /// cash count, but a Manager reconciling the drawer usually wants the full picture.</summary>
    decimal MpesaSalesTotal,
    decimal CardSalesTotal);