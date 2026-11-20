using Kinevor.Modules.IdentityAccess.Application;

namespace Kinevor.Modules.IdentityAccess.Tests;

/// <summary>RFC 6238 vectors and recovery-code primitives.</summary>
public class TotpTests
{
    // Base32 of the RFC 6238 SHA-1 seed "12345678901234567890".
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    /// <summary>Six-digit codes match the published SHA-1 test vectors.</summary>
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1_111_111_109L, "081804")]
    [InlineData(1_111_111_111L, "050471")]
    [InlineData(1_234_567_890L, "005924")]
    [InlineData(2_000_000_000L, "279037")]
    [InlineData(20_000_000_000L, "353130")]
    public void ComputeCode_MatchesRfc6238Vectors(long unixSeconds, string expected)
    {
        var instant = DateTime.UnixEpoch.AddSeconds(unixSeconds);

        Assert.Equal(expected, Totp.ComputeCode(RfcSecret, instant));
    }

    /// <summary>Verification tolerates one step of clock drift but not three.</summary>
    [Fact]
    public void VerifyCode_AllowsOneStepOfDrift()
    {
        var now = DateTime.UtcNow;
        var code = Totp.ComputeCode(RfcSecret, now);

        Assert.True(Totp.VerifyCode(RfcSecret, code, now.AddSeconds(30)));
        Assert.True(Totp.VerifyCode(RfcSecret, code, now.AddSeconds(-30)));
        Assert.False(Totp.VerifyCode(RfcSecret, code, now.AddSeconds(90)));
    }

    /// <summary>Recovery-code hashes ignore case, spaces and formatting dashes.</summary>
    [Fact]
    public void RecoveryCode_HashIsNormalised()
    {
        var hash = Totp.HashRecoveryCode("ABCD-EFGH");

        Assert.Equal(hash, Totp.HashRecoveryCode(" abcd efgh "));
        Assert.NotEqual(hash, Totp.HashRecoveryCode("ABCD-EFGI"));
    }

    /// <summary>The enrolment URI carries the secret and issuer.</summary>
    [Fact]
    public void OtpAuthUri_ContainsSecretAndIssuer()
    {
        var uri = Totp.BuildOtpAuthUri("Kinevor", "member@kinevor.demo", RfcSecret);

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains($"secret={RfcSecret}", uri);
        Assert.Contains("issuer=Kinevor", uri);
    }

    /// <summary>Generated recovery codes are unique and use the expected format.</summary>
    [Fact]
    public void RecoveryCodes_AreUniqueAndFormatted()
    {
        var codes = Enumerable.Range(0, 16).Select(_ => Totp.GenerateRecoveryCode()).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches("^[A-Z2-7]{4}-[A-Z2-7]{4}$", code));
    }
}
