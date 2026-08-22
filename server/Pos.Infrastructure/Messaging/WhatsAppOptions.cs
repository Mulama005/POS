namespace Pos.Infrastructure.Messaging;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    public string PhoneNumberId { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string ApiVersion { get; init; } = "v21.0";
}
