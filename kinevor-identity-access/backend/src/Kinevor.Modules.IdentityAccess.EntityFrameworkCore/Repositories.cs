using Kinevor.Modules.IdentityAccess.Application;
using Kinevor.Modules.IdentityAccess.Domain;
using Kinevor.Platform.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Kinevor.Modules.IdentityAccess.EntityFrameworkCore;

/// <summary>EF Core account repository.</summary>
public sealed class UserAccountRepository(IdentityDbContext db) : IUserAccountRepository
{
    /// <summary>Finds an account by normalised e-mail.</summary>
    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var parsed = Email.Create(email);
        if (parsed.IsFailure)
            return null;

        var target = parsed.Value;
        return await db.UserAccounts.FirstOrDefaultAsync(account => account.Email == target, ct);
    }

    /// <summary>Finds an account by id.</summary>
    public Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.UserAccounts.FirstOrDefaultAsync(account => account.Id == id, ct);

    /// <summary>Lists accounts ordered by display name.</summary>
    public async Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken ct = default) =>
        await db.UserAccounts.AsNoTracking().OrderBy(account => account.DisplayName).ToListAsync(ct);

    /// <summary>Tracks a new account.</summary>
    public void Add(UserAccount account) => db.UserAccounts.Add(account);

    /// <summary>Marks an account as modified.</summary>
    public void Update(UserAccount account) => db.UserAccounts.Update(account);

    /// <summary>Reloads a tracked account after a concurrency conflict, including its concurrency token.</summary>
    public async Task<bool> ReloadAsync(UserAccount account, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var current = await db.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == account.Id, ct);
        if (current is null)
            return false;

        var entry = db.Entry(account);
        entry.CurrentValues.SetValues(current);
        entry.OriginalValues.SetValues(current);
        entry.State = EntityState.Unchanged;
        return true;
    }
}

/// <summary>EF Core refresh-token repository.</summary>
public sealed class RefreshTokenRepository(IdentityDbContext db) : IRefreshTokenRepository
{
    /// <summary>Finds a refresh token by hash.</summary>
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(token => token.TokenHash == tokenHash, ct);

    /// <summary>Tracks a new refresh token.</summary>
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    /// <summary>Revokes every unrevoked refresh token for the account.</summary>
    public async Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        var active = await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in active)
            token.Revoke(now);
    }
}
