# Kinevor Identity & Access

JWT authentication and role-aware sessions for the Kinevor gym platform. This
module contains a .NET 10 API, a standalone Angular library and demo, and a
Flutter package with a runnable example. Each layer works on its own; the demo
runs from this monorepo.

[![CI](https://github.com/georgevictorkamal/Kinevor-Gym/actions/workflows/ci.yml/badge.svg)](https://github.com/georgevictorkamal/Kinevor-Gym/actions/workflows/ci.yml)
[![Flutter](https://img.shields.io/badge/Flutter-3.47%2B-02569B?logo=flutter)](https://flutter.dev)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

![Kinevor identity login](docs/screenshots/web-login.png)

## Why this module

Identity is the front door to every Kinevor application. This module packages
that door as a vertical slice: domain rules, application handlers, a versioned
NuGet package, an HTTP API, an Angular package, a Flutter package, demos and a
CI/release pipeline.

- Members, trainers, staff and admins sign in through one hardened endpoint.
- Short-lived JWT access tokens keep the API stateless.
- Refresh tokens are hashed, single-use and rotated on every exchange.
- Angular and Flutter clients share the same login and session contract.
- Hosts decide where tokens are stored and how their own routes are guarded.

## Architecture

```mermaid
flowchart LR
    W[Angular login + session demo] -->|HTTP| A[Identity API]
    M[Flutter login + session] -->|HTTP| A
    A --> APP[Application layer<br/>MediatR commands and queries]
    APP --> D[Domain<br/>UserAccount + RefreshToken]
    APP --> H[PBKDF2 hasher + JWT issuer]
    APP --> R[Repositories]
    R --> EF[EF Core]
    EF --> DB[(SQLite demo)]
    A -.reuses.-> P[Kinevor.Platform<br/>envelope, policies, current user]
```

The backend is organized into domain/application/API folders within a single project. The API
depends on the published `Kinevor.Platform.*` packages for the response
envelope, authorization policies and current-user abstractions. The Angular and
Flutter clients are thin presentation layers over a typed gateway/repository.

## Features

- E-mail/password login with PBKDF2-HMAC-SHA256 (600,000 iterations, rehashed on login when lower).
- Per-account lockout after five consecutive failures for fifteen minutes, with the
  same response and PBKDF2 cost for unknown, locked and wrong-password attempts.
- Optional TOTP multi-factor authentication (RFC 6238) with authenticator-app
  enrolment, encrypted secrets and eight single-use recovery codes stored as
  hashes.
- Replay-safe second factors: an accepted TOTP time step is persisted and can
  never be used again, recovery codes are consumed atomically, and optimistic
  concurrency prevents parallel logins from accepting the same factor twice.
- Clear login challenge contract: a correct password on an MFA account returns
  `200` with `data.status = "mfa_required"`; a missing code is not a failed
  attempt, while a wrong code counts toward the lockout.
- HS256 JWT access tokens with configurable issuer, audience and lifetime.
- Opaque refresh tokens stored as SHA-256 hashes, rotated on every exchange and
  revoked on logout.
- Role claims for `Admin`, `Staff`, `Trainer` and `Member`, enforced by the
  platform `AdminOnly` policy on the account list.
- `UserAccount` invariants: normalized e-mail, supported role, display name and
  password hash.
- Platform `ApiResponse<T>` envelope and Problem Details errors.
- Angular: standalone split-screen login, session view, typed gateway/facade,
  route guard, injectable providers and Vitest tests.
- Flutter: injectable repository/client, branded login and session screens,
  theme, configurable strings and golden tests.
- SQLite demo with four seeded accounts and Swagger UI with bearer support.

## Endpoints

| Method | Route | Policy | Used by |
| --- | --- | --- | --- |
| `POST` | `/api/v1/auth/login` | anonymous | Web and Flutter login; returns a session or an MFA challenge; optional `mfaCode` or `recoveryCode` |
| `POST` | `/api/v1/auth/refresh` | anonymous | Session rotation |
| `POST` | `/api/v1/auth/logout` | anonymous | Web and Flutter sign-out |
| `GET` | `/api/v1/auth/session` | authenticated | Session screen, route guard |
| `POST` | `/api/v1/auth/mfa/enrol` | authenticated | Starts TOTP enrolment |
| `POST` | `/api/v1/auth/mfa/confirm` | authenticated | Confirms the first code and returns recovery codes |
| `POST` | `/api/v1/auth/mfa/disable` | authenticated | Disables MFA after verifying a code |
| `GET` | `/api/v1/auth/accounts` | `AdminOnly` | Admin account browser |
| `GET` | `/health` | anonymous | Demo health check |
| `GET` | `/swagger` | development only | API explorer |

Refresh and logout accept the refresh token in the body. Access tokens are sent
as `Authorization: Bearer <token>`.

### Lockout and MFA

Five consecutive failed sign-ins lock an account for fifteen minutes. The counter
resets on success and after the window expires. Unknown, locked and
wrong-password attempts all return the same `401` message and run one PBKDF2
verification, so callers cannot enumerate accounts or lock state.

A correct password on an account with MFA returns `200` and a challenge instead
of tokens:

```json
{ "success": true, "data": { "status": "mfa_required" }, "message": "Second factor required." }
```

The client resubmits the same credentials with `mfaCode` or `recoveryCode`. A
missing code does not increment the failure counter; a wrong or already-used
code does. Only a valid second factor issues tokens, so the challenge cannot be
mistaken for a signed-in session.

The demo lockout is intentionally simple and can be held indefinitely: someone
who knows an account's e-mail can keep it locked by triggering five failures in
each fifteen-minute window. A production host should pair the counter with
per-IP and per-device rate limiting, a CAPTCHA or progressive delay, and a
notification to the account owner.

MFA enrolment is two steps:

```text
POST /api/v1/auth/mfa/enrol     -> { secret, otpAuthUri }
POST /api/v1/auth/mfa/confirm   { code } -> { recoveryCodes: [ ... ] }
```

The first endpoint stores a pending secret; the second verifies a current code,
enables MFA and returns eight recovery codes **once**. Later logins send
`mfaCode` (six digits) or `recoveryCode` (single use) alongside the password.
`POST /api/v1/auth/mfa/disable` accepts either factor.

Accepted TOTP steps are recorded on the account (`LastAcceptedMfaStep`), so a
code observed on the wire cannot be replayed, even within its drift window.
Every account mutation also advances `ConcurrencyStamp`, an EF Core concurrency
token. Parallel sign-ins therefore cannot overwrite the failed-attempt counter,
consume the same recovery code twice, or accept the same TOTP step: exactly one
writer wins and the others receive the generic `401`.

The module encrypts the TOTP secret with ASP.NET Core Data Protection before it
is written to SQLite. The demo persists its key ring under the host's data
directory (`DataProtection:KeysPath`, `/data/keys` in Compose), so enrolled
accounts survive a restart. Production hosts must store the key ring in a
managed key service, back it up and plan rotation; losing the keys makes every
enrolled secret unreadable. Recovery codes are already stored as SHA-256 hashes.

## Screenshots

### Web

| Login | Session |
| --- | --- |
| ![Web login](docs/screenshots/web-login.png) | ![Web session](docs/screenshots/web-session.png) |

### Flutter

| Login | Session |
| --- | --- |
| ![Flutter login](docs/screenshots/mobile-login.png) | ![Flutter session](docs/screenshots/mobile-session.png) |

### API

![Swagger UI](docs/screenshots/api-swagger.png)

## Quick start

The commands below use PowerShell; on macOS or Linux use forward slashes.

### Prerequisites

- .NET SDK 10 (the solution uses `.slnx`) and the ASP.NET Core 10 runtime.
- Node.js 22.12+ and npm 11+.
- Flutter 3.50.1 (pinned in `.fvmrc`).

### 1. Build the platform packages

The backend restores `Kinevor.Platform.*` from the sibling platform folder's
local package feed. Pack it once:

```powershell
dotnet pack ..\kinevor-platform\backend\Kinevor.Platform.slnx -c Release `
  -o ..\kinevor-platform\backend\artifacts\packages
```

If the platform packages are published to a registry, configure that source
instead and skip the local pack step.

### 2. Run the API

```powershell
dotnet restore backend\Kinevor.Modules.IdentityAccess.slnx
dotnet run --project backend\samples\Kinevor.Modules.IdentityAccess.DemoApi
```

The API listens on `http://localhost:5082`. Open
`http://localhost:5082/swagger` for the explorer. The demo creates a SQLite
database and seeds four accounts on first run:

| Role | E-mail | Password |
| --- | --- | --- |
| Admin | `admin@kinevor.demo` | `Kinevor!2026` |
| Staff | `staff@kinevor.demo` | `Kinevor!2026` |
| Trainer | `trainer@kinevor.demo` | `Kinevor!2026` |
| Member | `member@kinevor.demo` | `Kinevor!2026` |

The identity demo uses `EnsureCreatedAsync`, not migrations. The account table
now has `ConcurrencyStamp` and `LastAcceptedMfaStep` columns, so an existing
`identity-demo.db` or the Docker `identity-data` volume must be deleted once
before starting this version; the host recreates and reseeds it.

The demo host generates an ephemeral signing key when `Jwt:SigningKey` is not
configured, so tokens do not survive a restart. Supply `Jwt:SigningKey` (at
least 32 characters), `Jwt:Issuer` and `Jwt:Audience` from your secret store
when you need stable tokens.

### 3. Run the web demo

For local development, build the platform web package once:

```powershell
cd ..\kinevor-platform\web
npm install
npm run build
cd ..\..\kinevor-identity-access\web
npm run install:local
npm start
```

`npm run install:local` keeps the permanent `file:` dependency on the sibling
platform build, verifies that build exists, and installs with `--install-links`
so the package is copied into this workspace's `node_modules`. It never rewrites
`package.json` or the lockfile. If the platform package is published to a
registry, authenticate the scope in `.npmrc` and use the normal path instead:

```powershell
cd web
npm install
npm start
```

The web demo runs at `http://localhost:4201` with routes:

- `/login` - seeded split-screen sign-in
- `/session` - authenticated account, token metadata and session actions

### 4. Run the Flutter example

```powershell
cd mobile\packages\kinevor_identity_access
flutter pub get
cd example
flutter run -d chrome
```

The example targets `http://localhost:5082/api/v1` by default. Override it for an
emulator or remote host:

```powershell
flutter run -d chrome --web-port 4221 `
  --dart-define=KINEVOR_API_BASE_URL=http://localhost:5082/api/v1
```

On an Android emulator use `http://10.0.4.0:5082/api/v1`. The demo API allows the
web demo origin `http://localhost:4201`; add your host origin before running the
Flutter web example from another port.

### 5. Refresh screenshots

With both servers running:

```powershell
cd web
npm run capture:screenshots
```

The Playwright script writes the web and API images to `docs/screenshots`.
Regenerate the Flutter goldens with:

```powershell
cd mobile\packages\kinevor_identity_access
flutter test --update-goldens test\identity_golden_test.dart
```

## Testing

| Area | Command |
| --- | --- |
| Backend | `dotnet test backend\Kinevor.Modules.IdentityAccess.slnx -c Release` |
| Web unit tests | `cd web; npm test` |
| Web builds | `cd web; npm run build:all` |
| Flutter analysis | `cd mobile\packages\kinevor_identity_access; flutter analyze` |
| Flutter tests | `cd mobile\packages\kinevor_identity_access; flutter test` |

The backend suite covers domain invariants, password hashing, lockout, TOTP
vectors, replay protection, recovery-code reuse, refresh rotation, parallel
account writes and API behavior against the demo host. The web suite covers the
HTTP gateway and facade with Vitest. The Flutter suite covers the client,
login/session widgets, second-factor step and golden images.

## Packages

| Ecosystem | Package | Contents |
| --- | --- | --- |
| NuGet | `Kinevor.Modules.IdentityAccess` | Domain, application, API controller and module registration |
| NuGet | `Kinevor.Modules.IdentityAccess.EntityFrameworkCore` | `IdentityDbContext`, mapping and repositories |
| npm | `@georgevictorkamal/kinevor-identity-access-web` | Angular components, gateway, facade, guard, providers and routes |
| Flutter | `kinevor_identity_access` | API client, repository, widgets, theme and strings |

If the Angular package is published to a registry, install it with:

```powershell
npm install @georgevictorkamal/kinevor-identity-access-web
```

Consume the Flutter package from the monorepo by path or git:

```yaml
dependencies:
  kinevor_identity_access:
    git:
      url: https://github.com/georgevictorkamal/Kinevor-Gym.git
      path: mobile/packages/kinevor_identity_access
      ref: main
```

Both clients use the same API contract. The Angular package expects
`@georgevictorkamal/kinevor-platform-web` as a peer dependency.

The monorepo CI packs the platform once and feeds the artifacts to each module
job. Modules do not need a published registry to build.

## Design decisions

- **Stateless access, stateful revocation.** Access tokens are self-contained;
  refresh tokens live in the database as hashes so logout and rotation are
  immediate.
- **Rotation, not reuse.** Every refresh exchange revokes the presented token and
  issues a new pair, limiting replay after theft.
- **Aggregate-first identity.** `UserAccount` validates e-mail, role and display
  name; the application layer never constructs an invalid account.
- **Host-owned sessions.** The Angular facade persists to storage and the Flutter
  package returns sessions to the host. Neither package assumes a global state
  container.
- **Constant-shape password errors.** Invalid e-mail, wrong password and locked
  account return the same message so callers cannot enumerate accounts or infer
  lock state. A valid password on an MFA account returns an explicit challenge;
  only someone who already knows the password can observe MFA enrolment.
- **Replay-safe second factors.** Accepted TOTP steps, consumed recovery codes
  and failed-attempt counters advance an EF Core concurrency token, so parallel
  requests cannot reuse a code or overwrite each other's state.
- **MFA secrets are encrypted at rest.** TOTP secrets are protected with ASP.NET
  Core Data Protection before they reach the database; recovery codes are stored
  as SHA-256 hashes. A production host must persist the Data Protection key ring
  in a managed key service and back it up, or enrolled accounts become
  unreadable.
- **No committed signing key.** The demo host generates an ephemeral key when
  configuration does not provide one; a production host must use a managed
  secret.

## Repository layout

```text
kinevor-identity-access/
  backend/
    src/Kinevor.Modules.IdentityAccess/          domain, application, API
    src/Kinevor.Modules.IdentityAccess.EntityFrameworkCore/
    samples/Kinevor.Modules.IdentityAccess.DemoApi/
    tests/Kinevor.Modules.IdentityAccess.Tests/
  web/
    projects/kinevor-identity-access/            Angular library
    projects/demo/                               demo host
    scripts/                                     local install + screenshots
  mobile/
    packages/kinevor_identity_access/            Flutter package
    packages/kinevor_identity_access/example/    runnable demo
  docs/
    screenshots/
```

## Related modules

- [Kinevor Platform](../kinevor-platform) -
  shared API, auth, i18n, notices and Angular primitives.
- [Kinevor Members](../kinevor-members)
- [Kinevor Memberships](../kinevor-memberships)
- [Kinevor Classes & Bookings](../kinevor-classes-bookings)
- [Kinevor Check-ins & Access](../kinevor-checkins-access)
- [Kinevor Payments & Billing](../kinevor-payments-billing)
- [Kinevor Trainers](../kinevor-trainers)
- [Kinevor Public Site](../kinevor-public-site)
- [Kinevor Staff Dashboard](../kinevor-staff-dashboard)
- [Kinevor Notifications](../kinevor-notifications)

## Verification

- Run this module's backend, web and mobile suites (commands above).
- The root CI runs the module in its backend, web and/or mobile matrix.
- See [PUBLISHING.md](PUBLISHING.md) only if you intend to distribute packages.

## Security

These demo hosts refuse to start outside Development and Demo and use SQLite. For a real
host, issue a strong random signing key, serve the API over HTTPS, restrict CORS
to known origins, and keep refresh tokens out of logs. Swagger and seeded
credentials are available in Development only. Do not commit connection
strings, PATs or `.env` files. `UserAccount.MfaSecret` is already encrypted at
rest; persist its Data Protection key ring in a managed key service before
enabling MFA outside the demo.

## License

MIT. See [LICENSE](LICENSE).
