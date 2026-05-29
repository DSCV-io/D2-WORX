<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/geo-default

> Parent: [`server/shared/typescript/`](../../README.md)
>
> **Audience**: backend Node/TypeScript engineers + BFF composition-root code that needs to bind the actual geo catalog data (per-entity records + lookup maps + nested objects) at process start.

Codegen-emitted in-memory catalog data for the seven geo reference catalogs. Mirrors [`D2.Shared.Geo.Default`](../../../dotnet/geo/default/README.md) (.NET). The interfaces, branded code types, and record shapes this package consumes live in [`@d2/geo-abstractions`](../abstractions/README.md); this package contributes only the per-entity data + the wire-nav coordinator.

## Per-catalog imports (bundle-friendly)

The `exports` map exposes each catalog as a separate sub-path so bundlers can tree-shake away catalogs the consumer does not import. Example:

```ts
// Import the Country catalog + the coordinator (importing the coordinator
// guarantees the catalogs are fully wired before consumer code reads them).
import { Countries, CountryLookup } from "@d2/geo-default/countries";
import "@d2/geo-default/init";

const usa = Countries.US;
console.log(usa.displayName);

// Iterate via CountryLookup.all (ordered).
for (const c of CountryLookup.all) console.log(c.displayName);

// Nav refs populated via the wire-nav step.
console.log(usa.primaryLanguage?.displayName, usa.subdivisions.length);

// Typed code set for O(1) membership.
if (usa.geopoliticalEntityShortCodes.has(GeopoliticalEntityCode.NATO)) {
  /* ... */
}
```

## Sub-path layout (mirrors the `exports` map)

| Sub-path                                | Shape                                                                                            | Use                                                                                          |
| --------------------------------------- | ------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------- |
| `@d2/geo-default/countries`             | `Countries.US` getter + `CountryLookup.byCode` + `byIso31661Alpha2` + `byIso31661Alpha3` + `all` | Typed access + iteration via `CountryLookup.all`                                             |
| `@d2/geo-default/subdivisions`          | Nested `{ US: { NY: SubdivisionCode } }` + `SubdivisionLookup.byCode` + `byCountry` + `all`      | Strongly-typed reference (`Subdivisions.US.NY`); flat `SubdivisionLookup` for iteration      |
| `@d2/geo-default/currencies`            | `Currencies.USD` getter + `CurrencyLookup.byCode` + `all`                                        | Typed access + iteration                                                                     |
| `@d2/geo-default/languages`             | `Languages.en` getter + `LanguageLookup.byCode` + `all`                                          | Typed access + iteration                                                                     |
| `@d2/geo-default/locales`               | Nested `{ en: { US: LocaleCode } }` + `LocaleLookup.byCode` + `byTag` + `all`                    | Strongly-typed reference (`Locales.en.US`); flat `LocaleLookup` for iteration                |
| `@d2/geo-default/timezones`             | Nested `{ America: { New_York: TimezoneCode } }` + `TimezoneLookup.byCode` + `all`               | Strongly-typed reference (`Timezones.America.New_York`); flat `TimezoneLookup` for iteration |
| `@d2/geo-default/geopolitical-entities` | `GeopoliticalEntities.EU` getter + `GeopoliticalEntityLookup.byCode` + `all`                     | Typed access + iteration                                                                     |
| `@d2/geo-default/init`                  | Top-level `initializeGeoData()` call                                                             | Importing this sub-path is the idempotent wire-nav trigger.                                  |

The record shape mirrors the [`@d2/geo-abstractions` record shapes](../abstractions/README.md): one interface per catalog with universal dual-rep for every relationship (typed code field for O(1) membership + nav record field for ordered iteration). Nullable single-primary navs use `?:` per the workspace `undefined`-over-`null` convention.

### Two-pass populate

Records are constructed in two passes per catalog module. The first pass populates scalars + every code-rep field (typed `*Code` values, `Set<TCode>` for set FKs); nav-rep fields (`country`, `primaryLanguage`, `subdivisions`, etc.) start at `undefined` / `[]`. The wire-nav step then mutates nav-rep refs via a one-time cast.

```ts
// First pass — declare with code-rep populated and nav-rep at defaults.
const us: Country = {
  iso31661Alpha2Code: CountryCode.US,
  displayName: "United States",
  primaryLanguageIso6391Code: LanguageCode.en, // code rep
  primaryCurrencyIso4217AlphaCode: CurrencyCode.USD,
  territoryIso31661Alpha2Codes: new Set<CountryCode>([
    CountryCode.PR /* ... */,
  ]),
  localeIetfBcp47Tags: new Set<LocaleCode>([
    /* ... */
  ]),
  geopoliticalEntityShortCodes: new Set<GeopoliticalEntityCode>([
    GeopoliticalEntityCode.NATO /* ... */,
  ]),
  subdivisionIso31662Codes: new Set<SubdivisionCode>(),
  currencyIso4217AlphaCodes: new Set<CurrencyCode>([CurrencyCode.USD]),
  subdivisions: [],
  primaryLanguage: undefined /* ... nav rep defaults */,
  // ... rest of fields ...
};
// ... all records ...

// Wire-nav step — populate nav refs via one-time cast.
const mut = us as { -readonly [K in keyof Country]: Country[K] };
mut.primaryLanguage = LanguageLookup.byCode[LanguageCode.en];
mut.subdivisions = SubdivisionLookup.byCountry["US"];
// ... all nav refs ...
```

The cast is confined to this package's codegen-emitted module-init code. Consumer code MUST treat record fields as `readonly` (compile-time enforced). See [the `@d2/geo-abstractions` record-shape architecture section](../abstractions/README.md#record-shape-architecture) for the full cycle-resolution design.

### Wire-nav coordinator

`geo-data-initializer.g.ts` carries:

- An idempotent `initializeGeoData()` function guarded by an `_initialized` flag (short-circuits on subsequent calls).
- A top-level call that runs `initializeGeoData()` exactly once when this module is imported. ESM module caching makes repeat imports a no-op.

Importing `@d2/geo-default/init` (or any sub-path that transitively imports it) guarantees every catalog is fully wired before consumer code reads it. The coordinator runs each catalog's first-pass module-init (handled automatically by ESM import-graph evaluation), then invokes the `wireNav` functions in dependency order:

1. `wireSubdivisionNav` (Country → Subdivision.country)
2. `wireCountryNav` (consumes Subdivision / Currency / Locale / Language)
3. `wireLocaleNav` (depends on Country + Language)
4. `wireCurrencyNav` (depends on Country.currencies)
5. `wireLanguageNav` (depends on Country.primaryLanguage + Locale.language)
6. `wireTimezoneNav` (depends on Country)
7. `wireGeopoliticalEntityNav` (depends on Country)

### Lookup-pattern idioms

- `Countries.US` — country by typed code, O(1) (getter on the `Countries` accessor object)
- `country.subdivisions` — all subdivisions of a country, iterable list (wire-nav populated)
- `country.geopoliticalEntityShortCodes.has(GeopoliticalEntityCode.EU)` — O(1) typed-code membership
- `SubdivisionLookup.byCode[Subdivisions.US.NY]` — subdivision by typed code, O(1)
- `subdivision.country?.displayName` — wire-nav parent country
- `Subdivisions.US.NY` — typed nested-object access; leaf returns the `SubdivisionCode` branded string
- `Timezones.America.New_York` — typed nested-object access; leaf returns the `TimezoneCode` branded string
- `GeopoliticalEntities.EU.memberCountries` — wire-nav member country list

### Cache-aside reminder for the TS name resolver

`tryResolveCountryByName(name)` / `tryResolveSubdivisionByName(name, parentCountry)` build their normalized-name → record map lazily on first call (cache-aside pattern). First lookup is O(n) over the catalog; subsequent lookups are O(1) against the cached map. The TS resolver mirrors the .NET `DefaultGeoNameResolver` byte-for-byte over the same `confusables.fixture.json` parity fixture.

## Name resolver

`DefaultGeoNameResolver` (`src/name-resolution/default-geo-name-resolver.ts`) implements `IGeoNameResolver` and resolves free-form place-name strings — from WhoIs responses, IP-geolocation enrichment, vendor API replies, user input — to the matching catalog `Country` / `Subdivision` record via a four-pass fail-closed cascade.

### Public API

| Member                                             | Returns                    | Notes                                                                                                  |
| -------------------------------------------------- | -------------------------- | ------------------------------------------------------------------------------------------------------ |
| `tryResolveCountryByName(name)`                    | `D2Result<Country>`        | Free function — Pass-1 exact → Pass-2 startsWith → Pass-3 contains → Pass-4 length-scaled Levenshtein. |
| `tryResolveSubdivisionByName(name, parentCountry)` | `D2Result<Subdivision>`    | Scoped to `parentCountry`'s subdivisions; `parentCountry` is required.                                 |
| `DefaultGeoNameResolver` class                     | `IGeoNameResolver`         | Class wrapper for DI parity with the .NET side.                                                        |
| `countryFor(context)`                              | `Country \| undefined`     | Default-layer record-returning helper over `IRequestContext.countryCode`.                              |
| `subdivisionFor(context)`                          | `Subdivision \| undefined` | Default-layer record-returning helper over `IRequestContext.subdivisionCode`.                          |

### Configuration

The resolver carries no Options object. All thresholds are hardcoded constants:

| Constant            | Value                              | Rationale                                                 |
| ------------------- | ---------------------------------- | --------------------------------------------------------- |
| `MAX_NAME_LENGTH`   | 256                                | DoS guard — bound Pass-4 Levenshtein DP cost.             |
| `MIN_LENGTH_PASS_2` | 4                                  | Pass-2 startsWith below 4 chars overmatches.              |
| `MIN_LENGTH_PASS_3` | 5                                  | Pass-3 contains below 5 chars overmatches.                |
| `MIN_LENGTH_PASS_4` | 5                                  | Pass-4 fuzzy on short inputs collapses distinct entities. |
| `maxDistance`       | `Math.min(2, Math.floor(len / 5))` | Length-scaled Levenshtein bound.                          |

### Telemetry

The resolver intentionally carries no instrumentation (no spans, no counters, no metrics). It runs at every third-party text ingestion point; the hot-path cost-benefit favors zero overhead.

### Edge cases / gotchas

- **Ambiguity sentinel.** When two records normalize to the same name the cache stores a single entry tagged `{ kind: "ambiguous" }`. Pass-1 hits return `notFound`; Pass-2 / 3 / 4 walks exclude ambiguous entries from the candidate pool.
- **Cache memory profile.** Country map: ~250 records × ~6 matchable name fields. Per-country subdivision maps: ~3,600 subdivisions × ~5 matchable name fields spread across per-country dictionaries.
- **No namespace shadowing.** Unlike .NET, TypeScript has no extension methods, so the Default-layer helpers carry `*For` names (`countryFor`, `subdivisionFor`) instead of reusing the field name. Pick the function directly via import.
- **Pass-3 ambiguity classes.** Inputs like `"Korea"`, `"Congo"`, `"Carolina"` return `notFound` by design — multiple records contain the substring, and the resolver refuses to guess.
- **PII discipline.** The `name` parameter is treated as opaque PII inside the resolver. The resolver never logs the input or attaches it to a `D2Result` message field.
- **TraceId flow.** `D2Result` instances carry `traceId === undefined`. Callers replay the handler-scoped traceId via `result.withTraceId(context.traceId)` at the call site.

### Debugging scenarios

- **`tryResolveCountryByName("Korea")` returns `notFound`.** Pass-3 ambiguity blocker — `"korea"` after normalization is a substring of both DPRK and ROK display names. Pass `"Republic of Korea"` or `"North Korea"` for an unambiguous answer.
- **First call is slow; subsequent calls are fast.** Cache-aside: the first lookup builds the normalized-name → record map; subsequent lookups are O(1) `Map.get` reads.
- **`tryResolveSubdivisionByName("Georgia", Countries.CA)` returns `notFound`.** Parent-context discipline: Georgia is a US state (US-GA), not a Canadian subdivision. Pass `Countries.US` to get US-GA.

## Codegen pattern

Files under `src/generated/` are emitted by `tools/ts-codegen/src/geo-emitter/` from the seven `contracts/geo/*.spec.json` files. Files directly under `src/` are tiny re-export shims that match the sub-path layout above — they exist so the `exports` map can point at concrete files, and so `pnpm exec tsc -b` recognizes every sub-path.

## Dependencies

- `@d2/geo-abstractions` — provides the single record shapes (`Country` / `Subdivision` / …), branded code types (`SubdivisionCode` / `LocaleCode` / `TimezoneCode`), and `Code`-suffixed real enums (`CountryCode` / `CurrencyCode` / `LanguageCode` / `GeopoliticalEntityCode`), plus the `IGeoNameResolver` contract + `normalize` / `compare` helpers consumed by the resolver.
- `@d2/result` — `D2Result` envelope + semantic factories (`ok`, `notFound`, `validationFailed`) returned by every public resolver method.
- `@d2/utilities` — `truthyOrUndefined` boundary helper used in input validation.
- `@d2/request-context-abstractions` — `IRequestContext` receiver for the Default-layer record-returning helpers.
