<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/geo/` — geo reference data contracts

> Parent: [`/`](../../README.md)

Audience: developers contributing to or consuming the geo data pipeline (Tier 1 src-data, Tier 2 codegen-ready specs, Tier 3 generated downstream code).

## Consumed by

The Tier 2 `*.spec.json` files at this directory's root are the codegen inputs:

- **.NET** — [`public/packages/dotnet/geo/source-gen/`](../../public/packages/dotnet/geo/source-gen/README.md) (Roslyn source-gen → record shapes, branded code types, lookups, and catalog data in `DcsvIo.D2.Geo.Abstractions` / `DcsvIo.D2.Geo.Default`)
- **TypeScript** — [`tools/ts-codegen` › `geo-emitter/`](../../tools/ts-codegen/README.md) (→ record shapes, branded code types, Zod schemas, and catalog data in `@dcsv-io/d2-geo-abstractions` / `@dcsv-io/d2-geo-default`)

Both consume the same Tier 2 specs, so cross-language parity is structural. See [docs/SRC_GEN.md](../../docs/SRC_GEN.md) for the codegen pattern and [contracts catalog](../README.md) for the full contract index.

## Three-tier story

The geo data flows through three tiers from upstream ingestion to consumed code:

- **Tier 1** — `src-data/` — ingestion pipeline tooling output. Verbose; one JSON file per catalog with `_provenance` per entry + `sources[]` + `fieldCoverage` diagnostics. NOT consumed by codegen directly. Source: `tools/geo-data-pipeline/` pulls from CLDR / IANA tzdb / libphonenumber / datasets/\* / Wikidata SPARQL / debian/iso-codes.
- **Tier 2** — `*.spec.json` at this directory's root — denormalized + reorganized in the platform's preferred style. Match the canonical entity record shapes (`Country` / `Locale` / `Currency` / `Language` / `Subdivision` / `Timezone` / `GeopoliticalEntity`) 1:1. **THESE are the files codegen consumes**.
- **Tier 3** — generated C# + TS code produced by codegen (`DcsvIo.D2.Geo.Default` / `@dcsv-io/d2-geo-default` packages) consuming Tier 2. Lives in the downstream geo libs, not in this directory.

## Layout

```
contracts/geo/
├── src-data/                          ← TIER 1: ingestion pipeline output (verbose, per-catalog)
│   ├── countries.spec.json
│   ├── subdivisions.spec.json
│   ├── currencies.spec.json
│   ├── languages.spec.json
│   ├── locales.spec.json
│   ├── timezones.spec.json
│   └── README.md
├── overlays/                          ← Trackable manual patches applied at Tier 2 build time
│   ├── countries.overlays.spec.json     (additions/overrides/removals with reason + addedAt)
│   └── README.md
├── countries.spec.json                ← TIER 2: codegen-ready (pipeline-derived + overlays applied: $generated: true)
├── subdivisions.spec.json
├── currencies.spec.json
├── languages.spec.json
├── locales.spec.json
├── timezones.spec.json
├── geopolitical-entities.spec.json    ← TIER 2 peer: hand-rolled ($generated: false, $source: manual)
└── README.md (this file)
```

**Subdirectories** with their own READMEs:

- [`src-data/`](src-data/README.md) — Tier 1 pipeline-raw geo specs (verbose, per-catalog ingestion output; NOT consumed by codegen directly)
- [`overlays/`](overlays/README.md) — Tier 2 manual patches applied at build time (additions / overrides / removals with `reason` + `addedAt`)

See [KNOWN_WARNINGS.md](KNOWN_WARNINGS.md) for documented build-time warnings and design-rationale entries for the geo data pipeline (expected D2GEO010 / D2GEO011 warnings, allow-listed duplicates, and ambiguity-sentinel behavior).

**Overlays** are the trackable extension point for entities upstream sources omit (e.g., Kosovo) or get wrong. Each overlay entry carries a `reason` + `addedAt` so policy decisions are audit-trail visible — run `pnpm geo:overlays` from `tools/geo-data-pipeline/` to list active patches. See [`overlays/README.md`](./overlays/README.md) for when to overlay vs fix upstream vs hand-roll.

Every file at this directory's root carries `"$generated": true | false` + `"$source"` so consumers + tooling can unambiguously identify pipeline-derived vs hand-rolled.

## Tier 2 details

### Pipeline-derived (6 files)

Produced by `tools/geo-data-pipeline/src/tier-2/build-codegen-specs.ts` from Tier 1 src-data + the hand-rolled GeopoliticalEntity catalog. Strip `_provenance` and pipeline diagnostics; apply cross-catalog M:M backfill + Locale denormalization + `IsSupported` / `IsSelectable` derivation. Carry `"$generated": true, "$source": "pipeline-derived"`.

| File                     | Entries                        | Shape             |
| ------------------------ | ------------------------------ | ----------------- |
| `countries.spec.json`    | 249                            | `CountrySpec`     |
| `subdivisions.spec.json` | 5,046                          | `SubdivisionSpec` |
| `currencies.spec.json`   | 326 (178 active + 148 retired) | `CurrencySpec`    |
| `languages.spec.json`    | 183                            | `LanguageSpec`    |
| `locales.spec.json`      | 1,089                          | `LocaleSpec`      |
| `timezones.spec.json`    | 312                            | `TimezoneSpec`    |

**Do not hand-edit.** Re-run `pnpm geo:refresh` to regenerate from source.

### Hand-rolled peer (1 file)

`geopolitical-entities.spec.json` — sibling of the pipeline-derived Tier 2 specs but **not derived from any upstream source** — CLDR / ISO / IANA don't ship supranational grouping data (continents, regions, trade blocs, military alliances). Hand-maintained catalog of 59 supranational groupings × 1,249 country-GE memberships across 11 type-enum values. Carries `"$generated": false, "$source": "manual"`.

Codegen treats this file identically to the pipeline-derived Tier 2 peers — same shape conventions, same downstream `GeopoliticalEntity` record type. The only difference is provenance.

## Cross-tier integrity (parity tests)

`tools/geo-data-pipeline/tests/parity/tier-2-output.test.ts` enforces:

1. **Schema-shape sanity** — every pipeline-derived Tier 2 file has `$generated: true`, the hand-rolled GE peer has `$generated: false`
2. **Cross-catalog FK integrity** — every referenced ID (subdivision → country, locale → country, etc.) resolves to a real entry
3. **M:M inverse-nav symmetry** — `Country.subdivisionISO31662Codes` ↔ `Subdivision.countryISO31661Alpha2Code`, and similar for timezones, locales, GE, languages
4. **Denormalization integrity** — `Locale.firstDayOfWeek` MUST equal `Country[locale.country].firstDayOfWeek` (the drift guard)
5. **Derived-flag consistency** — `Currency.isSupported` / `Language.isSupported` derive correctly from `contracts/messages/*.json` file presence
6. **Encoding integrity** — invisibles (NBSP, NNBSP, RLM) survive write/read round-trip without normalization
7. **GE-Country references** — every country code in the hand-rolled catalog resolves (with known-orphan exemptions documented)

Run via `pnpm test` from `tools/geo-data-pipeline/`. Any drift between catalogs fails the build.

## How to refresh

```bash
cd tools/geo-data-pipeline

# All-in-one: regenerate Tier 1 src-data + Tier 2 output + run parity tests
pnpm geo:refresh

# Or step-by-step:
pnpm write:countries
pnpm write:subdivisions
pnpm write:timezones
pnpm write:languages
pnpm write:locales
pnpm write:currencies
pnpm tier-2:build    # produces the 6 pipeline-derived contracts/geo/*.spec.json from Tier 1 src-data
pnpm test             # parity tests
```

## License attribution

All sources are PolyForm-Strict-compatible:

- **CLDR**: Unicode-3.0 (no share-alike)
- **IANA tzdb**: Public domain
- **datasets/country-codes**, **datasets/currency-codes**, **datasets/language-codes**: PDDL (public domain)
- **libphonenumber**: Apache-2.0
- **Wikidata**: CC0
- **debian/iso-codes**: LGPL-2.1+

Composite Tier 2 output inherits derived-work licensing. Per-source license attribution is enumerated above.

---

## Navigation

**Consumed by:**
- `public/packages/dotnet/geo/source-gen/` — .NET Roslyn source-gen; emits geo record types, code wrapper structs, lookup tables, and enum constants into `DcsvIo.D2.Geo.Abstractions` and `DcsvIo.D2.Geo.Default`
- `tools/ts-codegen/src/geo-emitter/` — `tools/ts-codegen` geo emitter; generates TypeScript record shapes, branded code types, Zod schemas, and catalog data into `@dcsv-io/d2-geo-abstractions` and `@dcsv-io/d2-geo-default`

**Generated output** is committed + byte-gated — see [docs/SRC_GEN.md](../../docs/SRC_GEN.md).

Part of the [contracts catalog](../README.md).
