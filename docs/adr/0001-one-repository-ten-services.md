# ADR 0001 — One repository, ten independent services

- **Status:** Accepted
- **Date:** 2026-11-07
- **Supersedes:** none

## Context

Kinevor demonstrates how a gym-management product can be split into clear
business capabilities while staying easy to review, clone and run. The
alternatives considered were separate repositories per service, a single
monolithic application, or a runtime service mesh.

## Decision

Keep one repository containing:

- a shared platform kernel (`kinevor-platform`) with domain primitives, API
  envelopes, authorization policies and Angular client services;
- ten vertical services, each with its own backend, database, tests and — where
  the capability needs one — its own Angular library and Flutter package.

Services reference the platform only at build time. They do not call each other,
share a database, use a message bus or participate in cross-service transactions.
There is no API gateway.

## Consequences

- One clone, one CI matrix and one screenshot set make the project easy to
  evaluate.
- A module can be moved to its own repository by making its folder the
  repository root; only the platform dependency path changes.
- Cross-service workflows remain the caller's responsibility. That is a
  deliberate limitation, not an accident.
- Shared changes must be validated against every consumer; the CI matrices do
  that work.
- Platform packages are versioned and consumed like external dependencies, which
  keeps module builds honest.

See [ARCHITECTURE.md](../../ARCHITECTURE.md) for the full topology.
