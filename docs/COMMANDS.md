<!--
Copyright (c) DCSV. All rights reserved.
-->

# D²-WORX Operational Commands

> **Audience**: Claude (and human contributors) who need the operational commands for D²-WORX. Updated when commands change.

> ⚠️ **DO NOT START SERVICES MANUALLY** — Never run `dotnet run`, `pnpm dev`, `pnpm preview`, or any long-running server directly. Services are managed by Docker Compose.
> E2E tests that self-manage their infrastructure (Testcontainers, child processes with cleanup) ARE allowed — they start and stop their own services.

## Docker Compose (service lifecycle)

```bash
make up                                                                    # Start all services (detached)
make down                                                                  # Stop all services
docker compose -f infra/compose/compose.yml --env-file .env.local --env-file .env.secrets up -d      # Direct invocation
```

## Build

```bash
dotnet build server/D2.slnx                                                # Full .NET solution
dotnet build server/services/{service}/api/{service}.API.csproj            # Single project
cd server/web && pnpm install && pnpm exec svelte-check                    # SvelteKit type check
```

## Rider/ReSharper Inspections (.NET)

```bash
# Full solution (WARNING+ severity, text output, no build — run after dotnet build)
jb inspectcode server/D2.slnx --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log

# Single project (faster — use during focused work)
jb inspectcode server/D2.slnx --project="Edge.App" --severity=WARNING --format=Text --no-build --output=inspectcode.log && cat inspectcode.log
```

These catch warnings that `dotnet build` does NOT surface: `[MustDisposeResource]` misuse, captured variable/closure issues, object initialization suggestions, and other JetBrains-specific inspections. Must be zero warnings.

## Test

```bash
# .NET (xUnit v3 — Microsoft.Testing.Platform)
# Trait filters go after `--` as `--filter-trait "name=value"`.
# The VSTest-style `--filter` flag is silently ignored by MTP (warning MTP0001).
dotnet test server/D2.slnx                                                 # Full solution
dotnet test server/D2.slnx -- --filter-trait "Category=Unit"              # Unit-tagged only
dotnet test server/services/edge/tests                                      # Specific service

# SvelteKit
cd server/web && pnpm exec vitest run                                       # Unit tests (browser mode)
cd server/web && pnpm exec playwright test                                  # Playwright (mocked by default)
```

### Real-socket mutual-TLS harness proof (Linux/OpenSSL)

```bash
bash tools/scripts/run-mtls-proof.sh                                        # Build a Linux SDK image + run the mTLS harness over a real socket
```

The `MutualTlsSignerHarnessTests` exercise the shipped `AddD2MutualTls` require-and-validate path over a genuine TLS handshake on a loopback Kestrel endpoint. The six client-cert-presenting cases SKIP on Windows — Schannel cannot build a certificate context for a leaf chaining to a private CA without installing the root into the OS store (a clean-box limitation, not a harness defect). The deployment target is Linux/OpenSSL, where those cases EXECUTE: the script builds a small `mcr.microsoft.com/dotnet/sdk:10.0` image (`server/` + `contracts/` only; the repo `.dockerignore` excludes `obj/`+`bin/`, so the Windows host's build artifacts never seed the Linux build) and runs the harness filter inside a `--rm` container. It needs no Postgres/Redis/RabbitMQ — the harness is self-contained loopback. The cross-platform proof of the validator's conjunct matrix is the `SpiffeSanPeerValidatorTests` unit suite, which runs everywhere.

## Lint/Style

```bash
cd server/web && pnpm exec eslint .                                         # ESLint
cd server/web && pnpm exec prettier --check .                               # Prettier check
```

## Contract breaking-change gate

The always-on gate runs three arms against the integration baseline branch. Run locally before pushing a PR that touches contract files.

```bash
# Fetch the baseline first (required for all arms — substitute <baseline> with your
# integration baseline branch, e.g. git fetch origin <baseline>):
git fetch origin <baseline>

# All arms (proto + spec/i18n/OpenAPI):
node tools/contract-gate/dist/cli.js --against <baseline>

# Spec / i18n / OpenAPI arms only (no buf required):
node tools/contract-gate/dist/cli.js --against <baseline> --skip-proto

# Proto arm only (buf breaking at FILE level):
node tools/contract-gate/dist/cli.js --against <baseline> --proto-only

# Or run buf directly over the shared protos (substitute <baseline>):
pnpm --filter @d2/typespec-emitters exec buf breaking contracts/protos \
  --against '.git#branch=<baseline>,subdir=contracts/protos'

# Gate unit tests (owned-code validation):
pnpm --filter contract-gate test

# Rebuild the gate after editing src/ (required before running the CLI):
pnpm --filter contract-gate build
```

### Force valve

To intentionally break a stable contract, add one of these footers to a commit in the PR:

```
WIRE-BREAKING: <description>    # wire-axis (proto / OpenAPI)
BREAKING CHANGE: <description>  # api-axis (spec catalogs / i18n)
```

A `type!: subject` breaking shorthand on the subject line is also recognized.
Any breaking footer opens **all gate arms** for the PR.

**One conscious act**: the footer alone is not enough. Also:
1. Bump the package semver MAJOR (`dotnet versionize` or manual).
2. Add a `CHANGELOG.md` breaking entry describing the change and migration path.

### Deprecate-not-delete workflow

To retire a spec entry without an immediate forced break:

1. Keep the entry; add `"deprecated": true` (the gate PASSES — additive).
2. When telemetry confirms the entry is unused, delete it. The gate FAILS.
   Pull the force valve (`WIRE-BREAKING:` footer) + bump MAJOR + write CHANGELOG.

## Versioning

### Product version (the deployable `d2-version`)

```bash
dotnet tool restore                                                        # First-time setup
dotnet versionize --dry-run                                                # Preview bump (always do this first)
dotnet versionize                                                          # Bump version + update CHANGELOG + tag
git push --follow-tags
```

### Per-package version (consumable libs: `D2.Shared.*` + `@d2/*`)

The release runner reads `baseline..HEAD`, maps commits to packages by file-path
containment, and applies per-package semver + CHANGELOG entries.

```bash
# Dry-run — compute and report, write nothing (default).
# Substitute <baseline> with your integration baseline branch, or set D2_RELEASE_BASELINE:
pnpm --filter release-runner exec tsx src/cli.ts --against <baseline>

# Dry-run scoped to one package:
pnpm --filter release-runner exec tsx src/cli.ts --against <baseline> --package D2.Shared.Result

# Apply — write version slots + prepend CHANGELOG blocks:
pnpm --filter release-runner exec tsx src/cli.ts --against <baseline> --apply

# Runner unit tests:
pnpm --filter release-runner test
```

`--against` sets the integration baseline branch ref. If omitted, the runner checks
`D2_RELEASE_BASELINE`, then falls back to the built-in operational default. After
`--apply`, review the diffs before committing.

## Cutting a library release

Library snapshots are published to GitHub Releases via a manual workflow dispatch —
never auto-triggered on push. Registry publishing (npm / NuGet) is a separate, deliberate step, not performed by this workflow.

### Dry-run first (always)

Navigate to **Actions → Release libraries → Run workflow** in the GitHub UI and leave
`dry_run` set to `true` (the default). The workflow will:

1. Pack all 83 consumable libraries (54 .NET `.nupkg` + 29 TypeScript `.tgz`).
2. Assemble `d2-libs-<tag>.zip` with `nuget/`, `npm/`, `manifest.json`,
   `HOW-TO-USE.md`, and `LICENSE.md`.
3. Upload the zip + loose artifacts as a **workflow artifact** for inspection.
4. **Stop — no GitHub Release is created.**

Download and inspect the workflow artifact to verify the bundle is correct before
cutting a real release.

### Cut a real release

Run the workflow again with `dry_run` set to `false`. This runs the same pack + assemble
steps and then executes:

```
gh release create <tag> --title "D2 libraries <tag>" \
  --notes-file bundle/RELEASE-NOTES.md --prerelease \
  d2-libs-<tag>.zip bundle/nuget/*.nupkg bundle/npm/*.tgz
```

The release is marked as a pre-release (all packages are pre-stable `0.x`).

### Tag

The default tag is `libs-YYYY.MM.DD` derived from the run date. Override it via the
optional `tag` workflow input.

### List the consumable inventory locally

```bash
# Print the full 83-package inventory as JSON (read-only — writes nothing):
pnpm --filter release-runner exec tsx src/cli.ts --list
```

## Per-package pack

Proves a package is publish-ready (produces a valid artifact) without pushing to a registry.
Run locally to validate packaging metadata before a PR.

```bash
# .NET — pack one consumable (exercises the transitive ProjectReference closure):
dotnet pack server/shared/dotnet/result/core/D2.Shared.Result.csproj \
  --configuration Release --output /tmp/pack-smoke

# TS — build then pack one consumable (verifies files: ["dist"] allowlist):
pnpm --filter @d2/result build
pnpm --filter @d2/result pack --pack-destination /tmp/pack-smoke

# Inspect the tarball contents:
tar -tzf /tmp/pack-smoke/d2-result-*.tgz | head -20
```

CI runs a pack smoke on every PR (`pack-smoke` job in `.github/workflows/test.yml`) using
the same commands above.

## Important

When editing shared `.NET` libs in `server/shared/dotnet/`, run `dotnet build server/D2.slnx` to verify all consumers still compile. SvelteKit changes are isolated — `cd server/web && pnpm exec svelte-check`.
