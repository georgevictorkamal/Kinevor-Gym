# Deployment guide

Kinevor is a reference implementation with ten independent services. This guide
covers the two supported ways to run it — Docker Compose and local processes —
and the configuration each service needs when you host it yourself.

> **Demo only by default.** Every API host refuses to start unless
> `ASPNETCORE_ENVIRONMENT` is `Development` or `Demo`. The demo authentication
> headers, seeded accounts and SQLite files are not production controls. See
> [SECURITY.md](../SECURITY.md) before exposing anything publicly.
>
> **Loopback by default.** `docker-compose.yml` publishes every host port on
> `127.0.0.1` only. Do not change that binding unless you intend to expose the
> demo authentication headers to the network.

## Port map

| Service | API | Web demo |
| --- | ---: | ---: |
| trainers | 5081 | 4200 |
| identity & access | 5082 | 4201 |
| members | 5083 | 4202 |
| memberships | 5084 | 4203 |
| classes & bookings | 5085 | 4204 |
| check-ins & access | 5086 | 4205 |
| payments & billing | 5087 | 4206 |
| notifications | 5088 | — |
| staff dashboard | 5089 | 4207 |
| public site | 5090 | 4208 |

All host ports bind to `127.0.0.1` in the Compose stack. Every API exposes
`/health` and Swagger at `/swagger` in the demo environment.
Each service owns a separate SQLite database; nothing is shared at runtime.

## Option A — Docker Compose

Requirements: Docker Engine 24+ with Compose v2, and roughly 8 GB of free disk
for the build cache and images.

```bash
# Build and start all ten APIs and nine web demos
npm run demo:up

# Start only the public site and members showcase
docker compose -f deploy/demo/docker-compose.yml up --build \
  kinevor-public-site-api kinevor-public-site-web \
  kinevor-members-api kinevor-members-web

# Stop and keep the SQLite volumes
npm run demo:down

# Stop and delete every demo database
docker compose -f deploy/demo/docker-compose.yml down --volumes --remove-orphans
```

The first build compiles the platform packages and every module, so it takes
several minutes. Later builds reuse cached layers.

What the image definitions do:

- `deploy/demo/Dockerfile.api` packs the platform NuGet packages from the same
  checkout, publishes one `DemoApi` project and runs it on port `8080` inside
  the container. Host ports `127.0.0.1:5081`–`5090` map to `8080`. The publish passes
  `/p:RestoreForceEvaluate=true` because each pack has different hashes; the
  committed module lockfiles would otherwise fail with `NU1403`.
- `deploy/demo/Dockerfile.web` builds the platform Angular package, installs the
  module workspace with `install:local --ci`, builds the demo and serves the
  static output through nginx. Host ports `127.0.0.1:4200`–`4208` map to `80`.
- `deploy/demo/nginx.conf` adds SPA route fallback and a `/healthz` probe.
- `deploy/demo/docker-compose.yml` gives each service its own container,
  database volume and CORS origin. There is no gateway, message bus or shared
  database.

The web demos read their API origin from `config.js` at runtime. The compose
stack keeps the default `http://localhost:508x/api/v1` values, so the browser
must reach the API host ports directly.

## Option B — Local processes

```bash
# Once: build the platform contracts
dotnet pack kinevor-platform/backend/Kinevor.Platform.slnx -c Release \
  -o kinevor-platform/backend/artifacts/packages
cd kinevor-platform/web && npm ci && npm run build && cd ../..

# Restore against the freshly packed platform. Module lockfiles are committed
# for review, but their platform hashes are re-evaluated on every build.
dotnet restore kinevor-members/backend/Kinevor.Modules.Members.slnx --force-evaluate

# API + Swagger
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --no-restore --project kinevor-members/backend/samples/Kinevor.Modules.Members.DemoApi

# Web demo (second terminal)
cd kinevor-members/web
npm run install:local
npm start
```

On PowerShell, set the environment with `$env:ASPNETCORE_ENVIRONMENT = 'Development'`.
The module `nuget.config` and `install:local` script resolve the platform
artifacts by relative path, so no registry credentials are required.

## Runtime configuration

Configuration uses the standard ASP.NET Core providers: `appsettings.json`,
environment variables, command-line arguments and user secrets. Environment
variables use double underscores for nesting.

| Key | Used by | Purpose |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | all APIs | `Development` or `Demo`; anything else stops the host |
| `ASPNETCORE_URLS` | all APIs | Listening address, for example `http://+:8080` |
| `ConnectionStrings__<Key>` | all APIs | SQLite path or connection string. Keys: `Trainers`, `Identity`, `Members`, `Memberships`, `ClassesBookings`, `CheckInsAccess`, `PaymentsBilling`, `Notifications`, `StaffDashboard`, `PublicSite` |
| `Cors__AllowedOrigins__0…N` | all APIs | Exact browser origins allowed to call the API |
| `Jwt__SigningKey` | identity | At least 32 characters; a random key is generated when omitted |
| `Jwt__Issuer`, `Jwt__Audience` | identity | Token issuer and audience, defaults `kinevor.identity` / `kinevor.api` |
| `RateLimiting__PermitLimit` | public site | Requests per window, default `600` |
| `RateLimiting__WindowSeconds` | public site | Window length, default `60` |

The identity host applies a fixed 60-requests-per-minute limit. Every host also
sets `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
`Permissions-Policy`, `Cross-Origin-Opener-Policy` and a conservative
`Content-Security-Policy` via `UseKinevorSecurityHeaders()`.

### Deploying a single service

Build only the image you need:

```bash
docker build \
  -f deploy/demo/Dockerfile.api \
  --build-arg SERVICE=kinevor-members \
  -t kinevor-members-api .

docker run --rm -p 5083:8080 \
  -e ASPNETCORE_ENVIRONMENT=Demo \
  -e ConnectionStrings__Members="Data Source=/data/members-demo.db" \
  -e Cors__AllowedOrigins__0=https://members.example.com \
  -v kinevor-members-data:/data \
  kinevor-members-api
```

For the Angular demo, use `Dockerfile.web` with the same `SERVICE` argument.
The resulting nginx image serves a static bundle; point `config.js` at the API
origin during deployment, or replace the file in the image with your runtime
configuration.

Any OCI-compatible host works: Azure Container Apps, AWS ECS/Fargate, Google
Cloud Run, Kubernetes or a plain VM. Because services do not call each other,
each one can scale, fail and deploy independently. No gateway is required.

## Production hardening

The demo stack is intentionally simple. Before using it for real traffic:

1. Replace `X-Demo-*` authentication with an OIDC/OAuth provider and validate
   issuer, audience, lifetime and signing keys.
2. Remove demo seeding and demo-only endpoints; run EF Core migrations against a
   managed database instead of `EnsureCreated` on SQLite.
3. Store connection strings, JWT keys and provider credentials in a secret
   manager. Never commit them.
4. Put TLS, HSTS, request-size limits and a WAF or DDoS protection at the edge.
5. Replace the per-instance rate limiter with an edge or distributed limiter if
   you run more than one replica.
6. Configure exact client origins per environment; do not use wildcard CORS.
7. Add structured logs, traces, health probes and alerting. The existing `/health`
   endpoint is a liveness probe, not a readiness or dependency check.
8. Back up and test database restores. SQLite volumes need a snapshot-consistent
   backup strategy.
9. Run dependency, container and secret scanning in the deployment pipeline.
10. Verify release provenance before deploying a downloaded archive:

```bash
gh attestation verify Kinevor-v0.2.0.zip --repo georgevictorkamal/Kinevor-Gym
sha256sum Kinevor-v0.2.0.zip
```

## Release integrity

The tag-driven release workflow produces exactly one release asset: the source
zip. The workflow also publishes:

- an SPDX SBOM as a workflow artifact (`sbom-<tag>`);
- a signed build-provenance attestation for the zip.

The release notes include the SHA-256 and the `gh attestation verify` command.
The source archive is created with `git archive`, so it contains only tracked
files — no `.git`, `node_modules`, `bin`, `obj`, `dist` or `build` content.

## External integrations

These are deliberately **not activated** in the repository:

| Capability | Current state | What an adoption needs |
| --- | --- | --- |
| Payments | Simulated invoices and refunds | Stripe (or equivalent) account, keys, webhook endpoint, PCI scope review |
| E-mail / push | Delivery records only | Provider account, templates, credentials, bounce/suppression handling |
| QR check-in | Manual/demo check-in flows | Access-control hardware or QR scanner integration |
| App stores | Flutter packages and examples | Signing identities, store accounts, privacy declarations, release pipelines |
| Cloud hosting | Docker images and compose stack | Hosting account, DNS, TLS certificates, secrets and observability |

No credentials or live provider accounts are included in the repository.

## Troubleshooting

| Symptom | Cause and fix |
| --- | --- |
| API exits immediately | `ASPNETCORE_ENVIRONMENT` is not `Development` or `Demo` |
| Browser request blocked by CORS | The page origin is missing from `Cors__AllowedOrigins__*`; include scheme, host and port exactly |
| `NU1101` for `Kinevor.Platform.*` | Pack the platform into `kinevor-platform/backend/artifacts/packages` first |
| `NU1403` package content hash validation failed | The module lockfile was evaluated against a different platform pack. Restore or build with `--force-evaluate` / `-p:RestoreForceEvaluate=true`; do not edit the lockfile by hand |
| Module web build cannot resolve the platform package | Run the platform web build, then `npm run install:local` (it passes `--install-links` so the package is copied, not symlinked) |
| Stale data after a schema change | Stop the API and delete the module `.db` file, or `docker compose … down --volumes` |
| Port already in use | Stop the conflicting process or remap the host port in compose |
| Docker build is slow | The platform is rebuilt per image by design; keep the build cache or run a subset of services |
