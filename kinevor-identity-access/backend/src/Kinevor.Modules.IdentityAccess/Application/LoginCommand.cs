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
    string? RecoveryCode = null) : IRequest<AuthSessionDto>;

/// <summary>Handles <see cref="LoginCommand"/>.</summary>
public class LoginHandler(
    IUserAccountRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IAuthTokenService tokens,
    IMfaService mfa,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<LoginCommand, AuthSessionDto>
{
    private const string DummyPasswordHash =
        "600000.WYAavA/d4rooJPeQ4XOFJQ==.sSvFCVk0v1zH7QknuccrMJFpT4Df28j4KOHavBYynpE=";

    private const string InvalidCredentials = "Invalid e-mail or password.";
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>Verifies credentials and persists a refresh token.</summary>
    public async Task<AuthSessionDto> Handle(LoginCommand command, CancellationToken ct)
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
            {
                account.RegisterFailedLogin(now, MaxFailedLoginAttempts, LockoutDuration);
                users.Update(account);
                await unitOfWork.SaveChangesAsync(ct);
            }

            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (account.MfaEnabled && !VerifySecondFactor(account, command, now))
        {
            account.RegisterFailedLogin(now, MaxFailedLoginAttempts, LockoutDuration);
            users.Update(account);
            await unitOfWork.SaveChangesAsync(ct);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (passwordHasher.NeedsRehash(account.PasswordHash))
            account.UpdatePasswordHash(passwordHasher.Hash(command.Password));

        account.RecordSuccessfulLogin(now);
        users.Update(account);

        var session = AuthSessionFactory.Issue(account, tokens, now);
        refreshTokens.Add(RefreshToken.Create(account.Id, tokens.HashRefreshToken(session.RefreshToken), now));
        await unitOfWork.SaveChangesAsync(ct);
        return session;
    }

    private bool VerifySecondFactor(UserAccount account, LoginCommand command, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(account.MfaSecret))
            return false;

        if (!string.IsNullOrWhiteSpace(command.MfaCode)
            && mfa.VerifyCode(account.MfaSecret, command.MfaCode, now))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(command.RecoveryCode))
            return false;

        var hash = mfa.HashRecoveryCode(command.RecoveryCode);
        return account.ConsumeRecoveryCode(hash);
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
