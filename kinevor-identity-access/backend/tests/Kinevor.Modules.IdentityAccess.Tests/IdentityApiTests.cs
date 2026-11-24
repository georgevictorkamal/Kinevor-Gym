using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kinevor.Modules.IdentityAccess.Application;

namespace Kinevor.Modules.IdentityAccess.Tests;

/// <summary>HTTP contract for the identity and access module.</summary>
public class IdentityApiTests
{
    /// <summary>Valid credentials return a JWT and a refresh token.</summary>
    [Fact]
    public async Task Login_ReturnsTokenPair()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var session = await LoginAsync(client, "admin@kinevor.demo", "Kinevor!2026");

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.Equal("Admin", session.Role);
        Assert.True(session.ExpiresAt > DateTime.UtcNow);
    }

    /// <summary>Wrong credentials return 401 with the shared failure envelope.</summary>
    [Fact]
    public async Task Login_RejectsWrongPassword()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "member@kinevor.demo",
            password = "not-the-password",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
    }

    /// <summary>The session endpoint requires a bearer token and returns the caller.</summary>
    [Fact]
    public async Task Session_RequiresBearerTokenAndReturnsAccount()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var anonymous = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var session = await LoginAsync(client, "trainer@kinevor.demo", "Kinevor!2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var account = document.RootElement.GetProperty("data");
        Assert.Equal("trainer@kinevor.demo", account.GetProperty("email").GetString());
        Assert.Equal("Trainer", account.GetProperty("role").GetString());
    }

    /// <summary>Refreshing rotates the token: the old token is rejected.</summary>
    [Fact]
    public async Task Refresh_RotatesRefreshToken()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var session = await LoginAsync(client, "staff@kinevor.demo", "Kinevor!2026");
        var refreshed = await RefreshAsync(client, session.RefreshToken);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = session.RefreshToken,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.NotEqual(session.RefreshToken, refreshed.RefreshToken);
    }

    /// <summary>Replaying a rotated token revokes the whole token family, including the newest one.</summary>
    [Fact]
    public async Task Refresh_ReplayOfRotatedToken_RevokesAllSessions()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var session = await LoginAsync(client, "staff@kinevor.demo", "Kinevor!2026");
        var refreshed = await RefreshAsync(client, session.RefreshToken);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var afterReplay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refreshed.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReplay.StatusCode);
    }

    /// <summary>Login e-mail lookup is case-insensitive.</summary>
    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var session = await LoginAsync(client, "  Admin@Kinevor.Demo ", "Kinevor!2026");
        Assert.Equal("Admin", session.Role);
    }

    /// <summary>Logout revokes the refresh token.</summary>
    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var session = await LoginAsync(client, "member@kinevor.demo", "Kinevor!2026");
        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new
        {
            refreshToken = session.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = session.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    /// <summary>Only administrators can list accounts.</summary>
    [Fact]
    public async Task Accounts_RequiresAdminRole()
    {
        using var factory = new TestWebApplicationFactory();
        using var memberClient = factory.CreateClient();
        var member = await LoginAsync(memberClient, "member@kinevor.demo", "Kinevor!2026");
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);

        var forbidden = await memberClient.GetAsync("/api/v1/auth/accounts");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var adminClient = factory.CreateClient();
        var admin = await LoginAsync(adminClient, "admin@kinevor.demo", "Kinevor!2026");
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var response = await adminClient.GetAsync("/api/v1/auth/accounts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(4, document.RootElement.GetProperty("data").GetArrayLength());
    }

    /// <summary>Five wrong passwords lock the account, and locked/unknown responses are identical.</summary>
    [Fact]
    public async Task Login_LocksAccountAfterRepeatedFailures()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await PostLoginAsync(client, "member@kinevor.demo", "not-the-password");
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var locked = await PostLoginAsync(client, "member@kinevor.demo", "Kinevor!2026");
        var unknown = await PostLoginAsync(client, "nobody@kinevor.demo", "Kinevor!2026");

        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(),
            await locked.Content.ReadAsStringAsync());
    }

    /// <summary>A successful login clears the failure counter before the threshold.</summary>
    [Fact]
    public async Task Login_SuccessResetsFailedAttempts()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 4; attempt++)
            await PostLoginAsync(client, "staff@kinevor.demo", "not-the-password");

        await LoginAsync(client, "staff@kinevor.demo", "Kinevor!2026");

        for (var attempt = 0; attempt < 4; attempt++)
            await PostLoginAsync(client, "staff@kinevor.demo", "not-the-password");

        await LoginAsync(client, "staff@kinevor.demo", "Kinevor!2026");
    }

    /// <summary>
    /// MFA enrolment, challenge, replay rejection, TOTP login and single-use recovery
    /// codes work end to end. The challenge is a 200 and does not count as a failure.
    /// </summary>
    [Fact]
    public async Task Mfa_EnrolChallengeReplayLoginAndRecoveryCode()
    {
        using var factory = new TestWebApplicationFactory();
        using var adminClient = factory.CreateClient();

        var (secret, recoveryCodes) = await EnrolMfaAsync(factory, adminClient);
        var confirmationCode = Totp.ComputeCode(secret, factory.Clock.GetUtcNow().UtcDateTime);

        // Five correct-password challenges would lock the account if they counted as failures.
        for (var attempt = 0; attempt < 5; attempt++)
            await AssertMfaRequiredAsync(factory, "admin@kinevor.demo", "Kinevor!2026");

        using var replayClient = factory.CreateClient();
        var replay = await PostLoginAsync(
            replayClient, "admin@kinevor.demo", "Kinevor!2026", mfaCode: confirmationCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(Totp.StepSeconds);
        using var withTotp = factory.CreateClient();
        var totpLogin = await PostLoginAsync(
            withTotp, "admin@kinevor.demo", "Kinevor!2026",
            mfaCode: Totp.ComputeCode(secret, factory.Clock.GetUtcNow().UtcDateTime));
        Assert.Equal(HttpStatusCode.OK, totpLogin.StatusCode);

        using var withRecovery = factory.CreateClient();
        var recoveryLogin = await PostLoginAsync(
            withRecovery, "admin@kinevor.demo", "Kinevor!2026",
            recoveryCode: recoveryCodes[0]);
        Assert.Equal(HttpStatusCode.OK, recoveryLogin.StatusCode);

        using var reusedRecovery = factory.CreateClient();
        var reused = await PostLoginAsync(
            reusedRecovery, "admin@kinevor.demo", "Kinevor!2026",
            recoveryCode: recoveryCodes[0]);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    /// <summary>A wrong password for an MFA account stays indistinguishable from an unknown account.</summary>
    [Fact]
    public async Task Login_WrongPasswordForMfaAccountRemainsGeneric()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await EnrolMfaAsync(factory, client);

        var wrong = await PostLoginAsync(client, "admin@kinevor.demo", "not-the-password");
        var unknown = await PostLoginAsync(client, "nobody@kinevor.demo", "Kinevor!2026");

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(),
            await wrong.Content.ReadAsStringAsync());
    }

    /// <summary>A wrong TOTP code counts toward the lockout, unlike a missing-code challenge.</summary>
    [Fact]
    public async Task Mfa_WrongCodeLocksAccount()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        var (secret, _) = await EnrolMfaAsync(factory, client);

        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var invalidCode = FindInvalidCode(secret, now);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await PostLoginAsync(
                client, "admin@kinevor.demo", "Kinevor!2026", mfaCode: invalidCode);
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        // Move to a fresh TOTP step so the correct code is not a replay; the lockout must still win.
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(Totp.StepSeconds);
        var locked = await PostLoginAsync(
            client, "admin@kinevor.demo", "Kinevor!2026",
            mfaCode: Totp.ComputeCode(secret, factory.Clock.GetUtcNow().UtcDateTime));
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);

        factory.Clock.UtcNow = factory.Clock.UtcNow.AddMinutes(16);
        var afterLock = await PostLoginAsync(
            client, "admin@kinevor.demo", "Kinevor!2026",
            mfaCode: Totp.ComputeCode(secret, factory.Clock.GetUtcNow().UtcDateTime));
        Assert.Equal(HttpStatusCode.OK, afterLock.StatusCode);
    }

    /// <summary>MFA endpoints require an authenticated caller.</summary>
    [Fact]
    public async Task MfaEnrolment_RequiresAuthentication()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/mfa/enrol", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(string Secret, IReadOnlyList<string> RecoveryCodes)> EnrolMfaAsync(
        TestWebApplicationFactory factory,
        HttpClient client)
    {
        var session = await LoginAsync(client, "admin@kinevor.demo", "Kinevor!2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var enrolResponse = await client.PostAsync("/api/v1/auth/mfa/enrol", null);
        Assert.Equal(HttpStatusCode.OK, enrolResponse.StatusCode);
        using var enrolDocument = JsonDocument.Parse(await enrolResponse.Content.ReadAsStringAsync());
        var enrolment = enrolDocument.RootElement.GetProperty("data");
        var secret = enrolment.GetProperty("secret").GetString()!;
        Assert.StartsWith("otpauth://totp/", enrolment.GetProperty("otpAuthUri").GetString());

        var confirmResponse = await client.PostAsJsonAsync("/api/v1/auth/mfa/confirm", new
        {
            code = Totp.ComputeCode(secret, factory.Clock.GetUtcNow().UtcDateTime),
        });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        using var confirmDocument = JsonDocument.Parse(await confirmResponse.Content.ReadAsStringAsync());
        var recoveryCodes = confirmDocument.RootElement.GetProperty("data")
            .GetProperty("recoveryCodes")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToList();
        Assert.Equal(8, recoveryCodes.Count);
        return (secret, recoveryCodes);
    }

    private static async Task AssertMfaRequiredAsync(
        TestWebApplicationFactory factory,
        string email,
        string password)
    {
        using var client = factory.CreateClient();
        var response = await PostLoginAsync(client, email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        var data = document.RootElement.GetProperty("data");
        Assert.Equal(LoginResult.MfaRequiredStatus, data.GetProperty("status").GetString());
        Assert.False(data.TryGetProperty("accessToken", out _));
    }

    private static string FindInvalidCode(string secret, DateTime now)
    {
        for (var candidate = 0; candidate < 1_000_000; candidate++)
        {
            var code = candidate.ToString("D6", CultureInfo.InvariantCulture);
            if (!Totp.VerifyCode(secret, code, now))
                return code;
        }

        throw new InvalidOperationException("Could not find an invalid TOTP code.");
    }

    private static Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client,
        string email,
        string password,
        string? mfaCode = null,
        string? recoveryCode = null) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password, mfaCode, recoveryCode });

    private static async Task<Session> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        return new Session(
            data.GetProperty("accessToken").GetString()!,
            data.GetProperty("refreshToken").GetString()!,
            data.GetProperty("expiresAt").GetDateTime(),
            data.GetProperty("user").GetProperty("role").GetString()!);
    }

    private static async Task<Session> RefreshAsync(HttpClient client, string refreshToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        return new Session(
            data.GetProperty("accessToken").GetString()!,
            data.GetProperty("refreshToken").GetString()!,
            data.GetProperty("expiresAt").GetDateTime(),
            data.GetProperty("user").GetProperty("role").GetString()!);
    }

    private sealed record Session(string AccessToken, string RefreshToken, DateTime ExpiresAt, string Role);
}
