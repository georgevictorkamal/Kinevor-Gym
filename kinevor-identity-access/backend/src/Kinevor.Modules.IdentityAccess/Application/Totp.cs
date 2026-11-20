using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Kinevor.Modules.IdentityAccess.Domain;

namespace Kinevor.Modules.IdentityAccess.Application;

/// <summary>RFC 6238 time-based one-time passwords and Base32 secrets.</summary>
public static class Totp
{
    /// <summary>Default TOTP time step in seconds.</summary>
    public const int StepSeconds = 30;

    /// <summary>Default number of TOTP digits.</summary>
    public const int Digits = 6;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Computes a TOTP code for the supplied UTC instant.</summary>
    public static string ComputeCode(
        string base32Secret,
        DateTime utcNow,
        int stepSeconds = StepSeconds,
        int digits = Digits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);
        ArgumentOutOfRangeException.ThrowIfLessThan(stepSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(digits, 6);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digits, 8);

        var seconds = (long)Math.Floor((utcNow.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds);
        var counter = seconds / stepSeconds;

        Span<byte> counterBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);

        var key = DecodeBase32(base32Secret);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        var modulus = digits switch
        {
            6 => 1_000_000,
            7 => 10_000_000,
            8 => 100_000_000,
            _ => 1_000_000,
        };
        return (binary % modulus).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');
    }

    /// <summary>Verifies a code with a small drift window in both directions.</summary>
    public static bool VerifyCode(
        string base32Secret,
        string code,
        DateTime utcNow,
        int stepSeconds = StepSeconds,
        int digits = Digits,
        int allowedDriftSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Length != digits || !normalized.All(char.IsAsciiDigit))
            return false;

        ArgumentOutOfRangeException.ThrowIfNegative(allowedDriftSteps);

        for (var drift = -allowedDriftSteps; drift <= allowedDriftSteps; drift++)
        {
            // DateTime arithmetic handles daylight saving only for local values; all values here are UTC.
            var candidate = ComputeCode(base32Secret, utcNow.AddSeconds(drift * stepSeconds), stepSeconds, digits);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate),
                    Encoding.ASCII.GetBytes(normalized)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Creates a 160-bit Base32 secret suitable for authenticator apps.</summary>
    public static string GenerateSecret() => EncodeBase32(RandomNumberGenerator.GetBytes(20));

    /// <summary>Builds the standard <c>otpauth://</c> URI an authenticator app scans.</summary>
    public static string BuildOtpAuthUri(string issuer, string accountName, string base32Secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(base32Secret);

        var label = Uri.EscapeDataString($"{issuer}:{accountName}");
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}"
            + $"&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    /// <summary>Creates a formatted single-use recovery code.</summary>
    public static string GenerateRecoveryCode()
    {
        var raw = EncodeBase32(RandomNumberGenerator.GetBytes(5));
        return $"{raw[..4]}-{raw[4..]}";
    }

    /// <summary>Normalises and hashes a recovery code for storage.</summary>
    public static string HashRecoveryCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var normalized = code
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string EncodeBase32(ReadOnlySpan<byte> data)
    {
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
            output.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);

        return output.ToString();
    }

    private static byte[] DecodeBase32(string value)
    {
        var normalized = value
            .Trim()
            .TrimEnd('=')
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        var output = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var character in normalized)
        {
            var index = Base32Alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
                throw new FormatException("The TOTP secret is not valid Base32.");

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}

/// <summary>MFA secret, code and recovery-code operations.</summary>
public interface IMfaService
{
    /// <summary>Creates a new Base32 TOTP secret.</summary>
    string GenerateSecret();

    /// <summary>Builds the enrolment URI for an account.</summary>
    string BuildOtpAuthUri(UserAccount account, string secret);

    /// <summary>Verifies a TOTP code at the supplied instant.</summary>
    bool VerifyCode(string secret, string code, DateTime utcNow);

    /// <summary>Creates a set of plaintext recovery codes; only hashes are stored.</summary>
    IReadOnlyList<string> GenerateRecoveryCodes(int count = 8);

    /// <summary>Hashes a recovery code for storage and lookup.</summary>
    string HashRecoveryCode(string code);
}

/// <summary>Default TOTP implementation with no external dependencies.</summary>
public sealed class TotpMfaService : IMfaService
{
    private const string Issuer = "Kinevor";

    /// <summary>Creates a new secret.</summary>
    public string GenerateSecret() => Totp.GenerateSecret();

    /// <summary>Builds the authenticator-app enrolment URI.</summary>
    public string BuildOtpAuthUri(UserAccount account, string secret) =>
        Totp.BuildOtpAuthUri(Issuer, account.Email.Value, secret);

    /// <summary>Verifies a code with one step of drift in either direction.</summary>
    public bool VerifyCode(string secret, string code, DateTime utcNow) =>
        Totp.VerifyCode(secret, code, utcNow);

    /// <summary>Generates single-use recovery codes.</summary>
    public IReadOnlyList<string> GenerateRecoveryCodes(int count = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        return Enumerable.Range(0, count).Select(_ => Totp.GenerateRecoveryCode()).ToList();
    }

    /// <summary>Hashes a recovery code.</summary>
    public string HashRecoveryCode(string code) => Totp.HashRecoveryCode(code);
}
