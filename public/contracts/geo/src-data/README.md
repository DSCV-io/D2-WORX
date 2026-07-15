<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# `contracts/geo/src-data/` — pipeline-raw geo specs

## What's here

JSON specs produced end-to-end by `tools/geo-data-pipeline` from upstream sources (CLDR / IANA tzdb / libphonenumber / datasets/\* / Wikidata SPARQL / debian iso-codes). Every entry traces to real upstream data — zero hand-written or AI-generated content. Per-source provenance (URL + sha256 + license + fetchedAt) is recorded at the top of each spec file.

| File                     | Source pipeline                                                                                                                                       | Entries                        |
| ------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------ |
| `countries.spec.json`    | datasets/country-codes + CLDR localenames-full + CLDR supplemental (week / measurement / currency / territoryInfo) + libphonenumber + Wikidata SPARQL | 249                            |
| `subdivisions.spec.json` | debian/iso-codes iso_3166-2 + CLDR cldr-subdivisions-full + Wikidata SPARQL                                                                           | 5,046                          |
| `timezones.spec.json`    | IANA zone1970.tab + IANA backward + Node ICU `Intl.DateTimeFormat`                                                                                    | 312                            |
| `languages.spec.json`    | datasets/language-codes + Wikidata SPARQL P218 + CLDR scriptMetadata/languageData + CLDR cldr-localenames-full                                        | 183                            |
| `locales.spec.json`      | CLDR availableLocales + CLDR likelySubtags + Node ICU Intl.Locale/DisplayNames + CLDR cldr-numbers-full + CLDR cldr-dates-full                        | 1,089                          |
| `currencies.spec.json`   | datasets/currency-codes + CLDR currencyData + CLDR cldr-numbers-full                                                                                  | 326 (178 active + 148 retired) |

## NOT directly consumed by codegen

These files are **pipeline-raw** — they include:

- Per-entry `_provenance` fields (which source contributed which fields)
- Top-level `sources[]` array (full provenance per upstream pull)
- Top-level `fieldCoverage`, `orderBreakdown`, `wikidataFills`, `tagShape` and other build-time diagnostics
- `$note` flagging build-version + remaining gaps

The codegen-ready specs that `DcsvIo.D2.Geo.Default` / `@dcsv-io/d2-geo-default` consume live **one level up** at `contracts/geo/*.spec.json`, produced by the Tier 2 clean-pass (`tools/geo-data-pipeline/src/tier-2/`) which strips diagnostics + applies cross-catalog M:M backfill + Locale denormalization + `IsSelectable`/`IsSupported` derivation.

See [`../README.md`](../README.md) for the full three-tier layout (src-data → Tier 2 → hand-rolled GeopoliticalEntity).

## How to refresh

```bash
cd tools/geo-data-pipeline

# All-in-one: src-data + Tier 2 + parity tests
pnpm geo:refresh

# Or per-catalog (src-data only):
pnpm write:countries
pnpm write:subdivisions
pnpm write:timezones
pnpm write:languages
pnpm write:locales
pnpm write:currencies
```

Each run pulls from upstream (with 24h cache TTL — see `.cache/` for cached upstream snapshots + `.provenance.json` sidecars) and overwrites the corresponding `*.spec.json` in this directory.

## License attribution

Sources are mixed-license but all Apache-2.0-compatible (this open tree's license posture):

- **CLDR**: Unicode-3.0 (no share-alike)
- **IANA tzdb**: Public domain
- **datasets/country-codes**: PDDL (public domain dedication)
- **libphonenumber**: Apache-2.0
- **Wikidata**: CC0
- **debian/iso-codes**: LGPL-2.1+

The composite spec files inherit derived-work licensing — the union of the source licenses listed above.
