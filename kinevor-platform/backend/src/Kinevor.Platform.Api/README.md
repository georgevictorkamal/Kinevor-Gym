# Kinevor.Platform.Api

Shared ASP.NET Core contracts for Kinevor feature modules.

- `ApiResponse<T>` success and failure envelope.
- Role constants for admin, staff, trainer and member users.
- `AddKinevorPolicies()` authorization policy registration.
- `AddKinevorOpenTelemetry()` opt-in traces and metrics with an OTLP exporter.

## Observability

```csharp
builder.Services.AddKinevorOpenTelemetry(builder.Configuration);
```

Set `OpenTelemetry:Enabled=true`, optionally `OpenTelemetry:ServiceName`, and the
standard `OTEL_EXPORTER_OTLP_*` environment variables. ASP.NET Core and
`HttpClient` are instrumented. The method is a no-op when the flag is absent.

EF Core instrumentation is not included because its dependency is still
prerelease. Reference `OpenTelemetry.Instrumentation.EntityFrameworkCore` in the
host and add `AddEntityFrameworkCoreInstrumentation()` to the tracing pipeline
yourself when you need database spans.

## Install

```bash
dotnet add package Kinevor.Platform.Api --version 0.3.1
```

The package references `Kinevor.Platform.Application` and the ASP.NET Core
shared framework.
