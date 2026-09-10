using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Etims;

/// <summary>
/// Talks to the locally-running VSCU JAR (KRA's distribution model, Step 26) — not to
/// KRA's API servers directly. See the VSCU Specification Document v2.0, section 3.3.1
/// for the device-init contract this wraps.
/// </summary>
public sealed class EtimsService : IEtimsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly EtimsOptions _options;
    private readonly ILogger<EtimsService> _logger;

    public EtimsService(HttpClient httpClient, IOptions<EtimsOptions> options, ILogger<EtimsService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EtimsDeviceInitResult> InitDeviceAsync(CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)) missing.Add(nameof(_options.BaseUrl));
        if (string.IsNullOrWhiteSpace(_options.Tin)) missing.Add(nameof(_options.Tin));
        if (string.IsNullOrWhiteSpace(_options.BhfId)) missing.Add(nameof(_options.BhfId));
        if (string.IsNullOrWhiteSpace(_options.DeviceSerialNumber)) missing.Add(nameof(_options.DeviceSerialNumber));
        if (missing.Count > 0)
        {
            var fields = string.Join(", ", missing);
            _logger.LogWarning("eTIMS is missing config value(s): {Fields}. Check Etims:* in user-secrets/environment.", fields);
            return new EtimsDeviceInitResult(false, null, null, null, null, null,
                $"eTIMS is not configured on this server (missing: {fields}).");
        }

        // Defensive trim, same reasoning as the Daraja fix: a pasted TIN/serial with an
        // invisible trailing space/newline produces a request that looks right in logs
        // but doesn't match what KRA has on file.
        var tin = _options.Tin.Trim();
        var bhfId = _options.BhfId.Trim();
        var dvcSrlNo = _options.DeviceSerialNumber.Trim();
        var baseUrl = _options.BaseUrl.TrimEnd('/');

        var request = new InitDeviceRequest
        {
            Tin = tin,
            BhfId = bhfId,
            DvcSrlNo = dvcSrlNo
        };

        // The spec disagrees with itself on this path: section 3.3.1.1's field-level
        // table labels it "/initializer/selectInitInfo", but the worked Java sample in
        // section 2.3 calls it bare "/selectInitInfo" against the JAR's own root.
        // Starting with the fully-qualified path since it's attached to the actual
        // request/response schema, not just a sample snippet. If this 404s against your
        // running JAR, the next thing to try is the bare path — see the catch block below,
        // it'll tell you which one failed.
        const string path = "/initializer/selectInitInfo";
        var url = $"{baseUrl}{path}";

        try
        {
            _logger.LogInformation(
                "eTIMS init request: POST {Url} tin={Tin} bhfId={BhfId} dvcSrlNo={DvcSrlNo}",
                url, tin, bhfId, dvcSrlNo);

            using var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            // Logging the full raw body verbatim, not reconstructed field-by-field — this
            // is the exact lesson from the M-Pesa shortcode investigation: partial
            // diagnostics reconstructed from separate log statements hid the real bug for
            // most of a session. Don't repeat that here.
            _logger.LogInformation("eTIMS init raw response ({StatusCode}): {Raw}", (int)response.StatusCode, raw);

            if (!response.IsSuccessStatusCode)
            {
                return new EtimsDeviceInitResult(false, null, null, null, null, null,
                    $"VSCU JAR at {url} returned HTTP {(int)response.StatusCode}. " +
                    $"If this is a 404, try the bare \"/selectInitInfo\" path next — the spec is " +
                    $"inconsistent about which one is correct. Raw body: {raw}");
            }

            InitDeviceResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<InitDeviceResponse>(raw, JsonOpts);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "eTIMS init response was not valid JSON. Raw body: {Raw}", raw);
                return new EtimsDeviceInitResult(false, null, null, null, null, null,
                    $"VSCU JAR response wasn't valid JSON. Raw body: {raw}");
            }

            if (parsed is null)
            {
                return new EtimsDeviceInitResult(false, null, null, null, null, null,
                    "VSCU JAR returned an empty response body.");
            }

            // Per the spec's response-code table (section 4.14): "000" is a fresh,
            // successful handshake with full taxpayer/branch data attached. "902" ("This
            // device is installed") is what you get calling init again after that first
            // handshake already succeeded — the JAR doesn't repeat it, and `data` comes
            // back null. Both mean "the device is fine and KRA recognizes it" — 902 is
            // NOT an error, just a different, data-less shape of success. Anything else
            // is a real failure (901 "not a valid device", 900 "no header info", etc.).
            var alreadyInstalled = parsed.ResultCd == "902";
            var ok = parsed.ResultCd == "000" || alreadyInstalled;
            return new EtimsDeviceInitResult(
                ok,
                parsed.ResultCd,
                alreadyInstalled
                    ? "Device already initialized."
                    : parsed.ResultMsg,
                parsed.Data?.Info?.TaxprNm,
                parsed.Data?.Info?.BhfNm,
                parsed.Data?.Info?.DvcId,
                ok ? null : parsed.ResultMsg);
        }
        catch (HttpRequestException ex)
        {
            // This is a local network call, not an internet call — a connection failure
            // here almost always means "the JAR isn't running" or "wrong port", not a
            // KRA-side problem. Worth stating plainly since it's the first thing to rule
            // out, not to reason past.
            _logger.LogError(ex, "Could not reach the VSCU JAR at {Url}.", url);
            return new EtimsDeviceInitResult(false, null, null, null, null, null,
                $"Could not reach the VSCU JAR at {baseUrl}. Confirm the JAR process is actually " +
                $"running and listening on that port before assuming this is a KRA-side issue. ({ex.Message})");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "eTIMS init request to {Url} timed out.", url);
            return new EtimsDeviceInitResult(false, null, null, null, null, null,
                $"Request to the VSCU JAR at {baseUrl} timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling eTIMS init at {Url}.", url);
            return new EtimsDeviceInitResult(false, null, null, null, null, null, $"Unexpected error: {ex.Message}");
        }
    }

    private sealed class InitDeviceRequest
    {
        [JsonPropertyName("tin")] public string Tin { get; set; } = string.Empty;
        [JsonPropertyName("bhfId")] public string BhfId { get; set; } = string.Empty;
        [JsonPropertyName("dvcSrlNo")] public string DvcSrlNo { get; set; } = string.Empty;
    }

    private sealed class InitDeviceResponse
    {
        [JsonPropertyName("resultCd")] public string? ResultCd { get; set; }
        [JsonPropertyName("resultMsg")] public string? ResultMsg { get; set; }
        [JsonPropertyName("resultDt")] public string? ResultDt { get; set; }
        [JsonPropertyName("data")] public InitDeviceData? Data { get; set; }
    }

    private sealed class InitDeviceData
    {
        [JsonPropertyName("info")] public InitDeviceInfo? Info { get; set; }
    }

    private sealed class InitDeviceInfo
    {
        [JsonPropertyName("tin")] public string? Tin { get; set; }
        [JsonPropertyName("taxprNm")] public string? TaxprNm { get; set; }
        [JsonPropertyName("bhfId")] public string? BhfId { get; set; }
        [JsonPropertyName("bhfNm")] public string? BhfNm { get; set; }
        [JsonPropertyName("dvcId")] public string? DvcId { get; set; }

        // intrlKey/signKey/cmcKey deliberately NOT modeled here. Per the spec, every
        // other endpoint's request body only ever needs tin + bhfId — the JAR retrieves
        // and persists these three keys itself after a successful init and reuses them
        // internally. Modeling them here would invite some future call site to try
        // passing them back to the JAR, which the spec never shows as a real pattern.
    }
}