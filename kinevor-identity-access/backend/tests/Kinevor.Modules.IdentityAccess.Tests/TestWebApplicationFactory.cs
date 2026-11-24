using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kinevor.Modules.IdentityAccess.Tests;

/// <summary>Hosts the demo API against a unique temporary SQLite database and a manual clock.</summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"kinevor-identity-{Guid.NewGuid():N}.db");

    /// <summary>Clock shared by the API handlers; advance it to move TOTP steps or expire lockouts.</summary>
    public ManualTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Temporary SQLite database used by the host.</summary>
    public string DatabasePath => _databasePath;

    /// <summary>Points the host at the temporary database, deterministic key and manual clock.</summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Identity", $"Data Source={_databasePath}");
        builder.UseSetting("Jwt:Issuer", "kinevor.identity.tests");
        builder.UseSetting("Jwt:Audience", "kinevor.api.tests");
        builder.UseSetting("Jwt:SigningKey", "kinevor-tests-signing-key-32-characters-minimum");
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    /// <summary>Clears pooled connections before deleting the temporary database.</summary>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
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
