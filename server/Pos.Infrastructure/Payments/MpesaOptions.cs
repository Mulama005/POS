namespace Pos.Infrastructure.Payments;

public sealed class DarajaOptions
{
    public const string SectionName = "Mpesa";

    public string ConsumerKey { get; init; } = string.Empty;
    public string ConsumerSecret { get; init; } = string.Empty;

    /// <summary>
    /// Paybill: the Paybill number. Till (Buy Goods): the head-office STORE number - this is
    /// what Daraja's Go Live is registered against and what the STK password is built from.
    /// </summary>
    public string BusinessShortCode { get; init; } = string.Empty;

    public string Passkey { get; init; } = string.Empty;

    /// <summary>
    /// Till (Buy Goods) only: the actual Till number customers pay, sent as PartyB. Leave empty
    /// for a Paybill, where PartyB is the same as BusinessShortCode.
    /// </summary>
    public string TillNumber { get; init; } = string.Empty;

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