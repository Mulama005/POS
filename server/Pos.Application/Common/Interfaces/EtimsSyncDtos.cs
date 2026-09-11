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
    IReadOnlyList<EtimsItemClassDto> ItemClasses);