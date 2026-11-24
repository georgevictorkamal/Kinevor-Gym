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

    /// <summary>Verification reports the newest matched step for replay protection.</summary>
    [Fact]
    public void TryVerifyCode_ReturnsNewestMatchedStep()
    {
        var now = DateTime.UtcNow;
        var currentStep = Totp.ComputeStep(now);

        Assert.True(Totp.TryVerifyCode(
            RfcSecret, Totp.ComputeCode(RfcSecret, now), now, out var current));
        Assert.Equal(currentStep, current);

        var next = now.AddSeconds(Totp.StepSeconds);
        Assert.True(Totp.TryVerifyCode(
            RfcSecret, Totp.ComputeCode(RfcSecret, next), now, out var nextStep));
        Assert.Equal(currentStep + 1, nextStep);
    }

    /// <summary>A malformed code is rejected without producing a step.</summary>
    [Fact]
    public void TryVerifyCode_RejectsMalformedCode()
    {
        Assert.False(Totp.TryVerifyCode(RfcSecret, "12345", DateTime.UtcNow, out var step));
        Assert.Equal(0, step);
        Assert.False(Totp.TryVerifyCode(RfcSecret, "abcdef", DateTime.UtcNow, out _));
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

    /// <summary>Generated recovery codes carry 80 bits of entropy and use the expected format.</summary>
    [Fact]
    public void RecoveryCodes_AreUniqueAndFormatted()
    {
        var codes = Enumerable.Range(0, 16).Select(_ => Totp.GenerateRecoveryCode()).ToList();

        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches("^[A-Z2-7]{4}(-[A-Z2-7]{4}){3}$", code));
        Assert.All(codes, code => Assert.Equal(16, code.Count(char.IsAsciiLetterOrDigit)));
    }
}
