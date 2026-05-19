<!--
Copyright (c) DCSV. All rights reserved.
-->

# `tools/geo-data-pipeline/` — geo reference-data pipeline

TypeScript dev tool that pulls upstream geo reference data (CLDR, IANA tzdb, libphonenumber, datasets/*, Wikidata SPARQL, debian/iso-codes), transforms it, and writes the JSON catalogs at `contracts/geo/` that codegen consumes.

## Quick start

```bash
cd tools/geo-data-pipeline
pnpm install            # one-time install of devDependencies
pnpm geo:refresh        # full pipeline: src-data + Tier 2 + parity tests
```

## Architecture

Three-tier output layout at `contracts/geo/`:

- **Tier 1 — `src-data/*.spec.json`** — pipeline-raw output (faithful capture of each upstream source with diagnostics + per-entry `_provenance`)
- **Tier 2 — `*.spec.json` (root)** — codegen-ready entity-shaped specs (6 pipeline-derived + 1 hand-rolled peer: `geopolitical-entities.spec.json`)
- **Tier 3** — generated C# + TS code in the downstream geo libs (`D2.Shared.Geo.Default` / `@d2/geo-default`); produced from Tier 2; lives OUTSIDE this directory

See [`../../contracts/geo/README.md`](../../contracts/geo/README.md) for the tier details.

Inside the pipeline:

```
src/
├── fetchers/        ← one module per upstream source; each calls fetchAndCache()
├── transformers/    ← pure functions; turn raw upstream rows into partial spec entries
├── spec-writers/    ← per-catalog orchestrators (Tier 1 src-data writers)
├── tier-2/         ← reads all Tier 1 + the Tier 2 hand-rolled GE peer → writes Tier 2
├── cli/             ← refresh / diff / approve / bump-version entry points
└── util/            ← cache.ts (filesystem cache with 24h TTL) + json-encoding.ts
```

All upstream fetches go through `util/cache.ts` (HTTP GET with provenance sidecar + SHA-256 + 24h TTL). Re-running `pnpm geo:refresh` after the first pull is fast — only stale entries re-fetch.

## Operator workflows

| Workflow | Command | When to use |
|---|---|---|
| Refresh all catalogs | `pnpm geo:refresh` | Routine pipeline run; idempotent |
| Force-refresh a specific source | delete `.cache/<source>/<key>` then re-run | When upstream changes within the 24h TTL window |
| Diff between runs | `pnpm geo:diff` | Review what upstream changed before approving |
| Approve curated drift | `pnpm geo:approve` | Lock acknowledged upstream drift into per-source `.upstream-rejections.json` |
| List active overlays | `pnpm geo:overlays` | Audit active manual overlay patches across all overlay files with `addedAt` + `reason`; append `--json` for structured output |
| Bump catalog version | `pnpm geo:bump-version <semver>` | Cut a new `catalogVersion` value for the next refresh |
| Per-catalog refresh | `pnpm write:countries` (etc.) | Iterate on a single catalog without running the full pipeline |
| Tier 2 only | `pnpm tier-2:build` | Regenerate Tier 2 from existing Tier 1 (skip upstream pulls) |
| Parity tests | `pnpm test` | Verify cross-catalog FK integrity + denormalization + encoding round-trip |
| TypeScript build | `pnpm run build` | Verify TS compiles before commit (CI mirror) |

## Cache

- **Location**: `tools/geo-data-pipeline/.cache/` (gitignored)
- **TTL**: 24h per upstream entry (override via `fetchAndCache({ ttlHours: 0 })` to always refetch)
- **Bust**: delete an entry (`rm -rf .cache/<source>/<key>`) to force re-fetch on the next run
- **Provenance**: each cached file has a sibling `.provenance.json` with URL + license + SHA-256 + fetch timestamp

## Troubleshooting

### "Fetch failed: <url> -> 404 Not Found"

Upstream URL moved or the resource was renamed. Check the source file under `src/fetchers/` and update the URL constant. Common during CLDR major-version bumps.

### "Fetch timeout (60s)" / "Wikidata SPARQL fetch timeout (120s)"

Upstream is slow / unreachable. Re-run the command — the cache will preserve any successful intermediate fetches. If Wikidata SPARQL times out persistently, the public endpoint may be under load; wait + retry.

### Parity tests fail with "Country.X references unknown ..."

A cross-catalog FK is broken — typically because:

1. An upstream source dropped or renamed an entry (re-run `pnpm geo:refresh` to pull the latest); OR
2. The manual `geopolitical-entities.spec.json` references a country that's been removed from `countries.spec.json` (update the manual catalog).

The parity-test failure message names the catalog + entry + missing reference.

### Currency reconciliation logs are noisy

The currency-reconciliation pass (in `src/spec-writers/write-countries.ts`) logs every override between `datasets/country-codes` and CLDR `currencyData`. Normal noise — these are operator-visibility diagnostics. Examples:

- `[currency] fill XX: datasets=null -> CLDR-active="YYY"` — datasets had no primary currency, CLDR provided one (good)
- `[currency] override XX: datasets="YYY" is RETIRED in CLDR -> CLDR-active="ZZZ"` — datasets had a retired currency, CLDR's active replacement used (good)
- `[currency] WARN XX: datasets="YYY" retired but no CLDR active fallback` — operator review needed

### Skipped locales in currency / locale writers

CLDR doesn't ship `currencies.json` / `numbers.json` / `ca-gregorian.json` for every locale. The writers gracefully skip 404s and log the count: `[skip] N locales without cldr-numbers-full currencies.json`. Non-404 errors surface as warnings + re-throw — the pipeline aborts so the operator can diagnose.

## Dependencies

| Package | License | Why |
|---|---|---|
| `papaparse` | MIT | CSV parsing for datasets/* sources |
| `fast-xml-parser` | MIT | XML parsing for libphonenumber metadata |
| `tsx` | MIT | TS execution for CLI scripts (no precompile step) |
| `typescript` | Apache-2.0 | Build (`tsc -b`) |
| `vitest` | MIT | Parity + unit tests |
| `@types/node`, `@types/papaparse` | MIT | TS type declarations |

Standalone tool — no `@d2/*` workspace dependencies. Build and tests run in isolation.

## License attribution

All upstream sources are PolyForm-Strict-compatible:

- **CLDR**: Unicode-3.0 (no share-alike)
- **IANA tzdb**: Public domain
- **datasets/country-codes**, **datasets/currency-codes**, **datasets/language-codes**: PDDL
- **libphonenumber**: Apache-2.0
- **Wikidata**: CC0
- **debian/iso-codes**: LGPL-2.1+
