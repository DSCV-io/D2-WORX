<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Geo.Default

> Parent: [`public/packages/dotnet/README.md`](../../README.md)
>
> **Audience**: backend .NET service engineers who need the actual geo catalog data — the per-entity instances + lookup tables + nested static-class hierarchies — bound into memory at process start.

Source-generator-emitted in-memory catalog data for the seven geo reference catalogs. The types this assembly references live in [`DcsvIo.D2.Geo.Abstractions`](../abstractions/README.md); this assembly contributes the per-entity static instances + `FrozenDictionary` lookup tables + nested static-class hierarchies + a single `[ModuleInitializer]`-driven coordinator that wires cross-record nav refs after every catalog's static initializers have run.

## What lands here

Every type is codegen-emitted by `DcsvIo.D2.Geo.SourceGen` (`Generated/`) — there is zero hand-written `.cs` in this assembly besides the project file. The dispatcher reads the seven geo spec files under `contracts/geo/` and, when `compilation.AssemblyName == "DcsvIo.D2.Geo.Default"`, emits:

| File                            | Contents                                                                                                                                                                                                                                                                                                                                                                |
| ------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CountryLookup.g.cs`            | `public static class Countries` (per-country accessors → `Country`) + `public static class CountryLookup` with `ByCode: FrozenDictionary<CountryCode, Country>` (O(1)), `ByIso31661Alpha2` / `ByIso31661Alpha3` string indexes, and `All` list. Static constructor materializes every record with code-rep fields populated; `WireNav()` is invoked by the coordinator. |
| `SubdivisionLookup.g.cs`        | `public static class SubdivisionLookup` with `ByCode: FrozenDictionary<SubdivisionCode, Subdivision>` (O(1)) + `ByCountry: FrozenDictionary<CountryCode, IReadOnlyList<Subdivision>>` per-country index + `All` list. `WireNav()` wires `Country` + `ParentSubdivision` nav refs.                                                                                       |
| `CurrencyLookup.g.cs`           | `public static class Currencies` (per-currency accessors → `Currency`) + `public static class CurrencyLookup` with `ByCode` + `ByIso4217Alpha` + `All`. `WireNav()` walks every Country's `Currencies` list and back-fills each Currency's `AcceptedInCountries` + `AcceptedInCountryIso31661Alpha2Codes` reverse navs.                                                 |
| `LanguageLookup.g.cs`           | `public static class Languages` (per-language accessors → `Language`) + `public static class LanguageLookup` with `ByCode` + `ByIso6391` + `All`. `WireNav()` populates `SpokenInCountries` (via `Country.PrimaryLanguage` reverse-walk) + `Locales` (via `Locale.Language` reverse-walk) + paired typed code sets.                                                     |
| `LocaleLookup.g.cs`             | `public static class LocaleLookup` with `ByCode: FrozenDictionary<LocaleCode, Locale>` + `All`. `WireNav()` wires `Language` + `Country` nav refs.                                                                                                                                                                                                                      |
| `TimezoneLookup.g.cs`           | `public static class TimezoneLookup` with `ByCode: FrozenDictionary<TimezoneCode, Timezone>` + `All`. `WireNav()` wires `PrimaryCountry` + `CoApplicableCountries` navs.                                                                                                                                                                                                |
| `GeopoliticalEntityLookup.g.cs` | `public static class GeopoliticalEntities` (per-entity accessors → `GeopoliticalEntity`) + `public static class GeopoliticalEntityLookup` with `ByCode` + `All`. `WireNav()` resolves each entity's `MemberCountryIso31661Alpha2Codes` and populates `MemberCountries`.                                                                                                 |
| `SubdivisionsNested.g.cs`       | `Subdivisions.US.NY` nested static-class hierarchy. Each leaf is a `SubdivisionCode` constant. Codes that start with a digit get a leading underscore (`Subdivisions.AD._02`).                                                                                                                                                                                          |
| `LocalesNested.g.cs`            | `Locales.en.US` nested static-class hierarchy. Each leaf is a `LocaleCode` constant.                                                                                                                                                                                                                                                                                    |
| `TimezonesNested.g.cs`          | `Timezones.America.New_York` nested static-class hierarchy. Each leaf is a `TimezoneCode` constant.                                                                                                                                                                                                                                                                     |
| `GeoDataInitializer.g.cs`       | `internal static class GeoDataInitializer` with `[ModuleInitializer] internal static void Initialize()` — guarded by an `s_initialized` flag for idempotency; runs every per-catalog static ctor via `RuntimeHelpers.RunClassConstructor`, then invokes each `WireNav()` in dependency order.                                                                           |

### Two-pass populate

Records are constructed in two passes. The first pass runs in each catalog's static ctor: every record is materialized with scalars + every code-rep field (typed `*Code` values, hash-backed code sets) populated from spec data, while nav-rep fields (`Country`, `PrimaryLanguage`, `Subdivisions`, etc.) start at `null` or empty. The wire-nav step — `WireNav()` — mutates the nav-rep refs through the `internal set` accessors on the record. The coordinator (`GeoDataInitializer.Initialize`) ensures the first-pass ctors complete for every catalog BEFORE any `WireNav()` runs, and orders the `WireNav()` calls so dependencies are populated before dependants.

```csharp
// Inside CountryLookup.g.cs (illustrative shape)
internal static class CountryLookup
{
    static CountryLookup()
    {
        // First pass: create records with code-rep fields populated;
        // nav-rep fields default to null / empty.
        byCode[CountryCode.US] = new Country
        {
            Iso31661Alpha2Code = CountryCode.US,
            // ... scalars ...
            PrimaryLanguageIso6391Code = LanguageCode.En,    // code rep
            PrimaryCurrencyIso4217AlphaCode = CurrencyCode.USD,
            TerritoryIso31661Alpha2Codes = new HashSet<CountryCode> { /* ... */ }.ToFrozenSet(),
            LocaleIetfBcp47Tags = new HashSet<LocaleCode> { /* ... */ }.ToFrozenSet(),
            Currencies = new CountryCurrencyAcceptance[] { /* ... */ },
            // ...
        };
        // ... all records ...
    }

    internal static void WireNav()
    {
        // Wire-nav: populate nav refs by looking up resolved records via
        // the typed code reps populated in the first pass.
        var rec = ByCode[CountryCode.US];
        rec.PrimaryLanguage = LanguageLookup.ByCode[LanguageCode.En];
        rec.Subdivisions    = SubdivisionLookup.ByCountry[CountryCode.US];
        // ... all nav refs ...
    }
}
```

The `internal set` accessors on nav properties are reachable from this assembly only because `DcsvIo.D2.Geo.Abstractions` declares `[assembly: InternalsVisibleTo("DcsvIo.D2.Geo.Default")]`. See [the Abstractions README record-shape architecture section](../abstractions/README.md) for the full cycle-resolution design.

### Universal dual-representation

Every relationship on every record carries BOTH a typed code field AND a nav record field:

- **Single FK** — `PrimaryLanguageIso6391Code: LanguageCode?` paired with `PrimaryLanguage: Language?`. Code rep populated at first-pass time; nav rep wired in the wire-nav step.
- **Set FK** — `MemberCountryIso31661Alpha2Codes: IReadOnlySet<CountryCode>` (`FrozenSet`-backed; O(1) `Contains`) paired with `MemberCountries: IReadOnlyList<Country>` (ordered iteration). Code rep populated at first-pass time; nav rep wired in the wire-nav step.

The code rep exists for O(1) membership checks (`country.GeopoliticalEntityShortCodes.Contains(GeopoliticalEntityCode.EU)`); the nav rep exists for ordered iteration / property access (`foreach (var member in eu.MemberCountries) { ... }`). Both forms are always present; neither replaces the other.

### Lookup-pattern idioms

- `Countries.US` — per-country accessor returning `Country`, O(1)
- `CountryLookup.ByCode[CountryCode.US]` — explicit typed lookup, O(1)
- `country.Subdivisions` — all subdivisions of a country, iterable list (wire-nav populated)
- `country.GeopoliticalEntityShortCodes.Contains(GeopoliticalEntityCode.EU)` — O(1) typed-code membership check
- `country.PrimaryLanguage?.DisplayName` — nav-ref chain (nullable for uninhabited territories)
- `SubdivisionLookup.ByCode[Subdivisions.US.NY]` — subdivision by typed code, O(1)
- `subdivision.Country?.DisplayName` — wire-nav parent country
- `Timezones.America.New_York` — typed nested-class access; the leaf returns a `TimezoneCode`
- `GeopoliticalEntities.EU.MemberCountries` — wire-nav member list

### Cache-aside reminder for `IGeoNameResolver`

`DefaultGeoNameResolver` builds its normalized-name → record map lazily on first call (cache-aside pattern). First lookup is O(n) over the catalog; subsequent lookups are O(1) against the cached map. The implementation uses thread-safe lazy init (`Lazy<T>` with `LazyThreadSafetyMode.ExecutionAndPublication`) so concurrent first-callers race once on the build factory; only one wins and publishes. The TS `@dcsv-io/d2-geo-default` resolver mirrors this pattern over JS's single-thread execution model.

## Name resolver

`DefaultGeoNameResolver` (`NameResolution/DefaultGeoNameResolver.cs`) implements `IGeoNameResolver` and resolves free-form place-name strings — from WhoIs responses, IP-geolocation enrichment, vendor API replies, user input — to the matching catalog `Country` / `Subdivision` record via a four-pass fail-closed cascade.

### Public API

| Member                                                                                   | Returns                 | Notes                                                                                                                                                                                                                                                                              |
| ---------------------------------------------------------------------------------------- | ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `DefaultGeoNameResolver.TryResolveCountryByName(string name)`                            | `D2Result<Country>`     | Pass-1 exact → Pass-2 startsWith → Pass-3 contains → Pass-4 length-scaled Levenshtein cascade.                                                                                                                                                                                     |
| `DefaultGeoNameResolver.TryResolveSubdivisionByName(string name, Country parentCountry)` | `D2Result<Subdivision>` | Same cascade, scoped to `parentCountry.Subdivisions`. `parentCountry` is required (null returns `ValidationFailed`).                                                                                                                                                               |
| `IRequestContext.Country()` (Default-layer extension)                                    | `Country?`              | Record-returning wrapper over the Abstractions-layer `Country()` accessor. Returns the full catalog record so callers can read nested data (`request.Country()?.PrimaryLanguage?.DisplayName`) without a second lookup. Lives in the `DcsvIo.D2.Geo.Default.Extensions` namespace. |
| `IRequestContext.Subdivision()` (Default-layer extension)                                | `Subdivision?`          | Same shape as `Country()` for the subdivision raw field.                                                                                                                                                                                                                           |
| `services.AddD2GeoDefault()`                                                             | `IServiceCollection`    | Registers `IGeoNameResolver` as `DefaultGeoNameResolver` (singleton). The DI factory does NOT eagerly trigger cache build — the first resolver call performs the O(n) build.                                                                                                       |

### Configuration

The resolver carries no `Options` record. All thresholds are hardcoded constants documented at the call site:

| Constant             | Value             | Rationale                                                                                                                                                   |
| -------------------- | ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MAX_NAME_LENGTH`   | 256               | DoS guard — Pass-4 Levenshtein DP is O(input.Length × key.Length) per ~17,500 catalog keys; an unbounded input would amplify to billions of cells per call. |
| `MIN_LENGTH_PASS_2` | 4                 | Pass-2 startsWith below 4 chars is too promiscuous (matches half the catalog).                                                                              |
| `MIN_LENGTH_PASS_3` | 5                 | Pass-3 contains below 5 chars overmatches likewise.                                                                                                         |
| `MIN_LENGTH_PASS_4` | 5                 | Pass-4 fuzzy on short inputs collapses distinct entities.                                                                                                   |
| `maxDistance`        | `min(2, len / 5)` | Length-scaled Levenshtein bound — short inputs get distance 0 (exact via earlier passes); 10+ chars get distance 2; never larger.                           |

### Telemetry

The resolver intentionally carries no instrumentation (no `ActivitySource`, no `Meter`, no spans, no counters). It runs at every third-party text ingestion point; the hot-path cost-benefit favors zero overhead.

### Edge cases / gotchas

- **Ambiguity sentinel.** When two records normalize to the same name the cache stores a single entry tagged with `IsAmbiguous = true`. A Pass-1 lookup hitting the sentinel returns `D2Result.NotFound(TK.Geo.Errors.NAME_RESOLUTION_AMBIGUOUS)`; Pass-2 / 3 / 4 walks exclude ambiguous entries from the candidate pool. Both record-presence and ambiguity status publish atomically because they share one struct value in the cache map.
- **Cache memory profile.** Country map: ~250 records × ~6 matchable name fields = ~1,500 entries. Per-country subdivision maps: ~3,600 subdivisions total × ~5 matchable name fields = ~18,000 entries spread across the per-country dictionaries. Total bounded around 500 KB worst case.
- **Namespace shadowing.** The Default-layer `IRequestContext.Country()` and the Abstractions-layer `IRequestContext.Country()` share the same method name on the same receiver type. Pick exactly one namespace per call site via `using`: importing both produces CS0121 (ambiguous reference) at compile time. The PATTERNS.md namespace-disambiguated-extension entry documents the idiom.

  Treat the CS0121 as a deliberate design contract, not a bug — the compile-time error forces the call site to declare its intent (code rep via `DcsvIo.D2.Geo.Abstractions.Extensions`, record rep via `DcsvIo.D2.Geo.Default.Extensions`). Consumer guidance: prefer the Default-layer namespace at the call site since most callers want the resolved record (`country.PrimaryLanguage`, `country.Subdivisions`) and the Default-layer wrapper short-circuits to the Abstractions-layer code anyway when the catalog miss is defensive. If both namespaces must be in scope (rare — usually only inside cross-layer plumbing), drop the `using` directives for the colliding members and fully qualify the call (`DcsvIo.D2.Geo.Default.Extensions.IRequestContextGeoExtensions.Country(context)`). The matching Subdivision pair carries the same contract.

- **Pass-3 ambiguity classes.** Inputs like `"Korea"`, `"Congo"`, `"Carolina"` return `NotFound` by design — multiple records contain the substring, and the resolver refuses to guess. Callers needing one specific answer should pass the more precise display name (e.g. `"Republic of Korea"`).
- **PII discipline.** The `name` parameter is treated as opaque PII inside the resolver. The resolver never logs the input, never attaches it to a `D2Result` reason field, and never emits it as a span tag. Upstream callers preserving the raw upstream string for an audit trail own the redaction at their own layer.
- **Resolver is request-context-free.** `D2Result` instances carry `traceId = null`. Callers replay the handler-scoped traceId via `result.WithTraceId(context.TraceId)` at the call site.

### Debugging scenarios

- **`TryResolveCountryByName("Korea")` returns NotFound.** Pass-3 ambiguity blocker — `"korea"` after normalization is a substring of both `"democratic people's republic of korea"` (DPRK) and `"republic of korea"` (ROK). The resolver refuses to silently pick one. Pass either `"Republic of Korea"` or `"North Korea"` for an unambiguous answer.
- **First call is slow; subsequent calls are fast.** Cache-aside: the first lookup builds the O(n) normalized-name → record map; subsequent lookups are O(1) reads against the materialized `FrozenDictionary`. Total wall-clock impact of the first call is around 20–50 ms cold on a developer laptop; subsequent calls are microsecond-scale.
- **`TryResolveSubdivisionByName("Georgia", Countries.CA)` returns NotFound.** Parent-context discipline: Georgia is a US state (US-GA), not a Canadian subdivision. The resolver scopes the search to subdivisions of the supplied parent country and refuses to silently widen scope. Pass `Countries.US` to get US-GA.

## Codegen dispatch

`DcsvIo.D2.Geo.SourceGen` is a multi-target dispatcher — see [`../source-gen/README.md`](../source-gen/README.md) for the full design. When the generator runs against `DcsvIo.D2.Geo.Abstractions` it emits the spec-derived TYPES (record shapes, `*Code` enums, wrapper structs, `JsonConverter`s); when it runs against this assembly it emits the catalog DATA (per-entity static instances + lookups + coordinator). Both assemblies wire the same analyzer + the same seven `AdditionalFiles`.

## Dependencies

- `DcsvIo.D2.Geo.Abstractions` — the types this assembly instantiates plus the `IGeoNameResolver` contract + `NameNormalizer` + `LevenshteinComparer` helpers consumed by the resolver.
- `DcsvIo.D2.Utilities` — `Truthy()` / `Falsey()` extension methods used by both the emitted lookup-construction code AND the resolver input validation.
- `DcsvIo.D2.Result` — `D2Result` envelope returned by every public resolver method.
- `DcsvIo.D2.I18n.Abstractions` — `TKMessage` translation-key constants carried in `D2Result` message fields.
- `DcsvIo.D2.Context.Abstractions` — `IRequestContext` receiver for the Default-layer record-returning extensions.
- `Microsoft.Extensions.DependencyInjection.Abstractions` — `IServiceCollection` receiver for the `AddD2GeoDefault` registration extension.
- `DcsvIo.D2.Geo.SourceGen` — analyzer-only reference (no runtime dll).

Zero NodaTime / Configuration / IO dependencies.

## Bundle size

The emitted catalog data is the bulk of the ~200 KB geo footprint. Domain code that takes a `ProjectReference` here pays for the catalog; consumers that only need the type surface should reference `DcsvIo.D2.Geo.Abstractions` instead.
