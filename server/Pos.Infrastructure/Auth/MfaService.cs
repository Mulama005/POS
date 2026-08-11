using Microsoft.AspNetCore.DataProtection;
using OtpNet;
using Pos.Application.Common.Interfaces;

namespace Pos.Infrastructure.Auth;

/// <summary>
/// TOTP via the Otp.NET library, secret-at-rest encryption via ASP.NET Core's built-in
/// Data Protection API (no extra infrastructure needed — see the deployment note in
/// STEP10-SETUP.md about persisting keys once this runs on more than one instance).
/// </summary>
public sealed class MfaService : IMfaService
{
    private const string Purpose = "Pos.MfaSecret.v1";
    private readonly IDataProtector _protector;

    public MfaService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20); // 160-bit, standard TOTP secret length
        return Base32Encoding.ToString(key);
    }

    public string GenerateOtpAuthUri(string rawSecret, string accountEmail, string issuer = "POS System")
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(accountEmail);
        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={rawSecret}&issuer={encodedIssuer}&digits=6&period=30";
    }

    public string EncryptSecret(string rawSecret) => _protector.Protect(rawSecret);

    public string DecryptSecret(string encryptedSecret) => _protector.Unprotect(encryptedSecret);

    public bool ValidateCode(string rawSecret, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var totp = new Totp(Base32Encoding.ToBytes(rawSecret));

        // VerificationWindow tolerates one 30s step either side, so a small clock
        // difference between server and phone doesn't lock a legitimate user out.
        var window = new VerificationWindow(previous: 1, future: 1);
        return totp.VerifyTotp(code, out _, window);
    }
}
