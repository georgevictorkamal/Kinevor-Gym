# Troubleshooting

Short answers to problems people hit when running Kinevor. If an entry does not
cover your case, ask in
[Discussions](https://github.com/georgevictorkamal/Kinevor-Gym/discussions) or
open an issue with the template.

## C# projects fail to restore with NU1301

The module backends reference the shared platform as NuGet packages from a local
folder feed:

```text
kinevor-platform/backend/artifacts/packages
```

When that folder is missing, every module restore fails with
`NU1301: The local source ... doesn't exist`. Pack the platform first:

```bash
dotnet pack kinevor-platform/backend/Kinevor.Platform.slnx -c Release \
  -o kinevor-platform/backend/artifacts/packages
```

- **Codespaces:** the devcontainer setup does this automatically. If the
  workspace opened before setup finished, run `bash .devcontainer/setup.sh` and
  reload the window.
- **After pulling changes to the platform:** repack, then force a restore of the
  module you are working on:

  ```bash
  dotnet restore kinevor-members/backend/Kinevor.Modules.Members.slnx --force-evaluate
  ```

## The Codespace setup stopped or Flutter is missing

`postCreateCommand` runs `.devcontainer/setup.sh`, which packs the platform and
installs Flutter on top of the devcontainer feature set. Rerun it from the
repository root:

```bash
bash .devcontainer/setup.sh
```

The script is idempotent. To work only on the .NET and Angular services without
the Flutter download, skip it:

```bash
KINEVOR_SKIP_FLUTTER=1 bash .devcontainer/setup.sh
```

## The demo stack takes a long time to start

`npm run demo:up` builds nineteen images from source. The first build needs
Docker with Compose v2, a network connection and about 8 GB of disk.

Pull the published release images instead of building:

```bash
npm run demo:images
```

Or start only the public site and members services:

```bash
npm run demo:up:showcase
```

## The demo feels slow on an Apple Silicon Mac

The published GHCR images target `linux/amd64`, so Docker Desktop runs them
through emulation. A local build is native:

```bash
npm run demo:up
```

Do not add `platform: linux/amd64` to the Compose file as a workaround. It makes
local ARM builds slower and hides missing multi-architecture support. See
[production readiness](production-readiness.md#8-containers-and-supply-chain).

## A demo port is already in use

The stack binds `4200`–`4208` for web demos and `5081`–`5090` for APIs, all on
`127.0.0.1`. Stop the previous stack first:

```bash
npm run demo:down
```

Then check which process holds the port:

```bash
# macOS / Linux
lsof -i :4208

# Windows PowerShell
Get-NetTCPConnection -LocalPort 4208
```

## A demo API exits immediately when run outside Docker

The demo hosts refuse to start unless the environment is `Development` or
`Demo`; this is deliberate. Set it before `dotnet run`:

```bash
ASPNETCORE_ENVIRONMENT=Demo dotnet run --project <path-to-DemoApi>
```

On PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Demo"
```

## `KINEVOR_VERSION` is ignored on Windows PowerShell

`KINEVOR_VERSION=0.3.1 npm run demo:images` is POSIX syntax. In PowerShell set
the variable first:

```powershell
$env:KINEVOR_VERSION = "0.3.1"
npm run demo:images
```

## Playwright cannot find a browser

The end-to-end suite needs Chromium. Install it once after `npm ci`:

```bash
npx playwright install chromium
```

On Linux CI or a minimal container you may also need the system dependencies:

```bash
npx playwright install --with-deps chromium
```

## Flutter commands use the wrong SDK

The repository pins Flutter in `.fvmrc` and the devcontainer installs that
version. Check what is on your path:

```bash
flutter --version
```

Use the pinned version before running analyze or tests. In a Codespace the SDK
lives in `~/flutter/bin`.

## Reading container logs

When a demo service starts but misbehaves, read its logs:

```bash
docker compose -f deploy/demo/docker-compose.yml logs -f kinevor-members-api
```

Add `--tail 200` to limit the output. Logs never contain the demo signing key
because no key is stored in the repository.
