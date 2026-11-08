# Security Policy

## Supported versions

This is a reference implementation. Security fixes are applied to the current
`main` branch and the latest `0.2.x` line. Older snapshots are not supported.

## Reporting a vulnerability

Please report security issues **privately**:

1. Use GitHub's **Report a vulnerability** flow on
   [georgevictorkamal/Kinevor-Gym](https://github.com/georgevictorkamal/Kinevor-Gym)
   (private security advisory), or
2. Contact the author, [George Victor Kamal](https://github.com/georgevictorkamal).

Do not open a public issue for a vulnerability. Include:

- the affected module and endpoint;
- reproduction steps and expected impact;
- the version or commit you tested;
- any suggested remediation.

You can expect an initial response within a few business days. Please allow time
for a fix before public disclosure.

## Scope

The repository is a reference implementation. The following are
**known, intentional limitations**, not vulnerabilities:

- **Demo authentication** — every demo API accepts `X-Demo-Role`,
  `X-Demo-Email`, `X-Demo-Member` and `X-Branch-Id` headers to make screenshots
  and tests deterministic. This bypasses real authentication by design. Every
  demo host calls `EnsureDemoHost()` and refuses to start unless
  `ASPNETCORE_ENVIRONMENT` is `Development` or `Demo`.
- **Ephemeral signing keys** — the identity demo host generates a random JWT key
  when `Jwt:SigningKey` is not configured, so tokens do not survive a restart.
  Configure a managed key if you need stable sessions.
- **SQLite** — services use local SQLite files for zero-setup demos. They are not
  hardened for multi-tenant or production workloads.
- **Simulated payments** — no card data is collected and no payment provider is
  called.
- **Simulated notifications** — no real e-mail or push provider is called.
- **Per-instance rate limiting** — the identity host allows 60 requests per
  minute per IP; the public site defaults to 600 and is configurable. Limiting is
  in-memory, per process and not a WAF, bot defence or distributed quota.
- **No WAF or DDoS protection** — the demo stack is not designed to be exposed
  directly to hostile internet traffic.

## HTTP controls

Every API host applies `UseKinevorSecurityHeaders()`:

| Header | Value |
| --- | --- |
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Referrer-Policy` | `no-referrer` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |
| `Cross-Origin-Opener-Policy` | `same-origin` |
| `Content-Security-Policy` | `default-src 'self'` with explicit allow-list for images, styles, scripts, connections, fonts and `frame-ancestors 'none'` |

CORS is restricted to configured origins via `Cors:AllowedOrigins`
(`Cors__AllowedOrigins__0…N`). The demo defaults are the matching localhost web
ports only. Wildcards are never used.

Swagger, demo authentication and seeded data are available only in
`Development` and `Demo`; a host configured as `Production` fails fast.

## Deployment checklist

If you adapt this code for a real environment:

- Replace demo authentication with an OIDC/OAuth provider and validate issuer,
  audience, lifetime and signing keys.
- Remove the demo header middleware, seeded accounts and Swagger exposure.
- Store connection strings, JWT keys and provider credentials in a secret
  manager, never in source or `.env` files committed to Git.
- Use a production database with migrations, backups and least-privilege
  credentials.
- Restrict CORS to the real client origins.
- Terminate TLS at the edge, enable HSTS and add request-size limits.
- Replace the per-instance rate limiter with an edge or distributed control.
- Review authorization policies per endpoint, not only per controller.
- Add structured audit logging, metrics and alerting.
- Keep dependency scanning and CodeQL enabled; add container image scanning
  before publishing images to a registry.
- Verify downloaded release archives with the published SHA-256 and provenance
  attestation before deploying them:

```bash
gh attestation verify Kinevor-v0.2.0.zip --repo georgevictorkamal/Kinevor-Gym
sha256sum Kinevor-v0.2.0.zip
```

## Repository hygiene

The root `.gitignore` excludes build output, SQLite databases, environment files,
package artifacts and editor state. Do not commit:

- access tokens, PATs, private keys or `.env` files;
- production connection strings;
- customer or member data;
- generated databases or screenshot fixtures containing personal data.

The release workflow publishes one source archive plus an SPDX SBOM artifact and
a signed build-provenance attestation. Published tags are immutable; security
fixes ship in a new version rather than rewriting an existing release.
