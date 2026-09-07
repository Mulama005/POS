using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Payments;

/// <summary>
/// Safaricom Daraja API — STK Push (Lipa na M-Pesa Online). Sandbox vs. production is
/// controlled entirely by DarajaOptions.UseSandbox, which switches the base URL —
/// credentials still need to be swapped separately when moving to production, since
/// sandbox and production credentials are issued independently by Safaricom.
/// </summary>
public sealed class DarajaService : IDarajaService
{
    private const string TokenCacheKey = "daraja_access_token";

    private readonly HttpClient _httpClient;
    private readonly DarajaOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DarajaService> _logger;

    public DarajaService(HttpClient httpClient, IOptions<DarajaOptions> options, IMemoryCache cache, ILogger<DarajaService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<StkPushInitiationResult> InitiateStkPushAsync(
        string phoneNumber,
        decimal amount,
        string accountReference,
        string transactionDesc,
        CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_options.ConsumerKey)) missing.Add(nameof(_options.ConsumerKey));
        if (string.IsNullOrWhiteSpace(_options.ConsumerSecret)) missing.Add(nameof(_options.ConsumerSecret));
        if (string.IsNullOrWhiteSpace(_options.BusinessShortCode)) missing.Add(nameof(_options.BusinessShortCode));
        if (string.IsNullOrWhiteSpace(_options.Passkey)) missing.Add(nameof(_options.Passkey));
        if (missing.Count > 0)
        {
            var fields = string.Join(", ", missing);
            _logger.LogWarning("Daraja is missing config value(s): {Fields}. Check Daraja:* in user-secrets/environment.", fields);
            return new StkPushInitiationResult(false, null, null, $"M-Pesa is not configured on this server (missing: {fields}).");
        }
        if (string.IsNullOrWhiteSpace(_options.CallbackBaseUrl))
        {
            return new StkPushInitiationResult(false, null, null, "M-Pesa callback URL is not configured.");
        }

        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        if (normalizedPhone is null)
        {
            return new StkPushInitiationResult(false, null, null, "Enter a valid Kenyan phone number (07xx, 01xx, or 2547xx).");
        }

        string accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to obtain a Daraja access token.");
            return new StkPushInitiationResult(false, null, null, "Could not reach M-Pesa right now. Try again shortly.");
        }

        // Safaricom expects this timestamp in East Africa Time (EAT, UTC+3, no DST) — not
        // UTC and not the server's local time zone, which can't be assumed. Password is a
        // hash of ShortCode+Passkey+Timestamp, so a wrong offset here produces a Password
        // that silently fails Safaricom's own validation, surfaced (confusingly) as the
        // same generic "Invalid BusinessShortCode" error as an actually-wrong shortcode.
        var eatNow = DateTime.UtcNow.AddHours(3);
        var timestamp = eatNow.ToString("yyyyMMddHHmmss");
        var password = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.BusinessShortCode}{_options.Passkey}{timestamp}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/mpesa/stkpush/v1/processrequest")
        {
            Content = JsonContent.Create(new
            {
                BusinessShortCode = _options.BusinessShortCode,
                Password = password,
                Timestamp = timestamp,
                TransactionType = _options.TransactionType,
                Amount = (long)Math.Round(amount, MidpointRounding.AwayFromZero), // Daraja expects a whole-shilling integer
                PartyA = normalizedPhone,
                PartyB = _options.BusinessShortCode,
                PhoneNumber = normalizedPhone,
                CallBackURL = $"{_options.CallbackBaseUrl.TrimEnd('/')}/api/payment-callbacks/mpesa",
                AccountReference = Truncate(accountReference, 12), // Daraja limits this field's length
                TransactionDesc = Truncate(transactionDesc, 13),
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Daraja STK push request failed: {Status} {Body}", response.StatusCode, body);
            return new StkPushInitiationResult(false, null, null, "M-Pesa declined the request. Try again.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var responseCode = root.TryGetProperty("ResponseCode", out var rc) ? rc.GetString() : null;
        if (responseCode != "0")
        {
            var description = root.TryGetProperty("ResponseDescription", out var rd) ? rd.GetString() : "Unknown error.";
            _logger.LogWarning("Daraja STK push rejected: {Description}", description);
            return new StkPushInitiationResult(false, null, null, description);
        }

        var checkoutRequestId = root.TryGetProperty("CheckoutRequestID", out var cr) ? cr.GetString() : null;
        var merchantRequestId = root.TryGetProperty("MerchantRequestID", out var mr) ? mr.GetString() : null;

        return new StkPushInitiationResult(true, checkoutRequestId, merchantRequestId, null);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(TokenCacheKey, out string? cached) && cached is not null)
        {
            return cached;
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ConsumerKey}:{_options.ConsumerSecret}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/oauth/v1/generate?grant_type=client_credentials");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var token = body.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Daraja token response did not include an access_token.");

        // Daraja returns expires_in as a JSON string (e.g. "3599"), not a number.
        var expiresIn = body.TryGetProperty("expires_in", out var e) && int.TryParse(e.GetString(), out var seconds)
            ? seconds
            : 3599;

        // Cache for a bit less than the real expiry so a request never starts with a
        // token that's about to expire mid-flight.
        _cache.Set(TokenCacheKey, token, TimeSpan.FromSeconds(Math.Max(expiresIn - 60, 60)));
        return token;
    }

    /// <summary>Daraja expects 2547XXXXXXXX / 2541XXXXXXXX — no leading 0 or +.</summary>
    private static string? NormalizePhoneNumber(string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        if (digits.StartsWith('0') && digits.Length == 10)
        {
            digits = "254" + digits[1..];
        }
        else if ((digits.StartsWith('7') || digits.StartsWith('1')) && digits.Length == 9)
        {
            digits = "254" + digits;
        }

        var isValid = digits.Length == 12 && digits.StartsWith("254") &&
            (digits[3] == '7' || digits[3] == '1');

        return isValid ? digits : null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}