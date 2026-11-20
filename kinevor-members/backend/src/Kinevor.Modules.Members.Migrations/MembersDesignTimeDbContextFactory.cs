using Kinevor.Modules.Members.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kinevor.Modules.Members.Migrations;

/// <summary>
/// Creates the members context for <c>dotnet ef</c>. The connection string is only used to
/// generate SQLite DDL and is never opened by the migration commands.
/// </summary>
public sealed class MembersDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MembersDbContext>
{
    /// <summary>Builds a context against a local SQLite file for migration scaffolding.</summary>
    /// <param name="args">EF Core arguments, ignored.</param>
    public MembersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("KINEVOR_MEMBERS_CONNECTION")
            ?? "Data Source=members-design.db";

        var options = new DbContextOptionsBuilder<MembersDbContext>()
            .UseSqlite(connectionString, sqlite =>
                sqlite.MigrationsAssembly(typeof(MembersDesignTimeDbContextFactory).Assembly.GetName().Name))
            .Options;
        return new MembersDbContext(options);
    }
}
