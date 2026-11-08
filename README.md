<p align="center">
  <img src="docs/branding/kinevor-gym-logo.png" alt="Kinevor" width="260">
</p>

<h1 align="center">Kinevor</h1>

<p align="center">
  <strong>A modular gym-management platform — ten independent services, one repository.</strong><br>
  Built with .NET 10, Angular 22, Flutter 3 and a shared platform kernel.
</p>

<p align="center">
  <a href="https://github.com/georgevictorkamal/Kinevor-Gym/actions/workflows/ci.yml"><img src="https://github.com/georgevictorkamal/Kinevor-Gym/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/georgevictorkamal/Kinevor-Gym/releases/latest"><img src="https://img.shields.io/github/v/release/georgevictorkamal/Kinevor-Gym?label=release&color=blue" alt="Release"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10"></a>
  <a href="https://angular.dev/"><img src="https://img.shields.io/badge/Angular-22-DD0031?logo=angular&logoColor=white" alt="Angular 22"></a>
  <a href="https://flutter.dev/"><img src="https://img.shields.io/badge/Flutter-3.47.8-02569B?logo=flutter&logoColor=white" alt="Flutter"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="MIT License"></a>
</p>

<p align="center">
  <a href="#-screenshots">Screenshots</a> •
  <a href="#-quick-start">Quick Start</a> •
  <a href="ARCHITECTURE.md">Architecture</a> •
  <a href="#-services">Services</a> •
  <a href="CONTRIBUTING.md">Contributing</a>
</p>

---

![Kinevor public website](docs/screenshots/public-web.png)

## 🎯 What is Kinevor?

Kinevor is a **reference implementation** of a gym-management product built around
**service boundaries, layered backend design, reusable frontend packages and
automated verification**.

Each of the ten business modules owns its domain rules, HTTP API, SQLite database
and test suite. Where the capability needs a UI, it also ships a standalone
Angular library and/or a Flutter package. The shared platform kernel provides
domain primitives, application contracts, the API response envelope and Angular
client services.

> **Not a distributed system.** Modules share versioned platform contracts at
> build time, but they do _not_ call each other, share databases or require each
> other at runtime. Every demo API starts on its own port with its own seeded
> SQLite file. See [ARCHITECTURE.md](ARCHITECTURE.md).

## ✨ Key Features

<table>
<tr>
<td width="50%">

### 🏗️ Modular Architecture
Ten independently buildable services with explicit boundaries — no cross-module
project references. Each can be split into its own repository without changing
application code.

</td>
<td width="50%">

### 🔁 Full-Stack Per Module
Every service ships its own .NET API, Angular library and/or Flutter package,
plus Swagger docs, demo data and screenshots. A complete vertical slice from
domain to UI.

</td>
</tr>
<tr>
<td>

### ✅ 481 Automated Tests
208 .NET + 99 Angular + 128 Flutter unit tests plus 46 Playwright end-to-end
checks covering domain rules, HTTP endpoints, gateways, components, screen
states, accessibility and responsive layouts — all passing in CI.

</td>
<td>

### ⚙️ CI with Matrix Builds
GitHub Actions matrices prove every module builds and tests independently.
Coverage is collected per service, Playwright checks the running demos, CodeQL
scans C# and TypeScript, and OpenSSF Scorecard monitors the repository.

</td>
</tr>
<tr>
<td>

### 🌍 i18n & RTL Ready
The Angular platform package includes English/Arabic localization with RTL
layout, locale-aware dates, numbers and currency formatting.

</td>
<td>

### 📱 Cross-Platform
Web (Angular 22 with signals and standalone components), mobile (Flutter with
Material 3 and golden tests) and API (ASP.NET Core 10 with MediatR CQRS).

</td>
</tr>
</table>

## 📊 At a Glance

| | |
| --- | --- |
| **Business modules** | 10, plus a shared platform kernel |
| **Backend** | 10 ASP.NET Core 10 APIs, EF Core 10, SQLite, MediatR |
| **Web** | 9 Angular 22 libraries with runnable demos |
| **Mobile** | 9 Flutter packages with runnable examples |
| **Automated tests** | **481** — 208 .NET · 99 Angular · 128 Flutter · 46 Playwright |
| **Screenshots** | 57 captured from the running applications |
| **CI** | Hygiene, backend/web/mobile matrices, coverage, E2E, CodeQL, dependency review, Scorecard |

## 🖼️ Screenshots

| Public site | Staff dashboard |
| --- | --- |
| ![Public website landing page](docs/screenshots/public-web.png) | ![Staff operations dashboard](docs/screenshots/dashboard-web.png) |

| Members | Classes & bookings |
| --- | --- |
| ![Member directory](docs/screenshots/members-web.png) | ![Class schedule](docs/screenshots/classes-web.png) |

| Check-ins & access | Payments & billing |
| --- | --- |
| ![Front-desk check-in](docs/screenshots/checkins-web.png) | ![Billing dashboard](docs/screenshots/billing-web.png) |

| Members on mobile | Classes on mobile |
| --- | --- |
| ![Member profile on mobile](docs/screenshots/members-mobile.png) | ![Class schedule on mobile](docs/screenshots/classes-mobile.png) |

<details>
<summary><strong>📸 57 more screenshots</strong> across Swagger explorers, staff workflows, member screens and mobile states</summary>
<br>

Each module's `docs/screenshots/` folder contains its full set — API explorer,
web views and mobile screens. Browse:
[identity](kinevor-identity-access/docs/screenshots/) ·
[members](kinevor-members/docs/screenshots/) ·
[memberships](kinevor-memberships/docs/screenshots/) ·
[classes](kinevor-classes-bookings/docs/screenshots/) ·
[check-ins](kinevor-checkins-access/docs/screenshots/) ·
[payments](kinevor-payments-billing/docs/screenshots/) ·
[notifications](kinevor-notifications/docs/screenshots/) ·
[trainers](kinevor-trainers/docs/screenshots/) ·
[dashboard](kinevor-staff-dashboard/docs/screenshots/) ·
[public site](kinevor-public-site/docs/screenshots/)

</details>

## 🧩 Services

| Service | Responsibility | API | Web | Flutter | Port |
| --- | --- | :---: | :---: | :---: | ---: |
| [`kinevor-platform`](kinevor-platform/README.md) | Shared kernel: domain primitives, application contracts, API envelope, Angular services | — | ✔ | — | — |
| [`kinevor-identity-access`](kinevor-identity-access/README.md) | JWT authentication, refresh tokens and role-aware sessions | ✔ | ✔ | ✔ | 5082 |
| [`kinevor-members`](kinevor-members/README.md) | Member records, health context, progress, consents and privacy | ✔ | ✔ | ✔ | 5083 |
| [`kinevor-memberships`](kinevor-memberships/README.md) | Plans, subscriptions, renewal and benefit snapshots | ✔ | ✔ | ✔ | 5084 |
| [`kinevor-classes-bookings`](kinevor-classes-bookings/README.md) | Timetable, capacity, bookings and waitlists | ✔ | ✔ | ✔ | 5085 |
| [`kinevor-checkins-access`](kinevor-checkins-access/README.md) | Eligibility rules, check-in/out, occupancy and visit history | ✔ | ✔ | ✔ | 5086 |
| [`kinevor-payments-billing`](kinevor-payments-billing/README.md) | Invoices, simulated payments, refunds and revenue reporting | ✔ | ✔ | ✔ | 5087 |
| [`kinevor-notifications`](kinevor-notifications/README.md) | Inbox, delivery records, preferences, templates and suppressions | ✔ | — | ✔ | 5088 |
| [`kinevor-staff-dashboard`](kinevor-staff-dashboard/README.md) | Branch-scoped KPIs, trends, occupancy and operational alerts | ✔ | ✔ | ✔ | 5089 |
| [`kinevor-trainers`](kinevor-trainers/README.md) | Trainer directory, profiles and public-facing roster | ✔ | ✔ | ✔ | 5081 |
| [`kinevor-public-site`](kinevor-public-site/README.md) | Marketing site, plans, timetable and trial capture | ✔ | ✔ | — | 5090 |

## 🏛️ Architecture

```mermaid
flowchart TB
    subgraph Platform["Platform kernel · build-time contracts"]
        Domain["Kinevor.Platform.Domain"]
        App["Kinevor.Platform.Application"]
        Api["Kinevor.Platform.Api"]
        Web["@georgevictorkamal/kinevor-platform-web"]
    end

    subgraph Services["Independent services · no runtime coupling"]
        ID["Identity & Access"]
        MB["Members"]
        MS["Memberships"]
        CB["Classes & Bookings"]
        CK["Check-ins & Access"]
        PB["Payments & Billing"]
        NT["Notifications"]
        TR["Trainers"]
        SD["Staff Dashboard"]
        PS["Public Site"]
    end

    Services -.->|"reference at build time"| Platform
```

Each module follows layered conventions — **Domain → Application → API → EF Core → DemoApi** — with tests beside the code they verify. The platform supplies `ApiResponse<T>`, authorization policies and Angular auth/i18n/theme services.

[📖 Full architecture guide →](ARCHITECTURE.md)

## 🛠️ Technology

| Layer | Stack |
| --- | --- |
| **Backend** | ASP.NET Core 10 (`net10.0`), EF Core 10, SQLite, MediatR 12, JWT bearer, PBKDF2, Swashbuckle, xUnit + `WebApplicationFactory`, coverlet coverage |
| **Web** | Angular 22 — standalone components, signals, typed gateways/facades, Angular Material, Vitest |
| **Mobile** | Flutter 3.47.8, Material 3, typed HTTP client + repository, widget + golden tests |
| **Delivery** | Docker Compose demo stack, nginx static hosting, OpenAPI snapshots, tag-driven release with SBOM and build provenance |
| **Tooling** | GitHub Actions matrices, Playwright + axe, NuGet, npm, Dart/Flutter, CodeQL, Dependabot, OpenSSF Scorecard |

## 🚀 Quick Start

### Prerequisites

| Tool | Version |
| --- | --- |
| .NET SDK | **10** |
| Node.js | **24 LTS** with **npm 11+** |
| Flutter | **3.47.8** (stable channel) |
| Docker | Optional — Docker Engine 24+ with Compose v2 for the demo stack |

### 1. Build the platform (once)

```bash
# Pack the shared .NET packages
dotnet pack kinevor-platform/backend/Kinevor.Platform.slnx -c Release \
  -o kinevor-platform/backend/artifacts/packages

# Build the shared Angular package
cd kinevor-platform/web
npm ci
npm run build
cd ../..
```

### 2. Run any service

Pick a module — **members** is a good starting point:

```bash
# API + Swagger → http://localhost:5083
dotnet restore kinevor-members/backend/Kinevor.Modules.Members.slnx --force-evaluate
dotnet run --no-restore --project kinevor-members/backend/samples/Kinevor.Modules.Members.DemoApi
```

```bash
# Angular demo → http://localhost:4202
cd kinevor-members/web
npm run install:local
npm start
```

```bash
# Flutter example (Chrome)
cd kinevor-members/mobile/packages/kinevor_members/example
flutter pub get
flutter run -d chrome
```

> **Tip:** `npm run install:local` installs from the module's permanent `file:`
> dependency on the sibling platform build, copying it with npm's
> `--install-links`. Use the same pattern for any module — see its README for the
> exact port and routes.

### 3. Or run the whole stack with Docker

```bash
npm run demo:up
```

This builds ten API images and nine static web images from the same checkout.
APIs answer on `5081`–`5090`, web demos on `4200`–`4208`. See the
[deployment guide](docs/deployment.md) for the port map, a smaller showcase
command and production caveats.

### Repository verification

```bash
node scripts/verify-repo.mjs   # architecture, version and release guardrails
npm run e2e                    # Playwright + axe against the running demos
npm run openapi:export         # refresh docs/openapi snapshots
```

The demo APIs use demo role headers (Development and Demo only) for screenshots and tests.
See [SECURITY.md](SECURITY.md) for details.

## 🧪 Testing

| Suite | Tests | Result |
| --- | ---: | --- |
| .NET — platform + 10 module APIs | 208 | ✅ passed |
| Angular — 9 module libraries | 99 | ✅ passed |
| Flutter — 9 packages | 128 | ✅ passed |
| Playwright E2E — public site + members | 46 | ✅ passed |
| **Total** | **481** | **✅ all passed** |

.NET tests run with `coverlet.collector`; CI publishes a Cobertura report per
service. Playwright runs every audited route at 1440×1040 and 390×844, checks
for horizontal overflow, and fails on serious or critical axe violations.

```bash
# Backend (with coverage)
dotnet restore kinevor-members/backend/Kinevor.Modules.Members.slnx --force-evaluate
dotnet test kinevor-members/backend/Kinevor.Modules.Members.slnx -c Release \
  --no-restore --collect:"XPlat Code Coverage"

# Web
cd kinevor-members/web && npm run install:local && npm test

# Mobile
cd kinevor-members/mobile/packages/kinevor_members && flutter test

# End-to-end (builds are required first; see playwright.config.ts)
npm run e2e
```

## 🔄 Continuous Integration

The [CI workflow](.github/workflows/ci.yml) runs on every push and pull request:

1. **Repository hygiene** — architecture rules, version alignment and release guardrails
2. **Platform backend** — locked restore, build, test with coverage and pack the .NET kernel
3. **Platform web** — install and build the Angular package
4. **Module backend matrix** — each API restores, builds, tests with coverage and uploads its report
5. **Module web matrix** — each Angular workspace tests and builds independently
6. **Module mobile matrix** — each Flutter package formats, analyzes and tests
7. **End-to-end** — builds the public site and members demos, then runs Playwright on desktop and mobile
8. **Dependency review and Scorecard** — pull-request dependency checks and weekly repository scoring
9. **CI success** — a single required gate that fails if any job fails or is cancelled

A separate [CodeQL workflow](.github/workflows/codeql.yml) scans C# and
TypeScript weekly. [Dependabot](.github/dependabot.yml) checks NuGet, npm, pub
and Actions dependencies, and the [Release workflow](.github/workflows/release.yml)
publishes one source zip with an SPDX SBOM artifact and a signed build-provenance
attestation.

## 📁 Repository Layout

```text
kinevor/
├── kinevor-platform/           shared kernel: .NET packages + Angular library
├── kinevor-identity-access/    backend/ · web/ · mobile/ · docs/
├── kinevor-members/            backend/ · web/ · mobile/ · docs/
├── kinevor-memberships/        backend/ · web/ · mobile/ · docs/
├── kinevor-classes-bookings/   backend/ · web/ · mobile/ · docs/
├── kinevor-checkins-access/    backend/ · web/ · mobile/ · docs/
├── kinevor-payments-billing/   backend/ · web/ · mobile/ · docs/
├── kinevor-notifications/      backend/ · mobile/ · docs/
├── kinevor-trainers/           backend/ · web/ · mobile/ · docs/
├── kinevor-staff-dashboard/    backend/ · web/ · mobile/ · docs/
├── kinevor-public-site/        backend/ · web/ · docs/
├── deploy/demo/                Dockerfiles, nginx config and compose stack
├── e2e/                        Playwright public-site and members suites
├── docs/adr/                   architecture decision records
├── docs/openapi/               generated OpenAPI snapshots
├── docs/branding/              logo and brand assets
├── docs/screenshots/           portfolio showcase images
├── scripts/                    verify, version bump, demo reset, OpenAPI export
├── .github/workflows/          CI, CodeQL and release pipelines
├── ARCHITECTURE.md             design and conventions
├── SECURITY.md                 security policy and demo caveats
├── CONTRIBUTING.md             how to build, test and contribute
├── CODE_OF_CONDUCT.md          community standards
└── CHANGELOG.md                release history
```

## 💡 Design Highlights

- **Explicit service boundaries** — one business capability per module, no
  cross-module references
- **Reusable contracts** — platform packages keep the response envelope,
  policies and client services consistent
- **Tests next to the code** — domain, handler, endpoint, component and golden
  tests live beside what they verify
- **Working demos** — every module ships a seeded API, a web demo and/or a mobile
  example, plus Swagger, screenshots and an OpenAPI snapshot
- **Realistic workflows** — masked member data, consent history, waitlists,
  access rules, refunds, occupancy and billing KPIs
- **Operable by default** — security headers, configurable CORS, per-instance
  rate limits, health probes, a Docker Compose stack and runtime API config
- **Verifiable releases** — one source zip, SHA-256, SPDX SBOM and a signed
  build-provenance attestation

## ⚠️ Scope & Limitations

This is a portfolio reference implementation, deliberately scoped:

- Services are independent and **not orchestrated** — no API gateway, message bus
  or cross-service transaction
- Payments are simulated; notifications do not call an external provider
- SQLite is used for zero-setup demos, not for production scale
- Demo auth headers are limited to the `Development` and `Demo` environments;
  hosts refuse to start in any other environment
- Rate limiting is per instance and in-memory — a demo safeguard, not a WAF
- Stripe, e-mail/push delivery, QR hardware and app-store publishing require
  external accounts and are documented, not activated
- Package publishing to registries is optional and documented per module

## 📚 Documentation

| Document | Description |
| --- | --- |
| [Architecture](ARCHITECTURE.md) | Design, conventions and module anatomy |
| [Deployment](docs/deployment.md) | Docker stack, runtime configuration and hardening |
| [OpenAPI](docs/openapi/README.md) | Generated API documents for all ten services |
| [ADRs](docs/adr/) | Architecture decision records |
| [Security](SECURITY.md) | Security policy, demo caveats and deployment checklist |
| [Contributing](CONTRIBUTING.md) | How to build, test and open a change |
| [Code of Conduct](CODE_OF_CONDUCT.md) | Community standards |
| [Changelog](CHANGELOG.md) | Release history |

**Module READMEs:**
[platform](kinevor-platform/README.md) ·
[identity](kinevor-identity-access/README.md) ·
[members](kinevor-members/README.md) ·
[memberships](kinevor-memberships/README.md) ·
[classes](kinevor-classes-bookings/README.md) ·
[check-ins](kinevor-checkins-access/README.md) ·
[payments](kinevor-payments-billing/README.md) ·
[notifications](kinevor-notifications/README.md) ·
[trainers](kinevor-trainers/README.md) ·
[dashboard](kinevor-staff-dashboard/README.md) ·
[public site](kinevor-public-site/README.md)

## 👤 Author

**George Victor Kamal** — [@georgevictorkamal](https://github.com/georgevictorkamal)

The public repository is
[`georgevictorkamal/Kinevor-Gym`](https://github.com/georgevictorkamal/Kinevor-Gym).
The Angular packages use the `@georgevictorkamal/*` scope and are built from this
monorepo; they are not yet published to a public npm registry. See each module's
`PUBLISHING.md` for the optional GitHub Packages flow.

## 📄 License

[MIT](LICENSE) — free to use, modify and distribute.

---

<p align="center">
  If you found this helpful, consider giving it a ⭐
</p>
