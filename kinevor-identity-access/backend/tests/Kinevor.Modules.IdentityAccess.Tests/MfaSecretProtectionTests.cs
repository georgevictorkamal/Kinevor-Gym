using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kinevor.Modules.IdentityAccess.Application;
using Microsoft.Data.Sqlite;

namespace Kinevor.Modules.IdentityAccess.Tests;

/// <summary>Verifies that TOTP secrets are encrypted before they reach the database.</summary>
public sealed class MfaSecretProtectionTests
{
    /// <summary>Enrolment stores ciphertext, and the plaintext secret still confirms and signs in.</summary>
    [Fact]
    public async Task Enrolment_StoresCiphertextAndStillVerifies()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@kinevor.demo",
            password = "Kinevor!2026",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var loginDocument = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var accessToken = loginDocument.RootElement
            .GetProperty("data")
            .GetProperty("accessToken")
            .GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var enrol = await client.PostAsync("/api/v1/auth/mfa/enrol", null);
        Assert.Equal(HttpStatusCode.OK, enrol.StatusCode);

        using var enrolDocument = JsonDocument.Parse(await enrol.Content.ReadAsStringAsync());
        var secret = enrolDocument.RootElement
            .GetProperty("data")
            .GetProperty("secret")
            .GetString()!;

        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();
        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT MfaSecret FROM UserAccounts WHERE Email = $email";
        read.Parameters.AddWithValue("$email", "admin@kinevor.demo");
        var stored = Assert.IsType<string>(await read.ExecuteScalarAsync());

        Assert.NotEqual(secret, stored);
        Assert.DoesNotContain(secret, stored, StringComparison.Ordinal);
        Assert.True(stored.Length > secret.Length);

        var confirm = await client.PostAsJsonAsync("/api/v1/auth/mfa/confirm", new
        {
            code = Totp.ComputeCode(secret, factory.Clock.GetUtcNow().UtcDateTime),
        });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // A fresh step proves the stored ciphertext round-trips during login too.
        factory.Clock.UtcNow = factory.Clock.UtcNow.AddSeconds(Totp.StepSeconds);
        using var mfaClient = factory.CreateClient();
        var mfaLogin = await mfaClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@kinevor.demo",
            password = "Kinevor!2026",
            mfaCode = Totp.ComputeCode(secret, factory.Clock.GetUtcNow().UtcDateTime),
        });
        Assert.Equal(HttpStatusCode.OK, mfaLogin.StatusCode);
    }
}
