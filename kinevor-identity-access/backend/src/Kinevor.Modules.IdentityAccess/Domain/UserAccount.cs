using Kinevor.Platform.Domain.Common;
using Kinevor.Platform.Domain.ValueObjects;

namespace Kinevor.Modules.IdentityAccess.Domain;

/// <summary>Account that can authenticate against the Kinevor platform.</summary>
public sealed class UserAccount : AggregateRoot
{
    /// <summary>Login e-mail address.</summary>
    public Email Email { get; private set; } = null!;

    /// <summary>Display name shown in the console.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Platform role granted to the account.</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Encoded PBKDF2 password hash.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>Whether the account may sign in.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Whether a second factor is enrolled.</summary>
    public bool MfaEnabled { get; private set; }

    /// <summary>Encrypted Base32 TOTP secret; the application layer protects it at rest.</summary>
    public string? MfaSecret { get; private set; }

    /// <summary>SHA-256 hashes of single-use MFA recovery codes.</summary>
    public List<string> MfaRecoveryCodeHashes { get; private set; } = [];

    /// <summary>UTC TOTP time step most recently accepted for sign-in or MFA management.</summary>
    public long? LastAcceptedMfaStep { get; private set; }

    /// <summary>Optimistic-concurrency token advanced by every account mutation.</summary>
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();

    /// <summary>Consecutive failed sign-in attempts since the last success or lock expiry.</summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>UTC timestamp until which sign-in is locked, when locked.</summary>
    public DateTime? LockoutEndsAt { get; private set; }

    /// <summary>UTC timestamp of the last successful login.</summary>
    public DateTime? LastLoginAt { get; private set; }

    private UserAccount() { }

    /// <summary>Validates and creates an account.</summary>
    /// <param name="email">Login e-mail address.</param>
    /// <param name="displayName">Display name.</param>
    /// <param name="role">Platform role.</param>
    /// <param name="passwordHash">Already encoded password hash.</param>
    /// <param name="now">Current UTC timestamp.</param>
    /// <returns>The account, or a validation error.</returns>
    public static Result<UserAccount> Create(
        string email, string displayName, string role, string passwordHash, DateTime now)
    {
        var emailResult = Email.Create(email);
        if (emailResult.IsFailure) return Result.Failure<UserAccount>(emailResult.Error);

        if (string.IsNullOrWhiteSpace(displayName))
            return Result.Failure<UserAccount>(Error.Validation("DisplayName", "Display name is required."));
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result.Failure<UserAccount>(Error.Validation("PasswordHash", "Password hash is required."));
        if (!AllowedRoles.Contains(role))
            return Result.Failure<UserAccount>(Error.Validation("Role", "Role must be Admin, Staff, Trainer or Member."));

        return Result.Success(new UserAccount
        {
            Email = emailResult.Value,
            DisplayName = displayName.Trim(),
            Role = role,
            PasswordHash = passwordHash,
            CreatedAt = now,
        });
    }

    /// <summary>Records a successful login and clears the failed-attempt counter.</summary>
    public void RecordSuccessfulLogin(DateTime now)
    {
        LastLoginAt = now;
        FailedLoginAttempts = 0;
        LockoutEndsAt = null;
        Touch();
    }

    /// <summary>Whether sign-in is currently locked.</summary>
    public bool IsLockedOut(DateTime now) => LockoutEndsAt is not null && LockoutEndsAt > now;

    /// <summary>Records a failed sign-in and starts a lock window at the threshold.</summary>
    public void RegisterFailedLogin(DateTime now, int maxAttempts, TimeSpan lockoutDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lockoutDuration, TimeSpan.Zero);

        if (LockoutEndsAt is not null && LockoutEndsAt <= now)
        {
            FailedLoginAttempts = 0;
            LockoutEndsAt = null;
        }

        FailedLoginAttempts++;
        if (FailedLoginAttempts >= maxAttempts)
            LockoutEndsAt = now.Add(lockoutDuration);
        Touch();
    }

    /// <summary>Starts MFA enrolment without enabling it until a first code is confirmed.</summary>
    public void BeginMfaEnrolment(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        MfaSecret = secret.Trim();
        MfaEnabled = false;
        MfaRecoveryCodeHashes = [];
        LastAcceptedMfaStep = null;
        Touch();
    }

    /// <summary>Accepts a TOTP time step once; returns false when it is at or before the last accepted step.</summary>
    public bool TryAcceptMfaStep(long step)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(step);

        if (LastAcceptedMfaStep is { } lastAccepted && step <= lastAccepted)
            return false;

        LastAcceptedMfaStep = step;
        Touch();
        return true;
    }

    /// <summary>Enables MFA with a fresh set of hashed recovery codes.</summary>
    /// <param name="recoveryCodeHashes">Hashes of the new single-use recovery codes.</param>
    public void ConfirmMfa(IEnumerable<string> recoveryCodeHashes)
    {
        if (string.IsNullOrWhiteSpace(MfaSecret))
            throw new InvalidOperationException("Start MFA enrolment before confirming it.");
        if (LastAcceptedMfaStep is null)
            throw new InvalidOperationException("Accept a verification step before confirming MFA.");

        var hashes = recoveryCodeHashes.Where(hash => !string.IsNullOrWhiteSpace(hash)).ToList();
        if (hashes.Count == 0)
            throw new ArgumentException("At least one recovery code is required.", nameof(recoveryCodeHashes));

        MfaEnabled = true;
        MfaRecoveryCodeHashes = hashes;
        Touch();
    }

    /// <summary>Consumes a recovery code once; returns false when it is unknown or already used.</summary>
    public bool ConsumeRecoveryCode(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return false;

        if (!MfaRecoveryCodeHashes.Contains(hash, StringComparer.Ordinal))
            return false;

        MfaRecoveryCodeHashes = MfaRecoveryCodeHashes
            .Where(candidate => !string.Equals(candidate, hash, StringComparison.Ordinal))
            .ToList();
        Touch();
        return true;
    }

    /// <summary>Disables MFA and clears the secret, recovery codes and accepted TOTP step.</summary>
    public void DisableMfa()
    {
        MfaEnabled = false;
        MfaSecret = null;
        MfaRecoveryCodeHashes = [];
        LastAcceptedMfaStep = null;
        Touch();
    }

    /// <summary>Replaces the encoded password hash (used when rehashing to a higher iteration count).</summary>
    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        Touch();
    }

    /// <summary>Enables or disables sign-in.</summary>
    public void SetActive(bool active)
    {
        IsActive = active;
        Touch();
    }

    private void Touch() => ConcurrencyStamp = Guid.NewGuid();

    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.Ordinal) { "Admin", "Staff", "Trainer", "Member" };
}
