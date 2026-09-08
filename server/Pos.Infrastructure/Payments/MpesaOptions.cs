namespace Pos.Infrastructure.Payments;

public sealed class DarajaOptions
{
    public const string SectionName = "Mpesa";

    public string ConsumerKey { get; init; } = string.Empty;
    public string ConsumerSecret { get; init; } = string.Empty;

    /// <summary>The Till or Paybill number sales are collected against.</summary>
    public string BusinessShortCode { get; init; } = string.Empty;

    public string Passkey { get; init; } = string.Empty;

    /// <summary>
    /// Must be a publicly reachable HTTPS URL — Safaricom posts the payment result here
    /// asynchronously, so localhost only works via a tunnel (e.g. ngrok) in development.
    /// </summary>
    public string CallbackBaseUrl { get; init; } = string.Empty;

    public bool UseSandbox { get; init; } = true;

    /// <summary>"CustomerPayBillOnline" for a Paybill number, "CustomerBuyGoodsOnline" for a Till number.</summary>
    public string TransactionType { get; init; } = "CustomerPayBillOnline";

    public string BaseUrl => UseSandbox ? "https://sandbox.safaricom.co.ke" : "https://api.safaricom.co.ke";
}