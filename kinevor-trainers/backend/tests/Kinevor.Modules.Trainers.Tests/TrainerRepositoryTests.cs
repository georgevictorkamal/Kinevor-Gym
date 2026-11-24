using Kinevor.Modules.Trainers.Domain;
using Kinevor.Modules.Trainers.EntityFrameworkCore;
using Kinevor.Platform.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Kinevor.Modules.Trainers.Tests;

/// <summary>EF Core behavior for the trainer repository.</summary>
public sealed class TrainerRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TrainersDbContext _db = null!;
    private TrainerRepository _repository = null!;
    private readonly SqlCaptureInterceptor _capture = new();

    /// <summary>Creates an isolated in-memory SQLite database.</summary>
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TrainersDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_capture)
            .Options;

        _db = new TrainersDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repository = new TrainerRepository(_db);
    }

    /// <summary>Releases the database and connection.</summary>
    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>Active filtering excludes non-active trainers.</summary>
    [Fact]
    public async Task GetActive_ExcludesInactiveTrainers()
    {
        var active = await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        var inactive = await AddTrainer("Omar", "Khaled", "omar@kinevor.demo");
        inactive.SetStatus(TrainerStatus.Inactive);
        await _db.SaveChangesAsync();

        var result = await _repository.GetActiveAsync();

        Assert.Single(result);
        Assert.Equal(active.Id, result[0].Id);
    }

    /// <summary>Search matches names and e-mails case-insensitively and pages the result.</summary>
    [Fact]
    public async Task Search_FiltersByNameAndEmail()
    {
        await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        await AddTrainer("Omar", "Khaled", "omar@kinevor.demo");
        await AddTrainer("Nour", "Adel", "nour@kinevor.demo");

        var byName = await _repository.SearchAsync("layla", 1, 10, onlyActive: false);
        var byEmail = await _repository.SearchAsync("KHALED", 1, 10, onlyActive: false);
        var page = await _repository.SearchAsync(null, 1, 2, onlyActive: false);

        Assert.Single(byName.Items);
        Assert.Single(byEmail.Items);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    /// <summary>Search filtering and paging are translated into SQL rather than evaluated in memory.</summary>
    [Fact]
    public async Task Search_TranslatesFilterAndPagingToSql()
    {
        await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        await AddTrainer("Omar", "Khaled", "omar@kinevor.demo");
        _capture.Commands.Clear();

        var result = await _repository.SearchAsync("layla", 1, 10, onlyActive: false);

        Assert.Single(result.Items);
        var command = Assert.Single(_capture.Commands
            .Where(sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase))
            .ToList());
        Assert.Contains("WHERE", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIKE", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOWER", command, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Search treats SQL wildcard characters as literal input.</summary>
    [Fact]
    public async Task Search_EscapesLikeWildcards()
    {
        await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        await AddTrainer("Omar", "Khaled", "omar@kinevor.demo");

        var percent = await _repository.SearchAsync("layla%", 1, 10, onlyActive: false);
        var underscore = await _repository.SearchAsync("_ayla", 1, 10, onlyActive: false);
        var everyRecord = await _repository.SearchAsync("%", 1, 10, onlyActive: false);

        Assert.Empty(percent.Items);
        Assert.Empty(underscore.Items);
        Assert.Empty(everyRecord.Items);
    }

    /// <summary>Search can restrict results to active trainers.</summary>
    [Fact]
    public async Task Search_OnlyActiveFiltersStatus()
    {
        await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        var inactive = await AddTrainer("Omar", "Khaled", "omar@kinevor.demo");
        inactive.SetStatus(TrainerStatus.Inactive);
        await _db.SaveChangesAsync();

        var result = await _repository.SearchAsync(null, 1, 10, onlyActive: true);

        Assert.Single(result.Items);
        Assert.Equal("Layla", result.Items[0].FirstName);
    }

    /// <summary>Inserting through the unit of work stamps the creation timestamp.</summary>
    [Fact]
    public async Task SaveChanges_StampsCreatedAt()
    {
        var trainer = await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        await _db.SaveChangesAsync();

        var stored = await _db.Trainers.SingleAsync(t => t.Id == trainer.Id);
        Assert.True(stored.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    private async Task<Trainer> AddTrainer(string firstName, string lastName, string email)
    {
        var result = Trainer.Create(firstName, lastName, email, "+201001234567",
            "Coach", [ClassType.Yoga], ["RYT-500"], 5);
        Assert.True(result.IsSuccess);
        _repository.Add(result.Value);
        await _db.SaveChangesAsync();
        return result.Value;
    }

    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
