using Kinevor.Modules.IdentityAccess.Application;
using Kinevor.Platform.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kinevor.Modules.IdentityAccess.Api;

/// <summary>Authentication endpoints for the identity module.</summary>
[ApiController, Route("api/v1/auth")]
public class AuthController(ISender sender) : ControllerBase
{
    /// <summary>Authenticates an account and returns an access/refresh token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthSessionDto>>> Login(
        [FromBody] LoginCommand command, CancellationToken ct)
    {
        var session = await sender.Send(command, ct);
        return Ok(ApiResponse<AuthSessionDto>.Ok(session, "Signed in."));
    }

    /// <summary>Rotates a refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthSessionDto>>> Refresh(
        [FromBody] RefreshTokenCommand command, CancellationToken ct)
    {
        var session = await sender.Send(command, ct);
        return Ok(ApiResponse<AuthSessionDto>.Ok(session, "Session refreshed."));
    }

    /// <summary>Revokes a refresh token.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> Logout(
        [FromBody] LogoutCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return Ok(ApiResponse.Ok("Signed out."));
    }

    /// <summary>Returns the currently authenticated account.</summary>
    [HttpGet("session")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserAccountDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserAccountDto>>> Session(CancellationToken ct)
    {
        var account = await sender.Send(new GetSessionQuery(), ct);
        return Ok(ApiResponse<UserAccountDto>.Ok(account));
    }

    /// <summary>Lists accounts for administrators.</summary>
    [HttpGet("accounts")]
    [Authorize(Policy = Policies.AdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserAccountDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserAccountDto>>>> Accounts(CancellationToken ct)
    {
        var accounts = await sender.Send(new GetAccountsQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<UserAccountDto>>.Ok(accounts));
    }

    /// <summary>Starts TOTP enrolment for the signed-in account.</summary>
    [HttpPost("mfa/enrol")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<MfaEnrolmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MfaEnrolmentDto>>> BeginMfaEnrolment(CancellationToken ct)
    {
        var enrolment = await sender.Send(new BeginMfaEnrolmentCommand(), ct);
        return Ok(ApiResponse<MfaEnrolmentDto>.Ok(enrolment, "MFA enrolment started."));
    }

    /// <summary>Confirms enrolment with the first TOTP code and returns recovery codes once.</summary>
    [HttpPost("mfa/confirm")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<MfaRecoveryCodesDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MfaRecoveryCodesDto>>> ConfirmMfa(
        [FromBody] ConfirmMfaCommand command, CancellationToken ct)
    {
        var codes = await sender.Send(command, ct);
        return Ok(ApiResponse<MfaRecoveryCodesDto>.Ok(codes, "MFA enabled."));
    }

    /// <summary>Disables MFA after verifying a current TOTP or recovery code.</summary>
    [HttpPost("mfa/disable")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> DisableMfa(
        [FromBody] DisableMfaCommand command, CancellationToken ct)
    {
        await sender.Send(command, ct);
        return Ok(ApiResponse.Ok("MFA disabled."));
    }
}
