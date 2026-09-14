namespace Pos.Infrastructure.Etims;

/// <summary>
/// Config for the KRA eTIMS VSCU integration, Step 26. Bound from the "Etims" section —
/// see VSCU Specification Document v2.0.
///
/// Important distinction from Daraja (M-Pesa): this is NOT a remote API we call over the
/// internet. Per section 2.2 of the spec, KRA distributes VSCU as a JAR file that must be
/// deployed and running on your own local web server; our backend calls THAT local JAR's
/// REST endpoints, and the JAR is the thing that actually talks to KRA's eTIMS servers.
/// BaseUrl below therefore points at wherever the VSCU JAR is running (typically
/// http://localhost:&lt;port&gt; on the same machine as this API), not at any KRA-owned host.
/// </summary>
public sealed class EtimsOptions
{
    public const string SectionName = "Etims";

    /// <summary>
    /// Base URL of the locally-running VSCU JAR, e.g. "http://localhost:8088" — the
    /// spec's own worked example (section 2.3) uses port 8088, but the JAR's actual
    /// configured port depends on how it was deployed; check its own config/logs on
    /// startup rather than assuming 8088.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>KRA PIN (Tax Identification Number), 11 characters, e.g. "A123456789Z".</summary>
    public string Tin { get; init; } = string.Empty;

    /// <summary>
    /// Branch office ID as registered in the eTIMS Taxpayer Portal's Device Management
    /// page — "00" for Headquarter unless the device was registered under a different
    /// branch.
    /// </summary>
    public string BhfId { get; init; } = "00";

    /// <summary>
    /// Device serial number exactly as it appears in the eTIMS Taxpayer Portal's Device
    /// Management page (Serial No column) — this is validated against what KRA has on
    /// file for the tin+bhfId pair, so it must match verbatim, not be an arbitrary
    /// locally-invented string.
    /// </summary>
    public string DeviceSerialNumber { get; init; } = string.Empty;
}