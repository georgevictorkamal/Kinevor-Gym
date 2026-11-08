# Architecture decision records

Short records of the decisions that shape the repository. Each one states the
context, the decision and its consequences so a future change can be evaluated
against it.

| ADR | Decision |
| --- | --- |
| [0001](0001-one-repository-ten-services.md) | One repository, ten independent services, no runtime orchestration |
| [0002](0002-platform-packages-by-version.md) | Platform packages are consumed by version from local/CI artifacts |
| [0003](0003-demo-environment-and-http-hardening.md) | A `Demo` environment plus shared headers, CORS and rate limits |
| [0004](0004-quality-gates-and-release-integrity.md) | Coverage, E2E, dependency review, SBOM and signed provenance |

ADRs are immutable once accepted. Supersede an old record with a new one rather
than rewriting history, matching the repository's tag policy.
