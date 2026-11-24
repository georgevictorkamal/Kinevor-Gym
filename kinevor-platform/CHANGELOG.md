# Changelog

All notable changes to this project are documented here.

## Unreleased

### Added

- `AddKinevorOpenTelemetry()` in `Kinevor.Platform.Api`: opt-in ASP.NET Core and
  `HttpClient` traces and metrics with an OTLP exporter configured through
  standard `OTEL_EXPORTER_OTLP_*` variables.
- French (`fr`) UI catalogue with full key parity and a language registry entry.

### Changed

- `Kinevor.Platform.Api` no longer depends on the prerelease
  `OpenTelemetry.Instrumentation.EntityFrameworkCore` package. EF Core traces are
  an explicit host opt-in, so the stable package has no prerelease dependencies
  and no `NU5104` suppression.

## 0.1.1 - 2026-11-05

### Security

- Updated the platform web development toolchain to Vite 8.3.5 and
  `@angular-devkit/build-angular` 22.2.6.

### Changed

- Aligned package versions with the Kinevor 0.1.1 monorepo release. No public
  API changes.

## 0.1.0 - 2026-10-22

### Added

- `Kinevor.Platform.Domain` shared kernel: entities, aggregates, result and
  error types, value objects, enums and specifications.
- `Kinevor.Platform.Application` common contracts: repositories, unit of work,
  current user, branch context, paging and exceptions.
- `Kinevor.Platform.Api` response envelope and authorization policy helpers.
- `@georgevictorkamal/kinevor-platform-web` Angular package with API
  envelopes, authentication, i18n, notices and theme services.
- GitHub Actions CI and documented NuGet and npm publishing steps.
