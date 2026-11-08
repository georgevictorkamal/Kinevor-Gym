# ADR 0004 — Quality gates and release integrity

- **Status:** Accepted
- **Date:** 2026-11-07
- **Supersedes:** none

## Context

The repository makes strong claims about independent services, accessibility and
release quality. Those claims need automated evidence, and a source archive
needs a verifiable chain of custody.

## Decision

- Run a repository hygiene job (`scripts/verify-repo.mjs`) that checks the
  architecture rules, version alignment, required files and release guardrails.
- Collect .NET coverage with `coverlet.collector` and upload the Cobertura
  output for the platform and every module.
- Run Playwright end-to-end tests against the public site and members demo on
  desktop (1440×1040) and mobile (390×844) viewports. Every audited route must
  have no horizontal overflow and no serious or critical axe violations.
- Run dependency review on pull requests and OpenSSF Scorecard on `main`.
- Require a single `CI success` gate before merging.
- Publish exactly one release asset, the `git archive` source zip, with:
  - its SHA-256 in the release notes;
  - a signed build-provenance attestation (`actions/attest-build-provenance`);
  - an SPDX SBOM uploaded as a workflow artifact (not a second release asset).
- Never move or reuse a published tag. Fixes go into a new patch release.

## Consequences

- A red matrix or a failing accessibility check blocks the release gate.
- Coverage is visible but not yet enforced by a threshold; that keeps the first
  hardening release honest instead of gaming a percentage.
- Visual regression snapshots are not part of the gate yet. Baselines must be
  generated on the same Linux renderer used by CI before they can be trusted.
- The release workflow needs `id-token: write` and `attestations: write` in
  addition to `contents: write`.
- Published tags remain immutable; users can verify a download with
  `gh attestation verify` and `sha256sum`.
