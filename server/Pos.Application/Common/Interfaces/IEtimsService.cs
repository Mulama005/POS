namespace Pos.Application.Common.Interfaces;

public sealed record EtimsDeviceInitResult(
    bool Success,
    string? ResultCode,
    string? ResultMessage,
    string? TaxpayerName,
    string? BranchName,
    string? DeviceId,
    string? ErrorMessage);

/// <summary>
/// KRA eTIMS VSCU integration, Step 26 — first slice only (device-init / connectivity
/// check). This talks to a locally-running VSCU JAR (KRA's own distribution model, see
/// EtimsOptions), not to KRA's API servers directly.
///
/// Per the VSCU Specification Document v2.0 (section 3.3.1, cross-checked against the
/// item-save and sales-save request shapes in sections 3.3.4/3.3.6), every other VSCU
/// endpoint only needs tin + bhfId in its request body — the intrlKey/signKey/cmcKey
/// returned by device-init are never sent back to the JAR by the caller. The JAR
/// retrieves and manages those keys internally after a successful init. This interface
/// deliberately does not expose them for that reason; callers only need to know whether
/// init succeeded.
/// </summary>
public interface IEtimsService
{
    /// <summary>
    /// Calls the VSCU JAR's device-initialization endpoint to confirm: (1) the JAR is
    /// running and reachable at EtimsOptions.BaseUrl, and (2) KRA recognizes our
    /// tin/bhfId/device-serial combination. This is a connectivity/identity check, not
    /// something that needs to run before every later call — the JAR only needs to be
    /// initialized once (or after being redeployed).
    /// </summary>
    Task<EtimsDeviceInitResult> InitDeviceAsync(CancellationToken cancellationToken = default);
}