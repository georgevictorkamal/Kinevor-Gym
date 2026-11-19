using System.Data.Common;
using Kinevor.Modules.Trainers.Domain;
using Kinevor.Modules.Trainers.EntityFrameworkCore;
using Kinevor.Platform.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kinevor.Modules.Trainers.Tests;

/// <summary>EF Core behavior for the trainer repository.</summary>
public sealed class TrainerRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TrainersDbContext _db = null!;
    private TrainerRepository _repository = null!;

    /// <summary>Creates an isolated in-memory SQLite database.</summary>
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TrainersDbContext>()
            .UseSqlite(_connection)
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

    /// <summary>Search translates text filtering and paging into SQL rather than loading all rows into memory.</summary>
    [Fact]
    public async Task Search_TranslatesFilterToSqlQuery()
    {
        await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        await AddTrainer("Omar", "Khaled", "omar@kinevor.demo");

        var interceptor = new SqlCaptureInterceptor();
        var options = new DbContextOptionsBuilder<TrainersDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        await using var instrumentedDb = new TrainersDbContext(options);
        var repo = new TrainerRepository(instrumentedDb);

        var result = await repo.SearchAsync("Layla Hassan", 1, 10, onlyActive: false);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Contains(interceptor.Commands, sql =>
            sql.Contains("LIKE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
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

    /// <summary>E-mail search matches only the address, not names.</summary>
    [Fact]
    public async Task Search_MatchesEmailAddressOnly()
    {
        await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        await AddTrainer("Omar", "Khaled", "omar@kinevor.demo");

        var byFullAddress = await _repository.SearchAsync("OMAR@KINEVOR", 1, 10, onlyActive: false);
        var byDomain = await _repository.SearchAsync("kinevor", 1, 10, onlyActive: false);

        Assert.Single(byFullAddress.Items);
        Assert.Equal("Omar", byFullAddress.Items[0].FirstName);
        Assert.Equal(2, byDomain.TotalCount);
    }

    /// <summary>LIKE wildcards in user input are matched literally, not as patterns.</summary>
    [Fact]
    public async Task Search_TreatsWildcardsLiterally()
    {
        await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");
        await AddTrainer("Omar", "Khaled", "omar@kinevor.demo");

        var percent = await _repository.SearchAsync("%", 1, 10, onlyActive: false);
        var underscore = await _repository.SearchAsync("_", 1, 10, onlyActive: false);

        Assert.Empty(percent.Items);
        Assert.Empty(underscore.Items);
    }

    
    /// <summary>E-mail search is translated to SQL against the Email column, not evaluated in memory.</summary>
    [Fact]
    public async Task Search_EmailPredicateRunsInSql()
    {
        await AddTrainer("Layla", "Hassan", "layla@kinevor.demo");

        var interceptor = new SqlCaptureInterceptor();
        var options = new DbContextOptionsBuilder<TrainersDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(interceptor)
            .Options;

        await using var instrumentedDb = new TrainersDbContext(options);
        var repo = new TrainerRepository(instrumentedDb);

        var result = await repo.SearchAsync("kinevor", 1, 10, onlyActive: false);


        Assert.Single(result.Items);
        Assert.Contains(interceptor.Commands, sql =>
            sql.Contains("Email", StringComparison.Ordinal) &&
            sql.Contains("LIKE", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("LOWER", StringComparison.OrdinalIgnoreCase));


    }
    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
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
