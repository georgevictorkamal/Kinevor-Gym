# Changelog

All notable changes to this project are documented here.

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
