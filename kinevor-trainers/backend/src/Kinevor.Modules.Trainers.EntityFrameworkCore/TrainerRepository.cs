using Kinevor.Modules.Trainers.Application;
using Kinevor.Modules.Trainers.Domain;
using Kinevor.Platform.Domain.Enums;
using Kinevor.Platform.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Kinevor.Modules.Trainers.EntityFrameworkCore;

/// <summary>EF Core repository for the <see cref="Trainer"/> aggregate.</summary>
public sealed class TrainerRepository(TrainersDbContext db) : ITrainerRepository
{
    /// <summary>Loads a tracked trainer by id.</summary>
    public Task<Trainer?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Trainers.FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <summary>Loads every trainer ordered by last name.</summary>
    public async Task<IReadOnlyList<Trainer>> ListAsync(CancellationToken ct = default) =>
        await db.Trainers.AsNoTracking()
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .ToListAsync(ct);

    /// <summary>Loads trainers matching a specification.</summary>
    public async Task<IReadOnlyList<Trainer>> ListAsync(ISpecification<Trainer> spec, CancellationToken ct = default) =>
        await Apply(spec).ToListAsync(ct);

    /// <summary>Counts trainers matching a specification.</summary>
    public Task<int> CountAsync(ISpecification<Trainer> spec, CancellationToken ct = default) =>
        db.Trainers.AsNoTracking().Where(spec.Criteria).CountAsync(ct);

    /// <summary>Whether any trainer matches a specification.</summary>
    public Task<bool> AnyAsync(ISpecification<Trainer> spec, CancellationToken ct = default) =>
        db.Trainers.AsNoTracking().Where(spec.Criteria).AnyAsync(ct);

    /// <summary>Stages a new trainer for insertion.</summary>
    public void Add(Trainer entity) => db.Trainers.Add(entity);

    /// <summary>Stages an existing trainer for update.</summary>
    public void Update(Trainer entity) => db.Trainers.Update(entity);

    /// <summary>Stages a trainer for removal.</summary>
    public void Remove(Trainer entity) => db.Trainers.Remove(entity);

    /// <summary>Returns trainers whose status is Active.</summary>
    public async Task<IReadOnlyList<Trainer>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Trainers.AsNoTracking()
            .Where(t => t.Status == TrainerStatus.Active)
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .ToListAsync(ct);

    /// <summary>
    /// Searches trainers by name or e-mail in the database, optionally restricting to active ones.
    /// </summary>
    public async Task<(IReadOnlyList<Trainer> Items, int TotalCount)> SearchAsync(
        string? search, int page, int pageSize, bool onlyActive = false, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Trainers.AsNoTracking();
        if (onlyActive)
            query = query.Where(t => t.Status == TrainerStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = search.Trim()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_")
                .ToLowerInvariant();
            var pattern = $"%{escaped}%";

            query = query.Where(t =>
                EF.Functions.Like((t.FirstName + " " + t.LastName).ToLower(), pattern, "\\")
                || EF.Functions.Like(((string)(object)t.Email).ToLower(), pattern, "\\"));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(t => t.LastName)
            .ThenBy(t => t.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    private IQueryable<Trainer> Apply(ISpecification<Trainer> spec)
    {
        IQueryable<Trainer> query = db.Trainers.AsNoTracking().Where(spec.Criteria);

        foreach (var include in spec.Includes)
            query = query.Include(include);

        if (spec.OrderBy is not null)
            query = query.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending is not null)
            query = query.OrderByDescending(spec.OrderByDescending);

        if (spec.Skip.HasValue)
            query = query.Skip(spec.Skip.Value);

        if (spec.Take.HasValue)
            query = query.Take(spec.Take.Value);

        return query;
    }
}
