using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Kinevor.Platform.Api;

/// <summary>Per-instance request rate limiting for public demo endpoints.</summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Registers a fixed-window, per-client-IP global limiter. This is a demo
    /// safeguard, not a substitute for an edge WAF in production.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="permitLimit">Requests allowed per window.</param>
    /// <param name="windowSeconds">Window length in seconds.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddKinevorRateLimiting(
        this IServiceCollection services,
        int permitLimit = 120,
        int windowSeconds = 60)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(permitLimit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(
                    client,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });
        });

        return services;
    }
}
