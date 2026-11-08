# Changelog

All notable changes to Kinevor are documented here. The repository uses a single
`0.x` line while the platform contracts and module packages evolve together.

## 0.2.0 — 2026-11-07

A hardening and showcase release: the ten services keep their independent
runtime model while gaining a demo environment, HTTP controls, end-to-end
verification, container images, OpenAPI snapshots and verifiable releases.

### Added

- **Demo environment** — every host calls `EnsureDemoHost()` and refuses to run
  outside `Development` or `Demo`; seeding follows the same rule.
- **HTTP hardening** — shared security headers on all APIs, configuration-driven
  CORS, and per-instance rate limiting for identity (60/min) and the public site
  (configurable, default 600/min).
- **Playwright end-to-end suites** — 46 checks across the public site and members
  demo at 1440×1040 and 390×844, covering navigation, forms, horizontal overflow
  and serious/critical axe accessibility violations.
- **.NET coverage** — `coverlet.collector` in every test project, with Cobertura
  reports uploaded per service in CI.
- **Docker demo stack** — `deploy/demo/` contains generic API and web Dockerfiles,
  an nginx SPA configuration and a Compose stack for all ten APIs and nine web
  demos, each with its own SQLite volume and CORS origins.
- **OpenAPI snapshots** — `docs/openapi/` contains generated documents for all ten
  APIs, an index and `npm run openapi:export`.
- **Release integrity** — the release workflow now publishes an SPDX SBOM
  workflow artifact and a signed `actions/attest-build-provenance` attestation
  alongside the single source zip.
- **CI workflow** — repository hygiene, backend coverage, web/mobile matrices,
  Playwright, dependency review, OpenSSF Scorecard and a single `CI success`
  gate.
- **Repository tooling** — `scripts/verify-repo.mjs`,
  `scripts/bump-version.mjs`, `scripts/reset-demos.mjs`, `scripts/serve-static.mjs`
  and root npm scripts for `verify`, `e2e`, `openapi:export`, `demo:up` and
  `demo:down`.
- **Public-site SEO** — canonical and robots metadata, Open Graph/Twitter tags,
  JSON-LD `ExerciseGym`, web manifest, icons, `robots.txt`, `sitemap.xml` and an
  Open Graph image.
- **Runtime API configuration** — every demo web app reads `config.js` at runtime
  instead of baking the API origin into the bundle.
- **Documentation** — deployment guide, four ADRs and refreshed README,
  architecture, security and contributing guides.

### Changed

- Aligned every package, project, manifest and lockfile to `0.2.0`.
- Switched the platform Angular package builder to `@angular/build:ng-packagr`,
  removed `@angular/platform-browser-dynamic` from all ten web workspaces and
  updated `@angular/build` to 22.2.6.
- Standardised the toolchain on Node 24, npm 11.21.2, Flutter 3.47.8, .NET 10,
  Angular 22 and TypeScript 6.
- Module Angular workspaces now declare the built platform package as a permanent
  `file:` dependency; `install:local` verifies the build and never mutates
  `package.json`.
- Committed NuGet and npm lockfiles for every project. Platform restore uses
  `--locked-mode`; module restore stays unlocked because locally rebuilt
  packages are not byte-deterministic.
- Replaced the public-site and members demos' Material tokens with accessible
  action and accent colours, added labelled loading spinners and fixed global
  form-field, select and paginator contrast.
- Rewrote the CI and release workflows for coverage, E2E, Scorecard, SBOM and
  provenance.

### Fixed

- Public site and members demo now report **zero serious or critical axe
  violations** and no page-level horizontal overflow at both target viewports.
- Members Material buttons, form labels, selects, checkboxes and paginators meet
  contrast requirements in light and dark sections.
- Mobile menu and footer link naming is unambiguous for screen readers.
- `install:local` works when invoked directly with `node` on Windows instead of
  failing with `spawnSync npm.cmd EINVAL`.
- Public-site rate limiting no longer causes false 429 responses during parallel
  end-to-end tests.
- Platform and module lockfiles were regenerated after dependency updates,
  keeping restore deterministic and vulnerability-free.

### Security

- Demo authentication is now gated on an explicit environment check.
- All APIs send browser security headers.
- CORS is configurable per deployment and defaults to localhost only.
- Rate limits reduce accidental abuse of the public demo endpoints.
- Release assets carry an SBOM and a signed provenance attestation.

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
