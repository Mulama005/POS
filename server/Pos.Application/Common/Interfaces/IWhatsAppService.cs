namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Sends the approved WhatsApp utility templates used by the POS application.
/// </summary>
public interface IWhatsAppService
{
    Task SendReceiptDeliveryAsync(
        string phoneNumber,
        decimal amount,
        string receiptUrl,
        CancellationToken cancellationToken = default);

    Task SendMpesaPaymentConfirmationAsync(
        string phoneNumber,
        decimal amount,
        string paymentReference,
        CancellationToken cancellationToken = default);

    Task SendRepairStatusUpdateAsync(
        string phoneNumber,
        string ticketNumber,
        string status,
        CancellationToken cancellationToken = default);
}
