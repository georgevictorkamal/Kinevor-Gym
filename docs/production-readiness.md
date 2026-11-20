# Production readiness

Kinevor is a reference implementation. The demo stack is deliberately simple:
SQLite, demo authentication headers, in-memory rate limiting and simulated
payments. This guide is a migration path for a team adapting one or more
services for real traffic. It describes changes, not a production configuration
that already exists in the repository.

> The demo hosts fail closed. `EnsureDemoHost()` stops any API whose
> `ASPNETCORE_ENVIRONMENT` is not `Development` or `Demo`. Do not weaken that
> guard in the demo projects; build a separate production host instead.

## Start with one service

Productionising all ten services at once hides mistakes. Pick one service with a
small surface first, usually **members** or **trainers**, and carry it through
the checklist below. The remaining services reuse the same patterns.

The demo hosts live in each module's `backend/samples/*.DemoApi` directory. A
production host can be a new project that references the module's application
and EF Core projects without the demo authentication, seeding, Swagger or CORS
defaults.

## Readiness matrix

| Capability | Demo implementation | Production requirement |
| --- | --- | --- |
| Authentication | `DemoAuthenticationHandler` and `X-Demo-*` headers | OIDC/OAuth or JWT bearer with issuer, audience and key validation |
| Persistence | SQLite; members applies a migrations baseline, other demos use `EnsureCreatedAsync()` | PostgreSQL (or another managed database) plus EF Core migrations |
| Rate limiting | In-memory fixed window in `RateLimitingExtensions` | Edge/WAF control or a distributed limiter shared by replicas |
| Secrets | `appsettings.json`, user secrets, random JWT key | Secret manager or managed identity; stable, rotatable signing keys |
| Payments | Simulated invoices and refunds behind a gateway port; Stripe sample is test-mode only | Provider integration with idempotency, webhooks and reconciliation |
| Notifications | Delivery records plus an optional SMTP adapter | Provider with retries, bounce/suppression handling and templates |
| Observability | `/health` liveness endpoint and opt-in OpenTelemetry | Readiness probes, structured logs, traces, metrics and alerts |
| Database topology | One SQLite file per service | One database (or schema) per service; no cross-service joins |
| Images | Public `linux/amd64` demo images | Scanned, multi-architecture images pinned by digest |

## 1. Replace demo authentication

Each demo host registers the demo scheme explicitly. For example,
`kinevor-members/backend/samples/Kinevor.Modules.Members.DemoApi/Program.cs`
contains:

```csharp
builder.Services
    .AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
        DemoAuthenticationHandler.SchemeName, _ => { });
```

In a production host:

1. Remove that registration, the `Authentication/DemoAuthenticationHandler.cs`
   file, the `X-Demo-*` header middleware and the demo data seeder.
2. Remove `app.EnsureDemoHost()` from the production host. Keep it in the demo
   host so the sample cannot be exposed by accident.
3. Add a real authentication handler. For an OIDC provider:

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.RequireSignedTokens = true;
    });
```

4. Keep the existing authorization policies in
   `kinevor-platform/backend/src/Kinevor.Platform.Api/AuthorizationOptionsExtensions.cs`
   and verify every endpoint has the policy it needs, not only the controller.
5. Map claims to the module's `ICurrentUser` and `IBranchContext`
   implementations. The demo implementations read headers; production
   implementations should read validated claims only.

If the identity service issues the tokens, configure a stable signing key or a
managed asymmetric key. The identity demo generates an ephemeral key when
`Jwt__SigningKey` is missing, so tokens do not survive a restart or scale-out.
Rotate keys with a `kid` and publish the public keys through a JWKS endpoint.

## 2. Move from SQLite to PostgreSQL

SQLite is used only in the demo hosts. The module DbContexts do not depend on a
provider. `kinevor-members/backend/samples/Kinevor.Modules.Members.DemoApi/Program.cs`
shows the pattern repeated by all ten services:

```csharp
builder.Services.AddDbContext<MembersDbContext>(
    options => options.UseSqlite(connectionString));
```

To move one service:

1. Add the provider and design-time tooling to the production host:

```bash
dotnet add <production-host>.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add <production-host>.csproj package Microsoft.EntityFrameworkCore.Design
```

2. Replace `UseSqlite` with `UseNpgsql` and read the connection string from the
   host configuration:

```csharp
builder.Services.AddDbContext<MembersDbContext>(
    options => options.UseNpgsql(
        builder.Configuration.GetConnectionString("Members")));
```

3. The members module ships the worked migrations example:
   `kinevor-members/backend/src/Kinevor.Modules.Members.Migrations` contains the
   SQLite baseline, and the library and EF Core projects contain none. Copy that
   layout for a production host and generate the migration against its own
   provider and migrations project:

```bash
dotnet ef migrations add InitialCreate \
  --project <host>.Migrations \
  --startup-project <host> \
  --output-dir Migrations
```

   Configure `MigrationsAssembly("<host>.Migrations")` in `UseNpgsql`. The SQLite
   baseline is a worked example, not a cross-provider migration.

4. Apply migrations as a deployment step, not on every process start. If the
   host applies them at startup, use `MigrateAsync()` and ensure only one
   instance runs the migration at a time.

5. Use one database per service, as the demo already does. Do not add a shared
   database or foreign keys across module boundaries; duplicate the small read
   models a screen needs instead.

Classes-and-bookings uses a `Guid` row version marked `IsConcurrencyToken()`,
which works on PostgreSQL. Review generated column types, indexes and unique
constraints rather than assuming the SQLite schema maps one-to-one.

## 3. Replace in-memory rate limiting

`kinevor-platform/backend/src/Kinevor.Platform.Api/RateLimitingExtensions.cs`
is a per-process fixed-window limiter keyed by the remote IP address. It is a
demo safeguard, not a bot defence or distributed quota.

Before exposing a service:

- Terminate TLS at a reverse proxy, CDN or load balancer and configure
  `ForwardedHeaders` with the exact known proxies. Do not trust arbitrary
  `X-Forwarded-For` values, or clients can choose their own rate-limit bucket.
- Put volumetric and bot protection at the edge. The application should not be
  the first line of defence.
- If you run more than one replica, use a distributed limiter (for example a
  Redis-backed limiter) or enforce quotas at the gateway. The built-in
  fixed-window limiter keeps its counters in memory per process.
- Apply tighter limits to login, token refresh and write endpoints than to
  public read endpoints.

## 4. Configure the edge and CORS

Every host reads exact origins from `Cors__AllowedOrigins__0...N`. Production
values must be the real client origins, including scheme and port. Never use a
wildcard with credentials.

At the edge, add:

- TLS termination with HSTS after the domain is confirmed;
- request-size and header-size limits;
- timeouts and connection limits;
- a WAF or DDoS protection layer;
- access logs that record the forwarded client IP from a trusted proxy only.

The services are independent and do not call one another, so an API gateway is
**optional**. A reverse proxy for TLS, host routing and quotas is useful; a
gateway that centralises business logic would recreate the coupling the
architecture avoids. If the browser needs a single origin, use a thin BFF or
reverse proxy and keep module logic in the services.

## 5. Secrets and key management

Move the following out of `appsettings*.json` and into a managed secret store:

- database connection strings;
- the identity signing key or certificate;
- payment and notification provider credentials;
- any SMTP, push or webhook secrets.

Prefer managed identities or workload identities over long-lived credentials.
Give each service its own least-privilege database account. Persist ASP.NET Core
Data Protection keys if you add cookies or antiforgery tokens; ephemeral keys
break sessions after a restart.

## 6. Observability and health

`/health` is a liveness check. It returns healthy without checking the database
or any dependency. Add:

- a readiness endpoint that checks the database and required downstream
  services, with a short timeout;
- structured logs with correlation IDs and no tokens or personal data;
- OpenTelemetry traces and metrics. `AddKinevorOpenTelemetry()` is available but
  off by default; enable it with `OpenTelemetry:Enabled=true` and point the
  standard `OTEL_EXPORTER_OTLP_*` variables at your collector;
- alerting on error rate, saturation, queue depth and failed provider calls;
- audit records for authentication, authorization and payment events.

## 7. Payments and notifications

The payments module records simulated invoices and refunds. Card capture goes
through an `IPaymentGateway` port; the default adapter is simulated and the
Stripe sample is test-mode only. A real provider integration needs:

- idempotency keys on every create/refund command;
- signature verification for webhooks and replay protection;
- an outbox or equivalent reliable publish path for events;
- reconciliation against the provider's ledger;
- a documented PCI scope; never store card data in these services.

The notifications module records deliveries and can deliver e-mail through an
opt-in SMTP adapter configured under `Notifications:Smtp`. For production, add
retries, bounce and complaint handling, suppression lists and per-channel
templates around that transport.

## 8. Containers and supply chain

The published demo images target `linux/amd64`. Building locally on an Apple
Silicon machine produces native images; pulling the published images there uses
emulation. Do not force `platform: linux/amd64` in the demo Compose file as a
blanket fix: it makes local ARM builds slower and can hide missing multi-arch
support. For production, build native or multi-architecture images and test
them on the target architecture.

Also:

- scan images and dependencies in the deployment pipeline;
- pin base images and deployed image digests, not only tags;
- keep the demo images public only if that is intentional; production images
  belong in a private registry;
- verify release provenance before deploying a downloaded archive;
- run the repository verifier and all test suites on every change.

```bash
gh attestation verify Kinevor-v0.3.0.zip --repo georgevictorkamal/Kinevor-Gym
sha256sum Kinevor-v0.3.0.zip
```

## 9. Go-live checklist

- [ ] A production host exists without demo authentication, seeding or Swagger.
- [ ] OIDC/JWT validation covers issuer, audience, lifetime and signing keys.
- [ ] The service uses PostgreSQL with reviewed migrations and a rollback plan.
- [ ] Each service has its own database and least-privilege credentials.
- [ ] Rate limits are enforced at the edge or by a distributed limiter.
- [ ] CORS lists exact origins; TLS, HSTS and request limits are configured.
- [ ] Secrets come from a managed store; signing keys are stable and rotatable.
- [ ] Readiness, logs, traces, metrics and alerts are in place.
- [ ] Payments and notifications use idempotent, verified provider adapters.
- [ ] Images are scanned, pinned by digest and built for the target architecture.
- [ ] Backups are automated and a restore has been tested.
- [ ] The runbook covers rollback, key rotation and provider outages.

## Implemented foundations

The 0.3.1 release implemented several of these steps; the production host still
owns the deployment-specific work above.

- [#24 EF Core migrations baseline for members](https://github.com/georgevictorkamal/Kinevor-Gym/issues/24)
- [#25 Opt-in OpenTelemetry traces and metrics](https://github.com/georgevictorkamal/Kinevor-Gym/issues/25)
- [#20 Payment gateway port with a Stripe test-mode sample](https://github.com/georgevictorkamal/Kinevor-Gym/issues/20)
- [#21 SMTP delivery adapter for notifications](https://github.com/georgevictorkamal/Kinevor-Gym/issues/21)
- [#22 TOTP multi-factor authentication](https://github.com/georgevictorkamal/Kinevor-Gym/issues/22)
- [#23 Account lockout after repeated failed sign-ins](https://github.com/georgevictorkamal/Kinevor-Gym/issues/23)
