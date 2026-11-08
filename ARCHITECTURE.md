# Kinevor — Architecture

## Overview

Kinevor is a **modular monorepo**: one shared platform kernel and ten vertical
business services. The kernel owns cross-cutting contracts; each service owns one
business capability end to end — domain rules, application handlers, HTTP API,
persistence, tests and its client packages.

The repository is **not a distributed system**. Modules share build-time
contracts, not runtime infrastructure.

```text
┌──────────────────────────────────────────────────────────────────────┐
│ kinevor-platform · shared kernel                                      │
│   Kinevor.Platform.Domain         entities, results, value objects    │
│   Kinevor.Platform.Application    repository/UoW contracts, paging    │
│   Kinevor.Platform.Api            ApiResponse<T>, roles, policies     │
│   @georgevictorkamal/kinevor-platform-web   Angular client services │
└──────────────────────────────────────────────────────────────────────┘
                 ▲                                      ▲
                 │ referenced at build time             │
      ┌──────────┴──────────┐                ┌──────────┴──────────┐
      │ kinevor-* service A │      ...       │ kinevor-* service N │
      │ own API · DB · UI   │                │ own API · DB · UI   │
      └─────────────────────┘                └─────────────────────┘

      no runtime calls, shared database or orchestration between services
```

## Runtime topology

A monorepo is a source-control boundary, not a runtime boundary. At runtime each
service is a separate process with its own configuration:

| Concern | Per-service reality |
| --- | --- |
| HTTP host | Its own demo API project and port (`5081`–`5090`) |
| Persistence | Its own SQLite file, created and seeded on first run |
| Authentication | The demo host's role handler, available only in `Development` or `Demo` |
| Clients | Its own Angular library/demo and/or Flutter package |
| Tests | Its own xUnit, Vitest, Flutter and shared Playwright scenarios |
| Delivery | Its own Docker image and host port; could be deployed independently |

No service resolves another service's client, calls another service's API or
writes another service's tables. The `localhost:50xx` ports appear only in each
module's own documentation and configuration. The only cross-module references
are build-time references to the platform packages.

This means the services can be split into separate repositories without changing
application code: the module folder would become the repository root, and the
platform packages are already consumed by version.

## Demo environment and HTTP hardening

Every demo host calls `EnsureDemoHost()` at startup and refuses to run unless
`ASPNETCORE_ENVIRONMENT` is `Development` or the dedicated `Demo` environment.
Seeding follows the same rule. This keeps the deterministic demo role headers
(`X-Demo-Role`, `X-Demo-Email`, `X-Demo-Member`, `X-Branch-Id`) out of any
accidental production deployment.

All hosts apply `UseKinevorSecurityHeaders()`: `X-Content-Type-Options`,
`X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`,
`Cross-Origin-Opener-Policy` and a conservative `Content-Security-Policy`.
CORS origins come from `Cors:AllowedOrigins` (environment variable
`Cors__AllowedOrigins__0…N`) with per-module localhost defaults.

The identity host limits requests to 60 per minute per client IP and generates a
random signing key when `Jwt:SigningKey` is absent. The public site uses a
configurable limiter, defaulting to 600 requests per minute so parallel
end-to-end runs do not produce false 429 responses. These limits are per
instance and in-memory; production needs an edge or distributed control.

## Why a monorepo

- One clone, one README, one CI workflow and one screenshot set for reviewers.
- Shared conventions stay in sync across ten services.
- Platform changes can be verified against every consumer in one run.
- Each service remains independently buildable and testable, which is what the
  CI matrices prove.

The trade-off is a larger checkout and a CI workflow that needs to distribute
platform artifacts. The build workflow below covers both.

## Platform kernel

`kinevor-platform` has no product UI or runnable API host. It supplies four
packages:

| Ecosystem | Package | Contents |
| --- | --- | --- |
| NuGet | `Kinevor.Platform.Domain` | `Entity`, `AggregateRoot`, `ValueObject`, `Result<T>`, `Error`, domain events, specifications, e-mail/phone value objects and shared enums |
| NuGet | `Kinevor.Platform.Application` | Repository and unit-of-work contracts, `ICurrentUser`, `IBranchContext`, `PagedResult<T>` and typed exceptions |
| NuGet | `Kinevor.Platform.Api` | `ApiResponse<T>`, role constants, policy names and `AddKinevorPolicies()` |
| npm | `@georgevictorkamal/kinevor-platform-web` | API envelope types, auth gateway/interceptor/guard, i18n with RTL, notices and theme services |

Backend projects target `net10.0`; the Angular package targets Angular 22.

The platform is versioned (`0.2.0`) and consumed like any other dependency. That
version boundary is deliberate: it keeps module builds honest about what they
actually depend on.

## Module anatomy

All ten business services follow the same layout:

```text
kinevor-<service>/
  backend/
    src/Kinevor.Modules.<Service>/                  one module assembly
      Api/                                           controllers and HTTP contracts
      Application/                                   commands, queries, handlers, DTOs
      Domain/                                        entities, rules, value objects
      DependencyInjection/                           module registration
    src/Kinevor.Modules.<Service>.EntityFrameworkCore/
      DbContext, configurations, repositories
    samples/Kinevor.Modules.<Service>.DemoApi/      runnable host: auth, seed, Swagger
    tests/Kinevor.Modules.<Service>.Tests/          domain, handler and endpoint tests
    Kinevor.Modules.<Service>.slnx
    nuget.config                                    nuget.org + local platform feed
    Directory.Build.props                           package metadata and version
  web/                                              (where applicable)
    projects/kinevor-<service>/                     Angular library
    projects/demo/                                  runnable demo
    scripts/install-local-platform.mjs
    scripts/capture-screenshots.mjs
  mobile/                                           (where applicable)
    packages/kinevor_<service>/                     Flutter package
    packages/kinevor_<service>/example/             runnable example
  docs/screenshots/
  README.md · CHANGELOG.md · PUBLISHING.md
```

`kinevor-notifications` has no Angular workspace. `kinevor-public-site` has no
Flutter package. The platform kernel has only backend and web packages.

## Backend layering

Each service's business code compiles into a single module assembly with explicit
layer folders; persistence lives in a separate EF Core assembly. This keeps the
solution small and deployment simple while preserving readable boundaries.

- **Domain** — entities and invariants, independent of EF Core and ASP.NET Core.
- **Application** — MediatR commands, queries and handlers; repository and unit
  of work interfaces; DTOs and mapping.
- **Api** — thin controllers that send a MediatR request and return
  `ApiResponse<T>`.
- **DependencyInjection** — a single `Add<Service>Module()` extension so the host
  stays declarative.
- **EntityFrameworkCore** — `DbContext`, entity configurations, repository
  implementations and module registration.
- **DemoApi** — composition root: SQLite, demo authentication, CORS, Swagger,
  seeding, exception middleware and `/health`.

The demo hosts translate platform exceptions to the shared envelope:

| Exception | HTTP status |
| --- | --- |
| `DomainException` | 400 |
| `NotFoundException` | 404 |
| `ForbiddenException` | 403 |
| `ConflictException` | 409 |
| anything else | 500 (message hidden, error logged) |

Reads and writes go through MediatR. There is no shared base handler, no
cross-module handler and no in-process event bus between services. Demo
authentication is a development-only scheme driven by `X-Demo-Role`,
`X-Demo-Email`, `X-Demo-Member` and `X-Branch-Id` headers.

## Web conventions

All Angular code uses Angular 22 conventions:

- **No NgModules** — standalone components, `bootstrapApplication`, `inject()`.
- **Signals** — component state and derived values are signals and computed
  values; templates use `@if` and `@for`.
- **Facades and gateways** — components depend on a feature facade; data access
  goes through a typed gateway interface with an HTTP implementation.
- **Functional interceptors and guards** — `HttpInterceptorFn` and
  `CanActivateFn`/`CanMatchFn`.
- **Library-first** — each service publishes an Angular library; the demo app is
  only a host that wires routes, providers and demo authentication.

Every module workspace declares the platform Angular package as a permanent
`file:` dependency on the sibling build:

```text
@georgevictorkamal/kinevor-platform-web:
  file:../../kinevor-platform/web/dist/kinevor-platform-web
```

`install:local` verifies that the build exists, checks the npm major version and
runs `npm ci` (with `--ci`) or `npm install` without mutating `package.json`.
Each workspace commits its `package-lock.json`. In CI, the platform build
artifact is restored to the same sibling path before the module install runs.

Both commands pass npm's `--install-links`, so the platform package is copied
into the workspace's `node_modules` instead of being symlinked outside it. That
keeps `@angular/*` resolution anchored to the module's own dependency tree; a
symlink would send Vite looking for Angular inside the platform workspace, which
has no `node_modules` in CI.

## Mobile conventions

Flutter packages are deliberately lightweight, with no state-management
framework:

- a typed `http` client and repository per service;
- immutable model classes with JSON parsing;
- an injectable repository interface so widget tests can use fakes;
- screen widgets with explicit loading, empty, error and data states;
- Material 3 theming with shared fonts and a string bundle;
- widget tests plus golden tests for key screens.

Each package has an `example/` application that runs on Chrome or a device. The
example is a demo host, not a second implementation.

## Contracts and consistency

The platform defines the shapes every service shares:

- `ApiResponse<T>` / `ApiResponse` for success and failure envelopes;
- policy names for admin, staff, trainer and member access;
- paging via `PagedResult<T>`;
- current-user and branch-context abstractions;
- the Angular auth, i18n, notice and theme services.

Each service still owns its request/response DTOs, status codes and business
rules. The platform does not define a shared database schema, entity model or
service registry.

## Build workflow

Modules need the platform packages before they can restore:

```bash
# NuGet feed expected by every module nuget.config
dotnet pack kinevor-platform/backend/Kinevor.Platform.slnx -c Release \
  -o kinevor-platform/backend/artifacts/packages

# Angular package expected by every module install:local script
cd kinevor-platform/web
npm ci
npm run build
```

The module `nuget.config` points to
`../../kinevor-platform/backend/artifacts/packages`. The Angular script resolves
`../../kinevor-platform/web/dist/kinevor-platform-web`. Both paths are relative
to the module and work on Windows, Linux and macOS.

Use npm 11 or later. npm 10's peer resolver crashes on the Angular 22 + Vitest
dependency graph, so `install:local` checks the npm major version and stops with
an upgrade hint instead of failing deep inside Arborist.

All projects commit `packages.lock.json`, and every Angular workspace commits
`package-lock.json`. CI restores the platform with `--locked-mode`, which is
safe because those packages come from one locked build. Module lockfiles are
committed for review but re-evaluated on every restore
(`dotnet restore --force-evaluate`, or
`dotnet build -p:RestoreForceEvaluate=true`): locally rebuilt platform
`.nupkg` files are not byte-deterministic, so validating a committed hash
against a fresh package cache fails with `NU1403`. Docker builds and the
OpenAPI export use the same property. This trade-off is recorded in
[ADR 0002](docs/adr/0002-platform-packages-by-version.md).

If the platform packages are published to a registry, the module projects can
restore them by version instead; `PUBLISHING.md` in each module documents that
optional path.

## Deployment

The supported demo deployment is a Docker Compose stack under `deploy/demo/`:

- one API image per service (`Dockerfile.api`, host ports `5081`–`5090`);
- one nginx image per Angular demo (`Dockerfile.web`, host ports `4200`–`4208`);
- one named volume per SQLite database;
- no gateway, broker or shared database.

The images build the platform packages from the same checkout, so a module
container can never silently use a stale platform release. See the
[deployment guide](docs/deployment.md) for commands, configuration keys,
single-service builds and production hardening.

## Testing strategy

| Layer | What is verified | Framework |
| --- | --- | --- |
| Domain and handlers | Invariants, state transitions, calculations, error paths | xUnit |
| HTTP API | Routing, status codes, envelopes, demo auth, seeded SQLite | xUnit + `WebApplicationFactory` |
| Angular gateways/facades | Request mapping, state and error handling | Vitest |
| Angular components | Rendering, interaction, conditional states | Vitest + Angular TestBed |
| Flutter data/widgets | API client, repository, screen states, retries | `flutter_test` |
| Flutter visuals | Golden layouts | `flutter_test` golden files |
| End-to-end | Routing, forms, navigation, horizontal overflow, axe accessibility | Playwright + `@axe-core/playwright` |

Verified totals on 2026-11-07:

| Suite | Tests |
| --- | ---: |
| .NET (platform + 10 services) | 208 |
| Angular (9 libraries) | 99 |
| Flutter (9 packages) | 128 |
| Playwright (public site + members, desktop and mobile) | 46 |
| **Total** | **481** |

.NET suites collect Cobertura coverage with `coverlet.collector`; CI uploads one
report per service. Playwright audits the public site (`/`, `/plans`,
`/trainers`, `/schedule`, `/trial`) and the members demo (`/members`,
`/members/new`, `/me`, a seeded detail route) at 1440×1040 and 390×844.

## CI pipeline

`.github/workflows/ci.yml` has nine job groups:

1. **Repository hygiene** — runs `scripts/verify-repo.mjs` to enforce architecture,
   version, lockfile and release guardrails.
2. **Platform backend** — locked restore, build, test with coverage, pack and
   upload the three NuGet packages.
3. **Platform web** — install, build and upload the Angular package.
4. **Module backend matrix** — one job per service; downloads the NuGet artifact
   into the sibling platform folder, then restores, builds, tests with coverage
   and uploads the report.
5. **Module web matrix** — one job per Angular workspace; downloads the platform
   web artifact, runs `install:local`, tests and builds the library and demo.
6. **Module mobile matrix** — one job per Flutter package; checks formatting,
   analyzes and runs `flutter test`.
7. **End-to-end** — builds the public site and members demos, then runs the
   Playwright suites on desktop and mobile Chromium.
8. **Dependency review and Scorecard** — reviews dependency changes on pull
   requests and publishes OpenSSF Scorecard results for `main`.
9. **CI success** — a single required gate that fails when any required job fails
   or is cancelled.

The matrices make independence visible: a failure in one service does not hide
the state of the others.

## Security boundaries

- Demo role headers, seeded credentials and the ephemeral JWT key are for local
  development and automated tests only; hosts refuse to start outside
  `Development` and `Demo`.
- CORS defaults to each module's localhost origin and must be configured
  explicitly for any other deployment.
- Security headers are applied by the APIs; HSTS and TLS remain edge concerns.
- Rate limiting is per instance and in-memory, not a WAF or distributed quota.
- SQLite files, `.env` files, tokens and build artifacts are ignored.
- No service stores payment card data; payments are simulated.
- No service sends real e-mail or push notifications.
- The public site uses fictional locations, plans, coaches and editorial content.

See [SECURITY.md](SECURITY.md) for reporting and deployment guidance. The
decisions behind the demo environment and HTTP controls are recorded in
[ADR 0003](docs/adr/0003-demo-environment-and-http-hardening.md).

## Optional next steps

The design stops at independent services. If the product needed a connected
runtime, the natural additions would be:

1. A gateway/BFF for a unified client origin.
2. A shared identity provider and token audience strategy.
3. Asynchronous integration events for cross-service workflows (for example,
   membership renewal → invoice → notification).
4. Production deployment manifests, managed databases, TLS/HSTS at the edge,
   distributed rate limiting and observability.

The demo Docker images and compose stack are a local evaluation aid, not a
production platform; none of the production additions above are claimed here.
