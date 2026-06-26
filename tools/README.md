<!--
Copyright (c) DCSV. All rights reserved.
-->

# tools/ — Dev Tooling

> Parent: [`/`](../README.md)

Scripts + utilities for developer workflows that aren't part of any service.

## Tools

| Tool                                                          | Purpose                                                                                                                                    |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| [`contract-gate/`](contract-gate/README.md)                   | Always-on PR-blocking breaking-change gate — three diff arms (buf proto, spec/i18n JSON-diff, OpenAPI JSON-diff) checked against the integration baseline |
| [`geo-data-pipeline/`](geo-data-pipeline/README.md)           | Geo reference-data ingestion pipeline — CLDR / IANA tzdb / libphonenumber / Wikidata → `contracts/geo/` catalogs codegen consumes          |
| [`loggermessage-splitter/`](loggermessage-splitter/README.md) | Splits the deterministic-ordered `LoggerMessage.g.cs` blob into per-class files for stable git diffs                                       |
| [`release-runner/`](release-runner/README.md)                 | Footer-keyed per-package semver + CHANGELOG automation — reads commit footers in the baseline..HEAD range and bumps each touched package    |
| [`ts-codegen/`](ts-codegen/README.md)                         | Per-topic `.g.ts` emitter scripts — TypeScript sibling of the .NET Roslyn source generators; both consume the same `contracts/` JSON specs |

`scripts/` holds shell utilities that don't warrant their own subdirectory README — covered inline below.

## Layout

```
tools/
  contract-gate/              PR-blocking breaking-change gate (proto + spec/i18n + OpenAPI). See contract-gate/README.md.
  geo-data-pipeline/          Geo reference-data ingestion (TypeScript). See geo-data-pipeline/README.md.
  loggermessage-splitter/     .NET 10 console tool — splits LoggerMessage.g.cs. See loggermessage-splitter/README.md.
  release-runner/             Footer-keyed per-package semver + CHANGELOG automation. See release-runner/README.md.
  ts-codegen/                 Per-topic .g.ts emitters. See ts-codegen/README.md.
  scripts/                    Shell scripts + small utilities
    gen-dev-keys.sh           Generates dev root key + per-domain encryption keys
                              Output → secrets/ (gitignored, Claude-deny-ruled)
```

## `scripts/gen-dev-keys.sh`

Generates the local-dev key material that `D2.Shared.Encryption` and `KeyCustodian` need.

```bash
./tools/scripts/gen-dev-keys.sh                    # generate any missing keys (idempotent)
./tools/scripts/gen-dev-keys.sh --rotate audit     # rotate the audit domain (new kid, old kept for grace)
./tools/scripts/gen-dev-keys.sh --force            # regenerate ALL keys (DESTRUCTIVE — invalidates encrypted data)
```

Output structure under `secrets/`:

```
secrets/
  auth/
    root.key                        Root key — encrypts all KeyCustodian keys at rest in auth_db
    audit-{yyyy}q{n}.key            Per-domain message-payload encryption keys
    notifications-{yyyy}q{n}.key
    courier-{yyyy}q{n}.key
```

All output is gitignored AND Claude-deny-ruled (`.claude/settings.json`).

## Adding new tooling

When a new dev tool / script is needed:

1. Add it to `tools/scripts/` (or a more specific subdirectory if it grows)
2. Make it executable (`chmod +x`)
3. Document it here + add a top-of-file comment block explaining what it does + how to invoke it
4. If it generates secrets / keys, output to `secrets/` (gitignored + deny-ruled)
5. If it generates non-secret artifacts, choose a sensible location (often `tools/output/` if temporary)

## `ts-codegen/`

Per-topic `tsx` emitter scripts that read JSON specs from `contracts/` and emit `*.g.ts` catalogs into the consuming `@d2/*` package's `src/` directory. Sibling to the .NET Roslyn source generators (`server/shared/dotnet/<cluster>/<name>/`) — both consume the same spec files so cross-language drift is structural, not aspirational.

```bash
pnpm codegen                       # runs every emitter via the orchestrator
pnpm codegen --force               # bypass mtime up-to-date check
pnpm --filter @d2/headers-http run prebuild   # per-package: re-emits just that catalog
```

Each codegen-consuming package's `package.json` declares a `prebuild` hook so `pnpm -r build` regenerates transparently. See `ts-codegen/README.md` for the full per-script catalog (auth-context, request-context, auth-scopes, auth-error-codes, auth-failures, headers, jwt-claims) plus diagnostic-ID conventions.

## What this directory is NOT

- **Not a service** — services live in `server/services/{service}/`
- **Not a shared library** — shared libs live in `server/shared/dotnet/{lib}/` (.NET) or `server/shared/typescript/{pkg}/` (TS)
- **Not infrastructure config** — that's `infra/`
- **Not application source** — these are operator/dev tools that run once or on-demand, not part of any service's runtime
