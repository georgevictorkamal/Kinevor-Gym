# Contributing

Thank you for looking at Kinevor. This is a portfolio reference implementation,
not a product with a public roadmap, but focused fixes and documentation
improvements are welcome.

## Before you start

1. Read [ARCHITECTURE.md](ARCHITECTURE.md) so changes stay inside the owning
   module.
2. Read [SECURITY.md](SECURITY.md). Do not open a public issue for a
   vulnerability.
3. Keep modules independent. Do not add a project reference from one
   `kinevor-*` service to another.
4. Read the relevant [architecture decision records](docs/adr/) before changing
   the platform dependency, demo-environment or release rules.

## Development setup

Follow the [Quick start](README.md#quick-start) in the root README:

1. Pack the platform NuGet packages and build the Angular platform library.
2. Run the module you are changing (`dotnet run`, `npm start`, or
   `flutter run`).
3. Use the seeded demo accounts documented in each module README.
4. Use Node 24 with npm 11+ and Flutter 3.47.8 to match CI.

For a full local stack, `npm run demo:up` builds the Docker Compose environment.
See [the deployment guide](docs/deployment.md).

## Making a change

- One concern per pull request (one module, or a shared platform contract).
- Match the existing layout: domain / application / API on the backend;
  gateway / facade / component on the web; client / repository / screen on
  mobile.
- Do not commit `.env` files, SQLite databases, `bin/`, `obj/`, `node_modules/`,
  `dist/`, or signing keys.
- Demo authentication headers and seeded passwords are intentional. Do not
  “fix” them into production auth in a drive-by change.
- Commit `packages.lock.json` and `package-lock.json` changes. Do not regenerate
  lockfiles as an unrelated drive-by change.

## Tests

Run the suites that cover what you touched:

```bash
dotnet restore <module>/backend/*.slnx --force-evaluate
dotnet test <module>/backend/*.slnx -c Release --no-restore
```

Module lockfiles are committed for review but intentionally re-evaluate the
platform package hashes, because a local platform pack is not byte-identical
between builds. If a restore fails with `NU1403`, re-run it with
`--force-evaluate` (or `-p:RestoreForceEvaluate=true` for `dotnet build` or
`dotnet publish`) instead of editing the lockfile.

```bash
dotnet test <module>/backend/*.slnx -c Release \
  --collect:"XPlat Code Coverage"
```

```bash
cd <module>/web
npm run install:local
npm test
```

```bash
cd <module>/mobile/packages/<package>
dart format --output=none --set-exit-if-changed lib test
flutter analyze
flutter test
```

End-to-end tests need a Release build of the public-site and members demos:

```bash
npm run openapi:export   # refresh docs/openapi after API contract changes
npm run e2e
```

CI runs repository guardrails, platform and module matrices, coverage, the
Playwright suite, dependency review and OpenSSF Scorecard. A change is not ready
while any required job or the `CI success` gate is failing.

## Release process

1. Never move, delete or reuse a published tag. Fixes ship in a new version.
2. Run `node scripts/bump-version.mjs <version>` and commit the result.
3. Confirm `node scripts/verify-repo.mjs`, all builds, all tests and the full
   Playwright suite are green.
4. Create an annotated `v<version>` tag and push it.
5. Wait for the release workflow. It publishes exactly one source zip, an SPDX
   SBOM workflow artifact and a build-provenance attestation.
6. Verify the release asset's SHA-256 before announcing it.

## Pull requests

- Use a short title that says why the change exists.
- Link the issue if there is one.
- Include screenshots when you change a visible screen.
- Include the route and viewport when you change an accessibility or responsive
  test.
- Be ready to discuss the service boundary: if two modules need the same data,
  prefer duplicating a read model over coupling them.

By contributing you agree that your work is licensed under the MIT License in
[LICENSE](LICENSE).
