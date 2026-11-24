using Kinevor.Modules.IdentityAccess.Domain;
using Kinevor.Modules.IdentityAccess.Application.Exceptions;
using MediatR;

namespace Kinevor.Modules.IdentityAccess.Application;

/// <summary>Authenticates an account and issues a token pair.</summary>
/// <param name="Email">Login e-mail.</param>
/// <param name="Password">Plaintext password.</param>
/// <param name="MfaCode">Six-digit TOTP code when MFA is enrolled.</param>
/// <param name="RecoveryCode">Single-use recovery code when the authenticator is unavailable.</param>
public record LoginCommand(
    string Email,
    string Password,
    string? MfaCode = null,
    string? RecoveryCode = null) : IRequest<LoginResult>;

/// <summary>Handles <see cref="LoginCommand"/>.</summary>
public class LoginHandler(
    IUserAccountRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IAuthTokenService tokens,
    IMfaService mfa,
    IMfaSecretProtector secretProtector,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<LoginCommand, LoginResult>
{
    private const string DummyPasswordHash =
        "600000.WYAavA/d4rooJPeQ4XOFJQ==.sSvFCVk0v1zH7QknuccrMJFpT4Df28j4KOHavBYynpE=";

    private const string InvalidCredentials = "Invalid e-mail or password.";
    private const int MaxFailedLoginAttempts = 5;
    private const int MaxConcurrencyRetries = 3;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Verifies credentials and either persists a refresh token or requests a second factor.</summary>
    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await users.GetByEmailAsync(command.Email, ct);

        // Unknown e-mails are checked against a fixed hash, so both paths run one
        // PBKDF2 verification and timing cannot reveal which accounts exist.
        var passwordValid = passwordHasher.Verify(
            command.Password,
            account?.PasswordHash ?? DummyPasswordHash);

        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (account is null)
            throw new AuthenticationFailedException(InvalidCredentials);

        // Locked accounts still run the password verification above so the response
        // shape and timing stay indistinguishable from an unknown account.
        if (account.IsLockedOut(now))
            throw new AuthenticationFailedException(InvalidCredentials);

        if (!account.IsActive || !passwordValid)
        {
            if (account.IsActive)
                await RegisterFailedLoginAsync(account, now, ct);

            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (account.MfaEnabled)
        {
            var hasMfaCode = !string.IsNullOrWhiteSpace(command.MfaCode);
            var hasRecoveryCode = !string.IsNullOrWhiteSpace(command.RecoveryCode);

            // A correct password without a factor is a challenge, not a failure. The
            // client must resubmit with the code and must keep the password.
            if (!hasMfaCode && !hasRecoveryCode)
                return LoginResult.Challenge();

            if (!TryVerifySecondFactor(account, command, now))
            {
                await RegisterFailedLoginAsync(account, now, ct);
                throw new AuthenticationFailedException(InvalidCredentials);
            }
        }

        if (passwordHasher.NeedsRehash(account.PasswordHash))
            account.UpdatePasswordHash(passwordHasher.Hash(command.Password));

        account.RecordSuccessfulLogin(now);
        users.Update(account);

        var session = AuthSessionFactory.Issue(account, tokens, now);
        refreshTokens.Add(RefreshToken.Create(account.Id, tokens.HashRefreshToken(session.RefreshToken), now));

        // A losing TOTP or recovery-code race must not be retried into a second
        // success; exactly one concurrent second-factor use wins.
        if (!await unitOfWork.TrySaveChangesAsync(ct))
            throw new AuthenticationFailedException(InvalidCredentials);

        return LoginResult.SignedIn(session);
    }

    private bool TryVerifySecondFactor(UserAccount account, LoginCommand command, DateTime now)
    {
        var secret = MfaSecretAccess.TryUnprotect(secretProtector, account.MfaSecret);
        if (secret is null)
            return false;

        if (!string.IsNullOrWhiteSpace(command.MfaCode)
            && mfa.TryVerifyCode(secret, command.MfaCode, now, out var matchedStep)
            && account.TryAcceptMfaStep(matchedStep))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(command.RecoveryCode))
            return false;

        return account.ConsumeRecoveryCode(mfa.HashRecoveryCode(command.RecoveryCode));
    }

    private async Task RegisterFailedLoginAsync(UserAccount account, DateTime now, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxConcurrencyRetries; attempt++)
        {
            if (!account.IsActive || account.IsLockedOut(now))
                return;

            account.RegisterFailedLogin(now, MaxFailedLoginAttempts, LockoutDuration);
            users.Update(account);

            if (await unitOfWork.TrySaveChangesAsync(ct))
                return;

            if (attempt == MaxConcurrencyRetries)
                return;

            // Another request changed the account first. Reload before retrying so
            // parallel failures each advance the counter instead of overwriting it.
            if (!await users.ReloadAsync(account, ct))
                return;
        }
    }
}

/// <summary>Builds token pairs shared by login and refresh.</summary>
internal static class AuthSessionFactory
{
    internal static AuthSessionDto Issue(UserAccount account, IAuthTokenService tokens, DateTime now)
    {
        var access = tokens.CreateAccessToken(account);
        var refresh = tokens.CreateRefreshToken();
        return new AuthSessionDto(access.Value, refresh, access.ExpiresAt, UserAccountDto.From(account));
    }
}
