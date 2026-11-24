using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Kinevor.Modules.IdentityAccess.Application;

/// <summary>Encrypts TOTP secrets before they are persisted.</summary>
public interface IMfaSecretProtector
{
    /// <summary>Encrypts a Base32 TOTP secret for storage.</summary>
    string Protect(string secret);

    /// <summary>Decrypts a stored TOTP secret.</summary>
    string Unprotect(string protectedSecret);
}

/// <summary>
/// ASP.NET Core Data Protection implementation of <see cref="IMfaSecretProtector"/>.
/// The host must persist the Data Protection key ring so enrolled accounts survive
/// a restart; production hosts should store the keys in a managed key service.
/// </summary>
public sealed class DataProtectionMfaSecretProtector : IMfaSecretProtector
{
    private const string Purpose = "Kinevor.Modules.IdentityAccess.MfaSecret.v1";
    private readonly IDataProtector _protector;

    /// <summary>Creates a protector scoped to MFA secrets.</summary>
    public DataProtectionMfaSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose);
    }

    /// <summary>Encrypts a TOTP secret.</summary>
    public string Protect(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return _protector.Protect(secret);
    }

    /// <summary>Decrypts a stored TOTP secret.</summary>
    public string Unprotect(string protectedSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedSecret);
        return _protector.Unprotect(protectedSecret);
    }
}

/// <summary>Shared failure handling for secrets that cannot be decrypted.</summary>
internal static class MfaSecretAccess
{
    /// <summary>Returns the plaintext secret, or null when the stored value is unusable.</summary>
    internal static string? TryUnprotect(IMfaSecretProtector protector, string? protectedSecret)
    {
        if (string.IsNullOrWhiteSpace(protectedSecret))
            return null;

        try
        {
            return protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            // A rotated or missing key ring must fail closed, not fall back to plaintext.
            return null;
        }
    }
}
