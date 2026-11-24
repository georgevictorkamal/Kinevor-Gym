# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Angular and Flutter login screens now render a second step for the
  authenticator code or a recovery code after the API returns an MFA challenge.
- `UserAccount.ConcurrencyStamp` is an EF Core optimistic-concurrency token and
  `UserAccount.LastAcceptedMfaStep` records the newest accepted TOTP step.

### Changed

- The identity demo schema gained two columns. Delete the existing
  `identity-demo.db` or Docker `identity-data` volume once so
  `EnsureCreatedAsync` recreates and reseeds it.

### Fixed

- A correct password on an MFA account now returns `200` with
  `data.status = "mfa_required"` instead of a generic `401`. A missing code no
  longer counts toward the lockout; an invalid code still does.
- TOTP codes are single-use: a step at or before `LastAcceptedMfaStep` is
  rejected by sign-in, confirm and disable.
- Parallel sign-ins can no longer overwrite the failed-attempt counter, consume
  the same recovery code twice or accept the same TOTP step. Failed attempts use
  a bounded reload-and-retry so concurrent failures still increment.
- MFA enrolment, confirmation and disable now report a concurrency conflict
  instead of silently overwriting another request's state.
- TOTP secrets are encrypted with ASP.NET Core Data Protection before they are
  stored. The demo persists the key ring under `DataProtection:KeysPath`
  (`/data/keys` in Compose); an existing plaintext secret is unreadable and must
  be re-enrolled after the database reset.

## [0.1.0] - 2026-10-20

### Added

- Standalone identity backend with `UserAccount` and `RefreshToken` aggregates,
  MediatR login/refresh/logout/session/account handlers, PBKDF2 hashing, HS256
  JWT issuance, EF Core persistence, SQLite demo host and platform policies.
- Angular library with split-screen login, authenticated session view, typed HTTP
  gateway, facade, session guard, providers and routes.
- Angular demo application with seeded accounts, the Kinevor visual system, Sora
  typography and Material icons.
- Flutter package with API client, repository, login and session screens, theme
  and strings.
- Flutter example application for Windows and web.
- Real web, mobile and Swagger screenshots under `docs/screenshots`.
- NuGet packing, GitHub Actions CI and documented tag-based release steps.
- MIT license and package documentation.
