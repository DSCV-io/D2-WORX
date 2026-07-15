<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Geo.Abstractions

> Parent: [`public/packages/dotnet/`](../../README.md)
>
> **Audience**: backend .NET service engineers who need strongly-typed access to ISO geo reference data (countries, subdivisions, currencies, languages, locales, timezones, geopolitical entities) without pulling in catalog data or temporal libraries.

Hand-written contract surface + source-generator-emitted spec-derived types for the D² geo stack. Domain code anywhere in the backend can take a `ProjectReference` here without paying for the ~200 KB catalog data (lives in `DcsvIo.D2.Geo.Default`) or NodaTime (lives in `DcsvIo.D2.Time`). The hand-written surface stays intentionally tiny — only the helpers and meta-records that the codegen-emitted types reference back into.

## File layout

| File | Purpose |
| ---- | ------- |
| `DcsvIo.D2.Geo.Abstractions.csproj` | Project file. References `DcsvIo.D2.Result` + `DcsvIo.D2.Utilities` + `DcsvIo.D2.Context.Abstractions`; wires `DcsvIo.D2.Geo.SourceGen` as an analyzer (`OutputItemType="Analyzer"` / `ReferenceOutputAssembly="false"`); surfaces the 7 geo spec files via `AdditionalFiles`; emits codegen output under `Generated/`. |
| `DeprecationInfo.cs` | Hand-written meta-record — applies uniformly to every reference-data entity (carried as an optional `Deprecation?` field on every catalog record). 4 fields: `DeprecatedAt` / `Reason` / `SupersededBy` / `SuccessorNote`. |
| `Extensions/IRequestContextGeoExtensions.cs` | Hand-written `extension(IRequestContext)` block — typed geo accessors (`Country()`, `Subdivision()`) over the raw string WhoIs fields the context interface carries for JWT wire fidelity. |
| `IGeoReference.cs` | Hand-written lookup contract — strongly-typed lookup methods returning the single record shape (`Country GetCountry(CountryCode code)`, etc.). |
| `NameResolution/IGeoNameResolver.cs` | Hand-written contract for free-form text → entity resolution. Returns the full record (not the code) so the caller sees the resolved record immediately. |
| `NameResolution/NameNormalizer.cs` | Hand-written pure helper — normalizes free-form place names (NFD decomposition → strip combining marks → ampersand-token substitution → invariant casefold → trim → collapse internal whitespace) into the canonical comparison form the name resolver indexes on. |
| `NameResolution/LevenshteinComparer.cs` | Hand-written pure helper — bounded Levenshtein edit distance with early termination at `maxDistance + 1`. Used by the fuzzy fallback of the name resolver to rank candidates after exact-lookup misses. |
| `Generated/` | Source-generator output directory — populated at compile time by `DcsvIo.D2.Geo.SourceGen`. Tracked in git so PR reviewers see codegen diffs without a local build. |

## Codegen wiring

`DcsvIo.D2.Geo.SourceGen` is the multi-assembly dispatcher that consumes the 7 geo spec files under `contracts/geo/` and emits code into two consumer assemblies:

- **This assembly** (`DcsvIo.D2.Geo.Abstractions`) receives the spec-derived **TYPES**: single record shape per entity (nav refs use `get; internal set;` enabled by `[assembly: InternalsVisibleTo("DcsvIo.D2.Geo.Default")]` for the two-pass populate from the data emitter), `Code`-suffixed real enums (`CountryCode`, `CurrencyCode`, `LanguageCode`, `GeopoliticalEntityCode`), wrapper structs (`SubdivisionCode`, `LocaleCode`, `TimezoneCode`), `JsonConverter`s with closed-set validation tables, and `GeoCatalog` constants (`CatalogVersion`, `CatalogPublishedAt`).
- `DcsvIo.D2.Geo.Default` receives the spec-derived **DATA**: per-entity static instances, nested static-class hierarchies (e.g. `Subdivisions.US.NY`), and the in-memory lookup tables.

The dispatch fires on `compilation.AssemblyName` so the analyzer reaches both consumer assemblies from a single project reference each. See [`../source-gen/README.md`](../source-gen/README.md) for the full multi-assembly dispatch design.

## Record shape architecture

### Single shape per entity

Every reference-data catalog ships ONE record shape. Each record carries scalars + universal dual-representation for every relationship.

| Catalog | Record type | Plural data accessor (in `DcsvIo.D2.Geo.Default`) | Lookup table |
| --- | --- | --- | --- |
| Country | `Country` | `Countries.US` | `CountryLookup.ByCode[CountryCode.US]` |
| Subdivision | `Subdivision` | (no plural; use `SubdivisionLookup`) | `SubdivisionLookup.ByCode[Subdivisions.US.NY]` |
| Currency | `Currency` | `Currencies.USD` | `CurrencyLookup.ByCode[CurrencyCode.USD]` |
| Language | `Language` | `Languages.En` | `LanguageLookup.ByCode[LanguageCode.En]` |
| Locale | `Locale` | (no plural; use `LocaleLookup`) | `LocaleLookup.ByCode[Locales.en.US]` |
| Timezone | `Timezone` | (no plural; use `TimezoneLookup`) | `TimezoneLookup.ByCode[Timezones.America.New_York]` |
| GeopoliticalEntity | `GeopoliticalEntity` | `GeopoliticalEntities.EU` | `GeopoliticalEntityLookup.ByCode[GeopoliticalEntityCode.EU]` |
| CountryCurrencyAcceptance (M:M) | `CountryCurrencyAcceptance` | (denormalized payload on `Country.Currencies`) | (no lookup — it's a join shape) |

### Universal dual representation

Every relationship on every record has BOTH a typed code field AND a nav record field:

| Cardinality | Code rep | Nav rep |
| --- | --- | --- |
| **Single FK** | `{Relationship?}{StandardName}: TCode?` (`PrimaryLanguageIso6391Code`, `SovereignCountryIso31661Alpha2Code`) | `{Relationship?}: TRecord?` (`PrimaryLanguage`, `SovereignCountry`) |
| **Set FK** | `{Relationship?}{StandardName}s: IReadOnlySet<TCode>` (`FrozenSet`-backed for O(1) `Contains`) | `{Relationship?}s: IReadOnlyList<TRecord>` (ordered iteration) |

The code rep enables O(1) membership checks (`country.GeopoliticalEntityShortCodes.Contains(GeopoliticalEntityCode.EU)`); the nav rep enables ordered iteration and property access (`foreach (var member in eu.MemberCountries) ...`). Both forms are always present; neither replaces the other.

### PK + FK naming convention

- **Name** describes WHAT the value IS: `Iso31661Alpha2Code`, `IetfBcp47Tag`, `IanaName`. Never bare `Code` on its own — the field name tells the reader the standard + the cardinality (singular vs plural).
- **Type** is the typed code wrapper (`CountryCode`, `SubdivisionCode`, `LocaleCode`, …).
- **Relationship prefix** on FKs disambiguates direction / cardinality when the receiver-type pairing isn't enough: `Primary` (primary among possibly many), `Sovereign` / `Territory` (hierarchy direction), `Member` (group membership), `SpokenIn` / `AcceptedIn` (reverse "consumed by" relationships), `CoApplicable` (parallel beyond a primary).

A reader landing on `Timezone.CoApplicableCountryIso31661Alpha2Codes: IReadOnlySet<CountryCode>` immediately knows: it's a set of countries (plural), it's the co-applicable ones (not the primary), the value is the ISO 3166-1 alpha-2 form, and the type is the closed-set `CountryCode` enum.

### Code-suffix naming on closed-set enums

Closed-set catalog-identifier enums carry the `Code` suffix to disambiguate from the record shape:

- **`Code`-suffixed enums**: `CountryCode`, `CurrencyCode`, `LanguageCode`, `GeopoliticalEntityCode`.
- **Type-discriminator enums (no `Code` suffix)**: `GeopoliticalEntityType`, `WritingDirection`, `DateFormatPattern`, `CurrencyAcceptanceLevel`, `MeasurementSystem`, `GeoDayOfWeek`.
- **Open-set wrapper structs (names already disambiguate)**: `SubdivisionCode`, `LocaleCode`, `TimezoneCode`.

`IGeoReference` signatures: `Country GetCountry(CountryCode code)`, `Subdivision GetSubdivision(SubdivisionCode code)`, `Currency GetCurrency(CurrencyCode code)`, `Language GetLanguage(LanguageCode code)`, `Locale GetLocale(LocaleCode code)`, `Timezone GetTimezone(TimezoneCode code)`, `GeopoliticalEntity GetGeopoliticalEntity(GeopoliticalEntityCode code)`.

Wrapper-struct derived property: `SubdivisionCode.ParentCountry` → `CountryCode`.

### Cycle resolution — internal set + InternalsVisibleTo

Cyclic record graphs (`Country.PrimaryLanguage → Language → SpokenInCountries → Country`) are resolved via friend-assembly mutation:

- `DcsvIo.D2.Geo.Abstractions.csproj` declares `[assembly: InternalsVisibleTo("DcsvIo.D2.Geo.Default")]`.
- Codegen emits nav properties as `get; internal set;` with sensible defaults (`null` for nullable single primaries; empty `FrozenSet` / `Array.Empty<T>()` for collections).
- Scalar required fields stay as `required init`.

**Caller-readonly guarantee**: from any assembly other than `DcsvIo.D2.Geo.Default`, `Countries.US.PrimaryLanguage = ...` MUST fail at compile time (CS0272). An outside-assembly test pins this guarantee.

**Internal-mutation discipline rule**: within `DcsvIo.D2.Geo.Default`, ONLY codegen-emitted static initializers in `CountryLookup` / `SubdivisionLookup` / `LocaleLookup` / `TimezoneLookup` / `CurrencyLookup` / `LanguageLookup` / `GeopoliticalEntityLookup` may write to nav properties. Hand-written code MUST NOT mutate them — that's a hand-written-code-touching-codegen-territory bug.

## Lookup-pattern idioms (in `DcsvIo.D2.Geo.Default`)

- `Countries.US.DisplayName` — per-country accessor returning the `Country` record, O(1)
- `country.PrimaryLanguage?.Endonym` — single FK nav (nullable for uninhabited territories AQ / BV / HM)
- `country.GeopoliticalEntityShortCodes.Contains(GeopoliticalEntityCode.EU)` — O(1) typed-code set membership
- `foreach (var sub in country.Subdivisions) { ... }` — ordered nav iteration
- `Subdivisions.US.NY` — typed nested-class access returning a `SubdivisionCode`
- `subdivision.Country?.DisplayName` — wire-nav parent country
- `Timezones.America.New_York` — typed nested-class returning a `TimezoneCode`

## What lands here vs. elsewhere

| Concern | Lives in |
| ------- | -------- |
| The lookup interface contracts (`IGeoReference`, `IGeoNameResolver`, `IRequestContextGeoExtensions`) | This assembly |
| Meta-records that apply to every entity (`DeprecationInfo`) | This assembly (hand-written). |
| Name-resolution helpers (`NameNormalizer`, `LevenshteinComparer`) | This assembly (hand-written, pure). |
| Single record shape per entity / `*Code` real enums / wrapper structs / JsonConverters | This assembly's `Generated/` (codegen-emitted). |
| Per-entity static catalog instances + lookup tables | `DcsvIo.D2.Geo.Default`'s `Generated/`. |
| Free `IClock` / `Instant` / `LocalDateTime` etc. | `DcsvIo.D2.Time` — never referenced from here, by design. |

## Dependencies

- `DcsvIo.D2.Result` — `D2Result<T>` return type for the name resolver's `TryResolve…` surface.
- `DcsvIo.D2.Utilities` — `Falsey()` / `Truthy()` boundary guards on the normalizer's input path + emitted code.
- `DcsvIo.D2.Context.Abstractions` — the `IRequestContext` interface the geo extensions hang off.
- `DcsvIo.D2.Geo.SourceGen` — analyzer-only reference (no runtime dll).

Zero NodaTime / catalog-data / DI / Configuration / IO dependencies. This is the domain-safe slice every other geo lib builds on top of.

## Known build-time warnings

The geo source-generator (`DcsvIo.D2.Geo.SourceGen`) emits a small number of expected `D2GEO010` catalog-uniqueness warnings (legitimate parent-child name collisions like Burkina Faso Centre / Kadiogo, real-world ambiguity like Malta's two Rabats). See [`contracts/geo/KNOWN_WARNINGS.md`](../../../../../contracts/geo/KNOWN_WARNINGS.md) for the full enumerated list + escalation triggers. New warnings NOT documented there should be investigated before suppressing.
