using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Kinevor.Platform.Api;

/// <summary>Environment rules shared by the Kinevor demo API hosts.</summary>
public static class DemoEnvironment
{
    /// <summary>The dedicated public demo environment name.</summary>
    public const string Demo = "Demo";

    /// <summary>
    /// Throws unless the host is running in <c>Development</c> or <c>Demo</c>.
    /// Demo authentication and seeded data must never be available in
    /// <c>Production</c>.
    /// </summary>
    /// <param name="app">The web application being started.</param>
    public static void EnsureDemoHost(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDemoOrDevelopment())
        {
            throw new InvalidOperationException(
                "The Kinevor demo API only runs in Development or Demo.");
        }
    }

    /// <summary>Returns whether the host may expose demo-only behaviour.</summary>
    /// <param name="environment">The host environment.</param>
    /// <returns><see langword="true"/> for Development and Demo.</returns>
    public static bool IsDemoOrDevelopment(this IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        return environment.IsDevelopment()
            || environment.IsEnvironment(Demo);
    }
}
