# ADR 0003 — Demo environment and HTTP hardening

- **Status:** Accepted
- **Date:** 2026-11-07
- **Supersedes:** none

## Context

Every API ships a runnable demo host with seeded SQLite data and deterministic
role headers (`X-Demo-Role`, `X-Demo-Email`, `X-Demo-Member`, `X-Branch-Id`).
Those conveniences are useful for reviewers and tests but must never become an
undocumented production backdoor. The hosts are also occasionally exposed as
public demos.

## Decision

- Add a named **`Demo`** environment alongside `Development`.
- Every host calls `EnsureDemoHost()` during startup and throws unless the
  environment is `Development` or `Demo`.
- Demo data is seeded only in those environments.
- Every host applies `UseKinevorSecurityHeaders()`:
  `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
  `Permissions-Policy`, `Cross-Origin-Opener-Policy` and a conservative
  `Content-Security-Policy`.
- CORS origins come from `Cors:AllowedOrigins` configuration with a per-module
  localhost fallback; `UseCors` runs after the headers middleware.
- The identity host applies a fixed 60 requests/minute per-IP limiter. The
  public site applies a configurable limiter, default 600 requests/minute, so
  parallel end-to-end tests do not produce false 429 responses.
- The identity host generates an ephemeral signing key when `Jwt:SigningKey` is
  absent, so demo tokens never outlive a restart by accident.

## Consequences

- Misconfiguring a host as `Production` fails fast instead of exposing demo
  authentication.
- Response headers cover the common browser risks without a proxy.
- Rate limiting is per instance and in-memory; it is a demo safeguard, not a
  WAF or a distributed quota.
- A real deployment must replace demo authentication, seed data and the
  ephemeral key. The checklist lives in [SECURITY.md](../../SECURITY.md).
