using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;
using Pos.Domain.Entities;
using Pos.Domain.Enums;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Messaging;

/// <summary>
/// Drop-in replacement for WhatsAppCloudApiService while there's no approved Meta
/// account/templates yet. Resolves the exact same message text a real template would
/// produce, logs it, and saves it so you can inspect it via GET /api/dev/whatsapp-messages.
/// Nothing else in the app needs to know or care which implementation is registered —
/// same pattern as ConsoleEmailSender for Step 13's invite flow.
/// </summary>
public sealed class MockWhatsAppService : IWhatsAppService
{
    private readonly PosDbContext _db;
    private readonly ILogger<MockWhatsAppService> _logger;

    public MockWhatsAppService(PosDbContext db, ILogger<MockWhatsAppService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task SendReceiptDeliveryAsync(
    string phoneNumber,
    decimal amount,
    string receiptUrl,
    CancellationToken cancellationToken = default)
    => LogAndSaveAsync(
        phoneNumber,
        "receipt_delivery",
        $"Thank you for your purchase. We received KES {amount:N2}. View your receipt: {receiptUrl}",
        cancellationToken);

    public Task SendMpesaPaymentConfirmationAsync(
    string phoneNumber,
    decimal amount,
    string paymentReference,
    CancellationToken cancellationToken = default)
    => LogAndSaveAsync(
        phoneNumber,
        "mpesa_payment_confirmation",
        $"We've received your M-Pesa payment of KES {amount:N2} (Ref: {paymentReference}). Thank you!",
        cancellationToken);

    public Task SendRepairStatusUpdateAsync(
    string phoneNumber,
    string ticketNumber,
    string status,
    CancellationToken cancellationToken = default)
    => LogAndSaveAsync(
        phoneNumber,
        "repair_status_update",
        $"Update on repair ticket {ticketNumber}: status is now {status}.",
        cancellationToken);

    private async Task LogAndSaveAsync(string toPhoneNumber, string templateName, string resolvedText, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[MOCK WHATSAPP — not actually sent] To: {Phone} | Template: {Template}\n{Text}",
            toPhoneNumber, templateName, resolvedText);

        _db.SentWhatsAppMessages.Add(new SentWhatsAppMessage
        {
            Id = Guid.NewGuid(),
            ToPhoneNumber = toPhoneNumber,
            TemplateName = templateName,
            ResolvedMessageText = resolvedText,
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}
