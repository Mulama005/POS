namespace Pos.Application.Common.Interfaces;

/// <summary>
/// TOTP (RFC 6238) generation and validation, plus at-rest encryption of the secret.
/// Application logic depends only on this interface — the actual TOTP library and
/// encryption mechanism live behind it in Pos.Infrastructure.
/// </summary>
public interface IMfaService
{
    /// <summary>
    /// Generates a new random Base32 TOTP secret (raw, unencrypted — caller is responsible
    /// for encrypting before persisting, via EncryptSecret).
    /// </summary>
    string GenerateSecret();

    /// <summary>
    /// Builds the otpauth:// URI an authenticator app (Google Authenticator, Authy, 1Password,
    /// etc.) scans as a QR code to start generating codes for this account.
    /// </summary>
    string GenerateOtpAuthUri(string rawSecret, string accountEmail, string issuer = "POS System");

    /// <summary>Encrypts a raw secret for storage on the User entity.</summary>
    string EncryptSecret(string rawSecret);

    /// <summary>Decrypts a stored secret back to its raw form for code validation.</summary>
    string DecryptSecret(string encryptedSecret);

    /// <summary>
    /// Validates a 6-digit code against the (decrypted) secret, allowing a small window
    /// either side for clock drift between the server and the user's phone.
    /// </summary>
    bool ValidateCode(string rawSecret, string code);
}
