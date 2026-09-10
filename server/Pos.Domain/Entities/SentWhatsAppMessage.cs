namespace Pos.Domain.Entities;

/// <summary>
/// Dev-only record of a message the MockWhatsAppService "sent" — i.e. resolved and
/// logged instead of actually calling Meta. Lets you trigger a real action in the app
/// (change a repair's status, complete a sale) and then go look at exactly what message
/// would have gone out, with real parameter substitution, before any Meta account or
/// template approval exists.
/// </summary>
public class SentWhatsAppMessage
{
    public Guid Id { get; set; }
    public string ToPhoneNumber { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string ResolvedMessageText { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
