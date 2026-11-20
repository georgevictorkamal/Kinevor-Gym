# Roadmap

Kinevor is a reference implementation of a gym-management platform: ten
independently runnable services on a shared platform kernel. The roadmap
therefore favours work that makes the existing services more reliable,
observable and easier to adopt over adding new product surface.

No dates are promised. Items move when they are ready and when CI proves them.

## Next — 0.3.0

The next minor release finishes the hardening work started in 0.2.x.

- **Identity** — account lockout after repeated failed sign-ins
  ([#23](https://github.com/georgevictorkamal/Kinevor-Gym/issues/23)) and TOTP
  multi-factor authentication
  ([#22](https://github.com/georgevictorkamal/Kinevor-Gym/issues/22)).
- **Trainers** — replace the in-memory search with a database-side query
  ([#18](https://github.com/georgevictorkamal/Kinevor-Gym/issues/18)).
- **Classes and bookings** — document the booking and waitlist flow with a
  Mermaid sequence diagram
  ([#17](https://github.com/georgevictorkamal/Kinevor-Gym/issues/17)) and add
  edge-case tests for booking ownership rules
  ([#16](https://github.com/georgevictorkamal/Kinevor-Gym/issues/16)).
- **Platform** — a French UI catalogue
  ([#15](https://github.com/georgevictorkamal/Kinevor-Gym/issues/15)), an EF Core
  migrations baseline for one module
  ([#24](https://github.com/georgevictorkamal/Kinevor-Gym/issues/24)) and
  OpenTelemetry traces and metrics
  ([#25](https://github.com/georgevictorkamal/Kinevor-Gym/issues/25)).

## Later

- **Notifications** — an SMTP delivery adapter with retries and suppression
  handling ([#21](https://github.com/georgevictorkamal/Kinevor-Gym/issues/21)).
- **Payments** — refunds that update invoice status
  ([#19](https://github.com/georgevictorkamal/Kinevor-Gym/issues/19)) and a
  payment-gateway port with a Stripe test-mode adapter
  ([#20](https://github.com/georgevictorkamal/Kinevor-Gym/issues/20)).

## Production path

The demo stack is intentionally loopback-only. Before anything is hosted, the
work in [production readiness](docs/production-readiness.md) must be done:

- PostgreSQL migrations and schema ownership per module;
- distributed rate limiting for authentication and write endpoints;
- centralized logs, metrics and traces;
- multi-architecture images, digest pinning and image scanning;
- an explicit policy for hosting, data retention and privacy.

## Not planned

- **A publicly hosted demo.** The demo APIs are not designed for public
  exposure. GitHub Codespaces and the published GHCR images are the supported
  ways to run the stack.
- **Publishing the Angular, NuGet or Flutter packages.** The packages are
  versioned internals of the monorepo. Publishing them needs a registry
  credential, a support policy and a semver contract that do not exist yet.
- **Workout plans, AI routines or a training tracker.** Kinevor models gym
  operations — members, memberships, classes, check-ins, payments and staff —
  not individual training programmes. Those features belong in a separate
  product.

## Proposing work

Open a [Discussion](https://github.com/georgevictorkamal/Kinevor-Gym/discussions)
for direction questions, or an issue using the templates for concrete changes.
Issues labelled `good first issue` are scoped for a first contribution. Every
change is expected to keep `npm run verify` and the module test suites green.
