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
                    ? "Device already initialized — connection confirmed, but this call doesn't re-return taxpayer/branch details (only the first-ever init does)."
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

    public async Task<EtimsCodesFetchResult> FetchCodesAsync(DateTime lastReqDt, CancellationToken cancellationToken = default)
    {
        var missing = ValidateConfig();
        if (missing is not null)
        {
            return new EtimsCodesFetchResult(false, null, missing, Array.Empty<EtimsCodeClassDto>());
        }

        var (tin, bhfId, _, baseUrl) = TrimmedConfig();
        const string path = "/code/selectCodes";
        var url = $"{baseUrl}{path}";
        var request = new CodesRequest
        {
            Tin = tin,
            BhfId = bhfId,
            LastReqDt = lastReqDt.ToString("yyyyMMddHHmmss")
        };

        try
        {
            _logger.LogInformation("eTIMS codes request: POST {Url} tin={Tin} bhfId={BhfId} lastReqDt={LastReqDt}",
                url, tin, bhfId, request.LastReqDt);

            using var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("eTIMS codes raw response ({StatusCode}, {Length} chars)", (int)response.StatusCode, raw.Length);

            if (!response.IsSuccessStatusCode)
            {
                return new EtimsCodesFetchResult(false, null,
                    $"VSCU JAR at {url} returned HTTP {(int)response.StatusCode}. Raw body: {raw}",
                    Array.Empty<EtimsCodeClassDto>());
            }

            CodesResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<CodesResponse>(raw, JsonOpts);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "eTIMS codes response was not valid JSON. Raw body: {Raw}", raw);
                return new EtimsCodesFetchResult(false, null, $"Response wasn't valid JSON. Raw body: {raw}", Array.Empty<EtimsCodeClassDto>());
            }

            if (parsed is null)
            {
                return new EtimsCodesFetchResult(false, null, "VSCU JAR returned an empty response body.", Array.Empty<EtimsCodeClassDto>());
            }

            if (parsed.ResultCd != "000")
            {
                // Unlike device-init, there's no documented "harmless alternate success
                // code" for this endpoint — anything other than 000 here is a real
                // failure and shouldn't be treated as partial success.
                return new EtimsCodesFetchResult(false, parsed.ResultCd, parsed.ResultMsg, Array.Empty<EtimsCodeClassDto>());
            }

            var classes = (parsed.Data?.ClsList ?? new List<CodeClassEntry>())
                .Select(cls => new EtimsCodeClassDto(
                    cls.CdCls ?? string.Empty,
                    cls.CdClsNm ?? string.Empty,
                    cls.CdClsDesc,
                    cls.UserDfnNm1,
                    cls.UserDfnNm2,
                    cls.UserDfnNm3,
                    cls.UseYn == "Y",
                    (cls.DtlList ?? new List<CodeDetailEntry>())
                        .Select(d => new EtimsCodeDetailDto(
                            d.Cd ?? string.Empty,
                            d.CdNm ?? string.Empty,
                            d.CdDesc,
                            d.SrtOrd ?? 0,
                            d.UseYn == "Y"))
                        .ToList()))
                .ToList();

            return new EtimsCodesFetchResult(true, parsed.ResultCd, null, classes);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the VSCU JAR at {Url}.", url);
            return new EtimsCodesFetchResult(false, null,
                $"Could not reach the VSCU JAR at {baseUrl}. ({ex.Message})", Array.Empty<EtimsCodeClassDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling eTIMS codes at {Url}.", url);
            return new EtimsCodesFetchResult(false, null, $"Unexpected error: {ex.Message}", Array.Empty<EtimsCodeClassDto>());
        }
    }

    public async Task<EtimsItemClassesFetchResult> FetchItemClassesAsync(DateTime lastReqDt, CancellationToken cancellationToken = default)
    {
        var missing = ValidateConfig();
        if (missing is not null)
        {
            return new EtimsItemClassesFetchResult(false, null, missing, Array.Empty<EtimsItemClassDto>());
        }

        var (tin, bhfId, _, baseUrl) = TrimmedConfig();
        const string path = "/itemClass/selectItemsClass";
        var url = $"{baseUrl}{path}";
        var request = new ItemClassesRequest
        {
            Tin = tin,
            BhfId = bhfId,
            LastReqDt = lastReqDt.ToString("yyyyMMddHHmmss")
        };

        try
        {
            _logger.LogInformation("eTIMS item-classes request: POST {Url} tin={Tin} bhfId={BhfId} lastReqDt={LastReqDt}",
                url, tin, bhfId, request.LastReqDt);

            using var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            // This dataset is the big one (KRA's full product taxonomy — potentially
            // tens of thousands of entries), so log length rather than the full raw body
            // by default; InitDeviceAsync's "log everything verbatim" approach was right
            // for a small, rare payload, but doing that here on every sync would flood
            // the log for no diagnostic benefit once this is known to work.
            _logger.LogInformation("eTIMS item-classes raw response ({StatusCode}, {Length} chars)", (int)response.StatusCode, raw.Length);

            if (!response.IsSuccessStatusCode)
            {
                return new EtimsItemClassesFetchResult(false, null,
                    $"VSCU JAR at {url} returned HTTP {(int)response.StatusCode}. Raw body: {raw}",
                    Array.Empty<EtimsItemClassDto>());
            }

            ItemClassesResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ItemClassesResponse>(raw, JsonOpts);
            }
            catch (JsonException jex)
            {
                _logger.LogError(jex, "eTIMS item-classes response was not valid JSON.");
                return new EtimsItemClassesFetchResult(false, null, "Response wasn't valid JSON.", Array.Empty<EtimsItemClassDto>());
            }

            if (parsed is null)
            {
                return new EtimsItemClassesFetchResult(false, null, "VSCU JAR returned an empty response body.", Array.Empty<EtimsItemClassDto>());
            }

            if (parsed.ResultCd != "000")
            {
                return new EtimsItemClassesFetchResult(false, parsed.ResultCd, parsed.ResultMsg, Array.Empty<EtimsItemClassDto>());
            }

            var items = (parsed.Data?.ItemClsList ?? new List<ItemClassEntry>())
                .Select(i => new EtimsItemClassDto(
                    i.ItemClsCd ?? string.Empty,
                    i.ItemClsNm ?? string.Empty,
                    i.ItemClsLvl ?? 0,
                    i.TaxTyCd,
                    i.MjrTgYn is null ? null : i.MjrTgYn == "Y",
                    i.UseYn == "Y"))
                .ToList();

            return new EtimsItemClassesFetchResult(true, parsed.ResultCd, null, items);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach the VSCU JAR at {Url}.", url);
            return new EtimsItemClassesFetchResult(false, null,
                $"Could not reach the VSCU JAR at {baseUrl}. ({ex.Message})", Array.Empty<EtimsItemClassDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling eTIMS item-classes at {Url}.", url);
            return new EtimsItemClassesFetchResult(false, null, $"Unexpected error: {ex.Message}", Array.Empty<EtimsItemClassDto>());
        }
    }

    /// <summary>Returns a human-readable "missing config" message, or null if config is
    /// complete. Shared by the two fetch methods; InitDeviceAsync keeps its own inline
    /// copy of this check deliberately unchanged since it's already confirmed working —
    /// not worth the risk of refactoring proven code for the sake of avoiding a few
    /// duplicated lines.</summary>
    private string? ValidateConfig()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)) missing.Add(nameof(_options.BaseUrl));
        if (string.IsNullOrWhiteSpace(_options.Tin)) missing.Add(nameof(_options.Tin));
        if (string.IsNullOrWhiteSpace(_options.BhfId)) missing.Add(nameof(_options.BhfId));
        if (missing.Count == 0) return null;

        var fields = string.Join(", ", missing);
        _logger.LogWarning("eTIMS is missing config value(s): {Fields}. Check Etims:* in user-secrets/environment.", fields);
        return $"eTIMS is not configured on this server (missing: {fields}).";
    }

    private (string Tin, string BhfId, string DvcSrlNo, string BaseUrl) TrimmedConfig() =>
        (_options.Tin.Trim(), _options.BhfId.Trim(), _options.DeviceSerialNumber.Trim(), _options.BaseUrl.TrimEnd('/'));

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

    // --- /code/selectCodes wire types ---

    private sealed class CodesRequest
    {
        [JsonPropertyName("tin")] public string Tin { get; set; } = string.Empty;
        [JsonPropertyName("bhfId")] public string BhfId { get; set; } = string.Empty;
        [JsonPropertyName("lastReqDt")] public string LastReqDt { get; set; } = string.Empty;
    }

    private sealed class CodesResponse
    {
        [JsonPropertyName("resultCd")] public string? ResultCd { get; set; }
        [JsonPropertyName("resultMsg")] public string? ResultMsg { get; set; }
        [JsonPropertyName("data")] public CodesData? Data { get; set; }
    }

    private sealed class CodesData
    {
        [JsonPropertyName("clsList")] public List<CodeClassEntry>? ClsList { get; set; }
    }

    private sealed class CodeClassEntry
    {
        [JsonPropertyName("cdCls")] public string? CdCls { get; set; }
        [JsonPropertyName("cdClsNm")] public string? CdClsNm { get; set; }
        [JsonPropertyName("cdClsDesc")] public string? CdClsDesc { get; set; }
        [JsonPropertyName("userDfnNm1")] public string? UserDfnNm1 { get; set; }
        [JsonPropertyName("userDfnNm2")] public string? UserDfnNm2 { get; set; }
        [JsonPropertyName("userDfnNm3")] public string? UserDfnNm3 { get; set; }
        [JsonPropertyName("useYn")] public string? UseYn { get; set; }
        [JsonPropertyName("dtlList")] public List<CodeDetailEntry>? DtlList { get; set; }
    }

    private sealed class CodeDetailEntry
    {
        [JsonPropertyName("cd")] public string? Cd { get; set; }
        [JsonPropertyName("cdNm")] public string? CdNm { get; set; }
        [JsonPropertyName("cdDesc")] public string? CdDesc { get; set; }
        [JsonPropertyName("srtOrd")] public int? SrtOrd { get; set; }
        [JsonPropertyName("useYn")] public string? UseYn { get; set; }
    }

    // --- /itemClass/selectItemsClass wire types ---

    private sealed class ItemClassesRequest
    {
        [JsonPropertyName("tin")] public string Tin { get; set; } = string.Empty;
        [JsonPropertyName("bhfId")] public string BhfId { get; set; } = string.Empty;
        [JsonPropertyName("lastReqDt")] public string LastReqDt { get; set; } = string.Empty;
    }

    private sealed class ItemClassesResponse
    {
        [JsonPropertyName("resultCd")] public string? ResultCd { get; set; }
        [JsonPropertyName("resultMsg")] public string? ResultMsg { get; set; }
        [JsonPropertyName("data")] public ItemClassesData? Data { get; set; }
    }

    private sealed class ItemClassesData
    {
        [JsonPropertyName("itemClsList")] public List<ItemClassEntry>? ItemClsList { get; set; }
    }

    private sealed class ItemClassEntry
    {
        [JsonPropertyName("itemClsCd")] public string? ItemClsCd { get; set; }
        [JsonPropertyName("itemClsNm")] public string? ItemClsNm { get; set; }
        [JsonPropertyName("itemClsLvl")] public int? ItemClsLvl { get; set; }
        [JsonPropertyName("taxTyCd")] public string? TaxTyCd { get; set; }
        [JsonPropertyName("mjrTgYn")] public string? MjrTgYn { get; set; }
        [JsonPropertyName("useYn")] public string? UseYn { get; set; }
    }
}