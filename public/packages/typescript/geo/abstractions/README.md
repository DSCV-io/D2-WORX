<!--
Copyright (c) DCSV. All rights reserved.
-->

# @dcsv-io/d2-geo-abstractions

> Parent: [`public/packages/typescript/`](../../README.md)
>
> **Audience**: backend Node/TypeScript service and BFF engineers who need
> a data-free reference-data type surface — interfaces, meta-records, and
> name-resolution primitives — without dragging the full geo catalog
> (`@dcsv-io/d2-geo-default`).

Codegen-emitted reference-data type surface + hand-written meta-record +
name-resolution primitives. Mirrors `DcsvIo.D2.Geo.Abstractions` (.NET).

## Overview

The geo reference-data layer ships in two TS packages:

- **`@dcsv-io/d2-geo-abstractions`** — this package. Type shapes (record interfaces,
  branded typed-code wrappers, validation schemas) + `DeprecationInfo` +
  name-resolution helpers. Near-zero runtime payload at import — pure types
  plus two small string-algorithm functions.
- **`@dcsv-io/d2-geo-default`** — the catalog data itself (~200 KB of country /
  subdivision / currency / language / locale / timezone / geopolitical-entity
  records). Depends on this package.

Domain code that takes a `Country` parameter imports from
`@dcsv-io/d2-geo-abstractions`; only composition-root / catalog-bootstrap code
imports `@dcsv-io/d2-geo-default`. This keeps the ~200 KB catalog out of bundles
that only need the type shapes.

## Record shape architecture

### Single shape per entity

Every reference-data catalog ships ONE record interface. Each record carries
scalars + universal dual-representation for every relationship.

| Catalog                         | Record interface            | Plural data accessor (in `@dcsv-io/d2-geo-default`)    | Lookup table                                                 |
| ------------------------------- | --------------------------- | ---------------------------------------------- | ------------------------------------------------------------ |
| Country                         | `Country`                   | `Countries.US`                                 | `CountryLookup.byCode[CountryCode.US]`                       |
| Subdivision                     | `Subdivision`               | (no plural; use `SubdivisionLookup`)           | `SubdivisionLookup.byCode["US-NY"]`                          |
| Currency                        | `Currency`                  | `Currencies.USD`                               | `CurrencyLookup.byCode[CurrencyCode.USD]`                    |
| Language                        | `Language`                  | `Languages.en`                                 | `LanguageLookup.byCode[LanguageCode.en]`                     |
| Locale                          | `Locale`                    | (no plural; use `LocaleLookup`)                | `LocaleLookup.byTag["en-US"]`                                |
| Timezone                        | `Timezone`                  | (no plural; use `TimezoneLookup`)              | `TimezoneLookup.byCode["America/New_York"]`                  |
| GeopoliticalEntity              | `GeopoliticalEntity`        | `GeopoliticalEntities.EU`                      | `GeopoliticalEntityLookup.byCode[GeopoliticalEntityCode.EU]` |
| CountryCurrencyAcceptance (M:M) | `CountryCurrencyAcceptance` | (denormalized payload on `country.currencies`) | (no lookup — it's a join shape)                              |

### Universal dual representation

Every relationship on every record carries BOTH a typed code field AND a nav
record field:

| Cardinality   | Code rep                                                                                     | Nav rep                                                    |
| ------------- | -------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| **Single FK** | `{relationship?}{StandardName}?: TCode` (`primaryLanguageIso6391Code?: LanguageCode`)        | `{relationship?}?: TRecord` (`primaryLanguage?: Language`) |
| **Set FK**    | `{relationship?}{StandardName}s: ReadonlySet<TCode>` (`Set<TCode>`-backed for O(1) `.has()`) | `{relationship?}s: readonly TRecord[]` (ordered iteration) |

The code rep enables O(1) membership checks
(`country.geopoliticalEntityShortCodes.has(GeopoliticalEntityCode.EU)`); the
nav rep enables ordered iteration and property access
(`for (const member of eu.memberCountries) ...`). Both forms are always
present; neither replaces the other.

Nullable single-primary navs use `?:` (per the workspace `undefined`-over-
`null` convention) — `undefined` for uninhabited territories (AQ / BV / HM)
on `country.primaryLanguage` / `primaryCurrency` / `primaryLocale`.

### PK + FK naming convention

- **Name** describes WHAT the value IS: `iso31661Alpha2Code`,
  `ietfBcp47Tag`, `ianaName`. Never bare `code` on its own.
- **Type** is the typed code wrapper (`CountryCode`, `SubdivisionCode`,
  `LocaleCode`, …).
- **Relationship prefix** on FKs disambiguates direction / cardinality:
  `primary` (primary among possibly many), `sovereign` / `territory`
  (hierarchy direction), `member` (group membership), `spokenIn` /
  `acceptedIn` (reverse "consumed by"), `coApplicable` (parallel beyond a
  primary).

### Code-suffix naming on closed-set enums

Closed-set catalog-identifier enums carry the `Code` suffix to disambiguate
from the record shape:

- **`Code`-suffixed enums**: `CountryCode`, `CurrencyCode`, `LanguageCode`,
  `GeopoliticalEntityCode`.
- **Type-discriminator enums (no `Code` suffix)**: `GeopoliticalEntityType`,
  `WritingDirection`, `DateFormatPattern`, `CurrencyAcceptanceLevel`,
  `MeasurementSystem`, `DayOfWeek`.
- **Open-set branded wrappers (names already disambiguate)**:
  `SubdivisionCode`, `LocaleCode`, `TimezoneCode`.

### Cycle resolution — multi-pass cast pattern

Cyclic record graphs (`country.primaryLanguage → language.spokenInCountries
→ country`) are resolved at codegen time via a multi-pass declare-then-mutate
pattern. Since TS `readonly` is compile-time only (no runtime enforcement),
the data emitter declares records with code-rep fields populated and nav-rep
fields at defaults (`undefined` / `[]`) in the first pass, then mutates nav
refs via a one-time type cast in the wire-nav step:

```ts
// First pass — declare with code-rep populated and nav-rep at defaults.
const us: Country = {
  iso31661Alpha2Code: CountryCode.US,
  displayName: "United States",
  primaryLanguageIso6391Code: LanguageCode.en, // code rep
  territoryIso31661Alpha2Codes: new Set<CountryCode>([
    CountryCode.PR /* ... */,
  ]),
  subdivisionIso31662Codes: new Set<SubdivisionCode>(),
  subdivisions: [],
  primaryLanguage: undefined, // nav rep defaults
  // ... rest of fields ...
};

// Wire-nav step — populate nav refs via cast (one-time mutation).
(us as { -readonly [K in keyof Country]: Country[K] }).primaryLanguage =
  LanguageLookup.byCode[LanguageCode.en];
```

The cast is confined to codegen-emitted module-init code under
`@dcsv-io/d2-geo-default`. Hand-written consumer code MUST treat the record fields
as `readonly` (compile-time enforced). Wiring nav refs outside of
codegen-emitted module init is a hand-written-code-touching-codegen-territory
bug.

## Public surface

The hand-written surface in this package is intentionally tiny — the bulk
of the type catalog materializes under `src/generated/` from the same JSON
specs that drive the .NET source-generator. Hand-written files:

| Export                         | Source file                                   | Purpose                                                                                                                       |
| ------------------------------ | --------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `DeprecationInfo` (interface)  | `src/deprecation-info.ts`                     | Meta-record carried on every catalog record as an optional `deprecation` field. Mirrors .NET `DeprecationInfo` sealed record. |
| `IGeoReference` (interface)    | `src/interfaces/i-geo-reference.ts`           | Strongly-typed lookup contract — `getCountry(code)`, `getSubdivision(code)`, etc. Returns the single record shape.            |
| `IGeoNameResolver` (interface) | `src/name-resolution/i-geo-name-resolver.ts`  | Free-form text → entity resolver contract. Returns full records (not codes) so caller sees resolved record immediately.       |
| `normalize(input)`             | `src/name-resolution/name-normalizer.ts`      | NFD-fold + diacritic-strip + locale-invariant lowercase + whitespace normalize + `&`↔`and` substitution.                      |
| `compare(a, b, maxDistance)`   | `src/name-resolution/levenshtein-comparer.ts` | Classic Wagner-Fischer Levenshtein with early-termination at `maxDistance + 1`.                                               |
| `isWithin(a, b, maxDistance)`  | `src/name-resolution/levenshtein-comparer.ts` | Convenience predicate over `compare`.                                                                                         |

The codegen-emitted spec-derived type catalog (record shapes, branded
typed-code wrappers, Zod schemas, `GEO_CATALOG_VERSION`) lives under
`src/generated/`.

## Codegen pattern

This package follows the standard `tools/ts-codegen` pattern: hand-written
files live directly under `src/`, codegen-emitted files materialize under
`src/generated/` and are tracked in git so PR reviewers see codegen diffs
without a local build. The emitter is `tools/ts-codegen/src/geo-emitter/`
and runs as part of the workspace codegen orchestrator (`pnpm codegen`).

## Parity with .NET

Mirrors `DcsvIo.D2.Geo.Abstractions`:

- `DeprecationInfo` ↔ `DcsvIo.D2.Geo.Abstractions.DeprecationInfo` —
  same four fields, same JSON wire shape. `DateOnly DeprecatedAt`
  serializes to ISO-8601 calendar-date string; the TS-side mirror uses
  `string` carrying the same `YYYY-MM-DD` text.
- Record shapes — every `Country` / `Subdivision` / `Currency` / `Language`
  / `Locale` / `Timezone` / `GeopoliticalEntity` field is byte-for-byte
  parity with the .NET counterpart (modulo TS casing — `iso31661Alpha2Code`
  on TS ↔ `Iso31661Alpha2Code` on .NET).
- `normalize` ↔ `NameNormalizer.Normalize` — same six-step pipeline.
  Cross-language parity is pinned by a byte-equivalent fixture.
- `compare` / `isWithin` ↔ `LevenshteinComparer.Compare` /
  `LevenshteinComparer.IsWithin` — same Wagner-Fischer DP, same
  early-termination sentinel (`maxDistance + 1`).

Optional fields use `undefined` (not `null`) per workspace TS convention.
`null` arriving from the .NET wire normalizes to `undefined` at the Zod
deserialization boundary.

## Dependencies

- `zod` — runtime dep for the codegen-emitted Zod schemas
  (`subdivision-code.g.ts`, `fixed-enums.g.ts`, etc.).

## Telemetry

No telemetry surface — foundation lib emits no spans or metrics. Consumers
instrument the resolver call sites in their own OTel setup.

## Configuration

No configuration — zero-config; the catalog version is baked in at codegen
time via `GEO_CATALOG_VERSION`.

## Known build-time warnings

The TS geo emitter (`tools/ts-codegen/src/geo-emitter/`) emits a small
number of expected `D2GEO010` catalog-uniqueness warnings (legitimate
parent-child name collisions like Burkina Faso Centre / Kadiogo,
real-world ambiguity like Malta's two Rabats). See
[`contracts/geo/KNOWN_WARNINGS.md`](../../../../../contracts/geo/KNOWN_WARNINGS.md)
for the full enumerated list + escalation triggers. New warnings NOT
documented there should be investigated before suppressing.
