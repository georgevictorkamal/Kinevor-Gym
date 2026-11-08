using Microsoft.Extensions.Configuration;

namespace Kinevor.Platform.Api;

/// <summary>CORS configuration shared by the demo hosts.</summary>
public static class CorsConfiguration
{
    /// <summary>
    /// Reads <c>Cors:AllowedOrigins</c> from configuration and falls back to
    /// the local development origins supplied by the host.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="defaults">Origins used when no configuration is present.</param>
    /// <returns>The allowed origins.</returns>
    public static string[] GetAllowedOrigins(
        this IConfiguration configuration,
        params string[] defaults)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration
            .GetSection("Cors:AllowedOrigins")
            .GetChildren()
            .Select(section => section.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        return configured.Length > 0 ? configured : defaults;
    }
}
