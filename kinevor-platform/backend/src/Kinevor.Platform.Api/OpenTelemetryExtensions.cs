using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Kinevor.Platform.Api;

/// <summary>
/// Opt-in OpenTelemetry wiring for Kinevor hosts. Tracing and metrics stay off unless
/// <c>OpenTelemetry:Enabled</c> is true, so demos and tests incur no exporter cost.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>Configuration key that enables the pipeline.</summary>
    public const string EnabledKey = "OpenTelemetry:Enabled";

    /// <summary>Configuration key for the service name reported to the backend.</summary>
    public const string ServiceNameKey = "OpenTelemetry:ServiceName";

    /// <summary>
    /// Adds ASP.NET Core and HTTP client instrumentation plus the OTLP exporter.
    /// The exporter reads <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>, <c>OTEL_EXPORTER_OTLP_HEADERS</c>
    /// and <c>OTEL_EXPORTER_OTLP_PROTOCOL</c> from the environment, so no endpoint is hard-coded.
    /// EF Core instrumentation is intentionally not a dependency of this stable package; hosts
    /// that want it reference the prerelease package and register it themselves.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Host configuration read for the enable flag and service name.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddKinevorOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetValue<bool>(EnabledKey))
            return services;

        var serviceName = configuration[ServiceNameKey];
        if (string.IsNullOrWhiteSpace(serviceName))
            serviceName = "kinevor-api";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation())
            .UseOtlpExporter();

        return services;
    }
}
