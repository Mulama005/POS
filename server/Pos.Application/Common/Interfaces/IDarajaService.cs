namespace Pos.Application.Common.Interfaces;

public sealed record StkPushInitiationResult(
    bool Success,
    string? CheckoutRequestId,
    string? MerchantRequestId,
    string? ErrorMessage);

/// <summary>
/// Safaricom Daraja API — STK Push (Lipa na M-Pesa Online), Step 27. Initiating a push
/// only starts the flow; Safaricom confirms success or failure asynchronously via
/// PaymentCallbacksController, not as this call's return value — a "Success" result
/// here only means the prompt was sent to the customer's phone, not that they paid.
/// </summary>
public interface IDarajaService
{
    Task<StkPushInitiationResult> InitiateStkPushAsync(
        string phoneNumber,
        decimal amount,
        string accountReference,
        string transactionDesc,
        CancellationToken cancellationToken = default);
}