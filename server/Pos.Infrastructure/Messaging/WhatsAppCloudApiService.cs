using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Messaging;

/// <summary>
/// WhatsApp Cloud API implementation. Each public method maps to one approved
/// utility template and preserves that template's two parameter positions.
/// </summary>
public sealed class WhatsAppCloudApiService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;

    public WhatsAppCloudApiService(HttpClient httpClient, IOptions<WhatsAppOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task SendReceiptDeliveryAsync(
        string phoneNumber,
        decimal amount,
        string receiptUrl,
        CancellationToken cancellationToken = default) =>
        SendTemplateAsync(
            phoneNumber,
            "receipt_delivery",
            FormatAmount(amount),
            receiptUrl,
            cancellationToken);

    public Task SendMpesaPaymentConfirmationAsync(
        string phoneNumber,
        decimal amount,
        string paymentReference,
        CancellationToken cancellationToken = default) =>
        SendTemplateAsync(
            phoneNumber,
            "mpesa_payment_confirmation",
            FormatAmount(amount),
            paymentReference,
            cancellationToken);

    public Task SendRepairStatusUpdateAsync(
        string phoneNumber,
        string ticketNumber,
        string status,
        CancellationToken cancellationToken = default) =>
        SendTemplateAsync(
            phoneNumber,
            "repair_status_update",
            ticketNumber,
            status,
            cancellationToken);

    private async Task SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string firstParameter,
        string secondParameter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PhoneNumberId) ||
            string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            throw new InvalidOperationException(
                "WhatsApp is not configured. Set WhatsApp:PhoneNumberId and WhatsApp:AccessToken via user secrets or environment variables.");
        }

        var recipient = NormalizePhoneNumber(phoneNumber);
        if (recipient.Length == 0)
        {
            throw new ArgumentException("A WhatsApp recipient phone number is required.", nameof(phoneNumber));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to = recipient,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = "en" },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = new[]
                            {
                                new { type = "text", text = firstParameter },
                                new { type = "text", text = secondParameter }
                            }
                        }
                    }
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string NormalizePhoneNumber(string phoneNumber) =>
        new(phoneNumber.Where(char.IsDigit).ToArray());
}
