using Kinevor.Modules.IdentityAccess.Application.Exceptions;
using Kinevor.Modules.IdentityAccess.Domain;
using Kinevor.Platform.Application.Common;
using Kinevor.Platform.Application.Exceptions;
using Kinevor.Platform.Domain.Common;
using MediatR;

namespace Kinevor.Modules.IdentityAccess.Application;

/// <summary>Enrolment material returned once, before MFA is confirmed.</summary>
/// <param name="Secret">Base32 TOTP secret for manual entry.</param>
/// <param name="OtpAuthUri">URI an authenticator app can scan.</param>
public record MfaEnrolmentDto(string Secret, string OtpAuthUri);

/// <summary>Plaintext recovery codes returned once, when MFA is confirmed.</summary>
/// <param name="RecoveryCodes">Single-use codes; only hashes are stored.</param>
public record MfaRecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

/// <summary>Starts MFA enrolment for the signed-in account.</summary>
public record BeginMfaEnrolmentCommand : IRequest<MfaEnrolmentDto>;

/// <summary>Handles <see cref="BeginMfaEnrolmentCommand"/>.</summary>
public class BeginMfaEnrolmentHandler(
    IUserAccountRepository users,
    ICurrentUser currentUser,
    IMfaService mfa,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<BeginMfaEnrolmentCommand, MfaEnrolmentDto>
{
    /// <summary>Generates a secret and stores it as pending until a code is confirmed.</summary>
    public async Task<MfaEnrolmentDto> Handle(BeginMfaEnrolmentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await MfaAccountLoader.LoadAsync(users, currentUser, ct);
        if (account.MfaEnabled)
            throw new ConflictException("MFA is already enabled. Disable it before enrolling again.");

        var secret = mfa.GenerateSecret();
        account.BeginMfaEnrolment(secret);
        users.Update(account);
        await unitOfWork.SaveChangesAsync(ct);

        return new MfaEnrolmentDto(secret, mfa.BuildOtpAuthUri(account, secret));
    }
}

/// <summary>Confirms MFA with the first valid code and returns recovery codes once.</summary>
/// <param name="Code">Six-digit code from the authenticator app.</param>
public record ConfirmMfaCommand(string Code) : IRequest<MfaRecoveryCodesDto>;

/// <summary>Handles <see cref="ConfirmMfaCommand"/>.</summary>
public class ConfirmMfaHandler(
    IUserAccountRepository users,
    ICurrentUser currentUser,
    IMfaService mfa,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<ConfirmMfaCommand, MfaRecoveryCodesDto>
{
    /// <summary>Enables MFA and persists hashed recovery codes.</summary>
    public async Task<MfaRecoveryCodesDto> Handle(ConfirmMfaCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await MfaAccountLoader.LoadAsync(users, currentUser, ct);
        if (account.MfaEnabled)
            throw new ConflictException("MFA is already enabled.");
        if (string.IsNullOrWhiteSpace(account.MfaSecret))
            throw new DomainException("Start MFA enrolment before confirming it.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!mfa.VerifyCode(account.MfaSecret, command.Code, now))
            throw new AuthenticationFailedException("Invalid verification code.");

        var recoveryCodes = mfa.GenerateRecoveryCodes();
        account.ConfirmMfa(recoveryCodes.Select(mfa.HashRecoveryCode));
        users.Update(account);
        await unitOfWork.SaveChangesAsync(ct);
        return new MfaRecoveryCodesDto(recoveryCodes);
    }
}

/// <summary>Disables MFA after verifying a current code.</summary>
/// <param name="Code">TOTP or recovery code.</param>
public record DisableMfaCommand(string Code) : IRequest<Result>;

/// <summary>Handles <see cref="DisableMfaCommand"/>.</summary>
public class DisableMfaHandler(
    IUserAccountRepository users,
    ICurrentUser currentUser,
    IMfaService mfa,
    IIdentityUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : IRequestHandler<DisableMfaCommand, Result>
{
    /// <summary>Verifies the code, then clears the secret and recovery codes.</summary>
    public async Task<Result> Handle(DisableMfaCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await MfaAccountLoader.LoadAsync(users, currentUser, ct);
        if (!account.MfaEnabled)
            throw new DomainException("MFA is not enabled.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var totpValid = !string.IsNullOrWhiteSpace(account.MfaSecret)
            && mfa.VerifyCode(account.MfaSecret, command.Code, now);
        var recoveryValid = !totpValid
            && account.ConsumeRecoveryCode(mfa.HashRecoveryCode(command.Code));

        if (!totpValid && !recoveryValid)
            throw new AuthenticationFailedException("Invalid verification code.");

        account.DisableMfa();
        users.Update(account);
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Loads the signed-in account for MFA operations.</summary>
internal static class MfaAccountLoader
{
    internal static async Task<UserAccount> LoadAsync(
        IUserAccountRepository users,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        if (currentUser.UserId is null)
            throw new AuthenticationFailedException("No authenticated account.");

        return await users.GetByIdAsync(currentUser.UserId.Value, ct)
            ?? throw new NotFoundException("UserAccount", currentUser.UserId.Value);
    }
}
