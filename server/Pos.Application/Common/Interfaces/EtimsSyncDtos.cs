namespace Pos.Application.Common.Interfaces;

// --- Code list (/code/selectCodes) ---

public sealed record EtimsCodeDetailDto(
    string Cd,
    string CdNm,
    string? CdDesc,
    int SrtOrd,
    bool UseYn);

public sealed record EtimsCodeClassDto(
    string CdCls,
    string CdClsNm,
    string? CdClsDesc,
    string? UserDfnNm1,
    string? UserDfnNm2,
    string? UserDfnNm3,
    bool UseYn,
    IReadOnlyList<EtimsCodeDetailDto> Codes);

public sealed record EtimsCodesFetchResult(
    bool Success,
    string? ResultCode,
    string? ErrorMessage,
    IReadOnlyList<EtimsCodeClassDto> CodeClasses);

// --- Item classification list (/itemClass/selectItemsClass) ---

public sealed record EtimsItemClassDto(
    string ItemClsCd,
    string ItemClsNm,
    int ItemClsLvl,
    string? TaxTyCd,
    bool? MjrTgYn,
    bool UseYn);

public sealed record EtimsItemClassesFetchResult(
    bool Success,
    string? ResultCode,
    string? ErrorMessage,
    IReadOnlyList<EtimsItemClassDto> ItemClasses,
    string? ResultDt = null);

// --- Sales transaction (/trnsSales/saveSales) ---

public sealed record EtimsSaleSaveItemRequest(
    int ItemSeq,
    string ItemClsCd,
    string ItemCd,
    string ItemNm,
    string? Bcd,
    string PkgUnitCd,
    decimal Pkg,
    string QtyUnitCd,
    decimal Qty,
    decimal Prc,
    decimal SplyAmt,
    decimal DcRt,
    decimal DcAmt,
    string TaxTyCd,
    decimal TaxblAmt,
    decimal TaxAmt,
    decimal TotAmt);

public sealed record EtimsSaleSaveRequest(
    string TraderInvoiceNumber,
    long InvoiceNumber,
    long OriginalInvoiceNumber,
    string? CustomerTin,
    string? CustomerName,
    string SalesTypeCode,
    string ReceiptTypeCode,
    string? PaymentTypeCode,
    string SalesStatusCode,
    DateTime ConfirmedAt,
    DateTime SaleDate,
    DateTime? StockReleasedAt,
    decimal TaxableAmountA,
    decimal TaxableAmountB,
    decimal TaxableAmountC,
    decimal TaxableAmountD,
    decimal TaxableAmountE,
    decimal TaxRateA,
    decimal TaxRateB,
    decimal TaxRateC,
    decimal TaxRateD,
    decimal TaxRateE,
    decimal TaxAmountA,
    decimal TaxAmountB,
    decimal TaxAmountC,
    decimal TaxAmountD,
    decimal TaxAmountE,
    decimal TotalTaxableAmount,
    decimal TotalTaxAmount,
    decimal TotalAmount,
    string RegistrantId,
    string RegistrantName,
    string ModifierId,
    string ModifierName,
    string? CustomerMobileNumber,
    long ReceiptReportNumber,
    string? TradeName,
    string? Address,
    string? TopMessage,
    string? BottomMessage,
    string BuyerAcceptanceYn,
    IReadOnlyList<EtimsSaleSaveItemRequest> Items);

public sealed record EtimsSaleSaveResult(
    bool Success,
    string? ResultCode,
    string? ResultMessage,
    DateTime? ResultDate,
    long InvoiceNumber,
    long? ReceiptNumber,
    long? TotalReceiptNumber,
    string? InternalData,
    string? ReceiptSignature,
    DateTime? ReceiptPublishedDate,
    string? SdcId,
    string? MrcNo,
    string? ErrorMessage);
// --- Item management (/items/saveItems) ---

public sealed record EtimsItemSaveRequest(
    string ItemClsCd,
    string ItemCd,
    string ItemTyCd,
    string ItemNm,
    string OrgnNatCd,
    string PkgUnitCd,
    string QtyUnitCd,
    string TaxTyCd,
    decimal DftPrc,
    string IsrcAplcbYn,
    string UseYn,
    string RegrId,
    string RegrNm,
    string ModrId,
    string ModrNm,
    string? AddInfo,
    decimal? SftyQty,
    string? Bcd)
{
    public string? ItemStdNm { get; init; }
    public string? BtchNo { get; init; }
}

public sealed record EtimsItemSaveResult(
    bool Success,
    string? ResultCode,
    string? ResultMessage,
    DateTime? ResultDate,
    string ItemCd,
    string? ErrorMessage);