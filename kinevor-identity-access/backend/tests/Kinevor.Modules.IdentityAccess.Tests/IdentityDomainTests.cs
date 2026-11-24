using Kinevor.Modules.IdentityAccess.Application;
using Kinevor.Modules.IdentityAccess.Domain;

namespace Kinevor.Modules.IdentityAccess.Tests;

/// <summary>Domain and security primitives.</summary>
public class IdentityDomainTests
{
    /// <summary>Accounts reject unsupported roles.</summary>
    [Fact]
    public void Create_RejectsUnknownRole()
    {
        var result = UserAccount.Create(
            "owner@kinevor.demo", "Owner", "SuperUser", "encoded-hash", DateTime.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("Validation.Role", result.Error.Code);
    }

    /// <summary>Accounts normalise e-mail addresses.</summary>
    [Fact]
    public void Create_NormalisesEmail()
    {
        var result = UserAccount.Create(
            "  OWNER@Kinevor.Demo ", "Owner", "Admin", "encoded-hash", DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("owner@kinevor.demo", result.Value.Email.Value);
    }

    /// <summary>PBKDF2 hashes verify only the correct password.</summary>
    [Fact]
    public void PasswordHasher_RoundTrips()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("Kinevor!2026");

        Assert.True(hasher.Verify("Kinevor!2026", hash));
        Assert.False(hasher.Verify("wrong", hash));
        Assert.False(hasher.Verify("Kinevor!2026", "malformed"));
    }

    /// <summary>Refresh tokens stop being active once revoked.</summary>
    [Fact]
    public void RefreshToken_RevokeStopsExchange()
    {
        var now = DateTime.UtcNow;
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", now);

        Assert.True(token.IsActive(now));
        token.Revoke(now.AddMinutes(1));
        Assert.False(token.IsActive(now.AddMinutes(2)));
    }

    /// <summary>Five consecutive failures lock the account for the configured window.</summary>
    [Fact]
    public void FailedLogins_LockAccountUntilWindowExpires()
    {
        var account = CreateAccount();
        var now = DateTime.UtcNow;

        for (var attempt = 0; attempt < 5; attempt++)
            account.RegisterFailedLogin(now, 5, TimeSpan.FromMinutes(15));

        Assert.True(account.IsLockedOut(now));
        Assert.False(account.IsLockedOut(now.AddMinutes(16)));

        account.RegisterFailedLogin(now.AddMinutes(16), 5, TimeSpan.FromMinutes(15));
        Assert.Equal(1, account.FailedLoginAttempts);
        Assert.False(account.IsLockedOut(now.AddMinutes(16)));
    }

    /// <summary>A successful login clears the failure counter and the lock window.</summary>
    [Fact]
    public void SuccessfulLogin_ResetsLockoutState()
    {
        var account = CreateAccount();
        var now = DateTime.UtcNow;

        for (var attempt = 0; attempt < 4; attempt++)
            account.RegisterFailedLogin(now, 5, TimeSpan.FromMinutes(15));

        account.RecordSuccessfulLogin(now);

        Assert.Equal(0, account.FailedLoginAttempts);
        Assert.Null(account.LockoutEndsAt);
        Assert.False(account.IsLockedOut(now));
        Assert.Equal(now, account.LastLoginAt);
    }

    /// <summary>Recovery codes enable MFA and can be consumed only once.</summary>
    [Fact]
    public void MfaRecoveryCode_IsSingleUse()
    {
        var account = CreateAccount();
        var hash = Totp.HashRecoveryCode("ABCD-EFGH");

        account.BeginMfaEnrolment("secret");
        Assert.False(account.MfaEnabled);
        Assert.True(account.TryAcceptMfaStep(100));

        account.ConfirmMfa([hash]);
        Assert.True(account.MfaEnabled);
        Assert.True(account.ConsumeRecoveryCode(hash));
        Assert.False(account.ConsumeRecoveryCode(hash));

        account.DisableMfa();
        Assert.False(account.MfaEnabled);
        Assert.Null(account.MfaSecret);
        Assert.Null(account.LastAcceptedMfaStep);
        Assert.Empty(account.MfaRecoveryCodeHashes);
    }

    /// <summary>A TOTP step can be accepted once and never moves backwards.</summary>
    [Fact]
    public void MfaStep_RejectsReplayAndOlderSteps()
    {
        var account = CreateAccount();
        var hash = Totp.HashRecoveryCode("ABCD-EFGH");

        account.BeginMfaEnrolment("secret");
        Assert.True(account.TryAcceptMfaStep(100));
        account.ConfirmMfa([hash]);

        Assert.False(account.TryAcceptMfaStep(100));
        Assert.False(account.TryAcceptMfaStep(99));
        Assert.Equal(100, account.LastAcceptedMfaStep);

        Assert.True(account.TryAcceptMfaStep(101));
        Assert.Equal(101, account.LastAcceptedMfaStep);

        account.DisableMfa();
        Assert.Null(account.LastAcceptedMfaStep);
    }

    /// <summary>Every account mutation advances the optimistic-concurrency stamp.</summary>
    [Fact]
    public void Mutations_AdvanceConcurrencyStamp()
    {
        var account = CreateAccount();
        var initial = account.ConcurrencyStamp;

        account.RegisterFailedLogin(DateTime.UtcNow, 5, TimeSpan.FromMinutes(15));
        var afterFailure = account.ConcurrencyStamp;
        Assert.NotEqual(initial, afterFailure);

        account.RecordSuccessfulLogin(DateTime.UtcNow);
        Assert.NotEqual(afterFailure, account.ConcurrencyStamp);
    }

    private static UserAccount CreateAccount()
    {
        var result = UserAccount.Create(
            "owner@kinevor.demo", "Owner", "Admin", "encoded-hash", DateTime.UtcNow);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
