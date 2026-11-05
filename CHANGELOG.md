# Changelog

All notable changes to Kinevor are documented here. The repository uses a single
`0.x` line while the platform contracts and module packages evolve together.

## 0.1.1 — 2026-11-05

### Security

- Updated the platform web workspace to Vite 8.3.5 and
  `@angular-devkit/build-angular` 22.2.6, resolving the open Vite and
  `launch-editor` dependency advisories. No runtime code changed.

### Changed

- Aligned all current package versions to `0.1.1`.
- Refreshed version references in package guides and architecture docs.
- Documented the release workflow in the repository layout.

## 0.1.0 — 2026-11-05

### Added

- Shared `kinevor-platform` kernel: domain primitives, application contracts,
  API response envelope and authorization policies.
- `@georgevictorkamal/kinevor-platform-web` Angular library with API
  envelopes, authentication, i18n/RTL, notices and theming.
- Ten business services, each with its own .NET API, SQLite database, tests and
  demo host:
  - identity & access
  - members
  - memberships
  - classes & bookings
  - check-ins & access
  - payments & billing
  - notifications
  - trainers
  - staff dashboard
  - public site
- Nine Angular libraries with runnable demo applications.
- Nine Flutter packages with runnable examples.
- 57 application screenshots covering web, mobile and API surfaces.
- Monorepo CI: platform build/pack plus backend, web and mobile module matrices.
- CodeQL analysis for C# and TypeScript.
- Dependabot updates for NuGet, npm, pub and GitHub Actions.
- Flutter format and `flutter analyze` checks in the mobile CI matrix.
- Tag-driven release workflow that packages the tracked source tree as one zip
  and publishes it with a GitHub release.
- Per-module READMEs, changelogs and publishing guides.
- Root architecture, security, contributing and code-of-conduct documentation.
- GitHub issue templates for bugs and feature requests.
- Kinevor Gym brand mark on the README, public site and demo hosts.

### Changed

- Backend projects target `net10.0` with EF Core 10 and ASP.NET Core 10 packages.
- Angular workspaces and libraries target Angular 22, Angular Material 22 and
  TypeScript 6.
- Demo apps use a 256 px logo asset; the full-size brand image remains in
  `docs/branding/` for the README and social preview.
- Removed obsolete `RollForward=LatestMajor` settings from demo and test hosts
  now that every backend targets `net10.0`.
- Removed the deprecated TypeScript `baseUrl` option; workspace path mappings
  are now relative and no longer rely on a suppression.
- Public-site demo no longer enables view transitions, which could stall initial
  and client-side navigation in Angular 22 and leave the router outlet empty.
- Repository links, package metadata, issue-template links and module docs now
  use the canonical `georgevictorkamal/Kinevor-Gym` URL.
- Angular packages are documented as monorepo builds rather than implying they
  are already available from a public npm registry.

### Fixed

- Memberships mobile screenshots now resolve from the monorepo instead of the
  retired standalone module repository.
- Public plans and trial screens use larger, higher-contrast copy for prices,
  feature lists, form labels and supporting text.
- The trial aside orbit no longer overrides the aside stacking context, and the
  mobile aside is compact enough to keep the form near the top of the page.
- Trial validation focuses and scrolls to the first invalid field, successful
  submissions bring the confirmation into view, and starting another request
  returns to the form.
- Wide billing and check-in tables scroll inside their cards instead of widening
  the document on phone-sized viewports.
- The check-ins demo header hides its brand copy below 560 px so the navigation
  and demo badge no longer overlap.

### Security

- Demo APIs refuse to start outside the Development environment.
- Removed the committed JWT signing key. The identity demo generates an
  ephemeral key when `Jwt:SigningKey` is not configured.
- Login verifies unknown e-mails against a dummy hash, so response timing does
  not reveal which accounts exist.
- Pinned SQLitePCLRaw 2.1.14 (SQLite 3.54.0) to clear the high-severity
  advisory GHSA-2m69-gcr7-jv3q in the SQLite-backed demo hosts and tests.

### Verified

- 208 .NET tests, 99 Angular tests and 128 Flutter tests pass.
- Platform NuGet packing and Angular package build succeed from a clean checkout.
- Every module restores from the local platform feed and builds independently.
