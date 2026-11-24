using Kinevor.Modules.IdentityAccess.Application;
using Kinevor.Modules.IdentityAccess.Domain;
using Kinevor.Modules.IdentityAccess.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kinevor.Modules.IdentityAccess.Tests;

/// <summary>Optimistic-concurrency guarantees for tokens, accounts and MFA state.</summary>
public sealed class IdentityConcurrencyTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"kinevor-identity-concurrency-{Guid.NewGuid():N}.db");

    /// <summary>Two writers that revoke the same token: the second one must lose.</summary>
    [Fact]
    public async Task RevokingSameRefreshTokenTwice_SecondWriterIsRejected()
    {
        var now = DateTime.UtcNow;
        var token = RefreshToken.Create(Guid.NewGuid(), "concurrency-hash", now);

        await using (var seed = CreateContext())
        {
            await seed.Database.EnsureCreatedAsync();
            seed.RefreshTokens.Add(token);
            await seed.SaveChangesAsync();
        }

        await using var first = CreateContext();
        await using var second = CreateContext();

        var firstToken = await first.RefreshTokens.SingleAsync();
        var secondToken = await second.RefreshTokens.SingleAsync();

        firstToken.Revoke(now.AddMinutes(1));
        Assert.True(await first.TrySaveChangesAsync());

        secondToken.Revoke(now.AddMinutes(1));
        Assert.False(await second.TrySaveChangesAsync());
    }

    /// <summary>Two writers that consume the same recovery code: the second one must lose.</summary>
    [Fact]
    public async Task ConsumingSameRecoveryCodeTwice_SecondWriterIsRejected()
    {
        var hash = Totp.HashRecoveryCode("ABCD-EFGH");
        var account = CreateMfaAccount();
        await SeedAccountAsync(account);

        await using var first = CreateContext();
        await using var second = CreateContext();

        var firstAccount = await first.UserAccounts.SingleAsync();
        var secondAccount = await second.UserAccounts.SingleAsync();

        Assert.True(firstAccount.ConsumeRecoveryCode(hash));
        Assert.True(await first.TrySaveChangesAsync());

        // Each context still has the code locally; the concurrency token decides the winner.
        Assert.True(secondAccount.ConsumeRecoveryCode(hash));
        Assert.False(await second.TrySaveChangesAsync());
    }

    /// <summary>A retried failed-login write advances the counter instead of overwriting it.</summary>
    [Fact]
    public async Task FailedLoginsFromTwoContexts_ReloadAndBothCount()
    {
        var now = DateTime.UtcNow;
        var account = CreateAccount();
        await SeedAccountAsync(account);

        await using var first = CreateContext();
        await using var second = CreateContext();
        var repository = new UserAccountRepository(second);

        var firstAccount = await first.UserAccounts.SingleAsync();
        var secondAccount = await second.UserAccounts.SingleAsync();

        firstAccount.RegisterFailedLogin(now, 5, TimeSpan.FromMinutes(15));
        Assert.True(await first.TrySaveChangesAsync());

        secondAccount.RegisterFailedLogin(now, 5, TimeSpan.FromMinutes(15));
        Assert.False(await second.TrySaveChangesAsync());

        Assert.True(await repository.ReloadAsync(secondAccount));
        secondAccount.RegisterFailedLogin(now, 5, TimeSpan.FromMinutes(15));
        Assert.True(await second.TrySaveChangesAsync());

        await using var verification = CreateContext();
        var reloaded = await verification.UserAccounts.SingleAsync();
        Assert.Equal(2, reloaded.FailedLoginAttempts);
    }

    /// <summary>Two writers that accept the same TOTP step: exactly one must win.</summary>
    [Fact]
    public async Task AcceptingSameTotpStepTwice_SecondWriterIsRejected()
    {
        var account = CreateMfaAccount();
        await SeedAccountAsync(account);

        await using var first = CreateContext();
        await using var second = CreateContext();

        var firstAccount = await first.UserAccounts.SingleAsync();
        var secondAccount = await second.UserAccounts.SingleAsync();

        Assert.True(firstAccount.TryAcceptMfaStep(101));
        Assert.True(await first.TrySaveChangesAsync());

        Assert.True(secondAccount.TryAcceptMfaStep(101));
        Assert.False(await second.TrySaveChangesAsync());
    }

    private static UserAccount CreateAccount()
    {
        var result = UserAccount.Create(
            "member@kinevor.demo", "Member", "Member", "encoded-hash", DateTime.UtcNow);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static UserAccount CreateMfaAccount()
    {
        var account = CreateAccount();
        account.BeginMfaEnrolment("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ");
        Assert.True(account.TryAcceptMfaStep(100));
        account.ConfirmMfa([Totp.HashRecoveryCode("ABCD-EFGH")]);
        return account;
    }

    private async Task SeedAccountAsync(UserAccount account)
    {
        await using var seed = CreateContext();
        await seed.Database.EnsureCreatedAsync();
        seed.UserAccounts.Add(account);
        await seed.SaveChangesAsync();
    }

    private IdentityDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options);

    /// <summary>Clears pooled connections before deleting the temporary database.</summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // A finalizer still holding the file must not fail the test run.
        }
    }
}
