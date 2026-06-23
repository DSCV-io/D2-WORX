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

## Versioning

```bash
dotnet tool restore                                                        # First-time setup
dotnet versionize --dry-run                                                # Preview bump (always do this first)
dotnet versionize                                                          # Bump version + update CHANGELOG + tag
git push --follow-tags
```

## Important

When editing shared `.NET` libs in `server/shared/dotnet/`, run `dotnet build server/D2.slnx` to verify all consumers still compile. SvelteKit changes are isolated — `cd server/web && pnpm exec svelte-check`.
