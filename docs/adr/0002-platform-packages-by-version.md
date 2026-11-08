# ADR 0002 — Platform packages are consumed by version

- **Status:** Accepted
- **Date:** 2026-11-07
- **Supersedes:** none

## Context

The platform kernel is shared by ten services in two ecosystems. Project and
path references would let a module compile against unpublished local changes and
would hide the dependency review that a versioned package provides.

## Decision

Consume the platform through real packages:

- `.NET`: three NuGet packages (`Kinevor.Platform.Domain`,
  `Kinevor.Platform.Application`, `Kinevor.Platform.Api`) resolved from
  `kinevor-platform/backend/artifacts/packages` by every module
  `nuget.config`.
- Angular: `@georgevictorkamal/kinevor-platform-web`, declared as a `file:`
  dependency on the built Angular package at
  `kinevor-platform/web/dist/kinevor-platform-web`.

Module web workspaces keep the `file:` dependency permanently. `install:local`
verifies the platform build, runs `npm ci`/`npm install` against it and leaves
the manifest untouched. NuGet lockfiles are committed for every project.

CI packs/builds the platform first, uploads the artifacts, and restores them at
the same sibling paths in each matrix job. Platform restore uses
`--locked-mode`; module restore is deliberately unlocked because locally
rebuilt `.nupkg` files are not byte-deterministic and a locked restore would
fail with `NU1403`.

## Consequences

- A module cannot accidentally depend on unbuilt platform source.
- Version bumps are explicit and visible in diffs.
- The build has a required ordering: platform first, modules second.
- CI must transfer artifacts between jobs instead of relying on a shared
  checkout cache.
- Publishing to public registries remains optional; each module's
  `PUBLISHING.md` documents the GitHub Packages flow.
