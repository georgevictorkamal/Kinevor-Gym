using Kinevor.Platform.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Kinevor.Platform.Tests;

/// <summary>Opt-in behavior of the shared OpenTelemetry wiring.</summary>
public class OpenTelemetryExtensionsTests
{
    /// <summary>The pipeline stays off unless the host explicitly enables it.</summary>
    [Fact]
    public void Disabled_DoesNotRegisterProviders()
    {
        var services = new ServiceCollection();
        var configuration = Configuration(("OpenTelemetry:Enabled", "false"));

        var returned = services.AddKinevorOpenTelemetry(configuration);

        Assert.Same(services, returned);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(TracerProvider));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    /// <summary>Enabling the flag registers tracer and meter providers for the OTLP exporter.</summary>
    [Fact]
    public void Enabled_RegistersTracerAndMeterProviders()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = Configuration(
            ("OpenTelemetry:Enabled", "true"),
            ("OpenTelemetry:ServiceName", "kinevor-platform-tests"));

        services.AddKinevorOpenTelemetry(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());
    }

    /// <summary>Standard configuration values parse booleans case-insensitively.</summary>
    [Fact]
    public void Enabled_IsCaseInsensitive()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = Configuration(("OpenTelemetry:Enabled", "TRUE"));

        services.AddKinevorOpenTelemetry(configuration);

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<TracerProvider>());
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => (string?)pair.Value))
            .Build();
}
