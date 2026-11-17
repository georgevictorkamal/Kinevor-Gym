using Kinevor.Modules.IdentityAccess.Domain;
using Kinevor.Modules.IdentityAccess.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kinevor.Modules.IdentityAccess.Tests;

/// <summary>Optimistic-concurrency guarantees for refresh-token rotation.</summary>
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
