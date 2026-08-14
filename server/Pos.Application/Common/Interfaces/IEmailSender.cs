namespace Pos.Application.Common.Interfaces;

/// <summary>
/// Abstraction over sending email. No real provider (SendGrid, Postmark, SES, etc.) has
/// been chosen yet anywhere in the build plan — this interface is the seam so the invite
/// flow can be built and tested now, with a real provider swapped in later without
/// touching UsersController or the invite logic at all.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
