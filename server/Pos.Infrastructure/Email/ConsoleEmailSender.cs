using Microsoft.Extensions.Logging;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Email;

/// <summary>
/// Placeholder IEmailSender for local dev / until a real provider is wired up. Logs the
/// email instead of sending it, so the invite link is visible in the console — good enough
/// to test the whole invite flow end-to-end right now. Swap for a real provider
/// (SendGrid/Postmark/SES) behind the same interface when that's ready; nothing else in
/// the invite flow needs to change.
/// </summary>
public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DEV EMAIL — not actually sent] To: {ToEmail} | Subject: {Subject}\n{Body}",
            toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}
