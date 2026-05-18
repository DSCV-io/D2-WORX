<!--
Copyright (c) DCSV. All rights reserved.
-->

# PHASE_1.md — Geo libraries (v2 Phase 1)

**Purpose**: the AUTHORITATIVE design + rationale doc for v2 Phase 1. Covers every locked decision, the alternatives considered + rejected, the v1-vs-v2 comparison for each, the catalog coverage targets, the new rules.md predicates that will land at SHIP, and the scope boundary with revisit triggers. Survives context compaction. Stays committed through Phase 1's life; archives to `docs/archive/PHASE_1_GEO_LIBS.md` when Phase 2 (Contacts) begins, per the V2.md §10 lifecycle rule.

**Architectural source of truth**: [V2.md](V2.md) §4 Phase 1 row + §5.4 fingerprint design + §5.8 BFF trust boundary. This doc extends V2.md's Phase 1 row with the deep design content.

**Deliverable workspace**: [`docs/wip/0008-geo-libs/`](../wip/0008-geo-libs/) (gitignored) — execution journals + per-step status. The workspace README intentionally duplicates this doc's decision set for redundancy (user-approved).

---

## Status snapshot

| Stage | Status | Notes |
|---|---|---|
| PLAN | ✅ Complete (this doc) | Deep multi-turn design conversation locked all decisions; documented in full below |
| EXECUTE — Step 0: Branch checkout `n/geo-libs` off `nova` @ `66e41f0a` | ☐ Not started | Orchestrator-spawned Implementer per CLAUDE.md MANDATORY block 0 |
| EXECUTE — Step 1: `D2.Shared.Time` + `@d2/time` | ☐ Not started | NodaTime wrapper, IClock, EF value converters; Temporal API wrapper TS-side |
| EXECUTE — Step 2: `D2.Shared.Geo.Abstractions` + `@d2/geo-abstractions` | ☐ Not started | Types + lookup interfaces + DeprecationInfo (zero deps) |
| EXECUTE — Step 3: Spec files in `contracts/geo/` + .NET SourceGen + TS emitter | ☐ Not started | countries / subdivisions / currencies / languages / locales / timezones spec.json |
| EXECUTE — Step 4: `D2.Shared.Geo.Default` + `@d2/geo-default` | ☐ Not started | Codegen'd lookups + sub-exports for tree-shake |
| EXECUTE — Step 5: `D2.Shared.Location` + `@d2/location` | ☐ Not started | 3 VOs + composition function + `IPostalCodeValidator` |
| EXECUTE — Step 6: Cross-cutting (DROP `Region` field from IRequestContext spec — entire field removed, no rename; PATTERNS.md + new TIMESTAMPS.md; rules.md predicate drafts) | ☐ Not started | Doc updates + spec drop |
| EXECUTE — Step 7: Final-review (K=5 + Aggregator per audit-framework.md §3a-c) + Deliverable Completeness Checklist | ☐ Not started | |
| SHIP | ☐ Not started | Squash merge `n/geo-libs` → `nova`; apply **14 approved rules.md predicates** (§1.22-§1.30 + §1.31 strengthened + §1.32 new + §13.5 + §13.6 new + §7.x); snapshot README to `docs/dev/deliverables/0008-geo-libs.md`; update V2.md §4 Phase 1 row to ✅ |

**Status legend**: ✅ Complete · 🔄 In progress · ☐ Not started · ⏸ Blocked

**Branch**: `n/geo-libs` (TBD — Step 0 will check out from clean `nova` @ `66e41f0a`).

---

## Overview

Phase 1 ships **four .NET shared libraries** + **four TypeScript parity packages**, plus cross-cutting doc + spec changes. The work is one deliverable (`0008-geo-libs`) executed in 8 ordered steps. No new services. No DB migrations (the libs are pure embedded data + types). Estimated effort: ~2-3 weeks per V2.md §4 (slightly expanded from the original "1-2 wks" estimate because the locked PLAN folds D2.Shared.Time forward from "deferred" + ships a temporal model with full v1-vs-v2 reasoning).

**Forward-looking sections included** (per the §13.6 itemization-discipline + §1.31 v1-functional-preservation rules): this doc additionally carries (a) a **Phase 2 (D2.Shared.Contacts) v1 carry-forward inventory** so the Phase 2 PLAN doc inherits an already-audited scope baseline, (b) a **Phase 3 (Edge WhoIs module) v1 carry-forward inventory** for the same reason — *INCLUDING a net-new geo-security capability row* (per-country block-list + per-country `RateLimitTier` override + WhoIs-driven geographic risk scoring) operationalizing user's strategic framing about blocking high-abuse-risk countries, (c) an explicit **"Intentional drops from v1 (with rationale)"** section separating user-approved-and-rationale-documented v1 drops from §1.31-audit-caught silent drops (which this PLAN restores), and (d) a **"Languages deferred from Phase 1 (with revisit triggers)"** section documenting the user's strategic stance on which languages stay out of Phase 1 scope (Russian, mainland Chinese zh-CN, Brazilian Portuguese pt-BR, Hindi/Tamil/Bengali, Arabic, Hebrew, Turkish, SEA langs, Nordic langs) and the revisit triggers per language. See later sections in this doc for each.

**Scope highlights (2026-05-17 expansion)**: this PLAN locks v2 Phase 1 scope per the **data-vs-selectability separation principle** (Decision 6c) — catalog ships full; selectability is a boolean flag. Concretely: **~180 ISO 639-1 language catalog with 11 marked `IsSupported=true`** (en/es/fr/de/it/ja from v1 carry-forward + nl/ko/zh/pt/pl additive expansions; rest catalog-only), **~700 CLDR BCP-47 locale catalog with 18 marked `IsSelectable=true`** (10 v1-style + 8 NEW: en-AU, en-NZ, nl-NL, nl-BE, ko-KR, zh-TW, pt-PT, pl-PL — Commonwealth coverage + restoring Belgium/Netherlands/South Korea/Taiwan/Portugal/Poland selectable variants), **~600 IANA timezone catalog with ~150-200 marked `Selectable=true`**, **~3,600 subdivisions** (full ISO 3166-2 — confirmed user re-affirmation). `Country.PrimaryLocale` is ALWAYS set to the country's true majority primary locale per CLDR; consumers check `country.PrimaryLocale.IsSelectable` for UX availability (no more "primary locale = null" conflation — the catalog tells the truth, the flag governs UI).

### Dependency graph

```
D2.Shared.Time                      (NEW Phase 1 lib — NodaTime wrapper)
  └── depends on: NodaTime, Npgsql.NodaTime

D2.Shared.Geo.Abstractions          (types + lookup interfaces; ZERO deps — no NodaTime, no Geo.Default)
  └── depends on: Result, Utilities (Phase 0)

D2.Shared.Geo.Default               (embedded denormalized in-memory catalogs)
  └── depends on: Geo.Abstractions

D2.Shared.Location                  (3 value objects + composition function)
  └── depends on: Geo.Abstractions ONLY (NOT Default — keeps Location pure-domain)
```

TS parity (mirrors the .NET shape; same JSON specs feed both .NET SourceGen + TS emitter):

```
@d2/time                            (Temporal API wrapper; polyfilled for old browsers)
@d2/geo-abstractions
@d2/geo-default
@d2/location
```

### What we're building + why each lib is its own thing

1. **`D2.Shared.Time`** — NodaTime wrapper + `IClock` + `SystemClock` + `TestClock` + Npgsql.NodaTime EF value-converter config + `ZonedInstant` + `LocalAnchoredEvent` records. Pulled forward from "deferred" because Phases 4-5 (Notifications scheduling, dkron local-anchored jobs) and historical-accuracy use cases (invoicing / audit logs) need NodaTime now, not later. ~1.5MB DLL footprint is the price.
2. **`D2.Shared.Geo.Abstractions`** — pure types + lookup interfaces. Zero external deps (deliberately — no NodaTime drag). Domain layers in any service can reference this freely without pulling the catalog data or temporal lib.
3. **`D2.Shared.Geo.Default`** — embedded denormalized in-memory catalogs (countries, subdivisions, currencies, languages, locales, timezones), codegen'd from JSON specs. Heavy data (~110KB raw / ~30KB gzipped per the catalog-size analysis); separated from Abstractions so consumers that only need types/interfaces don't drag the data.
4. **`D2.Shared.Location`** — three value objects (`Coordinates`, `StreetAddress`, `AdminLocation`) + `ComposeLocationHash` free function + `IPostalCodeValidator` interface + `DefaultPostalCodeValidator`. Depends on Geo.Abstractions ONLY (NOT Default) so the Location lib stays pure-domain.

**Why this shape**: each lib has one clear responsibility. Mirrors Phase 0's Abstractions/.Default convention (`D2.Shared.Caching.Abstractions` + `.Local.Default` + `.Distributed.Redis`). Geo.Abstractions stays pure (just data records + lookup interfaces); Location depends only on Abstractions so it stays pure-domain without dragging in ~110KB of catalog data; Default holds the heavy embedded data; Time is wholly separate concern (temporal math).

### V1 vs v2 (whole-Phase comparison)

**V1** had ONE monolithic `D2.Geo` service running as a separate microservice:
- `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain` — entities + value objects
- `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.App` — handlers
- `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra` — repo + EF Core + seeding migrations
- `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.API` — gRPC + HTTP API surface
- `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Client` — .NET client lib for inter-service calls

Reference data lived in EF Core seed migrations (`Geo.Infra/Repository/Seeding/{Country,Subdivision,Currency,Language,Locale,Timezone}Seeding.cs`); consumers fetched via gRPC. WhoIs lived as a Geo entity. Location was a single atomic root entity in `Geo.Domain/Entities/Location.cs` bundling coordinates + street address + city + postal code + subdivision + country.

**V2** decomposes into shared libs — no Geo service exists. Per V2.md §3 "What's gone vs v1":
> ❌ `d2-geo` (.NET Geo service) — reference data → embedded library, WhoIs → Edge module, Contacts → distributed-per-service library

This Phase ships the "reference data → embedded library" + "Location value objects → shared library" parts. WhoIs absorbs into Edge as an internal module in Phase 3.

### Cross-language parity (V2.md §6 + 0007 wire-parity SHIP pattern)

Every catalog + every record carries identical wire shape across .NET and TS. Same JSON specs feed both .NET SourceGen + TS emitter. Cross-language fixture tests assert .NET ↔ TS produce byte-equal hashes / wall-clocks / instants for the same inputs (extends 0007's wire-parity pattern). No hand-mirrored constants — every cross-language identifier is spec-driven per rules.md §11.30.

---

## Functional preservation requirement (v1 → v2)

**Rule (locked, foundational)**: every v1 functional capability MUST be carried forward to v2 unless explicitly approved by the user as out-of-scope WITH a documented revisit trigger. Net-new v2 capabilities are welcome; they cannot REPLACE v1 capabilities without explicit user sign-off.

**How to apply** (at every PLAN phase, for every v2 lib / module): the FIRST step is to audit v1's equivalent module surface (`old/v1/D2-WORX/`) — enumerate every entity, every value object, every interface method, every seed entry, every navigation property, every constant. The SECOND step is to produce an inventory table mapping each v1 capability to its v2 location (lib + file + section). The THIRD step is to flag any v1 capability that is NOT carried forward — either with a revisit-trigger in scope-out (Decision 16), or with an explicit user-authorized drop.

**Failure mode this prevents**: someone rebuilds a v2 library without auditing v1's capability set; ships a leaner-than-v1 surface; consumer code that worked in v1 breaks silently when migrated. The catch: v1 is the reference implementation — its functional surface is the floor, not the ceiling.

### v1 functional inventory → v2 mapping (Phase 1 scope)

| v1 capability | v1 source (file:line) | v2 destination | v2 status |
|---|---|---|---|
| **Reference data: Country entity** (250 entries) | `Geo.Infra/Repository/Seeding/CountrySeeding.cs:34-44` | `D2.Shared.Geo.Abstractions/Country.cs` + `D2.Shared.Geo.Default/CountryLookup.cs` (codegen from `contracts/geo/countries.spec.json`) | ✅ Carried; v2 adds endonyms + phone metadata + Deprecation + CLDR enrichments |
| **Reference data: Subdivision entity** (183 entries, US/CA full + sparse) | `Geo.Infra/Repository/Seeding/SubdivisionSeeding.cs:36-43` | `D2.Shared.Geo.Abstractions/Subdivision.cs` + `D2.Shared.Geo.Default/SubdivisionLookup.cs` (codegen) | ✅ Carried + EXPANDED (full ISO 3166-2 ~3,600 entries; v1's sparse coverage was a known debt; confirmed full ISO 3166-2 per user re-affirmation 2026-05-17 — massive expansion from v1's 183-sparse stopgap) |
| **Reference data: Currency entity** (5 sample entries) | `Geo.Infra/Repository/Seeding/CurrencySeeding.cs:36-43` | `D2.Shared.Geo.Abstractions/Currency.cs` + `D2.Shared.Geo.Default/CurrencyLookup.cs` | ✅ Carried + EXPANDED (full ISO 4217 active ~180 entries; historical entries deferred per Decision 16) |
| **Reference data: Language entity** (6 entries) | `Geo.Infra/Repository/Seeding/LanguageSeeding.cs:35-40` | `D2.Shared.Geo.Abstractions/Language.cs` + `D2.Shared.Geo.Default/LanguageLookup.cs` | ✅ Carried + MASSIVELY EXPANDED (v1 = 6 → v2 = **~180 (full ISO 639-1 catalog; 11 marked `IsSupported=true`)** per Decision 6c data-vs-selectability principle. MORE preserving than v1 (every ISO 639-1 language now resolvable, not just 6); the 11 supported (en, es, fr, de, it, ja v1 carry-forward + nl, ko, zh, pt, pl NEW additive per user re-affirmation 2026-05-17) have translation files and `IsSupported=true`; the remaining ~169 ship catalog-only as `IsSupported=false`. v2 adds Deprecation + LTR/RTL writing direction + `IsSupported` flag.) |
| **Reference data: Locale entity** (138 entries) | `Geo.Infra/Repository/Seeding/LocaleSeeding.cs:38-45` | `D2.Shared.Geo.Abstractions/Locale.cs` + `D2.Shared.Geo.Default/LocaleLookup.cs` | ✅ Carried + MASSIVELY EXPANDED (138 v1 → **~700 v2, full CLDR BCP-47 locale catalog** per user re-affirmation 2026-05-17; ships every CLDR-defined locale for future selectable expansion without re-codegen; ~75-100KB gzipped trivial cost; **18 selectable** for UI: en-US, en-CA, en-GB, en-AU, en-NZ, es-ES, es-MX, fr-FR, fr-CA, de-DE, it-IT, ja-JP, nl-NL, nl-BE, ko-KR, zh-TW, pt-PT, pl-PL [10 v1-style + 8 NEW]; v2 adds `IsSelectable` flag + Deprecation + decimal/thousands separators + date format pattern + first-day-of-week) |
| **Reference data: Timezone entity** (309 entries) | `Geo.Infra/Repository/Seeding/TimezoneSeeding.cs:36-43` | `D2.Shared.Geo.Abstractions/Timezone.cs` + `D2.Shared.Geo.Default/TimezoneLookup.cs` | ✅ Carried + EXPANDED (~600 full IANA, ~150-200 selectable; v2 adds localized display names + aliases + Selectable + Deprecation) |
| **Reference data: GeopoliticalEntity entity** (59 entries; 23-value type enum) | `Geo.Domain/Entities/GeopoliticalEntity.cs` + `Geo.Domain/Enums/GeopoliticalEntityType.cs` + `Geo.Infra/Repository/Seeding/GeopoliticalEntitySeeding.cs` | `D2.Shared.Geo.Abstractions/GeopoliticalEntity.cs` + `D2.Shared.Geo.Abstractions/GeopoliticalEntityType.cs` + `D2.Shared.Geo.Default/GeopoliticalEntityLookup.cs` (codegen from `contracts/geo/geopolitical-entities.spec.json`) | ✅ Carried (entity + 23-value enum + 59 seeded entries + many-to-many Country↔GE navigation — see Decision 6a) |
| **Country ↔ GeopoliticalEntity many-to-many navigation** (`country_geopolitical_entities` EF join table) | `Geo.Infra/Repository/Seeding/CountryGeopoliticalEntitySeeding.cs` + `Geo.Domain/Entities/Country.cs:205` (`ICollection<GeopoliticalEntity> GeopoliticalEntities`) | Inline denormalized arrays on both sides: `Country.GeopoliticalEntities: IReadOnlyList<GeopoliticalEntity>` (full shape) / `GeopoliticalEntityShortCodes: IReadOnlyList<string>` (lite shape); `GeopoliticalEntity.Countries: IReadOnlyList<Country>` (full) / `CountryISO31661Alpha2Codes: IReadOnlyList<string>` (lite) | ✅ Carried (in-memory denormalized — see Decision 6a + "Denormalized return shapes" section) |
| **Coordinates VO** (lat/lon record) | `Geo.Domain/ValueObjects/Coordinates.cs:24-133` | `D2.Shared.Location/ValueObjects/Coordinates.cs` | ✅ Carried; v2 adds `AccuracyMeters` + `Geohash` + `PlusCode` + per-VO `HashId` |
| **StreetAddress VO** (Line1/2/3 record) | `Geo.Domain/ValueObjects/StreetAddress.cs:22-149` | `D2.Shared.Location/ValueObjects/StreetAddress.cs` | ✅ Carried; v2 adds per-VO `HashId` |
| **Location aggregate** (atomic Location bundling coordinates + street + city + postal + subdivision + country with single `HashId`) | `Geo.Domain/Entities/Location.cs:31-219` | DECOMPOSED into `Coordinates` + `StreetAddress` + `AdminLocation` (new VO) + `ComposeLocationHash` free function in `D2.Shared.Location` | ✅ Carried (composition gives the v1-style atomic identity to consumers who want it — see Decision 4) |
| **AdminLocation concept** (city + postal + subdivision + country slot of v1 Location) | Implicit in `Geo.Domain/Entities/Location.cs` fields | EXPLICIT new VO: `D2.Shared.Location/ValueObjects/AdminLocation.cs` | ✅ Carried + ELEVATED (v1 had it implicit; v2 makes it a first-class VO with its own HashId) |
| **Location hashing** (single atomic `HashId` from joined raw fields) | `Geo.Domain/Entities/Location.cs:155-194` (`Convert.ToHexString(SHA256.HashData(inputBytes))`) | `ComposeLocationHash` free function + per-VO HashIds; ALL prefixed `"v1."` (Decision 5) | ✅ Carried + IMPROVED (v1 had no version prefix → flag-day migration risk; v2's `v1.` prefix enables flag-day-free normalization changes) |
| **Reference data versioning** (v1 had implicit per-seed-migration version) | EF migration timestamps | Catalog version baked into emitted JSON spec + `D2.Shared.Geo.Default.GeoCatalog.CatalogVersion` constant (e.g., `"2026.05.17.1"`) + **`CatalogPublishedAt: Instant`** constant (item 6b — operator-visibility for stale deployments); per-entity `Deprecation` field for individual entry retirement | ✅ Carried + IMPROVED (v1 lost retired entries silently; v2's per-entity Deprecation + `v1.` hash prefix preserve history; CatalogPublishedAt enables admin dashboards to surface "Geo catalog: 2026.05.17.1 (published 2026-05-17)" for spot-checking stale deployments) |
| **Bulk reference-data load** (single in-process snapshot at startup) | `Geo.Infra` EF Core loads all seed data at service start | `D2.Shared.Geo.Default` loads all denormalized lookups at lib import (static readonly fields) | ✅ Carried (in-process direct access; faster than v1's gRPC round-trip per lookup) |
| **Multi-tier caching of reference data** (v1 `Geo.Client` cached gRPC responses in L1+L2) | `Geo.Client` multi-tier cache wrappers | NOT NEEDED (data is the constant in the lib — no cache layer because no remote call); tree-shakeable per-catalog imports replace bandwidth-shaping role of v1's cache | ✅ Carried (different mechanism — in-memory denormalized + tree-shake) |
| **Country → primary currency/language/locale** (v1 had FK columns) | `CountrySeeding.cs:34-44` (`PrimaryCurrencyISO4217AlphaCode`, `PrimaryLocaleIETFBCP47Tag`) | `Country.PrimaryCurrencyISO4217AlphaCode` (lite) + `Country.PrimaryCurrency` (full denormalized embedded) | ✅ Carried + DENORMALIZED (lite shape keeps FK code; full shape embeds resolved object) |
| **Country sovereign relationship** (v1 bare `SovereignISO31661Alpha2Code` for territories) | `Geo.Domain/Entities/Country.cs` + seed | `Country.SovereignCountryISO31661Alpha2Code` (lite — **PRESERVED with intentional clarification rename per ISO-suffix-with-target-type discipline**, item 5a) + `Country.SovereignCountry` (full embedded; nullable) + reciprocal `Country.TerritoryISO31661Alpha2Codes` / `Country.Territories` (inverse-nav restored per item 4a) | ✅ Carried + clarified rename |
| **Country.Currencies M:M** (v1 had M:M with Currency catalog) | `Geo.Domain/Entities/Country.cs` + `CountryCurrencySeeding.cs` | `Country.Currencies: IReadOnlyList<CountryCurrencyAcceptance>` + new `CurrencyAcceptanceLevel` enum (LegalTender / WidelyAccepted / Tourist) | ✅ RESTORED (item 2a + 2b — §1.31 audit caught silent drop) + ENRICHED with acceptance classification |
| **Country.Locales M:M** (v1 had M:M with Locale catalog) | `Geo.Domain/Entities/Country.cs` + `CountryLocaleSeeding.cs` | `Country.LocaleIETFBCP47Tags` (lite) + `Country.Locales` (full embedded) — completeness rule enforced | ✅ RESTORED (item 3a + 3b — §1.31 audit caught silent drop) |
| **Country.Territories inverse nav** (derived from sovereign FK) | derived | `Country.TerritoryISO31661Alpha2Codes` (lite) + `Country.Territories` (full embedded) — derived at codegen time | ✅ RESTORED (item 4a) |
| **Locale → language + country FKs** | `LocaleSeeding.cs:38-45` (`LanguageISO6391Code`, `CountryISO31661Alpha2Code`) | `Locale.LanguageISO6391Code` (lite) + `Locale.Language` (full) + same for Country | ✅ Carried |
| **Timezone → country FK** | `TimezoneSeeding.cs:36-43` (`CountryISO31661Alpha2Code`) | `Timezone.CountryISO31661Alpha2Code` (lite) + `Timezone.Country` (full) + `ITimezoneLookup.AllByCountry()` + `.GetPrimaryForCountry()` | ✅ Carried + EXPANDED (v2 adds primary-for-country smart default) |
| **Subdivision → country FK** | `SubdivisionSeeding.cs:36-43` (`CountryISO31661Alpha2Code`) | `Subdivision.CountryISO31661Alpha2Code` (lite) + `Subdivision.Country` (full embedded) | ✅ Carried |
| **Currency.DecimalPlaces** (v1 had it on Currency for formatting) | `CurrencySeeding.cs:36-43` (`DecimalPlaces`) | `Currency.DecimalPlaces` (unchanged) | ✅ Carried verbatim |
| **Timezone offset snapshots** (STD/DST as string `"+02:00"`) | `TimezoneSeeding.cs:36-43` | `Timezone.CurrentStdOffsetMinutes` (integer minutes — cleaner math) + `CurrentDstOffsetMinutes?` + abbreviations | ✅ Carried + IMPROVED (integer minutes instead of stringly-typed offset) |

**v1 capabilities deferred (with revisit triggers — per user-approved Decision 16)**:

| v1 capability | v2 fate | Revisit trigger |
|---|---|---|
| WhoIs entity (v1 lived as `Geo.Domain/Entities/WhoIs.cs`) | NOT in Phase 1 geo libs — Edge module in Phase 3 (per V2.md §3) | Phase 3 Edge module build |
| Contact entity (v1 was a Geo entity) | NOT in Phase 1 — `D2.Shared.Contacts` distributed-per-service library in Phase 2 (per V2.md §3) | Phase 2 Contacts |
| Geo handlers (FindWhoIs, GetWhoIsByIds, etc. — v1 had `Geo.App/CQRS/`) | NOT in Phase 1 — replaced by in-process lookup methods on the `I*Lookup` interfaces (no more gRPC calls for reference data); WhoIs-specific handlers belong in Edge | Phase 3 Edge (WhoIs); never (lookups replace handlers) |
| `ASType` free-form string classification (v1 stored on WhoIs as `string?`, NOT enum) | NOT in Phase 1 — Phase 3 WhoIs; flagged for revisit whether to formalize as enum | Phase 3 WhoIs design |
| GeopoliticalEntity membership history (join/leave dates per Country-GE pair) | NOT in Phase 1 — v1 also had no temporal tracking; carried forward as static M:M | Audit/analytics use case demands "when did Country X join EU?" |
| GeopoliticalEntity hierarchies (EU within Europe within Continent) | NOT in Phase 1 — v1 had flat M:M; no hierarchy | Use case demands "list all GEs containing Country X by hierarchy" |
| GeopoliticalEntity deprecation tracking (e.g., COMECON dissolved, Warsaw Pact dissolved) | DEFERRED — `DeprecationInfo` pattern from Decision 8 applies; Phase 1 seeds only active GEs | Historical-GE display use case surfaces |

**This inventory will be re-walked at every Phase 1 audit round** (per the new draft predicate §1.31 below). Any v1 capability not in this table = audit-finding.

---

## Phase 1 execution discipline (per-step review + ambiguity-pause)

*Specific to deliverable 0008-geo-libs; NOT general workflow. Reason: foundational work dealing with data structures + data stored and moved over wire. High cost to get wrong; high vigilance required.*

Two rules apply for the entire EXECUTE phase of this deliverable, layered on top of the canonical orchestrator-driven workflow (CLAUDE.md MANDATORY block 0). These rules are NOT general — they are Phase-1-scoped and do NOT promote to `rules.md` automatically. Phase 2 and Phase 3 PLAN docs MAY opt-in (see "Phase 2 + Phase 3 considerations" below) but inheritance is NOT automatic per §13.6 (explicit-per-item consent).

### Rule 1 — Per-step manual review + commit gate

After EACH step completes with a CLEAN audit (zero FINDING rows in the big table per [workflow.md §3](../dev/workflow.md#3-audit-loop-the-core-forcing-function)), the orchestrator MUST:

1. **PAUSE** execution — no auto-progression, no auto-commit.
2. **PRESENT** the step's completed work to the user for manual review:
   - What was implemented (which files, what types/handlers/specs/lookups landed)
   - What tests were added and which pass (count + categories)
   - What docs were updated (PATTERNS.md / TIMESTAMPS.md / per-lib READMEs / etc.)
   - Diff scope (files touched, lines added/removed — high-level summary, not a full diff dump)
   - Any deviation from the step's planned scope (Decision N reference + what changed + why)
3. **AWAIT EXPLICIT USER APPROVAL** before either of:
   - (a) Committing the step's work
   - (b) Proceeding to the next step

The orchestrator MUST NOT:

- Auto-commit on CLEAN audit (CLEAN audit is necessary, not sufficient — user review is the second gate)
- Auto-progress to the next step on CLEAN audit (next step requires its own explicit approval)
- Bundle multiple steps' commits together (each step is its own commit, its own approval)
- Treat prior "go ahead" from an earlier step as approval for any subsequent step (consent is per-step, not blanket)

**User approval = TWO explicit acts per step**: "commit this step" AND "proceed to next step." These MAY be combined in one message (e.g., "commit Step 3 + proceed to Step 4") but BOTH acts must be explicit per step. **Silence ≠ approval. Earlier approval ≠ current approval.** This is consistent with the existing §13.1 (per-commit explicit permission) and §13.6 (explicit-per-item consent) discipline, applied at the per-step gate.

### Rule 2 — Ambiguity pause

If ANY ambiguity surfaces during execution of a step, the orchestrator MUST stop and ask the user before making ANY assumption. Examples of ambiguity that trigger immediate pause:

- A design question the PLAN doesn't explicitly answer
- A field name / type / signature choice not specified in Decision 6 / Decision 6a / Decision 6b / Decision 6c / Decision 7 / etc.
- A scope question (does this fit Phase 1 or is it carry-forward to Phase 2 / Phase 3?)
- A behavior question (what should happen in edge case X — e.g., a country with no IsSupported currency? a locale whose Language.IsSupported is false?)
- A naming convention question (snake_case vs camelCase for JSON field Y? `*Code` vs `*Tag` suffix for a new identifier?)
- A test-coverage scope question (do we test path Z given the case-coverage checklist requires N categories?)
- A spec-file structure question (which fields are required vs optional in `country-currencies-overrides.spec.json` shape?)
- ANY moment where the orchestrator finds itself thinking "I'll just go with X" without explicit PLAN backing

The orchestrator MUST NOT:

- Make "reasonable assumptions" silently — the structural fix for the §13.6 / §1.31 failure modes (case #1-4 in the predicate body) is to NEVER assume silently
- Defer the question to a later review pass ("I'll flag this in the journal and ask after the step lands") — by then the wrong code is written, the wrong test is pinned, the wrong shape is emitted to spec
- Pick whichever option seems lighter-weight without asking ("the simpler choice is X" is a §13.6 violation when the PLAN doesn't specify X)
- Choose convention based on adjacent code if the PLAN is silent on the convention (adjacent code may itself be inconsistent — only the PLAN governs in this deliverable)

**When in doubt: STOP and ASK.** Cost of pausing = user's typing-speed response time (seconds). Cost of guessing wrong = one Implementer round + one Audit round + one Fixer round + the cumulative drift if user doesn't catch it (minutes-to-hours, and compounding across the foundational data structures every subsequent phase depends on).

### Why Phase 1 specifically

This deliverable establishes the foundational data structures (`Country` / `Subdivision` / `Currency` / `Language` / `Locale` / `Timezone` / `GeopoliticalEntity` / `Coordinates` / `StreetAddress` / `AdminLocation`) + the temporal model (`Instant` / `LocalDateTime` / `ZonedDateTime` + `IClock` + `LocalAnchoredEvent`) + the lookup-API surface that EVERY subsequent phase will consume. Phase 2 (Contacts) inherits Location + locale + timezone semantics. Phase 3 (Edge + WhoIs) inherits the WhoIs entity shape + auth context shapes referencing `SubdivisionISO31662Code` / `CountryISO31661Alpha2Code`. Phases 4-7 inherit the catalog + temporal patterns through Notifications / Files / Search / etc. **A wrong decision here propagates everywhere.** The per-step review + ambiguity-pause discipline is what keeps the foundation honest.

### Phase 2 + Phase 3 considerations

Those PLAN docs (when written) MAY opt-in to this discipline as well — both are also foundational-data-structure work (Phase 2 = `Contact` entity + value objects; Phase 3 = Edge auth + WhoIs entity surface, including the net-new per-country block-list + per-country `RateLimitTier` override + WhoIs-driven geographic risk scoring rows). **Recommend each Phase PLAN explicitly call out whether it adopts this discipline. NOT inherited by default** — must be explicitly carried forward per §13.6 (explicit-per-item consent). The discipline is high-vigilance: appropriate for foundational-data-structure phases, possibly overkill for incremental feature work in mature subsystems.

### Layering on standard workflow

This Phase-1-specific discipline is ADDITIVE to the canonical orchestrator-driven workflow (CLAUDE.md MANDATORY block 0 — Planner sub-agent → Implementer → Auditors (K=5) → Aggregator → Fixer per step). The standard workflow already requires explicit user permission for ANY commit ([rules.md §13.1](../dev/rules.md#13-permission--action-discipline)). Phase 1's amendment is the per-step REVIEW PAUSE — the orchestrator does not just request commit permission; the orchestrator presents the step's work for user MANUAL REVIEW first, then awaits the dual "commit + proceed" approval. The Ambiguity Pause rule is also additive — the standard workflow has [§1 "always ask when uncertain"](../../CLAUDE.md#7-behavioral-guidelines-dispositional--how-to-approach-work) (Behavioral Guideline #1) + [§13.4 "never defer without permission"](../dev/rules.md#13-permission--action-discipline); Phase 1's amendment HARDENS this by saying explicitly: any moment of "I'll just go with X" without PLAN backing triggers immediate user-ask, no exceptions for "obvious" cases.

---

## Lib enumeration

| # | Lib (.NET) | TS parity | Wave | Branch | Depends on | Status |
|---|---|---|---|---|---|---|
| 1 | `D2.Shared.Time` | `@d2/time` | A (foundation) | `n/geo-libs` | NodaTime, Npgsql.NodaTime | ☐ |
| 2 | `D2.Shared.Geo.Abstractions` | `@d2/geo-abstractions` | A (foundation) | `n/geo-libs` | Result + Utilities (Phase 0) | ☐ |
| 3 | `D2.Shared.Geo.Default` | `@d2/geo-default` | B (codegen-driven) | `n/geo-libs` | Geo.Abstractions | ☐ |
| 4 | `D2.Shared.Location` | `@d2/location` | C (consumes both) | `n/geo-libs` | Geo.Abstractions ONLY | ☐ |

All four ship together as a single squash-merge commit per the CLAUDE.md MANDATORY block 0 SHIP discipline. Within the deliverable, steps land in dependency order (see Step breakdown below).

---

## Locked decisions

For **every decision below**:
1. **What we decided** (the locked choice)
2. **Why** (rationale, including alternatives considered + rejected)
3. **v1 vs v2** comparison with v1 file + line refs where applicable

### Decision 1 — Lib structure (4 libs, not 1 or 2 or 7)

**What**: 4 libs split as Time + Geo.Abstractions + Geo.Default + Location. Plus 4 TS parity packages.

**Why**: each lib has one clear responsibility. Mirrors Phase 0's Abstractions/.Default convention (`D2.Shared.Caching.Abstractions` + `.Local.Default` + `.Distributed.Redis`).
- Geo.Abstractions stays pure (just data records + lookup interfaces; zero external deps; domain-safe)
- Location depends only on Abstractions so it stays pure-domain WITHOUT dragging in ~110KB of catalog data
- Default holds the heavy embedded data; consumers that only need types/interfaces don't pay for the data
- Time is a wholly separate concern (temporal math) and pulls in NodaTime (~1.5MB DLL footprint) which we don't want creeping into pure domain layers

**Alternatives rejected**:
- **One monolithic `D2.Shared.Geo` lib** (v1 shape minus the service wrapper). Rejected: violates the established Abstractions/.Default split convention; forces every consumer to take the ~110KB catalog data even if they only use lookup interfaces; ties pure domain code to NodaTime.
- **Two libs (Geo + Time)**. Rejected: same problem — Geo conflates pure types with embedded data; Location can't depend on Geo without becoming impure.
- **Seven libs (one per catalog: Countries, Subdivisions, Currencies, Languages, Locales, Timezones, Location)**. Rejected: over-granular; consumer DX collapses (every csproj needing geo lookups would carry 6 ProjectRefs); the catalogs are conceptually one bundle.

**v1 vs v2**:
| | v1 | v2 |
|---|---|---|
| Topology | One D2.Geo service (Domain + App + Infra + API + Client csprojs) | Four shared libs (no service) |
| Consumer access | gRPC call to Geo service via `Geo.Client` | Direct in-process import of `D2.Shared.Geo.Abstractions` / `.Default` / `.Location` |
| Reference-data hosting | EF Core seed migrations in `Geo.Infra/Repository/Seeding/*Seeding.cs` | Codegen'd in-memory catalogs from JSON specs |
| WhoIs | Geo domain entity (`Geo.Domain/Entities/WhoIs.cs`) | Edge internal module (Phase 3) |
| Contacts | Geo entity | Per-consuming-service library (`D2.Shared.Contacts` — Phase 2) |
| Latency | gRPC round-trip per lookup (cached via `Geo.Client` multi-tier cache) | Zero (direct in-memory access) |

---

### Decision 2 — Naming discipline (ISO-suffixed codes; NEVER `Region`)

**What**: every ISO-derived code field carries the standard name as suffix:
- `CountryISO31661Alpha2Code` (e.g., `"US"`)
- `CountryISO31661Alpha3Code` (e.g., `"USA"`)
- `CountryISO31661NumericCode` (e.g., `"840"`)
- `SubdivisionISO31662Code` (e.g., `"US-CA"`)
- `CurrencyISO4217AlphaCode` (e.g., `"USD"`)
- `CurrencyISO4217NumericCode` (e.g., `"840"`)
- `LanguageISO6391Code` (e.g., `"en"`)
- `LocaleIETFBCP47Tag` (e.g., `"en-US"`)
- `TimezoneIANAIdentifier` (e.g., `"America/Edmonton"`)

**NEVER**: `countryCode`, `region`, `currency`, `lang` — these generic names are forbidden.

**NEVER bare `Region`**: the term — as a standalone type or bare property name — clashes too much with UN M49 geopolitical regions, EU/NATO regions, AWS regions, K8s regions, etc. The rule is: **always use the `Subdivision*` prefix for geo-subdivision concepts** — `SubdivisionISO31662Code` (structured ISO 3166-2 FK), `SubdivisionShortCode` (abbreviation, e.g. `"CA"`). The `Subdivision*` prefix is unambiguous; bare `Region` / `Province` / `State` / `Prefecture` / etc. are not — all banned as type or property names. (NOTE: per CHANGE 2, `RequestContext` does NOT carry a free-form `SubdivisionName` — only the structured `SubdivisionISO31662Code`; unresolved upstream names are audit-trail-only on the Phase 3 WhoIs entity, not propagated to the app layer. See Decision 2 Delta 1 + "Intentional drops from v1" section.)

**Why**: the field name carries the standard it conforms to, eliminating "which currency code format is this?" ambiguity for the reader. Codegen + parity tests assume the names match the standard verbatim. v1 already does this — carry forward.

**v1 vs v2**:
- V1 uses the discipline rigorously throughout `Geo.Domain`:
  - `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/Location.cs:100` — `SubdivisionISO31662Code`
  - `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/Location.cs:108` — `CountryISO31661Alpha2Code`
- V2 carries forward verbatim. **Delta 1**: V2's `IRequestContext.spec.json` currently has a bare `Region` field alongside `Subdivision*` fields — Phase 1 **DROPS** that field entirely (no rename). With the enhanced `ISubdivisionLookup.ResolveByName` cascade (per CHANGE 1a — exact + startsWith + contains + Levenshtein ≤ 2; NFD-normalized) + the full ~3,600 ISO 3166-2 catalog, the resolution rate is very high. Unresolvable upstream names are typically (a) second-order subdivisions ISO 3166-2 doesn't cover (UK counties, Japanese wards, US counties), (b) non-administrative geographic regions ("Bay Area", "New England"), or (c) garbage upstream data — none actionable in the application layer. `RequestContext` carries ONLY `SubdivisionISO31662Code` (nullable when WhoIs name unresolvable). The raw IPinfo response — including the unresolved subdivision string — is preserved on the Phase 3 WhoIs entity (audit trail + debugging aid); it does NOT propagate to RequestContext. This SUPERSEDES the earlier rename decision (5a / III); see "Intentional drops from v1 (with rationale)" section for the user-approval reference + revisit trigger. (Cross-cutting change in Step 6.)
- **Delta 2** (item 5a — intentional ISO-suffix-with-target-type rename): V2 renames v1's bare `SovereignISO31661Alpha2Code` to **`SovereignCountryISO31661Alpha2Code`**. User confirmation: "verbose here is fine". The v1 name was ambiguous about what type the FK target was; the rename makes the FK target explicit (Country, not Subdivision or some other ISO 3166-1 holder). Codified by §7.x rename predicate as part of the broader naming-discipline catalog.

---

### Decision 3 — Coverage scope (catalog sizing)

**What** (PHASE 1 catalog targets):

| Catalog | V1 entries | V2 Phase 1 target | Source | Notes |
|---|---|---|---|---|
| Countries | 250 (full ISO 3166-1) | 250 (carry forward) | CLDR | full ISO 3166-1 |
| Subdivisions | 183 (US/Canada full + sparse other countries) | **~3,600 (FULL ISO 3166-2)** | CLDR | v1 was a stopgap; v2 ships complete (confirmed full ISO 3166-2 per user re-affirmation 2026-05-17 — massive expansion from v1's 183-sparse stopgap) |
| Currencies | 5 (USD, CAD, GBP, EUR, JPY — sample only) | **~180 active (full ISO 4217 current) with 11 marked `IsSupported=true`** (USD, CAD, GBP, AUD, NZD, EUR, MXN, JPY, KRW, TWD, PLN) — derived from the 18 supported locales' primary countries. Non-supported currencies are catalog-only (resolvable for historical references; not offered in UI billing/presentation flows). Exclude historical (DEM/ITL/FRF — see Decision 16). | ISO 4217 current list | exclude historical entries (see Decision 16) |
| Languages | 6 (en, es, fr, de, it, ja) | **~180 (full ISO 639-1) with 11 marked `IsSupported=true`** | CLDR + ISO 639-1 | Catalog ships ALL ~180 ISO 639-1 languages per Decision 6c (data-vs-selectability principle). The 11 supported (en, es, fr, de, it, ja, nl, ko, zh, pt, pl) have `IsSupported=true` (translation files exist); rest are catalog-only. Italian RESTORED + 5 NEW supported langs (nl, ko, zh, pt, pl) per user re-affirmation 2026-05-17 — additive expansions on v1's 6 baseline. |
| Locales | 138 (locale combinations) | **~700 (full CLDR BCP-47 locale catalog)** per user re-affirmation 2026-05-17 | CLDR | Ships the FULL CLDR BCP-47 set (~700 entries; ~75-100KB gzipped trivial cost) — enables future selectable expansion without re-codegen. Of these, **18 selectable** for UI dropdowns (`IsSelectable=true`) — see selectable list below; rest cataloged for Country.Locales M:M completeness + BCP-47 fallback resolution + future expansion headroom. (Earlier draft locked "9 selectable" before the locale-catalog discussion expanded scope to full CLDR with 18 selectable; that "9" is obsolete.) |
| Timezones | 309 | **~600 (full IANA) with ~150-200 marked `selectable:true` for UI dropdowns** | IANA tzdb | full IANA; `selectable` flag filters dropdowns |
| GeopoliticalEntities | 59 (continents + subcontinents + regions + economic blocs + political unions + military alliances) | **59 (full v1 carry-forward — verbatim)** | manual (curated from authoritative sources: UN/EU/NATO/ASEAN/G7/G20/BRICS/USMCA/etc.) | Carries forward all 59 v1 entries verbatim per §1.31; **23-value** `GeopoliticalEntityType` enum preserved across **4 categories** — General Geopolitical (3): Continent / SubContinent / GeopoliticalRegion; Economic (8): FreeTradeAgreement / CustomsUnion / CommonMarket / EconomicUnion / MonetaryUnion / BilateralInvestmentTreaty / DevelopmentAgreement / ResourceSharingAgreement; Political (6): PoliticalUnion / HumanRightsAgreement / EnvironmentalAgreement / GovernanceAndCooperationAgreement / PeaceTreaty / DemocracyPromotionAgreement; Military (6): MilitaryAlliance / ArmsControlAgreement / StatusOfForcesAgreement / PeacekeepingAgreement / SecurityCooperationAgreement / NonAggressionPact. Country↔GE M:M restored both sides per Decision 6 (Country.GeopoliticalEntityShortCodes lite + Country.GeopoliticalEntities full + GeopoliticalEntity.CountryISO31661Alpha2Codes lite + GeopoliticalEntity.Countries full). See Decision 6a for full entity shape + worked examples + Deferred items (membership history, hierarchies, deprecation). |

**Selectable locales (18 — `IsSelectable=true`)** — the curated subset exposed by language-picker UIs (covers 11 languages). Catalog still contains ~700 total locales (~682 non-selectable; full CLDR BCP-47 set per user re-affirmation 2026-05-17) that the Country.Locales M:M requires (Decision 6 / Country.Locales) but which users do NOT pick directly. Selectable list:

| BCP-47 tag | Primary language (ISO 639-1) | Country (ISO 3166-1 alpha-2) | Endonym |
|---|---|---|---|
| `en-US` | `en` | `US` | English (United States) |
| `en-CA` | `en` | `CA` | English (Canada) |
| `en-GB` | `en` | `GB` | English (United Kingdom) |
| `en-AU` | `en` | `AU` | English (Australia) |
| `en-NZ` | `en` | `NZ` | English (New Zealand) |
| `es-ES` | `es` | `ES` | Español (España) |
| `es-MX` | `es` | `MX` | Español (México) |
| `fr-FR` | `fr` | `FR` | Français (France) |
| `fr-CA` | `fr` | `CA` | Français (Canada) |
| `de-DE` | `de` | `DE` | Deutsch (Deutschland) |
| `it-IT` | `it` | `IT` | Italiano (Italia) |
| `ja-JP` | `ja` | `JP` | 日本語 (日本) |
| `nl-NL` | `nl` | `NL` | Nederlands (Nederland) |
| `nl-BE` | `nl` | `BE` | Nederlands (België) |
| `ko-KR` | `ko` | `KR` | 한국어 (대한민국) |
| `zh-TW` | `zh` | `TW` | 繁體中文 (台灣) |
| `pt-PT` | `pt` | `PT` | Português (Portugal) |
| `pl-PL` | `pl` | `PL` | Polski (Polska) |

**Selectable-locales delta vs v1**: v1 had 10 selectable (en-US, en-GB, en-CA, es-ES, es-MX, fr-FR, fr-CA, de-DE, it-IT, ja-JP); v2 ADDS 8 new entries spanning 5 new languages and 8 new selectable variants — `en-AU` + `en-NZ` for Commonwealth coverage, then `nl-NL` + `nl-BE` (Dutch — restores Belgium + Netherlands PrimaryLocales), `ko-KR` (Korean — restores South Korea PrimaryLocale), `zh-TW` (Chinese Traditional, Taiwan only — restores Taiwan PrimaryLocale; **zh-CN explicitly skipped per user** — see "Languages deferred from Phase 1" section), `pt-PT` (Portuguese Portugal only — restores Portugal PrimaryLocale; **pt-BR explicitly skipped per user**), `pl-PL` (Polish — restores Poland PrimaryLocale). `it-IT` is RESTORED (the earlier silent drop the §1.31 audit caught — see "Intentional drops from v1 (with rationale)" section + the §1.31 meta-observation). Final count: 18 selectable covering 11 supported languages.

**Selectability is codegen-enforced** — `IsSelectable=true` on a Locale entity MUST correspond to a `contracts/messages/{locale}.json` Paraglide message file existing in the repo at codegen time. Build fails on mismatch. Spec file driving the selectable list: `contracts/geo/selectable-locales.spec.json` (see Decision 17). Codified as draft predicate §1.32 below.

**Why** (per catalog):
- **Countries** — v1 was already complete; no reason to shrink.
- **Subdivisions** — v1's 183 stopped at US states + Canadian provinces + a sparse selection. V2 ships the FULL ISO 3166-2 set (~3,600) because Contacts (Phase 2) + future dispatch use cases need worldwide subdivision data; v1's stopgap was a known debt.
- **Currencies** — v1's 5 was demo data. V2 ships full ISO 4217 active (~180); historical entries (DEM Deutsche Mark, ITL Italian Lira, FRF French Franc, etc.) are deferred (see Decision 16 — they belong behind the Deprecation pattern when a use case for historical lookup surfaces).
- **Languages** — v1 had 6 (en, es, fr, de, it, ja). V2 ships the **full ~180 ISO 639-1 catalog** per Decision 6c (data-vs-selectability principle); **11 are marked `IsSupported=true`** (translation files exist). The 11 supported = v1's 6 (Italian RESTORED — earlier PLAN draft dropped Italian; the §1.31 audit caught it along with three other silent drops — see "Intentional drops from v1 (with rationale)" + the §1.31 meta-observation) + **5 NEW additive (nl, ko, zh, pt, pl)** per user re-affirmation 2026-05-17. Each new supported language restores a country whose UI we can now actually serve in its primary locale (per the IsSelectable flag): Dutch → Belgium + Netherlands; Korean → South Korea; Chinese (Traditional only) → Taiwan; Portuguese (Portugal only) → Portugal; Polish → Poland. Languages NOT supported (Russian, mainland Chinese zh-CN, Brazilian Portuguese pt-BR, Hindi/Tamil/Bengali, Arabic, Hebrew, Turkish, SEA langs, Nordic langs) STILL ship in the ~180 catalog (resolvable, displayable as Country.PrimaryLanguage etc.) — they're just `IsSupported=false`. See "Languages deferred from Phase 1" section below for the strategic supported-set rationale.
- **Locales** — v1 had 138 (every reasonable language-country combination). V2 ships the **FULL CLDR BCP-47 locale catalog (~700 entries)** per user re-affirmation 2026-05-17 — trivial cost (~75-100KB gzipped) and enables future selectable expansion without re-codegen. Of these ~700, **18 are marked `IsSelectable=true`** for UI dropdowns (the curated set covering 11 languages — see selectable list above); the rest are non-selectable variants like `fr-CI` (Côte d'Ivoire), `de-CH` (Switzerland-German), `it-CH` (Switzerland-Italian), `en-IN` (India-English) that Country.Locales M:M completeness requires + BCP-47 fallback resolution walks over (per Decision 7 / Locale.ResolveSelectable). Adding more selectable later = JSON edit to `contracts/geo/selectable-locales.spec.json` + matching Paraglide messages file + codegen run; the underlying Locale entry already exists in the ~700 catalog.
- **Timezones** — v1 had 309. V2 ships ~600 (full IANA tzdb) because we need NodaTime + IANA-id-canonical anyway (Decision 12); curating a 309-subset is more maintenance than carrying the full set + adding a `selectable:true` flag on ~150-200 of them for UI dropdowns. Niche zones (`Antarctica/Vostok`, `Pacific/Chatham`) become resolvable but don't pollute the dropdown.
- **GeopoliticalEntities** — v1 had 59 entries (`Geo.Infra/Repository/Seeding/GeopoliticalEntitySeeding.cs`) covering continents (7), subcontinents (7), geopolitical regions (9), free trade agreements (4), customs unions (2), common markets (2), economic unions (1), monetary unions (4), political unions (1=EU), governance/cooperation orgs (14 — AU/ArabLeague/ASEAN/BRICS/CARICOM/CoE/Commonwealth/G7/G20/GCC/NordicCouncil/OECD/OIF/OPEC/SAARC/UN), military alliances (6 — ANZUS/AUKUS/CSTO/FVEY/NATO/QUAD). V2 carries forward all 59 verbatim per §1.31 (functional preservation). The **23-value** `GeopoliticalEntityType` enum across **4 region-tagged categories** (General Geopolitical 3 + Economic 8 + Political 6 + Military 6) preserved verbatim — see Decision 6a for the full enum block. Country↔GE many-to-many restored both sides per Decision 6 (was a single Country navigation in v1; v2 has denormalized lite + full views on both sides per the broader denormalized-lookup-graph design). Deferred items (per Decision 6a): membership history (join/leave dates per Country-GE pair — v1 also had no temporal tracking), GE hierarchies (EU within Europe within Continent — v1 had flat M:M), GE deprecation tracking (e.g., COMECON / Warsaw Pact dissolved — defer to Deprecation pattern when historical-GE lookup use case surfaces).

**v1 vs v2 (per catalog)**:
- Countries: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/CountrySeeding.cs` (250 entries, hand-curated). V2 codegen'd from CLDR.
- Subdivisions: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/SubdivisionSeeding.cs` (US + CA full; other countries sparse). V2 codegen'd from CLDR (full).
- Currencies: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/CurrencySeeding.cs` lines 31-90 — 5 sample entries (USD/CAD/GBP/EUR/JPY). V2 codegen'd from ISO 4217 (~180 active).
- Languages: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/LanguageSeeding.cs` lines 31-81 — 6 entries. V2 = **~180 (full ISO 639-1 catalog) with 11 marked `IsSupported=true`** per user re-affirmation 2026-05-17 + Decision 6c data-vs-selectability principle. The 11 supported = en, es, fr, de, it, ja [v1 carry-forward — Italian RESTORED per §1.31 meta-observation] + nl, ko, zh, pt, pl [NEW additive]; rest of the ~180 ship as catalog-only with `IsSupported=false`.
- Locales: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/LocaleSeeding.cs` — 138 entries. V2 = **~700 (full CLDR BCP-47 locale catalog)** per user re-affirmation 2026-05-17; 18 marked `IsSelectable=true` for UI dropdowns.
- Timezones: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/TimezoneSeeding.cs` — 309 entries (IANA id + display name + offset snapshot + country FK). V2 = full IANA tzdb (~600) with `selectable` flag on a curated subset.
- GeopoliticalEntities: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/GeopoliticalEntitySeeding.cs` — 59 entries (continents/subcontinents/regions/economic/political/military). V2 = **59 (full v1 carry-forward — verbatim)**; spec-driven codegen replaces v1's EF seed.

Source: CLDR + ISO 639-1 (full ~180 language catalog) + libphonenumber metadata + IANA tzdb + manually-curated GeopoliticalEntity catalog (UN/EU/NATO/ASEAN/G7/G20/BRICS/USMCA/etc.) at build time. Build script pulls latest, regenerates JSON specs, codegen produces `.cs` + `.ts` catalogs.

---

### Decision 4 — Value objects (Location lib)

**What**: three value objects + one free composition function, NO Location aggregate.

#### Coordinates (record)
- `Latitude` (double, required, -90..+90, quantized to 5 decimals ≈ 1.1m precision)
- `Longitude` (double, required, -180..+180, quantized to 5 decimals)
- `AccuracyMeters` (double?, optional, **does NOT participate in hash** — accuracy is metadata, not identity)
- `Geohash` (string, lazy-computed — Niemeyer base32 standard, default 11 chars ≈ 1.1m, Redis-compatible)
- `PlusCode` (string, lazy-computed — Open Location Code, free/open Google standard, ~14m at 10 chars)
- Coordinate reference system: **WGS 84 / EPSG:4326** (documented in xmldoc; matches GPS, browser geolocation, PostGIS `geography(Point, 4326)`)
- Own `HashId = "v1." + sha256(geohash)` — uses geohash as the canonical content-addressable representation (quantization + format-fixed = deterministic across .NET ↔ TS)

#### StreetAddress (record)
- `Line1` (required, non-empty after cleaning)
- `Line2?` (optional)
- `Line3?` (optional; requires `Line2` to be non-empty — Line3 without Line2 = ValidationFailed)
- Own `HashId = "v1." + sha256(line1|line2|line3)` (cleaned, lowercased, pipe-joined, empty slots as `""`)

#### AdminLocation (record — NEW in v2, was implicit in v1)
- `City?` (cleaned)
- `PostalCode?` (uppercased for hashing — postal codes are conventionally uppercase)
- `SubdivisionISO31662Code?` (uppercased)
- `CountryISO31661Alpha2Code?` (uppercased)
- Own `HashId = "v1." + sha256(city|postal|sub|country)`
- This is what `IRequestContext.AdminLocationHashId` already references

#### Free function (in Location lib)
```csharp
public static string ComposeLocationHash(
    Coordinates? coordinates,
    StreetAddress? address,
    AdminLocation? admin)
{
    // Returns "v1." + sha256(geohash | adminHash | streetHash)
    // Missing slots hash as "" — deterministic, matches v1's pattern
}
```

**Why no Location aggregate type**: too rigid. V1 had a single atomic `Location` record bundling coordinates + street + city + postal + subdivision + country into one identity (`Location.HashId`). V2 prefers explicit composition because:
- Contacts (Phase 2) needs StreetAddress + AdminLocation but rarely Coordinates
- WhoIs (Phase 3, in Edge) needs Coordinates + AdminLocation but never StreetAddress
- Future dispatch may need Coordinates only
- A static aggregate forces every consumer to carry slots they don't use → wastes memory + clouds the type

Consumers compose what they need from the three VOs. The `ComposeLocationHash` free function gives the v1-style atomic identity to consumers who genuinely want it (e.g., dedup of "this exact location was seen before").

**v1 vs v2 (hash hierarchy)**:
- V1: ONE atomic `Location.HashId` computed from all raw fields joined together at `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/Location.cs:155-194`. Hash inputs = `[coordsParts, addressParts, cityClean, postalCodeClean, subdivisionCodeClean, countryCodeClean]`. No `v1.` prefix.
- V2: each VO computes its own hash; aggregate composes from sub-hashes; all hashes carry `v1.` prefix (see Decision 5).

**v1 source refs**:
- `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/Location.cs` (lines 31-219) — the v1 atomic Location record we're decomposing
- `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/ValueObjects/Coordinates.cs` (lines 24-133) — v1 Coordinates (no `AccuracyMeters`, no `Geohash`, no `PlusCode`, no `HashId`)
- `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/ValueObjects/StreetAddress.cs` (lines 22-149) — v1 StreetAddress (no `HashId`)

**v1 deltas v2 adds**:
- `Coordinates.AccuracyMeters` (optional metadata; NOT in hash)
- `Coordinates.Geohash` / `Coordinates.PlusCode` (lazy-computed derived properties)
- `Coordinates.HashId` (NEW — per-VO hash)
- `StreetAddress.HashId` (NEW — per-VO hash)
- `AdminLocation` (NEW VO — was implicit in v1 Location as the city/postal/subdivision/country fields)
- All hashes prefixed with `v1.` per Decision 5

---

### Decision 5 — Hash discipline (versioned + composition slot rule + normalization)

**What**: ALL content-addressable hashes carry `v1.` prefix (e.g., `"v1.A1B2C3..."`).

**Why**: allows future normalization changes without flag-day migrations. If we change postal-code normalization in v2 (e.g., add spacing rules for UK postcodes), old `v1.{hash}` records remain resolvable; new writes use `v2.{hash}`. The lookup layer reads both versions; the write path uses the current version. Equivalent to schema versioning for content-addressable IDs.

**Slot rule for composition**: missing components hash as `""` placeholder — deterministic; matches v1's `CleanStr() → null → ""` pattern. Sequence of inputs into the hash function is fixed (Coordinates first, AdminLocation second, StreetAddress third when composing); empty slots don't change position.

**Normalization (carried forward from v1)**:
- City: `CleanStr()` (trim + collapse whitespace), then lowercased in the array join
- PostalCode: `CleanStr()?.ToUpperInvariant()` (postal codes conventionally uppercase)
- ISO codes: `CleanStr()?.ToUpperInvariant()`
- Coordinates: format `"F5"` (5 decimals, invariant culture) — `lat.ToString("F5", InvariantCulture)`
- String array → `string.Join("|", parts.Select(x => CleanStr(x)?.ToLowerInvariant() ?? string.Empty))`

**v1 vs v2**:
- V1: hashes are bare hex (no version prefix). See `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/Location.cs:182` — `var hashId = Convert.ToHexString(SHA256.HashData(inputBytes));`. Future normalization change = mass cache invalidation flag-day.
- V2: `var hashId = "v1." + Convert.ToHexString(SHA256.HashData(inputBytes));`. Future normalization change = bump to `"v2."` and run both lookups during migration window.

---

### Decision 6 — Reference data entities (Geo.Abstractions)

Each entity below ships in `D2.Shared.Geo.Abstractions` as a sealed record. **Records, not classes** — they're immutable wire-shape data. Codegen emits the records + the lookups in `D2.Shared.Geo.Default`.

#### Country
- `ISO31661Alpha2Code` (PK; e.g., `"US"`)
- `ISO31661Alpha3Code` (e.g., `"USA"`)
- `ISO31661NumericCode` (e.g., `"840"`)
- `DisplayName` (English short name; e.g., `"United States"`)
- `OfficialName` (English official long name; e.g., `"United States of America"`)
- **NEW in v2**: `EndonymDisplayName` (native-language short; e.g., for `JP` = `"日本"`)
- **NEW in v2**: `EndonymOfficialName` (native-language official; e.g., for `JP` = `"日本国"`)
- `PhoneNumberPrefix` (e.g., `"1"`, `"44"`, `"81"`)
- **NEW in v2**: `PhoneNumberNationalFormat` (display template; e.g., for US = `"(NNN) NNN-NNNN"`)
- **NEW in v2**: `PhoneNumberMinDigits` (e.g., 10 for US)
- **NEW in v2**: `PhoneNumberMaxDigits` (e.g., 10 for US, 12 for some EU)
- `PrimaryLanguageISO6391Code` (single — **majority-population-preference** per user; NOT legal/political; e.g., Canada = `"en"`, Belgium = `"nl"`). **STAYS NON-NULLABLE** — always set to the country's full-ISO-639-1 majority primary language regardless of whether the language is in our supported set; used for endonym match logic, not UI-serving.
- `PrimaryCurrencyISO4217AlphaCode` (e.g., `"USD"`)
- **`PrimaryLocaleIETFBCP47Tag`** (e.g., `"en-US"`) — **NON-NULLABLE, required**. Every country ALWAYS has its true majority-population primary locale set per CLDR data; the locale ALWAYS exists in our ~700-entry catalog. Whether to actually serve UI in that locale is governed by `Country.PrimaryLocale.IsSelectable` — see the PrimaryLocale + IsSelectable principle box below.
- **RESTORED from v1** (was silently dropped in earlier PLAN draft — see §1.31 meta-observation): `SovereignCountryISO31661Alpha2Code?` — for territories like Puerto Rico → `"US"`; Åland Islands → `"FI"`. **Intentional rename from v1's bare `SovereignISO31661Alpha2Code`** (verbose-here-is-fine ISO-suffix-with-target-type discipline per Decision 2); the v1 name was ambiguous about FK target type.
- **RESTORED from v1**: `Currencies: IReadOnlyList<CountryCurrencyAcceptance>` (M:M with acceptance classification — see definition below). All currencies accepted/used in this country (legal tender + de-facto). Includes PrimaryCurrency; ordered with PrimaryCurrency first, then by descending acceptance. **De-facto currencies are more important than politically-correct ones** — see PATTERNS.md "Reference Data Philosophy" subsection (added Step 6).
- **RESTORED from v1**: `Locales: IReadOnlyList<Locale>` (full view) + `LocaleIETFBCP47Tags: IReadOnlyList<string>` (lite view). All locales spoken/used in this country (selectable + non-selectable variants). Includes PrimaryLocale. Ordered with PrimaryLocale first, then by descending speaker population. **Completeness rule** — see below.
- **RESTORED from v1**: `TerritoryISO31661Alpha2Codes: IReadOnlyList<string>` (lite) + `Territories: IReadOnlyList<Country>?` (full). Inverse-nav of SovereignCountryISO31661Alpha2Code — countries for which THIS country is sovereign. Derived at codegen time from existing sovereign FK data — no new data source. Empty list for most countries.
- `Deprecation?` (`DeprecationInfo` — see Decision 8)

##### PrimaryLocale + IsSelectable principle (locked 2026-05-17)

> **PrimaryLocale + IsSelectable principle** (locked 2026-05-17):
> Every country has its true majority-population primary locale set per CLDR data — `Country.PrimaryLocale` and `Country.PrimaryLocaleIETFBCP47Tag` are ALWAYS non-null. The locale ALWAYS exists in our ~700-entry catalog.
>
> Consumers check `country.PrimaryLocale.IsSelectable` to decide whether to actually serve UI in that locale:
> - `IsSelectable=true` (18 locales) → serve UI in `country.PrimaryLocale.IETFBCP47Tag`
> - `IsSelectable=false` (~682 locales) → fall through to user session preference → en-US default
>
> Don't substitute a "closest" country-local variant. The catalog tells the truth (Brazil's primary IS pt-BR even though we don't ship Portuguese-Brazilian translations); the IsSelectable flag governs UX availability.
>
> **Distinguished from `Country.PrimaryLanguageISO6391Code`**: that field is also always non-null (set to the country's full-ISO-639-1 majority-population primary language) — used for endonym match logic.

**Worked examples** — every country has `PrimaryLocale` ALWAYS set per CLDR; `IsSelectable` flag governs UX behavior:

| Country | True primary locale (PrimaryLocale) | IsSelectable | UX behavior |
|---|---|---|---|
| US | en-US | ✅ true | Serve en-US |
| UK | en-GB | ✅ true | Serve en-GB |
| Canada | en-CA (per majority rule) | ✅ true | Serve en-CA |
| Australia | en-AU | ✅ true | Serve en-AU |
| New Zealand | en-NZ | ✅ true | Serve en-NZ |
| France | fr-FR | ✅ true | Serve fr-FR |
| Spain | es-ES | ✅ true | Serve es-ES |
| Mexico | es-MX | ✅ true | Serve es-MX |
| Germany | de-DE | ✅ true | Serve de-DE |
| Italy | it-IT | ✅ true | Serve it-IT |
| Japan | ja-JP | ✅ true | Serve ja-JP |
| Netherlands | nl-NL | ✅ true | Serve nl-NL |
| Belgium | nl-BE | ✅ true | Serve nl-BE |
| South Korea | ko-KR | ✅ true | Serve ko-KR |
| Taiwan | zh-TW | ✅ true | Serve zh-TW |
| Portugal | pt-PT | ✅ true | Serve pt-PT |
| Poland | pl-PL | ✅ true | Serve pl-PL |
| Switzerland | de-CH (German majority ~63%) | ❌ false | de-CH catalog, no UI; fall through |
| Austria | de-AT | ❌ false | de-AT catalog, no UI; fall through |
| Argentina | es-AR | ❌ false | es-AR catalog, no UI; fall through |
| Côte d'Ivoire | fr-CI | ❌ false | fr-CI catalog, no UI; fall through |
| Brazil | pt-BR | ❌ false | pt-BR catalog, no UI; fall through |
| China mainland | zh-CN | ❌ false | zh-CN catalog, no UI; fall through |
| Hong Kong | zh-HK | ❌ false | zh-HK catalog, no UI; fall through |
| Russia | ru-RU | ❌ false | ru-RU catalog, no UI; fall through |
| India | hi-IN | ❌ false | hi-IN catalog, no UI; fall through |
| Saudi Arabia | ar-SA | ❌ false | ar-SA catalog, no UI; fall through |
| Israel | he-IL | ❌ false | he-IL catalog, no UI; fall through |
| Vietnam | vi-VN | ❌ false | vi-VN catalog, no UI; fall through |

**Belgium** — `PrimaryLocale = nl-BE (IsSelectable=true)`; was always set per data-vs-selectability principle (Decision 6c). Prior PLAN drafts that flagged Belgium for "explicit handling" predate the data-vs-selectability separation; with the principle now locked, Belgium's primary locale is simply nl-BE regardless of how Dutch ended up in the supported set.

**Consumer pattern when `!country.PrimaryLocale.IsSelectable`**:
1. Fall through to user's session preference (cookie / user profile)
2. Fall through to system default (`en-US`)
3. Do NOT substitute a "closest" country-local variant (the catalog tells the truth; selectability governs UX)

**§1.31 status**: NOT a violation. V1's `Country.PrimaryLocaleIETFBCP47Tag` was nullable in practice (Afghanistan had it null), but the v1→v2 design upgrades the modeling to "catalog ships full; selectability is a boolean flag" — every country gets its real primary locale set, and selectability is a separate concern. This is MORE preserving than v1.

**`PrimaryLanguageISO6391Code` is also NON-nullable** — true linguistic fact, set regardless of whether language is supported. Belgium's `PrimaryLanguageISO6391Code = "nl"` is set (true linguistic fact); endonym match logic just doesn't fire for un-supported langs. Both fields are always-set facts; UX-availability decisions are downstream via the `IsSelectable` / `IsSupported` flags.

##### Country.Currencies — M:M with acceptance level (item 2a + 2b)

```csharp
public sealed record CountryCurrencyAcceptance
{
    /// <summary>FK to Currency.ISO4217AlphaCode.</summary>
    public required string ISO4217AlphaCode { get; init; }

    /// <summary>Acceptance classification — legal tender vs widely accepted vs tourist.</summary>
    public required CurrencyAcceptanceLevel Level { get; init; }

    /// <summary>Denormalized embedded Currency record. Non-nullable in .NET (always populated from in-memory catalog).
    /// TS Lite-vs-Full split lives in CountryCurrencyAcceptanceLite (no currency field) + CountryCurrencyAcceptanceFull (has currency).</summary>
    public required Currency Currency { get; init; }
}

public enum CurrencyAcceptanceLevel
{
    /// <summary>Official sovereign currency. Most countries have exactly one (e.g., US → USD only; FR → EUR only).
    /// Some have more than one (e.g., SV → USD and SVC — though SVC is largely phased out;
    /// HK officially uses HKD but CNY is also legal in some contexts).</summary>
    LegalTender,

    /// <summary>De-facto commercial acceptance — not legally required but ubiquitous in trade.
    /// E.g., USD in Argentina (real-estate / large-value commerce), USD in Lebanon, EUR in Switzerland border regions, USD in Cambodia.</summary>
    WidelyAccepted,

    /// <summary>Narrow tourist-zone or specialty acceptance — present in tourism economies but not broadly usable.</summary>
    Tourist
}
```

**Worked examples** (de-facto over politically-correct):
- US: `Currencies = [{USD, LegalTender}]`
- AR (Argentina): `Currencies = [{ARS, LegalTender}, {USD, WidelyAccepted}]` — USD is real-estate / large-value standard
- SV (El Salvador): `Currencies = [{USD, LegalTender}, {SVC, LegalTender}]` — bimonetary system
- CH (Switzerland): `Currencies = [{CHF, LegalTender}, {EUR, WidelyAccepted}]` — EUR widely accepted in border / tourist zones
- LB (Lebanon): `Currencies = [{LBP, LegalTender}, {USD, WidelyAccepted}]` — USD-dual due to LBP volatility
- ZW (Zimbabwe): `Currencies = [{ZWL, LegalTender}, {USD, WidelyAccepted}]` — USD widely accepted; ZWL volatile
- HK (Hong Kong): `Currencies = [{HKD, LegalTender}, {CNY, WidelyAccepted}]` — CNY widely accepted in commerce alongside HKD
- KH (Cambodia): `Currencies = [{KHR, LegalTender}, {USD, WidelyAccepted}]` — USD widely accepted

**Data sources**: CLDR + ISO 4217 (LegalTender) + manual `contracts/geo/country-currencies-overrides.spec.json` (de-facto classifications). The manual override spec is the source-of-truth for WidelyAccepted + Tourist classifications; CLDR / ISO 4217 cannot represent de-facto realities. See Decision 17 (Cross-cutting) for the spec file shape.

##### Country.Locales — M:M (items 3a + 3b)

**Completeness rule**: For every Country, include ALL locale variants spoken there per CLDR — regardless of whether the backing language is in our supported set. Per Decision 6c data-vs-selectability principle, the catalog ships the full ~700 CLDR BCP-47 locale set; Country.Locales is the per-country slice. Drives both the per-country locale-picker UX AND the BCP-47 fallback resolution algorithm (Decision 7's `Locale.ResolveSelectable`).

**Worked examples**:
- France: `PrimaryLocale = "fr-FR"`; `Locales = [fr-FR]` (no other supported-language variants spoken in France)
- Canada: `PrimaryLocale = "en-CA"`; `Locales = [en-CA, fr-CA]` (both officially spoken; both selectable)
- Switzerland: `PrimaryLocale = "de-CH"` (per majority-population rule; ALWAYS set per Decision 6c, `IsSelectable=false`); `Locales = [de-CH, fr-CH, it-CH, rm-CH]` (all four CLDR-spoken variants). Per Decision 6c the full ~700 CLDR catalog ships, so rm-CH (Romansh) is included even though Romansh has `IsSupported=false`. de-CH / fr-CH / it-CH / rm-CH are all `IsSelectable=false`. Users browsing in `Accept-Language: de-CH` resolve via fallback to selectable `de-DE`.
- Belgium: `PrimaryLocale = "nl-BE"` (IsSelectable=true); was always set per data-vs-selectability principle (Decision 6c). `Locales = [nl-BE, fr-BE, de-BE]` (Dutch majority per CLDR ~60%; ordered with PrimaryLocale first). With nl-BE in the 18-selectable list, the UX directly serves nl-BE; prior PLAN drafts that flagged Belgium for "explicit handling" predate the data-vs-selectability separation.
- Côte d'Ivoire: `PrimaryLocale = "fr-CI"`; `Locales = [fr-CI]`. fr-CI is NOT selectable but IS in catalog. Users browsing from CI with `Accept-Language: fr-CI` resolve via fallback to selectable `fr-FR`.
- Japan: `PrimaryLocale = "ja-JP"`; `Locales = [ja-JP]`.
- India: `PrimaryLocale = "hi-IN"` (true majority — Hindi per CLDR; ALWAYS set per Decision 6c data-vs-selectability principle, `IsSelectable=false`); `Locales = [hi-IN, en-IN, ta-IN, bn-IN, ...]` (all variants spoken in India ship in the ~700 catalog; hi/ta/bn supported langs deferred for v2 — they're catalog-only as `IsSupported=false`). Users browsing with `Accept-Language: hi-IN` → ResolveSelectable returns null (no hi-* selectable) → caller falls back to en-US default; users browsing with `Accept-Language: en-IN` → ResolveSelectable falls through to en-GB or en-US.

**Why completeness matters**: this is the engine that enables "we have French French but not Ivorian French, show French French" UX outcome (the user's locked decision). Without Country.Locales completeness, fr-CI wouldn't exist in catalog → ResolveSelectable wouldn't find a fr-prefix Locale to walk to. With completeness, fr-CI exists (non-selectable) AND ResolveSelectable's language-prefix walk over all `ByLanguage("fr")` finds the selectable fr-FR.

##### Country.Territories — inverse nav (item 4a)

**Derived at codegen time** from existing SovereignCountryISO31661Alpha2Code data — no new data source needed. The codegen pass over countries.spec.json scans every Country's `SovereignCountryISO31661Alpha2Code` and back-fills the sovereign's `TerritoryISO31661Alpha2Codes`.

**Worked examples**:
- US: `TerritoryISO31661Alpha2Codes = [PR, GU, VI, AS, MP]` (Puerto Rico, Guam, US Virgin Islands, American Samoa, Northern Mariana Islands)
- FR: `TerritoryISO31661Alpha2Codes = [GP, MQ, GF, RE, YT, NC, PF, WF, BL, MF, PM]` (Guadeloupe, Martinique, French Guiana, Réunion, Mayotte, New Caledonia, French Polynesia, Wallis & Futuna, Saint-Barthélemy, Saint-Martin, Saint-Pierre & Miquelon)
- UK: `TerritoryISO31661Alpha2Codes = [IM, JE, GG, GI, FK, ...]` (Crown Dependencies: Isle of Man, Jersey, Guernsey; British Overseas Territories: Gibraltar, Falklands, etc.)
- FI: `TerritoryISO31661Alpha2Codes = [AX]` (Åland Islands)
- DK: `TerritoryISO31661Alpha2Codes = [FO, GL]` (Faroe Islands, Greenland)
- NL: `TerritoryISO31661Alpha2Codes = [AW, CW, SX]` (Aruba, Curaçao, Sint Maarten)
- NZ: `TerritoryISO31661Alpha2Codes = [CK, NU, TK]` (Cook Islands, Niue, Tokelau)
- Most countries: `TerritoryISO31661Alpha2Codes = []` (empty list — no territories)

DX win: "list US territories" is `country.Territories` (one access), not "scan all countries where `SovereignCountryISO31661Alpha2Code == 'US'`" (a full scan).

> **Phone metadata sourced from libphonenumber at codegen time** — build-time dep only; **NO runtime libphonenumber dependency**. Just the display format + min/max digits embedded in the JSON spec. Full libphonenumber-grade validation is a Phase 2 contact concern via the `IPhoneValidator` Validator-DI pattern (see Decision 13).

#### Subdivision
- `ISO31662Code` (PK; e.g., `"US-CA"`)
- `ShortCode` (e.g., `"CA"`)
- `DisplayName` (English short name; e.g., `"California"`)
- `OfficialName` (English official; e.g., `"State of California"`)
- **NEW in v2**: `EndonymDisplayName` (native; e.g., for `DE-BY` = `"Bayern"`, `JP-13` = `"東京都"`)
- **NEW in v2**: `EndonymOfficialName` (native official)
- `CountryISO31661Alpha2Code` (FK)
- `Deprecation?`
- **NO per-subdivision `PrimaryLanguageISO6391Code` override** — inherits from country per user-locked simplification trade-off (e.g., Catalan in Catalonia is still represented as Spanish-primary at the Subdivision level; consumer can layer richer language-region logic on top if needed).

#### Currency
- `ISO4217AlphaCode` (PK; e.g., `"USD"`)
- `ISO4217NumericCode` (e.g., `"840"`)
- `DisplayName` (e.g., `"US Dollar"`)
- `OfficialName` (e.g., `"United States Dollar"`)
- `DecimalPlaces` (e.g., 0 for JPY, 2 for USD, 3 for BHD)
- `Symbol` (Unicode; e.g., `"$"`, `"¥"`, `"€"`)
- **NEW in v2**: `IsSupported` (bool — true for the **11 supported currencies** with UI display + billing-presentation translations + formatting per Decision 6c data-vs-selectability principle; false for the ~169 catalog-only currencies. The 11 supported = USD/CAD/GBP/AUD/NZD/EUR/MXN/JPY/KRW/TWD/PLN — derived from the 18 supported locales' primary countries; see Decision 6c table.)
- `Deprecation?`

Currency has no nav properties — single TS interface (no Lite/Full split needed).

#### Language
- `ISO6391Code` (PK; e.g., `"en"`)
- `Name` (English; e.g., `"English"`)
- `Endonym` (native; e.g., `"日本語"` for Japanese)
- **NEW in v2**: `IsSupported` (bool — true for the 11 supported languages with translation files; false for the ~169 catalog-only languages from the full ISO 639-1 catalog). Per Decision 6c data-vs-selectability principle: the catalog ships ALL ~180 ISO 639-1 languages; the boolean flag governs whether translation files exist.
- **NEW in v2**: `WritingDirection` (`LTR | RTL` enum per Decision 6b — auto-flip RTL UI containers)
- `Deprecation?`

#### Locale
- `IETFBCP47Tag` (PK; e.g., `"en-US"`)
- `Name` (English description; e.g., `"English (United States)"`)
- `Endonym` (native; e.g., for `ja-JP` = `"日本語 (日本)"`)
- `LanguageISO6391Code` (FK)
- `CountryISO31661Alpha2Code` (FK)
- **NEW in v2**: `IsSelectable` (bool — true for the **18 curated UI-dropdown locales** covering 11 languages per the 2026-05-17 expansion; false for the ~682 non-selectable variants from the full ~700 CLDR BCP-47 catalog like `fr-CI`, `de-CH`, `it-CH`, `zh-CN`, `pt-BR`, `ru-RU`, etc.). Codegen-enforced: every `IsSelectable=true` locale MUST have a corresponding `contracts/messages/{locale}.json` Paraglide message file. Build fails on mismatch. Drives the language-picker UI source-of-truth (replaces v1's three-out-of-band lists: `PUBLIC_ENABLED_LOCALES` env var + Paraglide `settings.json` + message file presence).
- `Deprecation?`

#### Timezone
- `IANAIdentifier` (PK; e.g., `"America/Edmonton"`)
- `DisplayName` (English friendly; e.g., `"Mountain Time — Edmonton"`)
- **NEW in v2**: `LocalizedDisplayNames` (`IReadOnlyDictionary<string, string>` keyed by ISO 639-1; values for our 11 supported langs — en, es, fr, de, it, ja, nl, ko, zh, pt, pl)
- `CurrentStdOffsetMinutes` (e.g., `-420` for MST)
- `CurrentDstOffsetMinutes?` (e.g., `-360` for MDT; null if no DST)
- `CurrentStdAbbrev` (e.g., `"MST"`)
- `CurrentDstAbbrev?` (e.g., `"MDT"`)
- `CountryISO31661Alpha2Code` (primary country FK)
- **NEW in v2**: `Selectable` (bool — true for ~150-200 main zones; false for niche like `Antarctica/Vostok`)
- **NEW in v2**: `Aliases` (`IReadOnlyList<string>` — old IANA ids that map to this canonical; e.g., `["GB", "GB-Eire", "Europe/Belfast"]` for `Europe/London`)
- `Deprecation?`

**v1 vs v2 (per entity)**:
- Country (v1): `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/CountrySeeding.cs:34-44` — base fields (ISO codes + display + official + phone prefix + primary currency/locale + sovereign + Currencies M:M + Locales M:M). V2 adds: `EndonymDisplayName`, `EndonymOfficialName`, `PhoneNumberNationalFormat`, `PhoneNumberMinDigits`, `PhoneNumberMaxDigits`, `PrimaryLanguageISO6391Code`, CLDR enrichments (Decision 6b), `Deprecation?`. V2 RESTORES (after §1.31 audit caught the silent drop): `Currencies` (now with `CurrencyAcceptanceLevel` classification), `Locales` (M:M with completeness rule), `Territories` (inverse-nav derived from sovereign FK), `SovereignCountryISO31661Alpha2Code` (intentional rename from v1's bare `SovereignISO31661Alpha2Code` for ISO-suffix-with-target-type discipline per Decision 2). V2 KEEPS `PrimaryLocaleIETFBCP47Tag` non-nullable (always set to the country's true majority primary locale per CLDR; Decision 6c data-vs-selectability principle); UX-availability is governed by the separate `Locale.IsSelectable` boolean.
- Subdivision (v1): `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/SubdivisionSeeding.cs:36-43` — `ISO31662Code` + `ShortCode` + `DisplayName` + `OfficialName` + `CountryISO31661Alpha2Code`. V2 adds: `EndonymDisplayName`, `EndonymOfficialName`, `Deprecation?`.
- Currency (v1): `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/CurrencySeeding.cs:36-43` — same shape. V2 adds: `Deprecation?`.
- Language (v1): `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/LanguageSeeding.cs:35-40` — `ISO6391Code` + `Name` + `Endonym`. V2 adds: `Deprecation?`.
- Locale (v1): `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/LocaleSeeding.cs:38-45` — `IETFBCP47Tag` + `Name` + `Endonym` + `LanguageISO6391Code` + `CountryISO31661Alpha2Code`. V2 adds: `Deprecation?`.
- Timezone (v1): `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/TimezoneSeeding.cs:36-43` — `IANAIdentifier` + `CountryISO31661Alpha2Code` + `DisplayName` + `UTCOffsetSTD` + `UTCOffsetDST?` + `AbbreviationSTD` + `AbbreviationDST?`. V2 changes offset storage to integer minutes (cleaner for math), adds: `LocalizedDisplayNames`, `Selectable`, `Aliases`, `Deprecation?`.

---

### Decision 6a — GeopoliticalEntity (carried forward from v1; was missing from earlier PLAN)

**What**: a 7th reference data entity covering supranational groupings (continents, sub-continents, geopolitical regions, trade blocs, political unions, military alliances, governance/cooperation forums). 59 seeded entries across **23 type-enum values across 4 region-tagged categories** (verified verbatim from v1 source: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Enums/GeopoliticalEntityType.cs` `#region` tags — General Geopolitical 3 + Economic 8 + Political 6 + Military 6 = 23). Many-to-many relationship with `Country` (denormalized inline on both sides).

**Why now (not deferred)**: v1 had a full `GeopoliticalEntity` implementation (entity + 23-value enum + 59 seed entries + EF M:M junction with Country). Per the new "v1 functional preservation" requirement above, dropping it silently would be a leaner-than-v1 surface — a regression. Real consumer use cases: "list users in EU member states for GDPR compliance", "show NATO-only logistics", "filter UN sanctions list by member country", "compute G20 trade stats". All needed with zero remote lookup.

#### Entity (C#)

```csharp
namespace D2.Shared.Geo.Abstractions;

/// <summary>
/// A supranational geopolitical grouping (continent, trade bloc, political union, military alliance, etc.)
/// Mirrors v1 GeopoliticalEntity (old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/GeopoliticalEntity.cs).
/// </summary>
public sealed record GeopoliticalEntity
{
    /// <summary>Primary key. Short code (e.g., "EU", "NATO", "USMCA", "BRICS", "AF" for Africa continent).</summary>
    public required string ShortCode { get; init; }

    /// <summary>English display name (e.g., "European Union").</summary>
    public required string Name { get; init; }

    /// <summary>The 23-value type enum (see GeopoliticalEntityType).</summary>
    public required GeopoliticalEntityType Type { get; init; }

    // --- denormalized navigation (lite shape) ---

    /// <summary>ISO 3166-1 alpha-2 codes of member countries. Lite shape — strings only.</summary>
    public required IReadOnlyList<string> CountryISO31661Alpha2Codes { get; init; }

    // --- denormalized navigation (.NET: non-nullable list, always populated from in-memory catalog) ---

    /// <summary>Full Country records of member countries. Non-nullable in .NET.</summary>
    /// <remarks>Always populated in .NET (full denormalized graph). TS Lite-vs-Full split lives in
    /// GeopoliticalEntityLite (no countries[] field) + GeopoliticalEntityFull (has countries: CountryLite[]).
    /// See "Lite vs Full" subsection in Decision 7 + TS interface block in the Decision 6 / GeopoliticalEntity section.</remarks>
    public required IReadOnlyList<Country> Countries { get; init; }

    /// <summary>Append-only deprecation marker (per Decision 8). E.g., COMECON or Warsaw Pact if seeded historically.</summary>
    public DeprecationInfo? Deprecation { get; init; }
}
```

#### Type enum (23 values across 4 region-tagged categories; verbatim from v1)

```csharp
namespace D2.Shared.Geo.Abstractions;

/// <summary>
/// Type classification for GeopoliticalEntity. Carried forward verbatim from v1
/// (old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Enums/GeopoliticalEntityType.cs).
/// </summary>
public enum GeopoliticalEntityType
{
    // --- General Geopolitical (3) ---
    Continent = 0,
    SubContinent = 1,
    GeopoliticalRegion = 2,

    // --- Economic (8) ---
    FreeTradeAgreement = 10,
    CustomsUnion = 11,
    CommonMarket = 12,
    EconomicUnion = 13,
    MonetaryUnion = 14,
    BilateralInvestmentTreaty = 15,
    DevelopmentAgreement = 16,
    ResourceSharingAgreement = 17,

    // --- Political (6) ---
    PoliticalUnion = 20,
    HumanRightsAgreement = 21,
    EnvironmentalAgreement = 22,
    GovernanceAndCooperationAgreement = 23,
    PeaceTreaty = 24,
    DemocracyPromotionAgreement = 25,

    // --- Military (6) ---
    MilitaryAlliance = 30,
    ArmsControlAgreement = 31,
    StatusOfForcesAgreement = 32,
    PeacekeepingAgreement = 33,
    SecurityCooperationAgreement = 34,
    NonAggressionPact = 35,
}
```

#### Country ↔ GeopoliticalEntity (denormalized both sides)

**On `Country`** (Geo.Abstractions adds the navigation; lite + full):
```csharp
public sealed record Country
{
    // ... (existing fields per Decision 6) ...

    /// <summary>Lite: short codes of GEs this country belongs to. E.g., for "US": ["NA", "NATO", "UN", "G7", "G20", "OECD", "USMCA", "FVEY"].</summary>
    public required IReadOnlyList<string> GeopoliticalEntityShortCodes { get; init; }

    /// <summary>Full: embedded GeopoliticalEntity records. Non-nullable in .NET — always populated.
    /// TS Lite-vs-Full split lives in CountryLite (no geopoliticalEntities[] field) + CountryFull (has geopoliticalEntities: GeopoliticalEntityFull[]).</summary>
    public required IReadOnlyList<GeopoliticalEntity> GeopoliticalEntities { get; init; }
}
```

**v1 ref**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/Country.cs:205` (`ICollection<GeopoliticalEntity> GeopoliticalEntities`). V2 keeps both sides (lite + full); v1 was a single EF nav property.

#### Lookup interface (`IGeopoliticalEntityLookup`)

```csharp
namespace D2.Shared.Geo.Abstractions;

public interface IGeopoliticalEntityLookup
{
    /// <summary>Look up by short code (e.g., "EU", "NATO"). Returns deprecated entries by default (programmatic-safe per CHANGE 3 rationale).</summary>
    GeopoliticalEntity? ByShortCode(string shortCode, bool activeOnly = false);

    /// <summary>All GeopoliticalEntity entries. Default returns deprecated; pass activeOnly:true for UI dropdowns.</summary>
    IReadOnlyList<GeopoliticalEntity> All(bool activeOnly = false);

    bool IsDeprecated(string shortCode);

    /// <summary>Filter by type enum (e.g., Continent, MilitaryAlliance, FreeTradeAgreement).</summary>
    IReadOnlyList<GeopoliticalEntity> ByType(GeopoliticalEntityType type, bool activeOnly = false);

    /// <summary>All GEs whose country list contains the given ISO 3166-1 alpha-2 code.</summary>
    IReadOnlyList<GeopoliticalEntity> GetEntitiesForCountry(string countryISO31661Alpha2Code, bool activeOnly = false);

    /// <summary>All countries belonging to the named GE (e.g., "EU" → 27 Country records).</summary>
    IReadOnlyList<Country> GetCountriesForEntity(string geShortCode, bool activeOnly = false);

    /// <summary>
    /// Predicate check: is the country a member of the geopolitical entity? O(1) membership check
    /// (e.g., IsCountryMember("US","NATO") → true).
    /// </summary>
    /// <remarks>
    /// Intentionally has NO activeOnly parameter — membership is a deprecation-irrelevant fact.
    /// Whether the country or GE entry is deprecated does not affect "was this membership ever true."
    /// For active-filtered membership listing, use `GetEntitiesForCountry(code, activeOnly: true)` instead.
    /// </remarks>
    bool IsCountryMember(string countryISO31661Alpha2Code, string geShortCode);
}

// Note: lite vs full is NOT a lookup API concern — see "Lite vs Full" subsection at the end of Decision 7.
// .NET backend always returns the full denormalized graph (embedded Countries[] populated, NON-nullable); cost is just
// pointer dereferences against the in-memory denormalized catalog. There is no `*Lite` method variant.
// In TS, the same ByShortCode(code) API works for both lite and full imports — the RETURN TYPE differs per Pattern B:
// @d2/geo-default/geopolitical-entities → GeopoliticalEntityLite (no countries[] field);
// @d2/geo-default/geopolitical-entities/full → GeopoliticalEntityFull (with countries: CountryLite[]).
```

#### Sample seeded entries (59 total — v1 verbatim)

| Group | Count | Examples |
|---|---|---|
| Continents | 7 | `AF` (Africa), `AN` (Antarctica), `AS` (Asia), `EU` (Europe), `NA` (North America), `OC` (Oceania), `SA` (South America) |
| Sub-continents | 7 | `ARAB`, `CAM` (Central America), `CAS` (Central Asia), `EAS` (East Asia), `INDS` (Indian Subcontinent), `SCAN` (Scandinavia), `SEA` (Southeast Asia) |
| Geopolitical regions | 9 | `BALK` (Balkans), `BALT` (Baltics), `BENE` (Benelux), `CARIB` (Caribbean), `LATAM`, `MENA`, `NORD` (Nordics), `SAHEL`, `SSA` (Sub-Saharan Africa) |
| Free Trade Agreements | 4 | `AFTA`, `CPTPP`, `RCEP`, `USMCA` |
| Customs Unions | 2 | `EUCU` (EU Customs Union), `SACU` (Southern African Customs Union) |
| Common Markets | 2 | `EEA` (European Economic Area), `MERCOSUR` |
| Economic Unions | 1 | `EAEU` (Eurasian Economic Union) |
| Monetary Unions | 4 | `EZ` (Eurozone), `ECCU` (East Caribbean), `WAEMU` (West African), `CEMAC` (Central African) |
| Political Unions | 1 | `EUR` (European Union — political-union classification) |
| Governance & Cooperation | 14 | `AU` (African Union), `AL` (Arab League), `ASEAN`, `BRICS`, `CARICOM`, `COE` (Council of Europe), `CW` (Commonwealth), `G7`, `G20`, `GCC` (Gulf Cooperation Council), `NC` (Nordic Council), `OECD`, `OIF`, `OPEC`, `SAARC`, `UN` |
| Military Alliances | 6 | `ANZUS`, `AUKUS`, `CSTO`, `FVEY` (Five Eyes), `NATO`, `QUAD` |

**v1 seed source**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/GeopoliticalEntitySeeding.cs`.

#### Worked example: "list users in EU member states" (in-process — no service call)

```csharp
// Consumer code in any service that has IGeopoliticalEntityLookup injected:
var euMemberCodes = geLookup.GetCountriesForEntity("EU")
                            .Select(c => c.ISO31661Alpha2Code)
                            .ToHashSet();

// Now an in-process EF/DB query against the local user table:
var euUsers = await dbContext.Users
    .Where(u => euMemberCodes.Contains(u.CountryISO31661Alpha2Code))
    .ToListAsync();
```

No gRPC. No reference-data cache lookup. No round-trip. The denormalized arrays in `D2.Shared.Geo.Default` are loaded once at lib import and live as static readonly data.

#### v1 vs v2 comparison

| Concern | v1 | v2 |
|---|---|---|
| Entity definition | `Geo.Domain/Entities/GeopoliticalEntity.cs` (record with `ShortCode`, `Name`, `Type`, `ICollection<Country>` EF nav) | `D2.Shared.Geo.Abstractions/GeopoliticalEntity.cs` (sealed record + lite/full denormalized arrays + Deprecation) |
| Enum | `Geo.Domain/Enums/GeopoliticalEntityType.cs` (23 values, 4 region-tagged categories) | `D2.Shared.Geo.Abstractions/GeopoliticalEntityType.cs` (carried verbatim) |
| Seeding | `Geo.Infra/Repository/Seeding/GeopoliticalEntitySeeding.cs` (59 entries) + `CountryGeopoliticalEntitySeeding.cs` (M:M join rows) — EF Core migrations | `contracts/geo/geopolitical-entities.spec.json` (59 entries) → codegen emits both .NET + TS lookups |
| Consumer access | EF nav property on Country (lazy-loaded; cost = SQL JOIN) OR gRPC call to Geo service | Direct in-memory `country.GeopoliticalEntities` (full shape) or `country.GeopoliticalEntityShortCodes` (lite); O(1) access |
| Country ↔ GE relationship | M:M EF join table `country_geopolitical_entities`, no metadata | Denormalized arrays on both sides; no metadata in Phase 1 (membership history deferred) |
| Deprecation | None (silent removal) | `Deprecation?` field (per Decision 8) — append-only |
| Localization | None | Phase 1 ships English `Name` only; CLDR localization deferred (consistent with country name localization in Decision 16) |

#### Deferred (with revisit triggers)

| Item | Revisit trigger |
|---|---|
| Membership history (join/leave dates per Country-GE pair) | Audit/analytics demands "when did Country X join the EU?" (e.g., post-Brexit lookup of UK's EU period) |
| GeopoliticalEntity deprecation seeding (COMECON dissolved 1991, Warsaw Pact dissolved 1991, ECSC merged into EU 2002) | Historical-GE display use case |
| GeopoliticalEntity hierarchies (EU within Europe-continent; EZ within EU) | Use case demands "list all GEs containing Country X by hierarchy" |
| GeopoliticalEntity localized display names (CLDR) | Match-on-primary-lang for GE labels proves insufficient |
| GeopoliticalEntity endonyms (e.g., `"Union européenne"`, `"欧州連合"`) | Localized GE display surfaces as need |

---

### Decision 6b — V2 enrichments beyond v1 (CLDR-sourced; locked in Phase 1 scope)

**What**: a small set of CLDR-sourced properties added to existing reference entities. Each is a single property; each is sourced from CLDR (already a data source for endonyms — no new dependency); each enables a localization/formatting use case that consumers will hit immediately. Per the v1-no-loss directive, these ADD to v1's surface — they do not replace anything.

| Enrichment | Entity | Type | Source | Use case it enables | Cost |
|---|---|---|---|---|---|
| **Writing direction (LTR/RTL)** | `Language` | `enum WritingDirection { LTR, RTL }` | CLDR | Auto-flip UI containers + text alignment for Arabic / Hebrew / Farsi / Urdu users without per-component checks. NOT in Phase 1 catalog (Arabic/Hebrew deferred per "Languages deferred from Phase 1" section — RTL infra not built; all 11 v2 supported langs en/es/fr/de/it/ja/nl/ko/zh/pt/pl are LTR) BUT property exists; Phase 1 ships all 11 langs as LTR-only and the field is wired so Phase N can seed RTL langs without schema change | Trivial (1 byte enum) |
| **First day of week** | `Country` + `Locale` | `enum DayOfWeek` (Mon–Sun) | CLDR | Calendar widgets show correct week start: US/CA = Sunday; ISO/EU = Monday; some Middle Eastern = Saturday. Without it, US users see Mon-first calendars (wrong) | Trivial (1 byte enum) |
| **Weekend definition** | `Country` | `(DayOfWeek WeekendStart, DayOfWeek WeekendEnd)` tuple | CLDR | Business-day math + scheduling defaults: most countries Sat/Sun; some ME Fri/Sat; some Sun-only / Fri-only. Used by Phase 5 scheduling defaults | Trivial (2 bytes) |
| **Number formatting** | `Locale` | `string DecimalSeparator` + `string ThousandsSeparator` | CLDR | Correct number display: `1,000.50` (US) vs `1.000,50` (DE/IT) vs `1 000,50` (FR uses non-breaking space) vs `1'000.50` (de-CH apostrophe). Without it, consumers hand-roll per-locale | Trivial (2 strings per locale × ~700 locales = ~28KB; only ~72 bytes for the 18 selectable subset most consumers will hit) |
| **Date format pattern** | `Locale` | `enum DateFormatPattern { DMY, MDY, YMD }` | CLDR | Date picker / display order: US = MDY (`5/16/2026`); most of world = DMY (`16/5/2026`); ISO + JP = YMD (`2026-05-16`). Without it, ambiguous "5/12" parsing | Trivial (1 byte enum) |
| **Measurement system** | `Country` | `enum MeasurementSystem { Metric, Imperial, Mixed }` | CLDR | Unit display defaults: US = Imperial (miles, °F, lbs); UK = Mixed (miles for roads, but Celsius for weather, kg for groceries); rest = Metric. Used by Phase 6+ distance / weight displays | Trivial (1 byte enum) |

**All locked in Phase 1 scope**. Each is a single property, sourced from CLDR (already in the build pipeline for endonyms), and unlocks immediate consumer UX wins. Cumulative storage cost across all 250 countries + ~700 locales + 11 languages = ~few tens of KB across the full catalog — still well below noise level vs the ~200KB total v2 catalog footprint (expanded from ~110KB to accommodate the full ~700 CLDR locale set per 2026-05-17 expansion).

**Why this list, why now**: every one of these surfaces in a "we already need this for product launch" use case (date pickers, number formatting, calendar widgets) — making consumers reach for a separate `i18n` lib for what should be on the reference entity is bad DX. CLDR being already-in-the-pipe means no new build step / no new dependency / no new failure mode.

**Schema impact on entities** (additive; mirror in Decision 6 entity definitions at SHIP):
- `Language` gains: `WritingDirection`
- `Country` gains: `FirstDayOfWeek`, `WeekendStart`, `WeekendEnd`, `MeasurementSystem`
- `Locale` gains: `FirstDayOfWeek`, `DecimalSeparator`, `ThousandsSeparator`, `DateFormatPattern`

**Why on `Country` AND `Locale` for `FirstDayOfWeek`**: locale is the primary source (different locales in the same country can differ — e.g., en-US vs es-US both = Sunday), but country-level fallback handles the "we know country but not locale" case in WhoIs / IP-geolocation contexts.

**Not enriched in Phase 1 (deferred with triggers)**:
- Currency formatting style (symbol-before vs symbol-after, space vs no-space) — defer to Phase 2 Contacts when first money-display surfaces
- CLDR plural rules (one/few/many forms for ICU MessageFormat) — defer to Phase N when i18n message system matures
- Calendar systems beyond Gregorian (Buddhist, Hijri, Japanese era) — defer; niche for current product

---

### Decision 6c — Architectural principle: data-vs-selectability separation

**What**: across ALL Phase 1 reference entities, the catalog ships the FULL universe of valid entries; UI/UX availability is governed by a boolean selectability flag on the entity itself. Locked uniformly:

| Entity | Catalog scope | Selectability flag | Supported / selectable count |
|---|---|---|---|
| `Language` | ~180 (full ISO 639-1) | `IsSupported: bool` | 11 (en, es, fr, de, it, ja, nl, ko, zh, pt, pl) |
| `Locale` | ~700 (full CLDR BCP-47) | `IsSelectable: bool` | 18 (en-US, en-CA, en-GB, en-AU, en-NZ, es-ES, es-MX, fr-FR, fr-CA, de-DE, it-IT, ja-JP, nl-NL, nl-BE, ko-KR, zh-TW, pt-PT, pl-PL) |
| **`Currency`** | **~180 (full ISO 4217 active)** | **`IsSupported: bool`** | **11 (USD, CAD, GBP, AUD, NZD, EUR, MXN, JPY, KRW, TWD, PLN — derived from the 18 supported locales' primary countries)** |
| `Timezone` | ~600 (full IANA) | `Selectable: bool` | ~150-200 (curated UI subset) |
| `Country` | 250 (full ISO 3166-1) | n/a (always present) | n/a |
| `Subdivision` | ~3,600 (full ISO 3166-2) | n/a (always present) | n/a |
| `GeopoliticalEntity` | 59 (full v1 carry-forward) | n/a (always present) | n/a |

**Supported currency derivation** (11 currencies, locked at 2026-05-17):

| Currency | Primary user countries (supported locales) |
|---|---|
| USD | en-US |
| CAD | en-CA + fr-CA |
| GBP | en-GB |
| AUD | en-AU |
| NZD | en-NZ |
| EUR | es-ES, fr-FR, de-DE, it-IT, nl-NL, nl-BE, pt-PT (Eurozone members in supported set) |
| MXN | es-MX |
| JPY | ja-JP |
| KRW | ko-KR |
| TWD | zh-TW |
| PLN | pl-PL |

= 11 distinct currencies. All other ISO 4217 active currencies (~169) ship with `IsSupported=false`.

**Why**:
1. **Data integrity always preserved**: `Country.PrimaryLocale` is ALWAYS set to the country's true majority-population locale (per CLDR). Brazil's primary IS pt-BR; Russia's IS ru-RU; China mainland's IS zh-CN — regardless of whether we ship translations.
2. **UI/UX decisions are downstream**: consumer code uses `IsSelectable` / `IsSupported` / `Selectable` flags to decide what to actually offer to users. No conflation of data-truth and UX-availability.
3. **Expansion is cheap**: shipping translations for a new language = add Paraglide message file + flip the boolean flag in the spec. No catalog changes, no schema migration, no consumer code changes.
4. **§1.31 alignment**: maximally preserves v1 data; never silently drops a country's primary locale just because we don't translate to it.

**v1 vs v2**: v1 had this for Timezone (`Timezone` table was full IANA with no selectability flag — consumer had to filter manually). V2 unifies the pattern across Language + Locale + Timezone with explicit boolean flags. Catalog presence ≠ UI selectability — these are now distinct concerns by design.

**.NET vs TS modeling distinction**:
- **.NET backend**: ALWAYS loads the full catalog at startup; all nav properties are POINTERS to in-memory entries (essentially free). C# records have ALL navs NON-nullable. The ONLY `?` in C# is for genuinely-nullable real-world facts (`SovereignCountry?` — most countries have no sovereign; `Deprecation?` — most entries are active).
- **TS package consumers**: choose lite (just ISO codes, no embedded objects — small browser bundle) or full (denormalized — larger bundle) via import path. TS uses TWO separate interfaces per entity (`{Entity}Lite` + `{Entity}Full extends {Entity}Lite`). Full interfaces have all navs NON-optional; Lite has only ISO codes.
- **Cycle-breaking**: `Subdivision.Country` (in `SubdivisionFull`) returns `CountryLite` (not `CountryFull`) — prevents infinite type recursion. Same for other nested navs (`LocaleFull.country`, `TimezoneFull.country`, `GeopoliticalEntityFull.countries[]`, `CountryFull.territories[]` all use `CountryLite`).

This separation lets the .NET type system enforce non-null at compile time AND lets the TS type system clearly distinguish "I imported lite vs full" via separate interface types.

**Consumer pattern** (canonical):
```csharp
// Pattern 1: get country's primary locale + decide whether to serve UI in it
var country = countryLookup.ByAlpha2("BR");
// country.PrimaryLocale.IETFBCP47Tag = "pt-BR" (ALWAYS set)
// country.PrimaryLocale.IsSelectable = false (no Paraglide messages)
if (country.PrimaryLocale.IsSelectable)
    return country.PrimaryLocale;  // serve UI in pt-BR
else
    return userSession.PreferredLocale ?? defaultLocale;  // fall through

// Pattern 2: list languages with translations available
var supportedLangs = languageLookup.All().Where(l => l.IsSupported);
// returns 11 entries (en, es, fr, de, it, ja, nl, ko, zh, pt, pl)

// Pattern 3: list selectable locales for a UI dropdown
var selectable = localeLookup.AllSelectable();
// returns 18 entries (the curated subset)
```

---

### Decision 7 — Lookup interfaces (single methods + `activeOnly` parameter)

**Single methods with `bool activeOnly = false` parameter** — every lookup collapses what would be a `By*` / `By*Active` pair into ONE method that defaults to including deprecated entries. The default is "include deprecated" because backend programmatic lookups resolve historical references (a stored hash from 2015 that includes `YU` country code must still resolve `YU`); UI dropdowns consciously opt-in via `activeOnly: true`. Backwards-incompatible default flip is impossible to silently miss (compile-time signature change).

```csharp
public interface ICountryLookup
{
    /// <summary>Look up by ISO 3166-1 alpha-2 code. Returns deprecated entries by default (programmatic-safe).</summary>
    Country? ByAlpha2(string code, bool activeOnly = false);

    Country? ByAlpha3(string code, bool activeOnly = false);
    Country? ByNumeric(string code, bool activeOnly = false);

    /// <summary>NEW per CHANGE 1b — cascade name resolution. See "Name resolution cascade" subsection below.</summary>
    Country? ResolveByName(string upstreamName, bool activeOnly = false);

    IReadOnlyList<Country> All(bool activeOnly = false);
    bool IsDeprecated(string code);
}
```

**Same shape across** `ISubdivisionLookup` / `ICurrencyLookup` / `ILanguageLookup` / `ILocaleLookup` / `ITimezoneLookup` / `IGeopoliticalEntityLookup` — every per-key getter, every `All()`, every type-filter helper takes `bool activeOnly = false`.

**Timezone additionally** (because of the `Selectable` + country grouping use cases):
- `AllSelectable(bool activeOnly = false)` — curated subset for UI dropdowns; selectability + deprecation are orthogonal
- `AllByCountry(string iso3166_1_alpha2, bool activeOnly = false)` — narrowed by country
- `GetPrimaryForCountry(string iso3166_1_alpha2, bool activeOnly = false)` — smart default

**Locale additionally**:
- `AllSelectable(bool activeOnly = false)` — the 18 curated locales (IsSelectable=true)

**Language additionally**:
- `AllSupported(bool activeOnly = false)` — the 11 supported languages (IsSupported=true). Different name to mirror the `IsSupported` field (vs `IsSelectable`/`Selectable` on Locale/Timezone)

**Subdivision + Country additionally** (because of the WhoIs / IP-geolocation free-form name resolution use case):

```csharp
public interface ISubdivisionLookup
{
    // ... standard methods with bool activeOnly = false (see shape above) ...

    /// <summary>
    /// Resolves a free-form subdivision name (from WhoIs / IP geolocation / user input)
    /// to a Subdivision within the given country via the cascade name resolution
    /// (exact → startsWith → contains → Levenshtein ≤ 2 — see "Name resolution cascade" subsection below).
    ///
    /// Each pass normalizes via: lowercase + Unicode NFD + strip combining marks (Mn category) + trim,
    /// and matches against {DisplayName, OfficialName, EndonymDisplayName, EndonymOfficialName, ShortCode}.
    /// First match across the cascade wins.
    ///
    /// Returns null when:
    /// - The country has no subdivisions in our catalog (large parts of Africa, small countries, etc.)
    /// - The upstream name doesn't match ANY name field of any subdivision in that country at any pass
    /// - Country code is unknown/unresolvable
    /// </summary>
    Subdivision? ResolveByName(string upstreamName, string countryISO31661Alpha2Code, bool activeOnly = false);
}

public interface ICountryLookup
{
    // ... standard methods with bool activeOnly = false (see shape above) ...

    /// <summary>
    /// Resolves a free-form country name to a Country via the same cascade name resolution.
    /// Match fields: DisplayName, OfficialName, EndonymDisplayName, EndonymOfficialName,
    /// ISO31661Alpha2Code, ISO31661Alpha3Code.
    /// </summary>
    Country? ResolveByName(string upstreamName, bool activeOnly = false);
}
```

**Worked examples — `ISubdivisionLookup.ResolveByName`** (cascade):
- `ResolveByName("California", "US")` → `US-CA` (Pass 1 exact match on DisplayName)
- `ResolveByName("Sao Paulo", "BR")` → `BR-SP` (Pass 1 after NFD normalization — "Sao Paulo" with combining marks stripped matches "São Paulo")
- `ResolveByName("State of California", "US")` → `US-CA` (Pass 3 contains — `OfficialName="State of California"` contains the normalized form, or matches as exact OfficialName under Pass 1 depending on direction)
- `ResolveByName("California Republic", "US")` → `US-CA` (Pass 3 contains — "california republic" contains "california" matching DisplayName)
- `ResolveByName("Califrnia", "US")` → `US-CA` (Pass 4 Levenshtein distance 1)
- `ResolveByName("Cote d'Ivoire", "CI")` → matches Côte d'Ivoire-named subdivisions (Pass 1 after NFD)
- `ResolveByName("Île-de-France", "FR")` → `FR-IDF` (Pass 1 exact match — full Unicode preserved across normalization)
- `ResolveByName("Bayern", "DE")` → `DE-BY` (Pass 1 exact match on `EndonymDisplayName`)
- `ResolveByName("CA", "US")` → `US-CA` (Pass 1 exact match on `ShortCode`)
- `ResolveByName("Some Obscure Region", "XX")` → `null` (no match at any pass; country code unknown)
- `ResolveByName("California", "ZW")` → `null` (Zimbabwe catalog has no California-named subdivision)
- `ResolveByName("Anything", "TD")` → `null` if Chad has no subdivisions in our catalog (coverage gap)

**Worked examples — `ICountryLookup.ResolveByName`** (cascade):
- `ResolveByName("United States")` → US (Pass 1 exact on DisplayName)
- `ResolveByName("United States of America")` → US (Pass 1 exact on OfficialName)
- `ResolveByName("Estados Unidos")` → US (Pass 1 exact match — Spanish endonym IF shipped for the user's locale)
- `ResolveByName("USA")` → US (Pass 1 exact on `ISO31661Alpha3Code`)
- `ResolveByName("Allemagne")` → DE (Pass 1 exact on French endonym IF we ship endonyms in user's locale)
- `ResolveByName("Federal Republic of Germany")` → DE (Pass 3 contains match on OfficialName — "federal republic of germany" contains "germany")

**Why no external library**: ~3,600 subdivisions × 5 name fields = ~18,000 normalized strings; a linear scan with NFD-normalized comparison + small-distance Levenshtein is ~5ms cold / sub-ms warm. Lucene-grade FTS / token-aware matchers / locale-aware collators are overkill for catalog this size and add binary footprint we don't need. The cascade pattern is ~200 LOC of shared logic across .NET + TS.

**Why**: WhoIs (IPinfo and equivalent IP-geolocation providers) returns subdivision data as a free-form `region` STRING, not a code. Upstream-name → catalog-FK resolution succeeds across accent variants, casing inconsistencies, and minor typos without bouncing every imperfect upstream name to the unresolved bucket. The caller pattern is: attempt structured resolution; only populate `SubdivisionISO31662Code` when `ResolveByName` returns non-null. Phase 3 Edge's WhoIs entity persists the raw upstream response for audit, so unresolved subdivision strings are NOT lost — they live in the audit trail, just not in RequestContext (see CHANGE 2 / Decision 17 WhoIs enrichment flow + the "Intentional drops from v1" section).

#### Name resolution cascade

A small shared module under `D2.Shared.Geo.Abstractions/NameResolution/` (parallel TS at `@d2/geo-abstractions/src/name-resolution/`):

```
D2.Shared.Geo.Abstractions/NameResolution/
  ├── NameNormalizer.cs      (~50 LOC — lowercase + NFD + strip Mn + trim)
  └── LevenshteinComparer.cs (~80 LOC — bounded distance ≤ 2; early exit on threshold)
```

Identical logic shipped TS-side under `@d2/geo-abstractions/src/name-resolution/` (NFD via `String.prototype.normalize('NFD').replace(/\p{M}/gu, '')`).

**Algorithm steps** (every `ResolveByName` invocation):
1. **Normalize input**: lowercase + Unicode NFD + strip combining marks (`Mn` category) + trim → `q`
2. **Pre-build normalized name index** (one-time, at lib import — NOT per call): every entity → tuple of `(entity, normalizedNames[])` covering all matchable name fields
3. **Pass 1 — exact match**: scan for any entity whose any-normalized-name == `q`. First match wins.
4. **Pass 2 — startsWith**: scan for any entity whose any-normalized-name starts with `q` (or `q` starts with any-normalized-name — directional consistency configurable per call site)
5. **Pass 3 — contains/substring**: scan for any entity whose any-normalized-name contains `q` (or vice versa)
6. **Pass 4 — Levenshtein distance ≤ 2**: scan for any entity whose any-normalized-name has bounded edit distance ≤ 2 from `q`. Use length-difference early-exit for speed.
7. Return first match across the cascade. Return null when no pass produces a match.

**Cost**:
- Pre-normalization at lib import: ~5ms (one-time)
- Per-call cost (cold cache): ~3-5ms (Pass 1 hits ~99% of well-formed input → most calls don't reach Pass 4)
- Per-call cost (Pass 4 worst case — must scan all entities for Levenshtein): ~15-20ms against the full ~3,600 subdivision catalog; rare in practice

**Why a single cascade across all the entities**: same logic operates over Country names (~250 entities × ~6 fields) and Subdivision names (~3,600 × ~5 fields) — duplicating per entity type would be needless surface area. Locale and Timezone ResolveByName variants DEFER per CHANGE 1c/1d (no Phase 1 consumer).

**Locale additionally** (BCP-47 selectability + fallback resolution — items 1b + 1d):

```csharp
public interface ILocaleLookup
{
    // ... standard methods with bool activeOnly = false (ByIETFBCP47Tag / All / IsDeprecated / ByLanguage / ByCountry) ...

    /// <summary>All locales with IsSelectable=true (the curated 18 UI-dropdown subset per 2026-05-17 expansion, covering 11 languages).
    /// Source-of-truth for language-picker UIs; replaces v1's three out-of-band lists
    /// (PUBLIC_ENABLED_LOCALES env var + Paraglide settings.json + message file presence).
    /// Selectability and deprecation are orthogonal — pass activeOnly:true to additionally exclude deprecated.</summary>
    IReadOnlyList<Locale> AllSelectable(bool activeOnly = false);

    /// <summary>All locales (selectable + non-selectable) for the given language code.
    /// Used by ResolveSelectable's fallback walk.</summary>
    IReadOnlyList<Locale> ByLanguage(string iso6391Code, bool activeOnly = false);
}

// BCP-47 fallback resolution lives as a Locale extension in D2.Shared.Geo.Abstractions
// (kept near the catalog — the resolution algorithm operates over Locale entities).
public static class LocaleExtensions
{
    extension(Locale _)
    {
        /// <summary>Resolve a user-requested BCP-47 tag to a SELECTABLE Locale via fallback walk:
        /// (1) exact-tag match where IsSelectable=true → return it
        /// (2) extract language prefix from requested tag → find first IsSelectable=true Locale with that language
        /// (3) return null (caller falls back to system default — typically en-US)
        ///
        /// Worked examples (assuming the 18-locale selectable subset per 2026-05-17 expansion):
        /// - "fr-CI" → walks: no exact selectable match → language prefix "fr" → first selectable fr-* = "fr-FR" → returns fr-FR
        /// - "de-CH" → walks: no exact selectable match → language prefix "de" → "de-DE" → returns de-DE
        /// - "it-CH" → walks: no exact selectable match → language prefix "it" → "it-IT" → returns it-IT
        /// - "en-AU" → walks: exact selectable match → returns en-AU directly
        /// - "nl-BE" → walks: exact selectable match → returns nl-BE directly (NEW per 2026-05-17 expansion)
        /// - "ko-KR" → walks: exact selectable match → returns ko-KR directly
        /// - "zh-HK" → walks: no exact selectable match → language prefix "zh" → first selectable zh-* = "zh-TW" → returns zh-TW
        /// - "zh-CN" → walks: no exact selectable match → language prefix "zh" → "zh-TW" → returns zh-TW (only Chinese selectable)
        /// - "pt-BR" → walks: no exact selectable match → language prefix "pt" → "pt-PT" → returns pt-PT (only Portuguese selectable)
        /// - "ru-RU" → walks: no exact selectable match → no selectable ru-* → returns null (caller falls back to en-US)
        /// - "hi-IN" → walks: no exact selectable match → no selectable hi-* → returns null (Hindi deferred)
        /// - "ar-SA" → walks: no exact selectable match → no selectable ar-* → returns null (Arabic deferred — RTL infra not built)
        /// - "ja-JP" → walks: exact selectable match → returns ja-JP
        /// </summary>
        public static Locale? ResolveSelectable(string requestedTag, ILocaleLookup lookup)
        {
            if (requestedTag.Falsey()) return null;

            // (1) Exact match if selectable
            var exact = lookup.ByIETFBCP47Tag(requestedTag);
            if (exact is { IsSelectable: true }) return exact;

            // (2) Language-prefix walk
            var dashIdx = requestedTag.IndexOf('-');
            var language = dashIdx > 0 ? requestedTag[..dashIdx] : requestedTag;
            return lookup.ByLanguage(language).FirstOrDefault(l => l.IsSelectable);
        }
    }
}
```

**Why this lives in `D2.Shared.Geo.Abstractions` (not `D2.Shared.I18n`)**: the resolution algorithm operates over Locale entities + uses the Locale lookup interface — keeping it near the catalog is cleaner than crossing the i18n boundary. I18n consumers call into the geo lib for resolution; geo lib never depends on i18n.

**Why fallback resolution matters**: a user's browser sends `Accept-Language: fr-CI` (Côte d'Ivoire French). Our catalog has fr-CI as a non-selectable locale (required for Country.Locales M:M completeness — Côte d'Ivoire's primary locale) but the user can't pick it. The fallback walk finds `fr-FR` (the nearest selectable French) — UX: user gets French content instead of a forced fallback to English. This is the engine behind "we have French French but not Ivorian French, show French French" UX outcome.

**Consumer guidance**:
- Selector UIs / validators MUST pass `activeOnly: true` (don't let user choose a deprecated code for new data)
- Display / audit / hash-resolution code uses the default (`activeOnly: false`) — must still resolve historical references even if the code is deprecated
- Language-picker UIs MUST use `AllSelectable()` (don't expose non-selectable variants to users); pass `activeOnly: true` if dropdown should also exclude deprecated
- Browser/`Accept-Language`/upstream-supplied tags use `Locale.ResolveSelectable(tag, lookup)` for BCP-47 fallback resolution

**Why default is `activeOnly: false`**: deprecation is a real concern for long-lived reference data. If `YU` (Yugoslavia) is deprecated and we silently drop it from lookups, every historical record citing `YU` becomes unresolvable (hash citing `YU` now hashes to nothing). The safer default is "backend programmatic lookups get all entries including deprecated"; the UI layer explicitly opts in to filter. Silent backend resolution failure (hash references can't resolve) is worse than visible UI bug (deprecated entry showing in dropdown — user reports it).

**v1 vs v2**:
- V1: single lookup methods (`ByAlpha2(code)` returns whatever is in the DB; deprecated codes either silently drop or stay forever). No deprecation-aware distinction.
- V2: single methods with `bool activeOnly = false` parameter. New predicate (rules.md §1.23 — see "In-flight rules.md predicates" below) codifies "default is backend-safe (include deprecated); UI must consciously opt in to filter."

#### Lite vs Full is NOT a lookup API concern

The lookup API returns a single shape per entity for the .NET backend, but TS uses **Pattern B** (Decision 6c §"TS modeling") — separate `{Entity}Lite` + `{Entity}Full` interfaces per entity with embedded navs. **Lite vs Full is determined by:**

1. **TS import path**: `@d2/geo-default/countries` returns `CountryLite` (no embedded nav objects) vs `@d2/geo-default/countries/full` returns `CountryFull extends CountryLite` (with embedded nav objects, all non-optional).
2. **Wire serialization** (future Phase: gRPC/REST can choose lite vs full per endpoint — NOT Phase 1 scope)

**In .NET backend**: lookups always return the full Country graph. Embedded nav properties (`PrimaryCurrency`, `PrimaryLocale`, `PrimaryLanguage`, `GeopoliticalEntities[]`, `Subdivisions[]`, `Timezones[]`, `Territories[]`, `Locales[]`, `Currencies[]`, etc.) are ALWAYS populated and NON-nullable. Only genuinely-nullable real-world facts (`SovereignCountry?`, `Deprecation?`) carry `?`. Cost is just pointer dereferences against the in-memory denormalized catalog — essentially free. There is no `*Lite` method variant on the .NET API; it would not make sense (the assembly already loaded the full data).

**In TS**: the SAME `byAlpha2(code)` API works for both lite and full imports — the difference is the return TYPE:
- `import { CountryLookup } from "@d2/geo-default/countries"` — returns `CountryLite` (only ISO codes / FK code arrays; no embedded objects).
- `import { CountryLookup } from "@d2/geo-default/countries/full"` — returns `CountryFull` (denormalized with all embedded navs populated and non-optional).

**Cycle-breaking**: nested navs in Full interfaces use the corresponding Lite type to prevent infinite recursion. Example: `SubdivisionFull.country: CountryLite` (not `CountryFull` — otherwise `CountryFull.subdivisions[]` → `SubdivisionFull.country` → `CountryFull.subdivisions[]` → ∞). Same pattern for `LocaleFull.country`, `TimezoneFull.country`, `GeopoliticalEntityFull.countries[]`, `CountryFull.territories[]` — all use `CountryLite`.

Consumers choose the import path based on their bundle-size needs (browser dropdowns import lite; profile pages with rich embedded data import full). The Decision 14 tree-shake table reflects the data shape, not the API shape.

---

### Decision 8 — Deprecation pattern (NEW in v2)

**What**:
```csharp
public sealed record DeprecationInfo
{
    public required DateOnly DeprecatedAt { get; init; }
    public required string Reason { get; init; }
    public IReadOnlyList<string>? SupersededBy { get; init; }
    public string? SuccessorNote { get; init; }
}
```

Used on every reference data entity as an optional `Deprecation?` field.

**Reference data is APPEND-ONLY or UPDATE-IN-PLACE — never delete**. Retire entries by ADDING the `Deprecation` field. Examples:
- `YU` (Yugoslavia, dissolved 2003) → `SupersededBy = ["RS", "ME", "HR", "SI", "BA", "MK", "XK"]`
- `CS` (Czechoslovakia, dissolved 1993) → `SupersededBy = ["CZ", "SK"]`
- `SU` (Soviet Union, dissolved 1991) → `SupersededBy = ["RU", "UA", "BY", ...]`
- `ZR` (Zaire, renamed) → `SupersededBy = ["CD"]`
- `DEM` (Deutsche Mark) → `SupersededBy = ["EUR"]`
- `ITL` (Italian Lira) → `SupersededBy = ["EUR"]`
- `Asia/Saigon` (IANA tz) → `SupersededBy = ["Asia/Ho_Chi_Minh"]`

**Why**: silently invalidating content-addressable hashes referencing the removed codes is a correctness bug — history queries break, audit logs become unreadable, replay of old events fails. Append-only + Deprecation lets the lookup table answer two questions: "what does code X resolve to?" (always works) and "is X still active for new selection?" (filterable). This is the right shape for any append-only reference data — same model that ISO + IANA themselves use.

**v1 vs v2**:
- V1: NO deprecation field. Codes that get retired would simply be removed from the seed migration. This silently invalidates content-addressable hashes referencing the removed codes AND breaks historical lookups. The v1 model never had to deal with this because the catalogs were small + recent; v2's full ISO 3166-2 + full ISO 4217 + full IANA tzdb sets contain plenty of deprecated entries from day one.
- V2 fixes structurally with the `Deprecation?` field on every reference entity.

---

### Decision 9 — Endonym discipline

**What**: each Country gets ONE `PrimaryLanguageISO6391Code` reflecting **actual majority population usage** (not legal/political; e.g., Canada = `"en"` not `"fr"`; Belgium = `"nl"` not equal-status `"fr"`/`"de"`). Subdivisions inherit from country (no per-subdivision override per Decision 6).

Match logic in display code:
```
showEndonym = currentLocale.LanguageISO6391Code == country.PrimaryLanguageISO6391Code
```

**Why**: when a German user views a Bavarian subdivision picker, they want to see `"Bayern"` not `"Bavaria"`. When an English user views the same picker, they want `"Bavaria"`. The endonym field + the primary-language match gives consumers a clean way to do this without locale-string-table lookups.

**v1 vs v2**:
- V1: endonyms only on `Language` + `Locale`:
  - `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/LanguageSeeding.cs:38` — `Endonym = "English"`, `:79` — `Endonym = "日本語"`
  - `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/LocaleSeeding.cs:42` — `Endonym = "English (United States)"` (just a copy of the English `Name` for en-US; only meaningful for ja-JP / fr-FR / etc.)
- V2: adds `EndonymDisplayName` + `EndonymOfficialName` to **`Country`** and **`Subdivision`** (per Decision 6). The match-on-primary-language logic uses these for native-language display.

---

### Decision 10 — Timezone catalog vs NodaTime tzdb (two libs, two roles, one IANA-id bridge)

**What**: ship BOTH `D2.Shared.Geo.Default`'s timezone catalog AND `D2.Shared.Time`'s NodaTime tzdb wrapper. They serve different roles, both essential.

| Capability | NodaTime tzdb (D2.Shared.Time) | Geo.Default timezone catalog |
|---|---|---|
| Offset for IANA-id at instant X | ✅ | ❌ |
| Apply DST rules in effect at any past instant | ✅ | ❌ |
| Detect ambiguous / skipped local times | ✅ | ❌ |
| List ALL ~600 IANA zone ids | ✅ (via `DateTimeZoneProviders.Tzdb.Ids`) | ✅ (curated; mirrors NodaTime's set) |
| Human-friendly display name | ❌ | ✅ |
| Localized display names (11 langs) | ❌ | ✅ (via CLDR) |
| Country grouping | ❌ | ✅ |
| Primary timezone for country | ❌ | ✅ |
| Selectable subset (filter niche) | ❌ | ✅ |
| Snapshot abbreviations (MST/MDT/EST/EDT) | ✅ (instant-dependent) | ✅ (pre-computed snapshot) |
| Aliases (`Asia/Saigon` → `Asia/Ho_Chi_Minh`) | ✅ (canonical resolution) | ✅ (explicit list per zone) |

**Bridge**: IANA identifier. Storage uses IANA id; Geo.Default provides UI catalog (selectors / display names / country grouping); D2.Shared.Time provides runtime math (offset conversion / DST resolution / ambiguity handling).

**Why both**: the two libs answer fundamentally different questions. Geo.Default answers "what timezone should this UI dropdown show? what's the friendly name in user's language?" NodaTime answers "what's the offset at this specific instant? was 2:30am on this date ambiguous due to DST fall-back?" Conflating them either bloats Geo.Default (NodaTime's tzdb rules engine is ~MB-sized binary data) or strips Geo.Default of its UI catalog (NodaTime exposes IDs but no localized display names + no country grouping + no selectable flag).

**v1 vs v2**:
- V1: stored timezone metadata in EF Core (`old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/TimezoneSeeding.cs` — ~309 entries with IANA id + display name + offset snapshot + country FK). Used BCL `TimeZoneInfo` for runtime math — inconsistent across OS (Windows uses Windows TZ names; Linux uses IANA).
- V2: KEEPS the catalog (in `D2.Shared.Geo.Default`) as the UI / selector / display layer; ADDS NodaTime (in `D2.Shared.Time`) as the runtime rules engine. Also adds: localized display names (11 langs), `Selectable` flag (filter niche zones from dropdowns), `Aliases` list (transparent old→new IANA id mapping).

---

### Decision 11 — Temporal model (D2.Shared.Time — NEW Phase 1 lib, pulled forward from "deferred")

**What**: NEW `D2.Shared.Time` lib in Phase 1. Wraps NodaTime + adds `IClock` + `SystemClock` + `TestClock` + EF Core value converter wiring (via Npgsql.NodaTime plugin) + custom types `ZonedInstant` + `LocalAnchoredEvent`.

Lib contents:
```
D2.Shared.Time
├── Re-exports NodaTime types: Instant, LocalDateTime, LocalDate, LocalTime, ZonedDateTime, DateTimeZone, Period, Duration, OffsetDateTime
├── Custom types:
│   ├── IClock (interface, returns Instant; replaces any IClock-returning-DateTimeOffset patterns)
│   ├── SystemClock (default impl — wraps NodaTime SystemClock.Instance)
│   ├── TestClock (controllable for tests — Now property + Advance(Duration) + SetTo(Instant) methods)
│   ├── ZonedInstant (record: Instant + string IANAIdentifier — Category 1 storage with original context)
│   └── LocalAnchoredEvent (record: LocalDateTime + string IANAIdentifier — Category 3 storage)
├── EF Core value converters (mostly via Npgsql.NodaTime plugin's built-ins)
└── Documentation: PATTERNS.md update + NEW TIMESTAMPS.md authoritative reference
```

**Why NodaTime now, not deferred**:
- User has Category-3 use cases (Notifications scheduling, dkron local-anchored jobs) coming in Phases 4-5 (weeks away, not years)
- Historical-accuracy requirements (invoicing, audit logs) — BCL `DateTime`'s "convert this past timestamp to user's wall-clock" silently uses CURRENT DST rules, not the rules that were in effect AT that past instant. NodaTime gets this right out of the box.
- Establishing the right foundation NOW costs ~1-2 days; retrofitting later (rewriting every `DateTimeOffset.UtcNow` call, every DB schema's timestamp column, every test fixture) is expensive
- Per the user's "no defer without permission" rule, deferring a foundation lib that downstream phases need = silently building debt

**Alternatives rejected**:
- **Stay on BCL `DateTime` / `DateTimeOffset`**. Rejected: same v1 problems carry forward — DST math wrong for historical timestamps, no Category-3 storage shape, Windows ≠ Linux on tz identifier names, no `IClock` abstraction widely adopted.
- **Defer NodaTime to Phase 5 (Notifications)**. Rejected: Notifications already needs it AND Audit already wants it for replay accuracy AND historical invoicing accuracy is a Phase 6+ Files-side concern. Foundation lib goes in Phase 1.
- **Wrap a thinner subset of NodaTime first, expose more later**. Accepted partially — we wrap ONLY the types we need now (`Instant`, `LocalDateTime`, `LocalDate`, `LocalTime`, `ZonedDateTime`, `DateTimeZone`, `Period`, `Duration`, `OffsetDateTime`); expose more as consumers demand (deferred items list).

**v1 vs v2 (temporal)**:
- V1: BCL `DateTime` / `DateTimeOffset` everywhere. No category awareness. `TimeZoneInfo` for conversion (Windows TZ names ≠ IANA on Linux — fragile in mixed-OS dev environments). No `IClock` abstraction widely adopted (handler context exposed something but it wasn't enforced).
- V2: NodaTime `Instant` / `LocalDateTime` / `ZonedDateTime`. Three explicit categories documented per timestamp (see Decision 12). `IClock` injection mandatory. IANA-only zone identifiers. Historical-rule-accurate display OOTB via NodaTime's embedded tzdb.

---

### Decision 12 — Three timestamp categories (the key insight)

**What**: every timestamp in the codebase belongs to ONE of three categories. Storage shape + handling rules differ per category. Documented per-timestamp in xmldoc.

| Category | Examples | Storage shape |
|---|---|---|
| **1. Past instant** | Sign-in event, audit record, file upload, account creation, message sent, invoice creation | `Instant` (UTC) + OPTIONAL IANA tz id for original context |
| **2. Future fixed instant** | JWT exp, idempotency-key TTL, rate-limit window end, session expiry, scheduled-for-UTC dkron job, "fire at 2026-06-15T12:00:00Z" | `Instant` only — NO zone |
| **3. Future local-anchored event** | "9am Edmonton every Tuesday", "weekly digest at 8am user-local", "deliver by 5pm local Friday", recurring calendar event | `LocalDateTime` + IANA tz id (stored as separate columns) + denormalized `Instant nextFireUtc` cache (recomputed when tzdb updates or scheduling changes) |

**PG mapping**:
- Cat 1: `event_at TIMESTAMPTZ` + optional `event_at_zone TEXT NULL`
- Cat 2: `expires_at TIMESTAMPTZ` (no zone)
- Cat 3: `scheduled_local TIMESTAMP` + `scheduled_zone TEXT` + `next_fire_utc TIMESTAMPTZ NULL`

**Sort by absolute time**: always use the `Instant` / `next_fire_utc` column — zone-agnostic ordering, zero ambiguity.

**Ambiguous / skipped local times** (DST transitions, e.g., spring-forward "2:30am" doesn't exist; fall-back "1:30am" exists twice): use NodaTime's `Resolvers.LenientResolver` by default — deterministic, never throws. Codified as a rules.md predicate.

**Why**: the three categories have different operational semantics. Conflating them (e.g., storing "9am every Tuesday Edmonton" as a UTC instant) breaks the second DST transition — the recurring job fires at 8am one Tuesday and 10am the next. Storing Category 1 as `LocalDateTime` makes sorting by-when-it-actually-happened impossible. Storing Category 2 with a zone forces consumers to think about "does this zone affect the meaning?" (it doesn't — JWT exp is a fixed UTC instant; the wall-clock zone of the issuer/holder is irrelevant).

**v1 vs v2 (temporal-category awareness)**:
- V1: no category model. Everything stored as `DateTimeOffset` (a UTC instant + arbitrary offset metadata) or `DateTime` (timezone-naive). Category 3 use cases ("schedule for 9am local Edmonton") had to be hand-rolled with `TimeZoneInfo` conversions at runtime → wrong across DST transitions.
- V2: explicit three-category model. Documented per timestamp in xmldoc. Codified as rules.md §1.25 (Categorize every timestamp at design time) + §1.24 (Use NodaTime types) + §1.26 (Inject IClock).

---

### Decision 13 — Validator-DI pattern (postal codes in Phase 1; phone/email in Phase 2)

**What**:
- `IPostalCodeValidator` interface in Geo.Abstractions or `D2.Shared.Location` (Planner recommends Location — postal validation is tied to address shape)
- `DefaultPostalCodeValidator` with basic country-aware regex (lightweight; covers common formats: US 5/9-digit, CA `A1A 1A1`, UK alphanumeric, EU PLZ, JP, AU, etc.)
- Consumers DI-override with advanced impl (libphonenumber-style integration via a 3rd-party postal validation lib) for richer validation

**Phase 1 ships postal only**. Phone + email validators land in **Phase 2 Contacts** — same pattern; email + phone are contact concerns, not geo.

**Why**: the user wanted lightweight validators with override hooks so production can swap in a more comprehensive validator (e.g., one that calls a 3rd-party API or uses a heavyweight library) without changing call sites. Default impl ships with the lib; consumers override at DI registration time.

**v1 vs v2**:
- V1: no postal validator interface — validation was scattered across handlers + form schemas, country-by-country regex copy-pasted as needed.
- V2: single interface + default impl + DI override pattern. Consumers DI-replace for richer impls.

---

### Decision 14 — Bloat mitigation (TS/browser-bundle strategy)

**Catalog size analysis** (per the earlier sub-agent research + 2026-05-17 expansion):
- All 7 catalogs combined: ~200KB gzipped (with full ~700 CLDR BCP-47 locale set + full ISO 3166-2 subdivisions + GeopoliticalEntity catalog)
- Per-catalog gzipped sizes vary widely: subdivisions ~75 KB; locales ~75-100 KB; countries ~12 KB (lite) / 25 KB (full); GeopoliticalEntities ~3 KB
- Fine for SvelteKit per-route budgets (50-200KB typical — typical pages with selective imports stay well under)

**Mitigations shipped**:
1. **Per-catalog sub-exports**: `@d2/geo-default/countries`, `/subdivisions`, `/timezones`, `/currencies`, `/locales`, `/languages`, `/geopolitical-entities` — tree-shake-friendly entry points.
2. **Lite vs Full shapes** are determined by TS import path:
   - `@d2/geo-default/{entity}` → returns `{Entity}Lite` (only ISO codes for nav refs, no embedded objects). Small browser bundle (~50-60% smaller).
   - `@d2/geo-default/{entity}/full` → returns `{Entity}Full` (denormalized — embedded parent/child objects). Larger bundle.

   **NOT a lookup API concern** — same `byAlpha2(code)` method works for both imports; the return TYPE differs (`CountryLite` vs `CountryFull`) per Pattern B (Decision 6c §"TS modeling").

   **.NET backend**: always returns the Full graph — no lite mode. In-memory pointers are cheap; nav properties are always populated. The C# Country record has only ONE shape (with all navs NON-nullable).

Real impact: typical route with country + timezone selectors → ~8-10KB gzipped instead of ~30KB.

**Why**: SvelteKit per-route bundles are user-facing — every KB shipped to the browser costs latency on first paint. Tree-shake-friendly imports + lite views let consumers pay only for what they actually use. .NET-side this doesn't matter (the DLL is loaded once at process start), so the .NET surface is the full denormalized catalog.

**Deferred**: a `@d2/geo-default-browser` slim variant — deferred unless browser bundle becomes a real problem in practice. The sub-exports + lite views should be enough.

---

### Decision 15 — SvelteKit + TS strategy

**What**:
- `@d2/time` wraps **Temporal API** (Node 22+ native; polyfilled for older browsers via `temporal-polyfill` — ~30KB gzipped)
- `@d2/geo-default` directly importable from SvelteKit SSR (`+page.server.ts` `load()` functions) — static data resolved at build time, NO gRPC calls for selector data
- Cross-language fixture tests assert .NET ↔ TS produce identical instants / wall-clocks for the same inputs (extends 0007 wire-parity pattern — same fixture shape)

**Why Temporal not Luxon/date-fns**:
- Temporal is the standardized successor to JS's broken `Date` API; aligns conceptually with NodaTime on the .NET side (both treat "instant" + "zoned datetime" as distinct types)
- Native in Node 22+; polyfill is well-maintained and tree-shakes well
- Aligns with the three-category storage model (Decision 12) more naturally than Luxon

**Why direct SSR import of `@d2/geo-default`**:
- Selector data is static; baking it into the SSR bundle removes a network round-trip per request
- Matches v2's overall "no Geo service" architecture — there's no service to call

**v1 vs v2**:
- V1: Geo data fetched via gRPC from `d2-geo` service (cached in `Geo.Client` via multi-tier cache). TS-side: SvelteKit consumed via the BFF auth client → `d2-geo` gRPC.
- V2: in-process direct import on both .NET and TS sides. No service, no client lib, no caching layer (data is the constant in the lib).

---

### Decision 16 — Carry-forward / explicitly out of Phase 1 scope

These are NOT defers — they're scope boundaries with documented revisit triggers. Each one is a known future need that doesn't belong in Phase 1.

| Scope-out item | Revisit trigger |
|---|---|
| `LocationFix` (dynamic observed location) — for dispatch use cases (GPS-derived, time-stamped, accuracy-bounded, has source enum {GPS/WiFi/IP/UserEntered/Geocoded}, heading, speed, altitude, mock-detection) | When dispatch service is being built |
| Phone + email validators | Phase 2 Contacts (different domain) |
| what3words integration | REJECTED. Proprietary, licensing risk, niche. Not coming. |
| Altitude on Coordinates | Pending consumer (aerial / drone use case) |
| Second-order subdivision hierarchy (UK counties, Japanese wards, US counties, German Kreise) | Phase 1 first-order only. Revisit when dispatch needs per-county routing |
| Currency historical entries (DEM, ITL, FRF, etc.) | Exclude; revisit if historical-display use case surfaces (then add behind Deprecation pattern) |
| Country name localization beyond endonym (CLDR per-language country-name translation matrix — e.g., `"Allemagne"` for Germany in French) | Defer; endonym covers user's match-on-primary-lang use case |
| Runtime-loadable tzdb | Phase 1 ships bundled snapshot; runtime updates deferred unless faster-than-monthly turnaround needed |
| D2.Shared.Time exposing full NodaTime range | Phase 1 wraps just the types we need; expose more as consumers demand |
| TS `@d2/geo-default-browser` slim variant | Defer unless browser bundle becomes a real problem |

---

### Decision 17 — Cross-cutting changes (outside the 4 libs)

These land in Step 6 of the deliverable; they're not lib code but they're locked PLAN content.

**WhoIs enrichment flow** (downstream consumer pattern, implemented in Phase 3 — Edge):
Edge's WhoIs enrichment middleware will call `ISubdivisionLookup.ResolveByName(upstreamRegion, upstreamCountryCode)` (see Decision 7's "Subdivision additionally" subsection) on the IPinfo-returned `region` + `country` pair using the enhanced cascade name resolution (exact → startsWith → contains → Levenshtein ≤ 2; NFD-normalized; per CHANGE 1a). When resolution succeeds, `RequestContext.SubdivisionISO31662Code` gets populated with `result.ISO31662Code`. When resolution fails (catalog gap, name matches nothing at any pass, unknown country code), `RequestContext.SubdivisionISO31662Code` stays null. The raw upstream IPinfo response (including the unresolved subdivision string) is PRESERVED on the Phase 3 WhoIs entity as part of the cached row — debugging aid and audit trail — but is NEVER propagated to the RequestContext. With the enhanced cascade plus the full ~3,600 ISO 3166-2 catalog, the unresolvable fraction is small (mostly second-order subdivisions ISO 3166-2 doesn't cover, non-administrative geographic regions like "Bay Area" / "New England", or garbage upstream data — none actionable in the app layer).

| Change | Where | Why |
|---|---|---|
| **Drop** `Region` field from `IRequestContext.spec.json` (no rename — entire field removed) | `contracts/context/IRequestContext.spec.json` | Per Decision 2 (bare `Region` clashes with UN M49 / EU / NATO / AWS regions) AND per the user-approved drop framing (see "Intentional drops from v1 (with rationale)" section). With the enhanced ResolveByName cascade + full ~3,600 ISO 3166-2 catalog, resolution rate is very high; unresolvable upstream names aren't actionable in the application layer. `RequestContext` carries ONLY `SubdivisionISO31662Code` (nullable when WhoIs name unresolvable). Audit preservation lives on the Phase 3 WhoIs entity (raw IPinfo response cached including unresolved subdivision strings — debugging aid) — not on RequestContext. This SUPERSEDES the earlier rename decision (5a / III). |
| New section: Reference Data (Deprecation pattern + endonyms + lookup split semantics) | `docs/PATTERNS.md` | Per CLAUDE.md §3.5 Doc Update Map — Geo lib patterns belong in PATTERNS.md |
| **NEW subsection: Reference Data Philosophy** | `docs/PATTERNS.md` (new subsection added in Step 6) | Enshrines the user-stated principle: *"De-facto currencies are more important than politically-correct ones."* Drives Country.Currencies M:M classification (LegalTender vs WidelyAccepted vs Tourist) — see Decision 6 / Country.Currencies. Same principle generalizes to other ref data: catalog reality, not aspirational reality. |
| New section: Hash Composition (versioned + multi-component slot rule + normalization) | `docs/PATTERNS.md` | Codifies Decisions 4 + 5 for consumers |
| NEW doc: `docs/TIMESTAMPS.md` | `docs/TIMESTAMPS.md` (new file) | Authoritative temporal reference — full 3-category model + worked examples + Npgsql.NodaTime mapping notes + ambiguity / skip resolver discussion. Per CLAUDE.md §3.5, temporal categories deserve their own doc. |
| **NEW spec: `contracts/geo/selectable-locales.spec.json`** | `contracts/geo/selectable-locales.spec.json` (new file) | Drives `Locale.IsSelectable=true` for the **18 curated UI-dropdown locales** (per items 1b + 1c + the 2026-05-17 expansion). Codegen reads this spec, sets the `IsSelectable` flag on matching Locales, and enforces the build-time `contracts/messages/{locale}.json` Paraglide message file presence (per §1.32 predicate). Shape: `{ "selectable": ["en-US","en-CA","en-GB","en-AU","en-NZ","es-ES","es-MX","fr-FR","fr-CA","de-DE","it-IT","ja-JP","nl-NL","nl-BE","ko-KR","zh-TW","pt-PT","pl-PL"] }`. |
| **NEW spec: `contracts/geo/country-currencies-overrides.spec.json`** | `contracts/geo/country-currencies-overrides.spec.json` (new file) | Manual de-facto currency acceptance overrides on top of CLDR + ISO 4217 legal-tender data (per items 2a-2c). Source-of-truth for `CurrencyAcceptanceLevel.WidelyAccepted` + `Tourist` classifications (CLDR cannot represent these). Shape: `{ "AR": [{ "currency": "USD", "level": "WidelyAccepted" }], "CH": [{ "currency": "EUR", "level": "WidelyAccepted" }], "LB": [{ "currency": "USD", "level": "WidelyAccepted" }], "ZW": [{ "currency": "USD", "level": "WidelyAccepted" }], "HK": [{ "currency": "CNY", "level": "WidelyAccepted" }], "KH": [{ "currency": "USD", "level": "WidelyAccepted" }], ... }`. Codegen merges with CLDR-derived LegalTender data to produce final `Country.Currencies` arrays. |
| Add row to CLAUDE.md §3.5 Doc Update Map | `CLAUDE.md` §3.5 | Map "any timestamp / temporal work" → `docs/TIMESTAMPS.md` |
| Update V2.md Phase 1 row | `docs/v2/V2.md` §4 | Enrich Phase 1 row with the 4-lib enumeration + reference to PHASE_1.md |
| Update V2.md §5.4 if needed | `docs/v2/V2.md` §5.4 | If the `Region` drop (per CHANGE 2 — entire field removed; SUPERSEDES earlier rename decision) touches anything in §5.4 fingerprint design |

---

### Decision 18 — Step ordering

8 steps, ordered for dependency satisfaction + early-feedback on the foundation libs:

| # | Step | Notes |
|---|---|---|
| 0 | Branch checkout `n/geo-libs` off clean `nova` @ `66e41f0a` | Per the "Plans must include branch checkout as Step 0" feedback codified in memory |
| 1 | `D2.Shared.Time` (NodaTime wrapper + IClock + SystemClock + TestClock + Npgsql.NodaTime EF config) + `@d2/time` (Temporal wrapper) + parity tests | Foundation; nothing else depends on it within the deliverable but Phases 4-5 will |
| 2 | `D2.Shared.Geo.Abstractions` (types + lookup interfaces + DeprecationInfo) + `@d2/geo-abstractions` | Zero-dep foundation for Default + Location |
| 3 | Spec files in `contracts/geo/` (countries.spec.json, subdivisions.spec.json, currencies.spec.json, languages.spec.json, locales.spec.json, timezones.spec.json) + .NET SourceGen analyzer + TS emitter | Codegen infrastructure shared by Step 4 |
| 4 | `D2.Shared.Geo.Default` (codegen'd lookups) + `@d2/geo-default` + sub-exports for tree-shake | Consumes spec + codegen from Step 3 |
| 5 | `D2.Shared.Location` (3 VOs + composition function + IPostalCodeValidator interface + DefaultPostalCodeValidator) + `@d2/location` | Depends on Abstractions only (not Default) |
| 6 | Cross-cutting: DROP `Region` field from IRequestContext spec (entire field removed — no rename; per CHANGE 2 supersedes earlier rename decision); PATTERNS.md + TIMESTAMPS.md doc updates; rules.md predicate drafts; CLAUDE.md §3.5 Doc Update Map row; V2.md Phase 1 row enrichment | Per Decision 17 |
| 7 | Final-review (K=5 + Aggregator per audit-framework.md §3a-c) + Deliverable Completeness Checklist | Per CLAUDE.md MANDATORY block 3 |
| 8 | SHIP → squash merge `n/geo-libs` → `nova` | Per user direction + workflow.md §SHIP |

---

## Data contract examples (full C# + TS shapes per entity)

Per the user directive: every value object, every entity, every denormalized lookup return shape must be visible ahead of time. The following examples use real-world data (US, California, USD, en-US, America/Edmonton, EU, NATO, etc.) so consumers can visualize the actual shape they'll consume.

For each entity below: **(1) C# record + xmldoc**, **(2) TS interface (camelCase per TS convention)**, **(3) JSON spec entry** (what gets committed to `contracts/geo/*.spec.json`), **(4) what consumers get from `.full` lookup** (denormalized — parent FKs resolved as embedded objects), **(5) what consumers get from `.lite` lookup** where applicable (FKs stay as ISO codes), **(6) v1 reference**.

---

### Country

**C# record** (`D2.Shared.Geo.Abstractions/Country.cs`):
```csharp
namespace D2.Shared.Geo.Abstractions;

/// <summary>
/// A sovereign country or territory in ISO 3166-1.
/// Catalog source: CLDR; carry-forward + enrichment of v1 (CountrySeeding.cs:34-44).
/// </summary>
public sealed record Country
{
    // --- identity ---
    /// <summary>Primary key. ISO 3166-1 alpha-2 (e.g., "US", "DE", "JP").</summary>
    public required string ISO31661Alpha2Code { get; init; }
    /// <summary>ISO 3166-1 alpha-3 (e.g., "USA", "DEU", "JPN").</summary>
    public required string ISO31661Alpha3Code { get; init; }
    /// <summary>ISO 3166-1 numeric (e.g., "840", "276", "392").</summary>
    public required string ISO31661NumericCode { get; init; }

    // --- naming ---
    public required string DisplayName { get; init; }       // English short ("United States")
    public required string OfficialName { get; init; }      // English official ("United States of America")
    public required string EndonymDisplayName { get; init; } // Native short ("United States" for US; "日本" for JP)
    public required string EndonymOfficialName { get; init; }// Native official ("United States of America"; "日本国")

    // --- phone metadata (build-time from libphonenumber; NO runtime dep) ---
    public required string PhoneNumberPrefix { get; init; }            // "1", "49", "81"
    public required string PhoneNumberNationalFormat { get; init; }    // "(NNN) NNN-NNNN"
    public required int PhoneNumberMinDigits { get; init; }            // 10
    public required int PhoneNumberMaxDigits { get; init; }            // 10

    // --- v2 CLDR enrichments (per Decision 6b) ---
    public required DayOfWeek FirstDayOfWeek { get; init; }         // US: Sunday
    public required DayOfWeek WeekendStart { get; init; }           // US: Saturday
    public required DayOfWeek WeekendEnd { get; init; }             // US: Sunday
    public required MeasurementSystem MeasurementSystem { get; init; } // US: Imperial

    // --- foreign keys (lite shape: stay as ISO codes) ---
    public required string PrimaryLanguageISO6391Code { get; init; }   // "en" — ALWAYS NON-NULL (true linguistic fact regardless of support; per Decision 6c)
    public required string PrimaryCurrencyISO4217AlphaCode { get; init; }// "USD"
    /// <summary>ALWAYS NON-NULL per the PrimaryLocale + IsSelectable principle (Decision 6 boxed rule + Decision 6c).
    /// Every country has its true majority-population primary locale set per CLDR; the locale always exists in the ~700-entry catalog.
    /// Consumers check `Country.PrimaryLocale.IsSelectable` to decide whether to actually serve UI in that locale.</summary>
    public required string PrimaryLocaleIETFBCP47Tag { get; init; }    // "en-US" (always set)
    /// <summary>For territories. Intentional ISO-suffix-with-target-type rename of v1's bare SovereignISO31661Alpha2Code (Decision 2).
    /// Null for sovereigns; "US" for PR, "FI" for AX (Åland), etc.</summary>
    public string? SovereignCountryISO31661Alpha2Code { get; init; }

    // --- denormalized FK navigation (.NET: ALWAYS populated — full denormalized graph; navs are non-nullable pointers into the in-memory catalog) ---
    /// <summary>Non-nullable: every country has its true majority-population primary language set per CLDR.</summary>
    public required Language PrimaryLanguage { get; init; }
    /// <summary>Non-nullable: every country has its primary currency set.</summary>
    public required Currency PrimaryCurrency { get; init; }
    /// <summary>Non-nullable per Decision 6c: every country has its true majority-population primary locale set per CLDR.
    /// Always populated against the in-memory catalog. Check `PrimaryLocale.IsSelectable` for UX behavior per Decision 6c.</summary>
    public required Locale PrimaryLocale { get; init; }
    /// <summary>NULLABLE — real-world fact: most countries have no sovereign (US, FR, JP, etc. = null);
    /// only territories carry a non-null SovereignCountry (PR → US, AX → FI, etc.).</summary>
    public Country? SovereignCountry { get; init; }

    // --- M:M nav (.NET: non-nullable lists — empty if none, never null) ---
    public required IReadOnlyList<string> GeopoliticalEntityShortCodes { get; init; }
    public required IReadOnlyList<GeopoliticalEntity> GeopoliticalEntities { get; init; }
    public required IReadOnlyList<string> SubdivisionISO31662Codes { get; init; }
    public required IReadOnlyList<Subdivision> Subdivisions { get; init; }
    public required IReadOnlyList<string> TimezoneIANAIdentifiers { get; init; }
    public required IReadOnlyList<Timezone> Timezones { get; init; }

    // --- RESTORED from v1 (items 2a + 2b + 3a + 4a — see §1.31 meta-observation) ---

    /// <summary>v1-RESTORE: M:M with acceptance classification. All currencies legal-or-de-facto in this country.
    /// Non-nullable list (empty allowed). Ordered: PrimaryCurrency first, then by descending acceptance.
    /// See CountryCurrencyAcceptance + CurrencyAcceptanceLevel.
    /// De-facto over politically-correct (e.g., AR includes USD as WidelyAccepted; CH includes EUR; LB includes USD; ZW includes USD; HK includes CNY).</summary>
    public required IReadOnlyList<CountryCurrencyAcceptance> Currencies { get; init; }

    /// <summary>v1-RESTORE: all locales' BCP-47 tags spoken in this country (selectable + non-selectable). Includes PrimaryLocale.
    /// Ordered: PrimaryLocale first, then by descending speaker population.</summary>
    public required IReadOnlyList<string> LocaleIETFBCP47Tags { get; init; }

    /// <summary>v1-RESTORE: denormalized embedded Locale records. Non-nullable list.</summary>
    public required IReadOnlyList<Locale> Locales { get; init; }

    /// <summary>v1-RESTORE: inverse-nav of SovereignCountryISO31661Alpha2Code — countries for which THIS country is sovereign.
    /// Derived at codegen time from existing sovereign FK data. Empty list for most countries; non-empty for US (PR,GU,...), FR (GP,MQ,...), UK (IM,JE,GG,...), FI (AX), DK (FO,GL), NL (AW,CW,SX), NZ (CK,NU,TK), etc.</summary>
    public required IReadOnlyList<string> TerritoryISO31661Alpha2Codes { get; init; }

    /// <summary>v1-RESTORE: denormalized embedded Country records for territories. Non-nullable list (empty if no territories).</summary>
    public required IReadOnlyList<Country> Territories { get; init; }

    // --- deprecation ---
    public DeprecationInfo? Deprecation { get; init; }
}

// CountryCurrencyAcceptance + CurrencyAcceptanceLevel definitions (see Decision 6 / Country.Currencies)
public sealed record CountryCurrencyAcceptance
{
    public required string ISO4217AlphaCode { get; init; }
    public required CurrencyAcceptanceLevel Level { get; init; }
    /// <summary>Non-nullable in .NET — denormalized embedded Currency record (always populated from in-memory catalog).
    /// TS Lite-vs-Full split lives in CountryCurrencyAcceptanceLite (no Currency field) + CountryCurrencyAcceptanceFull (has Currency).</summary>
    public required Currency Currency { get; init; }
}

public enum CurrencyAcceptanceLevel { LegalTender, WidelyAccepted, Tourist }
```

**TS interfaces** (Pattern B — separate Lite + Full per Decision 14; `@d2/geo-abstractions/src/country.ts`):

```typescript
// LITE shape — from @d2/geo-default/countries
// Only ISO codes / FKs as strings; NO embedded nav objects. Small browser bundle.
export interface CountryLite {
  readonly iso31661Alpha2Code: string;
  readonly iso31661Alpha3Code: string;
  readonly iso31661NumericCode: string;
  readonly displayName: string;
  readonly officialName: string;
  readonly endonymDisplayName: string;
  readonly endonymOfficialName: string;
  readonly phoneNumberPrefix: string;
  readonly phoneNumberNationalFormat: string;
  readonly phoneNumberMinDigits: number;
  readonly phoneNumberMaxDigits: number;
  readonly firstDayOfWeek: DayOfWeek;
  readonly weekendStart: DayOfWeek;
  readonly weekendEnd: DayOfWeek;
  readonly measurementSystem: MeasurementSystem;
  readonly primaryLanguageISO6391Code: string;
  readonly primaryCurrencyISO4217AlphaCode: string;
  readonly primaryLocaleIETFBCP47Tag: string;
  readonly sovereignCountryISO31661Alpha2Code: string | null;  // real-world nullable
  // FK code arrays (no embedded objects on lite):
  readonly geopoliticalEntityShortCodes: readonly string[];
  readonly subdivisionISO31662Codes: readonly string[];
  readonly timezoneIANAIdentifiers: readonly string[];
  readonly currencies: readonly CountryCurrencyAcceptanceLite[];  // with acceptance level (LegalTender / WidelyAccepted / Tourist)
  readonly localeIETFBCP47Tags: readonly string[];
  readonly territoryISO31661Alpha2Codes: readonly string[];
  readonly deprecation: DeprecationInfo | null;
}

// FULL shape — from @d2/geo-default/countries/full
// Embedded nav objects all NON-OPTIONAL (denormalized graph; larger bundle).
// Cycle-break: nested navs back to Country use CountryLite (not CountryFull) to prevent infinite type recursion.
export interface CountryFull extends CountryLite {
  readonly primaryLanguage: Language;          // NON-optional
  readonly primaryCurrency: Currency;          // NON-optional
  readonly primaryLocale: LocaleFull;          // NON-optional
  readonly sovereignCountry: CountryLite | null;  // nullable (real-world); CountryLite to break cycle
  readonly geopoliticalEntities: readonly GeopoliticalEntityFull[];   // NON-optional (empty if none)
  readonly subdivisions: readonly SubdivisionFull[];                  // NON-optional
  readonly timezones: readonly TimezoneFull[];                        // NON-optional
  readonly currencies: readonly CountryCurrencyAcceptanceFull[];      // NON-optional
  readonly locales: readonly LocaleFull[];                            // NON-optional
  readonly territories: readonly CountryLite[];                       // NON-optional (empty if no territories); CountryLite to break cycle
}

// CountryCurrencyAcceptance — two shapes (lite has no embedded Currency; full does)
export interface CountryCurrencyAcceptanceLite {
  readonly iso4217AlphaCode: string;
  readonly level: "LegalTender" | "WidelyAccepted" | "Tourist";
}

export interface CountryCurrencyAcceptanceFull extends CountryCurrencyAcceptanceLite {
  readonly currency: Currency;  // NON-optional on full
}
```

**JSON spec entry** (`contracts/geo/countries.spec.json` — single entry):
```json
{
  "iso31661Alpha2Code": "US",
  "iso31661Alpha3Code": "USA",
  "iso31661NumericCode": "840",
  "displayName": "United States",
  "officialName": "United States of America",
  "endonymDisplayName": "United States",
  "endonymOfficialName": "United States of America",
  "phoneNumberPrefix": "1",
  "phoneNumberNationalFormat": "(NNN) NNN-NNNN",
  "phoneNumberMinDigits": 10,
  "phoneNumberMaxDigits": 10,
  "firstDayOfWeek": "Sunday",
  "weekendStart": "Saturday",
  "weekendEnd": "Sunday",
  "measurementSystem": "Imperial",
  "primaryLanguageISO6391Code": "en",
  "primaryCurrencyISO4217AlphaCode": "USD",
  "primaryLocaleIETFBCP47Tag": "en-US",
  "_primaryLocaleIETFBCP47Tag_note": "ALWAYS set per Decision 6c data-vs-selectability principle — US has en-US (IsSelectable=true); BR/RU/CH/AR/HK/IN etc. also always have their true primary locale set (pt-BR / ru-RU / de-CH / es-AR / zh-HK / hi-IN) with IsSelectable=false. Consumers check PrimaryLocale.IsSelectable for UX availability.",
  "sovereignCountryISO31661Alpha2Code": null,
  "geopoliticalEntityShortCodes": ["NA", "NATO", "UN", "G7", "G20", "OECD", "USMCA", "FVEY", "QUAD", "CPTPP"],
  "subdivisionISO31662Codes": ["US-AL", "US-AK", "US-AZ", "US-AR", "US-CA", "..."],
  "timezoneIANAIdentifiers": ["America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles", "..."],
  "currencies": [
    { "iso4217AlphaCode": "USD", "level": "LegalTender" }
  ],
  "localeIETFBCP47Tags": ["en-US", "es-US"],
  "territoryISO31661Alpha2Codes": ["PR", "GU", "VI", "AS", "MP"],
  "deprecation": null
}
```

**.NET return** — `countryLookup.ByAlpha2("US")` always returns the full denormalized graph: `PrimaryCurrency` / `PrimaryLocale` / `PrimaryLanguage` / `SovereignCountry` populated as full embedded records AND `GeopoliticalEntities` / `Subdivisions` / `Timezones` / `Territories` / `Locales` arrays populated as full embedded records. See "Denormalized return shapes" section below for the full worked example. Cost is just pointer dereferences (in-memory denormalized catalog loaded once at lib import).

**TS return shape** (same API; populated nav fields differ by import path):
- `import { CountryLookup } from "@d2/geo-default/countries"` (lite) → `countryLookup.byAlpha2("US")` returns the record with FK codes populated (`primaryCurrencyISO4217AlphaCode = "USD"`, `geopoliticalEntityShortCodes = ["NA","NATO","UN",...]`); embedded nav props (`primaryCurrency`, `geopoliticalEntities`, etc.) stay undefined.
- `import { CountryLookup } from "@d2/geo-default/countries/full"` (denormalized) → same API call returns the record with both FK codes AND embedded nav populated.

**v1 reference**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/CountrySeeding.cs:34-44` (base shape — no endonyms, no phone metadata, no CLDR enrichments, no Deprecation, no denormalized arrays).

---

### Subdivision

**C# record**:
```csharp
public sealed record Subdivision
{
    public required string ISO31662Code { get; init; }       // "US-CA"
    public required string ShortCode { get; init; }          // "CA"
    public required string DisplayName { get; init; }        // "California"
    public required string OfficialName { get; init; }       // "State of California"
    public required string EndonymDisplayName { get; init; } // "California" (= DisplayName for en-primary; "Bayern" for DE-BY; "東京都" for JP-13)
    public required string EndonymOfficialName { get; init; }// "State of California"
    public required string CountryISO31661Alpha2Code { get; init; }  // "US" (FK)
    public required Country Country { get; init; }            // non-nullable in .NET — always populated from in-memory catalog
    public DeprecationInfo? Deprecation { get; init; }
}
```

**TS interfaces** (Pattern B — separate Lite + Full):
```typescript
// LITE — from @d2/geo-default/subdivisions
export interface SubdivisionLite {
  readonly iso31662Code: string;
  readonly shortCode: string;
  readonly displayName: string;
  readonly officialName: string;
  readonly endonymDisplayName: string;
  readonly endonymOfficialName: string;
  readonly countryISO31661Alpha2Code: string;
  readonly deprecation: DeprecationInfo | null;
}

// FULL — from @d2/geo-default/subdivisions/full
// Country nav is CountryLite (cycle-break — CountryFull would recurse via subdivisions[]).
export interface SubdivisionFull extends SubdivisionLite {
  readonly country: CountryLite;  // NON-optional; CountryLite to break cycle
}
```

**JSON spec entry** (`contracts/geo/subdivisions.spec.json`):
```json
{
  "iso31662Code": "US-CA",
  "shortCode": "CA",
  "displayName": "California",
  "officialName": "State of California",
  "endonymDisplayName": "California",
  "endonymOfficialName": "State of California",
  "countryISO31661Alpha2Code": "US",
  "deprecation": null
}
```

**.NET return** — `subdivisionLookup.ByISO31662Code("US-CA")` always returns the record with `Country` populated as the full embedded US Country record (in-memory denormalized graph; no extra cost).

**TS return shape**: lite import path leaves `country` undefined and only populates `countryISO31661Alpha2Code = "US"`; `/full` import path populates both. See "Lite vs Full" subsection in Decision 7.

**v1 reference**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/SubdivisionSeeding.cs:36-43` (no endonyms, no Deprecation).

---

### Currency

**C# record**:
```csharp
public sealed record Currency
{
    public required string ISO4217AlphaCode { get; init; }     // "USD"
    public required string ISO4217NumericCode { get; init; }   // "840"
    public required string DisplayName { get; init; }          // "US Dollar"
    public required string OfficialName { get; init; }         // "United States Dollar"
    public required int DecimalPlaces { get; init; }           // 2 (JPY: 0; BHD: 3)
    public required string Symbol { get; init; }               // "$" (Unicode; "€", "¥", "£")

    /// <summary>
    /// True if we ship UI display + billing-presentation translations + formatting for this currency.
    /// False for catalog-only currencies (~169 unsupported active currencies).
    /// Per Decision 6c data-vs-selectability principle: the catalog ships all ~180 ISO 4217 active currencies;
    /// the 11 supported (USD, CAD, GBP, AUD, NZD, EUR, MXN, JPY, KRW, TWD, PLN — derived from the 18 supported
    /// locales' primary countries) have IsSupported=true.
    /// </summary>
    public required bool IsSupported { get; init; }

    public DeprecationInfo? Deprecation { get; init; }
}
```

**TS interface** (no Lite/Full split — Currency has no nav properties):
```typescript
export interface Currency {
  readonly iso4217AlphaCode: string;
  readonly iso4217NumericCode: string;
  readonly displayName: string;
  readonly officialName: string;
  readonly decimalPlaces: number;
  readonly symbol: string;
  readonly isSupported: boolean;   // true for 11 supported (UI + billing presentation); false for ~169 catalog-only
  readonly deprecation: DeprecationInfo | null;
}
```

**JSON spec entry** (`contracts/geo/currencies.spec.json`):
```json
{
  "iso4217AlphaCode": "USD",
  "iso4217NumericCode": "840",
  "displayName": "US Dollar",
  "officialName": "United States Dollar",
  "decimalPlaces": 2,
  "symbol": "$",
  "isSupported": true,
  "deprecation": null
}
```

Currency is flat (no FKs to other reference data); a single interface serves both import paths — no Lite/Full split needed.

**v1 reference**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/CurrencySeeding.cs:36-43`.

---

### Language

**C# record**:
```csharp
public sealed record Language
{
    public required string ISO6391Code { get; init; }              // "en"
    public required string Name { get; init; }                     // "English"
    public required string Endonym { get; init; }                  // "English" (en); "日本語" (ja)
    /// <summary>Writing direction (v2 enrichment per Decision 6b). en/es/fr/de/ja/nl/ko/zh/pt/pl/it = LTR; ar/he/fa/ur = RTL.</summary>
    public required WritingDirection WritingDirection { get; init; }
    /// <summary>
    /// True if we ship UI translations for this language (at least one Locale with IsSelectable=true exists).
    /// False for catalog-only languages (no translation files exist). Per Decision 6c data-vs-selectability principle:
    /// catalog ships all ~180 ISO 639-1 languages; the 11 supported (en, es, fr, de, it, ja, nl, ko, zh, pt, pl) have IsSupported=true.
    /// </summary>
    public required bool IsSupported { get; init; }
    public DeprecationInfo? Deprecation { get; init; }
}

public enum WritingDirection { LTR = 0, RTL = 1 }
```

**TS interface**:
```typescript
export interface Language {
  readonly iso6391Code: string;
  readonly name: string;
  readonly endonym: string;
  readonly writingDirection: "LTR" | "RTL";
  readonly isSupported: boolean;   // true for 11 supported (translation files exist); false for ~169 catalog-only
  readonly deprecation: DeprecationInfo | null;
}
```

**JSON spec entries** (`contracts/geo/languages.spec.json`) — showing both an `IsSupported=true` (English) and an `IsSupported=false` (Vietnamese) example:
```json
{
  "iso6391Code": "en",
  "name": "English",
  "endonym": "English",
  "writingDirection": "LTR",
  "isSupported": true,
  "deprecation": null
}
```
```json
{
  "iso6391Code": "vi",
  "name": "Vietnamese",
  "endonym": "Tiếng Việt",
  "writingDirection": "LTR",
  "isSupported": false,
  "deprecation": null
}
```

**v1 reference**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/LanguageSeeding.cs:35-40` (no `WritingDirection`, no `IsSupported`, no Deprecation; v1 had 6 entries hand-curated, v2 has ~180 full ISO 639-1 catalog).

---

### Locale

**C# record**:
```csharp
public sealed record Locale
{
    public required string IETFBCP47Tag { get; init; }                 // "en-US"
    public required string Name { get; init; }                         // "English (United States)"
    public required string Endonym { get; init; }                      // "English (United States)" (en-US); "日本語 (日本)" (ja-JP)
    public required string LanguageISO6391Code { get; init; }          // "en"
    public required string CountryISO31661Alpha2Code { get; init; }    // "US"
    /// <summary>v2 NEW: true for the 18 curated UI-dropdown locales (per 2026-05-17 expansion); false for non-selectable variants
    /// (fr-CI / de-CH / it-CH / zh-CN / pt-BR / ru-RU / etc. from the full ~700 CLDR catalog).
    /// Codegen-enforced — IsSelectable=true REQUIRES contracts/messages/{IETFBCP47Tag}.json to exist (build fails on mismatch).
    /// Drives Paraglide message file presence + language-picker UI source-of-truth.</summary>
    public required bool IsSelectable { get; init; }                   // en-US/nl-BE/ko-KR/zh-TW/pt-PT/pl-PL: true; fr-CI/zh-CN/pt-BR/ru-RU: false
    // v2 CLDR enrichments (per Decision 6b):
    public required DayOfWeek FirstDayOfWeek { get; init; }            // en-US: Sunday
    public required string DecimalSeparator { get; init; }             // en-US: "."; de-DE: ","; fr-FR: ","
    public required string ThousandsSeparator { get; init; }           // en-US: ","; de-DE: "."; fr-FR: " " (NBSP)
    public required DateFormatPattern DateFormatPattern { get; init; } // en-US: MDY; de-DE: DMY; ja-JP: YMD
    // denormalized FK navigation (.NET: non-nullable; always populated from in-memory catalog):
    public required Language Language { get; init; }
    public required Country Country { get; init; }
    public DeprecationInfo? Deprecation { get; init; }
}

public enum DateFormatPattern { DMY = 0, MDY = 1, YMD = 2 }
```

**TS interfaces** (Pattern B — separate Lite + Full):
```typescript
// LITE — from @d2/geo-default/locales
export interface LocaleLite {
  readonly ietfBCP47Tag: string;
  readonly name: string;
  readonly endonym: string;
  readonly languageISO6391Code: string;
  readonly countryISO31661Alpha2Code: string;
  readonly isSelectable: boolean;       // v2 NEW: true for the 18 selectable (covering 11 languages); false for non-selectable variants from the full ~700 CLDR catalog
  readonly firstDayOfWeek: DayOfWeek;
  readonly decimalSeparator: string;
  readonly thousandsSeparator: string;
  readonly dateFormatPattern: "DMY" | "MDY" | "YMD";
  readonly deprecation: DeprecationInfo | null;
}

// FULL — from @d2/geo-default/locales/full
// Country nav is CountryLite (cycle-break — CountryFull would recurse via locales[]).
export interface LocaleFull extends LocaleLite {
  readonly language: Language;        // NON-optional
  readonly country: CountryLite;      // NON-optional; CountryLite to break cycle
}
```

**JSON spec entry** (`contracts/geo/locales.spec.json`):
```json
{
  "ietfBCP47Tag": "en-US",
  "name": "English (United States)",
  "endonym": "English (United States)",
  "languageISO6391Code": "en",
  "countryISO31661Alpha2Code": "US",
  "isSelectable": true,
  "firstDayOfWeek": "Sunday",
  "decimalSeparator": ".",
  "thousandsSeparator": ",",
  "dateFormatPattern": "MDY",
  "deprecation": null
}
```

**.full return** — embeds full `Language` (English with `WritingDirection=LTR`) + full `Country` (US with all its enrichments + GE arrays).

**v1 reference**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/LocaleSeeding.cs:38-45` (no `IsSelectable`, no CLDR enrichments, no Deprecation, no embedded nav). V1 selectability was driven by THREE out-of-band sources that silently drifted: `PUBLIC_ENABLED_LOCALES` env var, Paraglide `settings.json`, and Paraglide message file presence. V2 collapses to ONE source-of-truth: `Locale.IsSelectable` with codegen-enforced consistency.

---

### Timezone

**C# record**:
```csharp
public sealed record Timezone
{
    public required string IANAIdentifier { get; init; }                  // "America/Edmonton"
    public required string DisplayName { get; init; }                     // "Mountain Time — Edmonton"
    /// <summary>NEW in v2: localized names keyed by ISO 639-1.</summary>
    public required IReadOnlyDictionary<string, string> LocalizedDisplayNames { get; init; }
    public required int CurrentStdOffsetMinutes { get; init; }            // -420 (MST = UTC-7)
    public int? CurrentDstOffsetMinutes { get; init; }                    // -360 (MDT = UTC-6); null for no-DST zones
    public required string CurrentStdAbbrev { get; init; }                // "MST"
    public string? CurrentDstAbbrev { get; init; }                        // "MDT"
    public required string CountryISO31661Alpha2Code { get; init; }       // "CA"
    public required Country Country { get; init; }                        // non-nullable in .NET — always populated from in-memory catalog
    /// <summary>NEW: true for UI dropdown inclusion (~150-200 main zones); false for niche (Antarctica/Vostok, etc.).</summary>
    public required bool Selectable { get; init; }
    /// <summary>NEW: old IANA ids that map to this canonical (e.g., "Asia/Saigon" → "Asia/Ho_Chi_Minh").</summary>
    public required IReadOnlyList<string> Aliases { get; init; }
    public DeprecationInfo? Deprecation { get; init; }
}
```

**TS interfaces** (Pattern B — separate Lite + Full):
```typescript
// LITE — from @d2/geo-default/timezones
export interface TimezoneLite {
  readonly ianaIdentifier: string;
  readonly displayName: string;
  readonly localizedDisplayNames: Readonly<Record<string, string>>;
  readonly currentStdOffsetMinutes: number;
  readonly currentDstOffsetMinutes: number | null;
  readonly currentStdAbbrev: string;
  readonly currentDstAbbrev: string | null;
  readonly countryISO31661Alpha2Code: string;
  readonly selectable: boolean;
  readonly aliases: readonly string[];
  readonly deprecation: DeprecationInfo | null;
}

// FULL — from @d2/geo-default/timezones/full
// Country nav is CountryLite (cycle-break — CountryFull would recurse via timezones[]).
export interface TimezoneFull extends TimezoneLite {
  readonly country: CountryLite;  // NON-optional; CountryLite to break cycle
}
```

**JSON spec entry** (`contracts/geo/timezones.spec.json`):
```json
{
  "ianaIdentifier": "America/Edmonton",
  "displayName": "Mountain Time — Edmonton",
  "localizedDisplayNames": {
    "en": "Mountain Time — Edmonton",
    "es": "Hora de las Montañas — Edmonton",
    "fr": "Heure des Rocheuses — Edmonton",
    "de": "Mountain Time — Edmonton",
    "it": "Ora delle Montagne — Edmonton",
    "ja": "山岳部時間 — エドモントン",
    "nl": "Mountain Time — Edmonton",
    "ko": "산악 시간 — 에드먼턴",
    "zh": "山區時間 — 愛德蒙頓",
    "pt": "Hora das Montanhas — Edmonton",
    "pl": "Czas górski — Edmonton"
  },
  "currentStdOffsetMinutes": -420,
  "currentDstOffsetMinutes": -360,
  "currentStdAbbrev": "MST",
  "currentDstAbbrev": "MDT",
  "countryISO31661Alpha2Code": "CA",
  "selectable": true,
  "aliases": [],
  "deprecation": null
}
```

**v1 reference**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Infra/Repository/Seeding/TimezoneSeeding.cs:36-43` (offsets as strings `"-07:00"` not integer minutes; no localized names; no aliases; no Selectable; no Deprecation).

---

### GeopoliticalEntity

Already shown in full (C#) under Decision 6a above. Summary: `ShortCode` PK, `Name`, `Type` (23-value enum across 4 region-tagged categories), `CountryISO31661Alpha2Codes` (lite M:M) / `Countries` (full embedded), `Deprecation?`. 59 seed entries.

**TS interfaces** (Pattern B — separate Lite + Full):
```typescript
// LITE — from @d2/geo-default/geopolitical-entities
export interface GeopoliticalEntityLite {
  readonly shortCode: string;
  readonly name: string;
  readonly type: GeopoliticalEntityType;  // string union mirroring the C# enum
  readonly countryISO31661Alpha2Codes: readonly string[];
  readonly deprecation: DeprecationInfo | null;
}

// FULL — from @d2/geo-default/geopolitical-entities/full
// Countries nav is CountryLite[] (cycle-break — CountryFull would recurse via geopoliticalEntities[]).
export interface GeopoliticalEntityFull extends GeopoliticalEntityLite {
  readonly countries: readonly CountryLite[];  // NON-optional (empty if none); CountryLite to break cycle
}
```

**v1 reference**: entity `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/GeopoliticalEntity.cs`; enum `Geo.Domain/Enums/GeopoliticalEntityType.cs`; seed `Geo.Infra/Repository/Seeding/GeopoliticalEntitySeeding.cs`; M:M join seed `CountryGeopoliticalEntitySeeding.cs`.

---

### DeprecationInfo

**C# record** (already shown in Decision 8; full shape here for completeness):
```csharp
public sealed record DeprecationInfo
{
    public required DateOnly DeprecatedAt { get; init; }
    public required string Reason { get; init; }
    public IReadOnlyList<string>? SupersededBy { get; init; }
    public string? SuccessorNote { get; init; }
}
```

**TS interface**:
```typescript
export interface DeprecationInfo {
  readonly deprecatedAt: string;       // ISO 8601 date: "2003-06-04"
  readonly reason: string;
  readonly supersededBy: readonly string[] | null;
  readonly successorNote: string | null;
}
```

**JSON spec entry** (embedded on a parent entity, e.g., a hypothetical retired country `YU`):
```json
{
  "iso31661Alpha2Code": "YU",
  "displayName": "Yugoslavia",
  "deprecation": {
    "deprecatedAt": "2003-06-04",
    "reason": "Dissolved into successor states",
    "supersededBy": ["RS", "ME", "HR", "SI", "BA", "MK", "XK"],
    "successorNote": "ISO 3166-1 alpha-2 code YU was reassigned; consumers resolving historical references should use SupersededBy as the modern country set"
  }
}
```

**v1 reference**: none — v2 net-new (v1 had no deprecation tracking).

---

### Coordinates

**C# record** (`D2.Shared.Location/ValueObjects/Coordinates.cs`):
```csharp
public sealed record Coordinates
{
    public required double Latitude { get; init; }       // -90..+90; quantized to 5 decimals ≈ 1.1m
    public required double Longitude { get; init; }      // -180..+180; quantized to 5 decimals
    public double? AccuracyMeters { get; init; }         // optional metadata; NOT in hash
    public string Geohash => /* lazy: Niemeyer base32, 11 chars */;
    public string PlusCode => /* lazy: Open Location Code */;
    public string HashId => /* "v1." + sha256(Geohash) */;
    public static D2Result<Coordinates> Create(double lat, double lon, double? accuracyMeters = null) { ... }
}
```

**TS interface** (`@d2/location/src/coordinates.ts`):
```typescript
export interface Coordinates {
  readonly latitude: number;
  readonly longitude: number;
  readonly accuracyMeters: number | null;
  readonly geohash: string;       // computed
  readonly plusCode: string;      // computed
  readonly hashId: string;        // "v1." + sha256(geohash)
}
```

**Example value** (Edmonton, AB):
```json
{
  "latitude": 53.54611,
  "longitude": -113.49083,
  "accuracyMeters": 25.0,
  "geohash": "c3nfhzs6c5n",
  "plusCode": "9558GHHM+8H",
  "hashId": "v1.7f3a8c..."
}
```

**v1 reference**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/ValueObjects/Coordinates.cs:24-133` (no `AccuracyMeters`, no `Geohash`, no `PlusCode`, no `HashId`).

---

### StreetAddress

**C# record** (`D2.Shared.Location/ValueObjects/StreetAddress.cs`):
```csharp
public sealed record StreetAddress
{
    public required string Line1 { get; init; }              // required non-empty after CleanStr
    public string? Line2 { get; init; }                      // optional
    public string? Line3 { get; init; }                      // requires Line2 non-empty
    public string HashId => /* "v1." + sha256(line1|line2|line3 cleaned lowered pipe-joined) */;
    public static D2Result<StreetAddress> Create(string line1, string? line2 = null, string? line3 = null) { ... }
}
```

**TS interface**:
```typescript
export interface StreetAddress {
  readonly line1: string;
  readonly line2: string | null;
  readonly line3: string | null;
  readonly hashId: string;
}
```

**Example value**:
```json
{
  "line1": "1600 Pennsylvania Avenue NW",
  "line2": null,
  "line3": null,
  "hashId": "v1.4ab9d2..."
}
```

**v1 reference**: `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/ValueObjects/StreetAddress.cs:22-149` (no `HashId`).

---

### AdminLocation (NEW VO in v2)

**C# record**:
```csharp
public sealed record AdminLocation
{
    public string? City { get; init; }                                // cleaned via CleanStr
    public string? PostalCode { get; init; }                          // uppercased
    public string? SubdivisionISO31662Code { get; init; }             // uppercased FK
    public string? CountryISO31661Alpha2Code { get; init; }           // uppercased FK
    public string HashId => /* "v1." + sha256(city|postal|sub|country) */;
    public static D2Result<AdminLocation> Create(string? city, string? postal, string? sub, string? country) { ... }
}
```

**TS interface**:
```typescript
export interface AdminLocation {
  readonly city: string | null;
  readonly postalCode: string | null;
  readonly subdivisionISO31662Code: string | null;
  readonly countryISO31661Alpha2Code: string | null;
  readonly hashId: string;
}
```

**Example value**:
```json
{
  "city": "Edmonton",
  "postalCode": "T5J 0K1",
  "subdivisionISO31662Code": "CA-AB",
  "countryISO31661Alpha2Code": "CA",
  "hashId": "v1.9c8b3f..."
}
```

**v1 reference**: implicit in `old/v1/D2-WORX/backends/dotnet/services/Geo/Geo.Domain/Entities/Location.cs:100-108` (city + postal + subdivision + country fields on the atomic Location record). V2 promotes to first-class VO.

---

## Lookup interface contracts (full signatures + return examples)

This section enriches Decision 7 with the COMPLETE method list for every lookup interface, plus a worked return-shape example for each method. Real data throughout.

### ICountryLookup

```csharp
namespace D2.Shared.Geo.Abstractions;

public interface ICountryLookup
{
    /// <summary>Look up by ISO 3166-1 alpha-2 code. Returns deprecated entries by default (programmatic-safe per CHANGE 3 rationale).</summary>
    Country? ByAlpha2(string code, bool activeOnly = false);

    Country? ByAlpha3(string code, bool activeOnly = false);
    Country? ByNumeric(string code, bool activeOnly = false);

    /// <summary>Resolve free-form country name (DisplayName, OfficialName, endonyms, alpha-2/alpha-3) via cascade
    /// (exact → startsWith → contains → Levenshtein ≤ 2) — see "Name resolution cascade" subsection. Each pass
    /// normalizes via lowercase + Unicode NFD + strip combining marks + trim. Returns null when no match found
    /// at any pass level.</summary>
    Country? ResolveByName(string upstreamName, bool activeOnly = false);

    /// <summary>All countries. Default returns deprecated entries; pass activeOnly:true for UI dropdowns.</summary>
    IReadOnlyList<Country> All(bool activeOnly = false);

    bool IsDeprecated(string alpha2Code);

    // --- convenience filters ---
    IReadOnlyList<Country> ByContinentShortCode(string geShortCode, bool activeOnly = false);    // e.g., "EU" → 50ish countries; "AF" → 54
    IReadOnlyList<Country> ByGeopoliticalEntity(string geShortCode, bool activeOnly = false);    // "NATO" → 32; "EU" → 27; "UN" → 193
    IReadOnlyList<Country> ByCurrencyAlpha(string isoAlphaCode, bool activeOnly = false);        // "EUR" → 20ish countries
    IReadOnlyList<Country> ByLocale(string ietfBCP47Tag, bool activeOnly = false);               // "en-US" → just US; "es-ES" → just Spain
}

// Note: lite vs full is NOT a lookup API concern — see "Lite vs Full" subsection at the end of Decision 7.
// .NET backend always returns the full denormalized graph (PrimaryCurrency / PrimaryLocale / GeopoliticalEntities[]
// / Subdivisions[] / Timezones[] / Territories[] / Locales[] / Currencies[] all populated, NON-nullable);
// cost is just pointer dereferences against the in-memory denormalized catalog. There is no `*Lite` method variant.
// In TS, the same byAlpha2(code) API works for both lite and full imports — the RETURN TYPE differs per Pattern B:
// @d2/geo-default/countries → CountryLite (no embedded nav objects);
// @d2/geo-default/countries/full → CountryFull (with all embedded navs populated non-optional).
```

**Worked return: `countryLookup.ByAlpha2("US")`** — in .NET, returns the full denormalized `Country` graph (PrimaryCurrency / PrimaryLocale / GEs / Subdivisions / Timezones / Territories / Locales / Currencies populated). In TS, the same API call returns the full graph under `/full` import path and a lite shape (nav fields undefined) under the default `/countries` import path. DX: backend gets full graph for free; browsers pick lite via import to keep bundle size low.

**Worked return: `countryLookup.ResolveByName("United States of America")`** → US Country record (Pass 1 exact match on OfficialName — see Decision 7 Name resolution cascade for the full algorithm + Country worked examples).

**Worked return: `countryLookup.ByContinentShortCode("EU")`** → 50ish `Country` records (Europe continent member countries — Russia, Norway, Switzerland, etc., not just EU-political-union members). DX: continent-grouping is a one-liner, not a series of lookups.

**Worked return: `countryLookup.ByGeopoliticalEntity("NATO")`** → 32 `Country` records (US, UK, FR, DE, IT, ES, NL, BE, LU, DK, NO, IS, PT, GR, TR, PL, CZ, HU, SK, SI, EE, LV, LT, RO, BG, HR, AL, ME, MK, FI, SE, US). DX: bypasses any need for a join table query.

---

### ISubdivisionLookup

```csharp
public interface ISubdivisionLookup
{
    /// <summary>Look up by ISO 3166-2 code (e.g., "US-CA"). Returns deprecated entries by default.</summary>
    Subdivision? ByISO31662Code(string code, bool activeOnly = false);

    IReadOnlyList<Subdivision> All(bool activeOnly = false);

    bool IsDeprecated(string iso31662Code);

    /// <summary>All subdivisions for the given country (e.g., "US" → 50 states + DC).</summary>
    IReadOnlyList<Subdivision> ByCountry(string iso31661Alpha2Code, bool activeOnly = false);

    /// <summary>Resolves a free-form subdivision name to a Subdivision within the given country via cascade name
    /// resolution (exact → startsWith → contains → Levenshtein distance ≤ 2 — see "Name resolution cascade"
    /// subsection). Each pass normalizes via lowercase + Unicode NFD + strip combining marks + trim, and matches
    /// against {DisplayName, OfficialName, EndonymDisplayName, EndonymOfficialName, ShortCode}. Returns null when
    /// no match found at any pass level (catalog gap, name doesn't match anything, or unknown country code).</summary>
    Subdivision? ResolveByName(string upstreamName, string countryISO31661Alpha2Code, bool activeOnly = false);
}
```

**Worked return: `subdivisionLookup.ByCountry("US")`** → 51 `Subdivision` records (50 states + DC). Each carries `ISO31662Code` ("US-CA", "US-NY", ...), `ShortCode` ("CA", "NY"), `DisplayName`, `OfficialName`, etc.

**Worked return: `subdivisionLookup.ResolveByName("Bayern", "DE")`** → `Subdivision { ISO31662Code="DE-BY", ShortCode="BY", DisplayName="Bavaria", EndonymDisplayName="Bayern", ... }`. DX: WhoIs returns endonym? Resolves anyway. Returns null when name doesn't match anything.

---

### ICurrencyLookup

```csharp
public interface ICurrencyLookup
{
    /// <summary>Look up by ISO 4217 alpha code (e.g., "USD"). Returns deprecated entries by default.</summary>
    Currency? ByAlpha(string code, bool activeOnly = false);

    Currency? ByNumeric(string code, bool activeOnly = false);

    IReadOnlyList<Currency> All(bool activeOnly = false);

    /// <summary>Returns the 11 supported currencies (IsSupported=true) — USD, CAD, GBP, AUD, NZD, EUR, MXN, JPY, KRW, TWD, PLN.
    /// Derived from the 18 supported locales' primary countries. Selectability/support and deprecation are orthogonal —
    /// pass activeOnly:true to additionally exclude deprecated.</summary>
    IReadOnlyList<Currency> AllSupported(bool activeOnly = false);

    bool IsDeprecated(string alphaCode);

    IReadOnlyList<Country> CountriesUsingCurrency(string alphaCode, bool activeOnly = false);    // "EUR" → 20ish Eurozone countries
}
```

**Worked return: `currencyLookup.ByAlpha("USD")`** → `Currency { ISO4217AlphaCode="USD", ISO4217NumericCode="840", DisplayName="US Dollar", DecimalPlaces=2, Symbol="$" }`. DX: complete formatting metadata in one shot — no follow-up lookups for symbol/decimals.

**Worked return: `currencyLookup.CountriesUsingCurrency("EUR")`** → 20 Country records. DX: bypasses building a reverse-lookup index.

---

### ILanguageLookup

```csharp
public interface ILanguageLookup
{
    /// <summary>Look up by ISO 639-1 code (e.g., "en"). Returns deprecated entries by default.</summary>
    Language? ByISO6391Code(string code, bool activeOnly = false);

    IReadOnlyList<Language> All(bool activeOnly = false);

    /// <summary>All languages with IsSupported=true (the 11 translation-file-backed languages per Decision 6c).
    /// Selectability and deprecation are orthogonal — pass activeOnly:true to additionally exclude deprecated.</summary>
    IReadOnlyList<Language> AllSupported(bool activeOnly = false);

    bool IsDeprecated(string code);

    IReadOnlyList<Language> AllLTR(bool activeOnly = false);     // all left-to-right scripts (v2 enrichment usage)
    IReadOnlyList<Language> AllRTL(bool activeOnly = false);     // all right-to-left
}
```

**Worked return: `languageLookup.ByISO6391Code("en")`** → `Language { ISO6391Code="en", Name="English", Endonym="English", WritingDirection=LTR }`. DX: writing direction immediately available for UI flip.

---

### ILocaleLookup

```csharp
public interface ILocaleLookup
{
    /// <summary>Look up by IETF BCP-47 tag (e.g., "en-US"). Returns deprecated entries by default.</summary>
    Locale? ByIETFBCP47Tag(string tag, bool activeOnly = false);

    IReadOnlyList<Locale> All(bool activeOnly = false);

    bool IsDeprecated(string tag);

    IReadOnlyList<Locale> ByLanguage(string iso6391Code, bool activeOnly = false);              // "en" → all en-* (selectable + non-selectable)
    IReadOnlyList<Locale> ByCountry(string iso31661Alpha2Code, bool activeOnly = false);        // "US" → en-US (all locales spoken in US per Country.Locales M:M)

    /// <summary>The 18 curated UI-dropdown locales (IsSelectable=true per 2026-05-17 expansion, covering 11 languages)
    /// — source-of-truth for language pickers. Selectability and deprecation are orthogonal — pass activeOnly:true to
    /// additionally exclude deprecated.</summary>
    IReadOnlyList<Locale> AllSelectable(bool activeOnly = false);
}

// Locale.ResolveSelectable extension lives in Geo.Abstractions (see Decision 7 for the algorithm + worked examples)
```

**Worked return: `localeLookup.ByIETFBCP47Tag("en-US")`** → `Locale { IETFBCP47Tag="en-US", Name="English (United States)", Endonym="English (United States)", LanguageISO6391Code="en", CountryISO31661Alpha2Code="US", IsSelectable=true, FirstDayOfWeek=Sunday, DecimalSeparator=".", ThousandsSeparator=",", DateFormatPattern=MDY }`. DX: complete locale formatting metadata in one shot — calendar week start, number format, date order all immediately available.

**Worked return: `localeLookup.AllSelectable()`** → 18 `Locale` records (the en-US, en-CA, en-GB, en-AU, en-NZ, es-ES, es-MX, fr-FR, fr-CA, de-DE, it-IT, ja-JP, nl-NL, nl-BE, ko-KR, zh-TW, pt-PT, pl-PL curated set per 2026-05-17 expansion — covering 11 languages). DX: language-picker dropdown is a one-liner.

**Worked return: `Locale.ResolveSelectable("fr-CI", localeLookup)`** → `Locale { IETFBCP47Tag="fr-FR", ... }` (nearest selectable French). DX: browser-supplied `Accept-Language: fr-CI` resolves to a selectable variant without bouncing to default en-US.

**Worked return: `Locale.ResolveSelectable("ru-RU", localeLookup)`** → `null`. DX: caller falls back to system default (en-US) — no Russian variant is selectable, so resolution honestly admits failure.

---

### ITimezoneLookup

```csharp
public interface ITimezoneLookup
{
    /// <summary>Look up by IANA identifier (e.g., "America/Edmonton"). Aliases (Asia/Saigon → Asia/Ho_Chi_Minh) resolve
    /// transparently. Returns deprecated entries by default.</summary>
    Timezone? ByIANAIdentifier(string id, bool activeOnly = false);

    IReadOnlyList<Timezone> All(bool activeOnly = false);

    bool IsDeprecated(string ianaIdentifier);

    /// <summary>~150-200 UI-friendly zones (Selectable=true). Selectability and deprecation are orthogonal —
    /// pass activeOnly:true to additionally exclude deprecated.</summary>
    IReadOnlyList<Timezone> AllSelectable(bool activeOnly = false);

    IReadOnlyList<Timezone> AllByCountry(string iso31661Alpha2Code, bool activeOnly = false);  // CA → 6 zones
    Timezone? GetPrimaryForCountry(string iso31661Alpha2Code, bool activeOnly = false);        // CA → America/Toronto
    IReadOnlyList<Timezone> ByOffsetMinutes(int stdOffsetMinutes, bool activeOnly = false);     // -300 → all UTC-5 zones
}
```

**Worked return: `timezoneLookup.ByIANAIdentifier("America/Edmonton")`** → full `Timezone` record with `LocalizedDisplayNames` dict + offset minutes + abbreviations + Selectable=true. DX: dropdown label in user's language is one property access.

**Worked return: `timezoneLookup.AllByCountry("CA")`** → 6 `Timezone` records (`America/St_Johns`, `America/Halifax`, `America/Toronto`, `America/Winnipeg`, `America/Edmonton`, `America/Vancouver`). DX: per-country timezone picker is a one-liner.

---

### IGeopoliticalEntityLookup

Full signature in Decision 6a. Method list summary (every getter takes `bool activeOnly = false`):

- `ByShortCode(shortCode, activeOnly)`
- `All(activeOnly)`
- `IsDeprecated(shortCode)`
- `ByType(GeopoliticalEntityType, activeOnly)`
- `GetEntitiesForCountry(countryISO31661Alpha2Code, activeOnly)`
- `GetCountriesForEntity(geShortCode, activeOnly)`
- `IsCountryMember(countryISO31661Alpha2Code, geShortCode)`

**Worked return: `geLookup.ByShortCode("EU")`** — in .NET, returns the full denormalized `GeopoliticalEntity` graph (`Countries` array populated with the 27 EU member Country records). In TS lite import path, `Countries` stays undefined; `CountryISO31661Alpha2Codes = ["AT","BE","BG","HR","CY","CZ","DK","EE","FI","FR","DE","GR","HU","IE","IT","LV","LT","LU","MT","NL","PL","PT","RO","SK","SI","ES","SE"]` (27 codes).

**Worked return: `geLookup.IsCountryMember("US", "NATO")`** → `true`. DX: O(1) membership check; consumers don't reach for SQL.

---

## Denormalized return shapes — why consumers don't need N+1 queries

The denormalized inline lookup pattern is the single biggest consumer-DX win in this Phase. Where v1 forced consumers into multiple gRPC round-trips to assemble a complete picture, v2 returns the complete graph in one in-process lookup. The cost — paid once at lib import — is the catalog data (7 catalogs: Countries / Subdivisions / Currencies / Languages / Locales / Timezones / GeopoliticalEntities; ~200KB raw / ~200KB gzipped per Decision 14 size analysis) living in `D2.Shared.Geo.Default` as static readonly fields. Subsequent lookups are O(1) dictionary access against a fat denormalized graph.

### Example 1: full denormalized return — .NET `countryLookup.ByAlpha2("US")` / TS `import { CountryLookup } from "@d2/geo-default/countries/full"; countryLookup.byAlpha2("US")`

```jsonc
{
  // --- identity ---
  "iso31661Alpha2Code": "US",
  "iso31661Alpha3Code": "USA",
  "iso31661NumericCode": "840",

  // --- naming ---
  "displayName": "United States",
  "officialName": "United States of America",
  "endonymDisplayName": "United States",
  "endonymOfficialName": "United States of America",

  // --- phone ---
  "phoneNumberPrefix": "1",
  "phoneNumberNationalFormat": "(NNN) NNN-NNNN",
  "phoneNumberMinDigits": 10,
  "phoneNumberMaxDigits": 10,

  // --- v2 CLDR enrichments ---
  "firstDayOfWeek": "Sunday",
  "weekendStart": "Saturday",
  "weekendEnd": "Sunday",
  "measurementSystem": "Imperial",

  // --- FK codes (lite still works on the full record — both shapes coexist) ---
  "primaryLanguageISO6391Code": "en",
  "primaryCurrencyISO4217AlphaCode": "USD",
  "primaryLocaleIETFBCP47Tag": "en-US",
  "sovereignCountryISO31661Alpha2Code": null,

  // --- DENORMALIZED EMBEDDED OBJECTS (the .full payoff) ---
  "primaryCurrency": {
    "iso4217AlphaCode": "USD",
    "iso4217NumericCode": "840",
    "displayName": "US Dollar",
    "officialName": "United States Dollar",
    "decimalPlaces": 2,
    "symbol": "$",
    "deprecation": null
  },
  "primaryLocale": {
    "ietfBCP47Tag": "en-US",
    "name": "English (United States)",
    "endonym": "English (United States)",
    "languageISO6391Code": "en",
    "countryISO31661Alpha2Code": "US",
    "firstDayOfWeek": "Sunday",
    "decimalSeparator": ".",
    "thousandsSeparator": ",",
    "dateFormatPattern": "MDY",
    "deprecation": null
  },
  "primaryLanguage": {
    "iso6391Code": "en",
    "name": "English",
    "endonym": "English",
    "writingDirection": "LTR",
    "deprecation": null
  },
  "sovereignCountry": null,

  // --- DENORMALIZED M:M ARRAYS (full embedded GE records) ---
  "geopoliticalEntities": [
    { "shortCode": "NA",     "name": "North America",                "type": "Continent",                          "countryISO31661Alpha2Codes": ["US","CA","MX","..."] },
    { "shortCode": "NATO",   "name": "North Atlantic Treaty Org.",   "type": "MilitaryAlliance",                   "countryISO31661Alpha2Codes": ["US","CA","UK","..."] },
    { "shortCode": "UN",     "name": "United Nations",               "type": "GovernanceAndCooperationAgreement",  "countryISO31661Alpha2Codes": ["US","CA","..."] },
    { "shortCode": "G7",     "name": "Group of Seven",               "type": "GovernanceAndCooperationAgreement",  "countryISO31661Alpha2Codes": ["US","CA","FR","DE","IT","JP","UK"] },
    { "shortCode": "G20",    "name": "Group of Twenty",              "type": "GovernanceAndCooperationAgreement",  "countryISO31661Alpha2Codes": ["..."] },
    { "shortCode": "OECD",   "name": "Org. for Economic Co-op.",     "type": "GovernanceAndCooperationAgreement",  "countryISO31661Alpha2Codes": ["..."] },
    { "shortCode": "USMCA",  "name": "US-Mexico-Canada Agreement",   "type": "FreeTradeAgreement",                 "countryISO31661Alpha2Codes": ["US","CA","MX"] },
    { "shortCode": "FVEY",   "name": "Five Eyes",                    "type": "SecurityCooperationAgreement",       "countryISO31661Alpha2Codes": ["US","CA","UK","AU","NZ"] },
    { "shortCode": "QUAD",   "name": "Quadrilateral Security Dialog","type": "SecurityCooperationAgreement",       "countryISO31661Alpha2Codes": ["US","JP","AU","IN"] },
    { "shortCode": "CPTPP",  "name": "Comprehensive & Progressive TPP","type": "FreeTradeAgreement",               "countryISO31661Alpha2Codes": ["..."] }
  ],

  // --- DENORMALIZED SUBDIVISIONS (all 50 states + DC; embedded back-ref omitted to avoid cycles) ---
  "subdivisions": [
    { "iso31662Code": "US-AL", "shortCode": "AL", "displayName": "Alabama",    "officialName": "State of Alabama",    "endonymDisplayName": "Alabama" },
    { "iso31662Code": "US-AK", "shortCode": "AK", "displayName": "Alaska",     "officialName": "State of Alaska",     "endonymDisplayName": "Alaska" },
    // ... (49 more) ...
    { "iso31662Code": "US-CA", "shortCode": "CA", "displayName": "California", "officialName": "State of California", "endonymDisplayName": "California" },
    { "iso31662Code": "US-DC", "shortCode": "DC", "displayName": "District of Columbia", "officialName": "District of Columbia" }
  ],

  // --- DENORMALIZED TIMEZONES (US has ~9 zones) ---
  "timezones": [
    { "ianaIdentifier": "America/New_York",    "displayName": "Eastern Time — New York",    "currentStdOffsetMinutes": -300, "currentDstOffsetMinutes": -240, "selectable": true },
    { "ianaIdentifier": "America/Chicago",     "displayName": "Central Time — Chicago",     "currentStdOffsetMinutes": -360, "currentDstOffsetMinutes": -300, "selectable": true },
    { "ianaIdentifier": "America/Denver",      "displayName": "Mountain Time — Denver",     "currentStdOffsetMinutes": -420, "currentDstOffsetMinutes": -360, "selectable": true },
    { "ianaIdentifier": "America/Los_Angeles", "displayName": "Pacific Time — Los Angeles", "currentStdOffsetMinutes": -480, "currentDstOffsetMinutes": -420, "selectable": true },
    { "ianaIdentifier": "America/Phoenix",     "displayName": "Mountain Standard Time — Phoenix (no DST)", "currentStdOffsetMinutes": -420, "currentDstOffsetMinutes": null, "selectable": true },
    { "ianaIdentifier": "America/Anchorage",   "displayName": "Alaska Time — Anchorage",    "currentStdOffsetMinutes": -540, "currentDstOffsetMinutes": -480, "selectable": true },
    { "ianaIdentifier": "Pacific/Honolulu",    "displayName": "Hawaii Time (no DST)",       "currentStdOffsetMinutes": -600, "currentDstOffsetMinutes": null, "selectable": true }
    // ... (Adak, Detroit, Indiana/* etc.) ...
  ],

  // --- DENORMALIZED CURRENCIES (M:M with acceptance level; .full embeds the Currency object on each) ---
  "currencies": [
    {
      "iso4217AlphaCode": "USD",
      "level": "LegalTender",
      "currency": {
        "iso4217AlphaCode": "USD",
        "iso4217NumericCode": "840",
        "displayName": "US Dollar",
        "officialName": "United States Dollar",
        "decimalPlaces": 2,
        "symbol": "$",
        "deprecation": null
      }
    }
    // ... (additional WidelyAccepted / Tourist entries if any) ...
  ],

  "deprecation": null
}
```

**Note**: cyclic back-refs are broken via the Lite type in TS Pattern B (Decision 6c §"TS modeling") — `SubdivisionFull.country: CountryLite` (not `CountryFull`); `LocaleFull.country: CountryLite`; `TimezoneFull.country: CountryLite`; `GeopoliticalEntityFull.countries[]: CountryLite[]`; `CountryFull.territories[]: CountryLite[]`. This prevents infinite recursion in the type AND in serialization. .NET-side, nav properties are populated as full records but the graph forms a DAG (no circular back-refs ever set on nested records). Consumers who need the back-ref from a nested record do a follow-up lookup (e.g., `subdivisionLookup.ByISO31662Code("US-CA").Country` → full Country graph again).

### Example 2: lite return — TS `import { CountryLookup } from "@d2/geo-default/countries"; countryLookup.byAlpha2("US")` (lite import; returns `CountryLite`)

Per Pattern B (Decision 6c §"TS modeling"), the lite import returns `CountryLite` — embedded nav OBJECTS aren't part of the type at all. Only ISO codes / FK code arrays are present.

```jsonc
{
  "iso31661Alpha2Code": "US",
  "iso31661Alpha3Code": "USA",
  "iso31661NumericCode": "840",
  "displayName": "United States",
  "officialName": "United States of America",
  "endonymDisplayName": "United States",
  "endonymOfficialName": "United States of America",
  "phoneNumberPrefix": "1",
  "phoneNumberNationalFormat": "(NNN) NNN-NNNN",
  "phoneNumberMinDigits": 10,
  "phoneNumberMaxDigits": 10,
  "firstDayOfWeek": "Sunday",
  "weekendStart": "Saturday",
  "weekendEnd": "Sunday",
  "measurementSystem": "Imperial",
  "primaryLanguageISO6391Code": "en",
  "primaryCurrencyISO4217AlphaCode": "USD",
  "primaryLocaleIETFBCP47Tag": "en-US",
  "sovereignCountryISO31661Alpha2Code": null,

  // --- FK code arrays only (no embedded objects on CountryLite) ---
  "geopoliticalEntityShortCodes": ["NA","NATO","UN","G7","G20","OECD","USMCA","FVEY","QUAD","CPTPP"],
  "subdivisionISO31662Codes": ["US-AL","US-AK","US-AZ","US-AR","US-CA","..."],
  "timezoneIANAIdentifiers": ["America/New_York","America/Chicago","America/Denver","America/Los_Angeles","..."],
  "currencies": [
    { "iso4217AlphaCode": "USD", "level": "LegalTender" }
  ],
  "localeIETFBCP47Tags": ["en-US","es-US"],
  "territoryISO31661Alpha2Codes": ["PR","GU","VI","AS","MP"],

  "deprecation": null
}
```

DX trade-off: lite is the transit-friendly / tree-shake-friendly shape — consumers serialize the same record over wire / cache without the embedded graph; payload stays small. Full is the answer-the-question-in-one-call shape — UI rendering / business logic that needs the resolved graph gets it for free.

### Example 3: the DX win — N+1 query pattern vs single denormalized lookup

```csharp
// ❌ v1 multi-call pattern: 4 gRPC round-trips assembling separate refs
//   var country = countryLookup.ByAlpha2("US");          // round-trip 1
//   var currency = currencyLookup.ByAlpha("USD");        // round-trip 2
//   var locale = localeLookup.ByIETFBCP47Tag("en-US");   // round-trip 3
//   var ges = geLookup.GetEntitiesForCountry("US");      // round-trip 4

// ✅ v2 denormalized: one lookup, full graph (.NET always returns full; TS via /full import)
var country = countryLookup.ByAlpha2("US");
// country.PrimaryCurrency.Symbol             // "$" — already populated
// country.PrimaryLocale.DecimalSeparator     // "." — already populated
// country.PrimaryLanguage.WritingDirection   // LTR — already populated
// country.GeopoliticalEntities[1].Name       // "North Atlantic Treaty Org." — already populated
// country.Subdivisions[4].EndonymDisplayName // "California" — already populated
// country.Timezones[2].CurrentStdAbbrev      // "MST" — already populated
```

**Cost analysis**:
- v1: 4 gRPC round-trips × ~5-50ms each = 20-200ms per page load
- naive v2: 4 in-process Dictionary lookups × ~100ns each = ~400ns per call (1000× faster than v1's single round-trip)
- denormalized v2: 1 Dictionary lookup × ~100ns; the embedded graph is already resolved at lib import (which itself is ~25-50ms one-time cost at process start)

**Memory cost** (paid once at lib import in `D2.Shared.Geo.Default`):
- Raw catalog data: ~200KB across 7 catalogs (Countries / Subdivisions / Currencies / Languages / Locales / Timezones / GeopoliticalEntities)
- Denormalized inline expansion (each Country carries embedded Currency / Locale / Language / GE list / Subdivision list / Timezone list — shared references, not deep clones): adds ~30-50% to working set
- Total: ~150-200KB resident in process memory
- For comparison: a single typical JSON HTTP response is often larger

**Why consumers never see N+1 pains**: every entity that has FKs ships in two shapes (lite + full); the lite shape is intentionally the default for transit-and-cache; the full shape is the answer-everything-in-one-call shape for UI rendering / business logic.

---

## Tree-shake worked examples (TS-specific)

The TS-side bundle strategy is per-catalog sub-exports + lite/full views (Decision 14). Browser bundle impact is the dominant DX concern on the SvelteKit BFF side; the .NET-side DLL footprint is non-issue (loaded once at process start).

### Per-catalog sub-exports — actual import shapes

```typescript
// Compact dropdown (country selector only)
import { CountryLookup } from "@d2/geo-default/countries";
// Bundle impact: ~12 KB gzipped (250 countries, lite shape — FKs as ISO codes)

// Full Country detail page (resolves embedded objects in one access)
import { CountryLookup } from "@d2/geo-default/countries/full";
// Bundle impact: ~25 KB gzipped (denormalized — Country has Currency/Locale/GEs/Subs/Timezones inline)

// Multiple selectors (country + timezone + subdivision)
import { CountryLookup } from "@d2/geo-default/countries";
import { TimezoneLookup } from "@d2/geo-default/timezones";
import { SubdivisionLookup } from "@d2/geo-default/subdivisions";
// Bundle impact: ~32 KB gzipped (lite shapes for all three; subdivisions is heaviest at ~75 KB but most pages only need subdivisions for one country, which is much smaller)

// Single full GeopoliticalEntity catalog
import { GeopoliticalEntityLookup } from "@d2/geo-default/geopolitical-entities";
// Bundle impact: ~3 KB gzipped (59 entities × ~600 bytes each, lite)

// All catalogs (composite — typical for an admin-tools page)
import * as Geo from "@d2/geo-default";
// Bundle impact: ~110 KB gzipped (everything)
```

### Per-catalog gzipped size table (v1 actuals + v2 expansion estimates)

| Import path | Entries | Raw | Gzipped |
|---|---|---|---|
| `@d2/geo-default/countries` (lite) | 250 | ~40 KB | ~12 KB |
| `@d2/geo-default/countries/full` (denormalized) | 250 | ~120 KB | ~25 KB |
| `@d2/geo-default/subdivisions` (lite) | ~3,600 | ~280 KB | ~75 KB |
| `@d2/geo-default/timezones` (lite) | ~600 | ~50 KB | ~12 KB |
| `@d2/geo-default/currencies` (lite) | ~180 | ~15 KB | ~4 KB |
| `@d2/geo-default/locales` (lite) | **~700 (full CLDR BCP-47 catalog; 18 selectable; rest non-selectable for Country.Locales M:M completeness + BCP-47 fallback + future expansion headroom)** | ~280-320 KB | **~75-100 KB** |
| `@d2/geo-default/languages` (lite) | **~180 (full ISO 639-1; 11 with `IsSupported=true`)** | ~25 KB | ~7 KB |
| `@d2/geo-default/geopolitical-entities` (lite) | 59 | ~10 KB | ~3 KB |
| **All** (composite via `@d2/geo-default`) | 5,150+ | ~680 KB | ~200 KB |

DX commentary: typical pages — login (country picker), signup (country + timezone + subdivision for billing), profile (country + locale) — land at 8-15 KB gzipped extra payload, well within SvelteKit per-route budget targets.

**Subdivisions special case**: full ISO 3166-2 ships ~75 KB gzipped, the heaviest catalog. The narrowing helper `subdivisionLookup.ByCountry("US")` runs against the in-memory denormalized index — so a SSR `+page.server.ts` that needs just US states can `import { SubdivisionLookup } from "@d2/geo-default/subdivisions"` and the bundle for the user receives only what the SSR `load()` returned (a 51-element array, ~3 KB serialized to the page payload). The ~75 KB never crosses the wire.

---

## Catalog coverage targets (consolidated)

| Catalog | V1 entries | V2 Phase 1 target | Source |
|---|---|---|---|
| Countries | 250 (full ISO 3166-1) | 250 (carry forward) | CLDR |
| Subdivisions | 183 (US/Canada + sparse) | ~3,600 (FULL ISO 3166-2, confirmed per user re-affirmation 2026-05-17) | CLDR |
| Currencies | 5 (sample) | **~180 active (full ISO 4217 current) with 11 marked `IsSupported=true`** (USD, CAD, GBP, AUD, NZD, EUR, MXN, JPY, KRW, TWD, PLN — derived from the 18 supported locales' primary countries) per Decision 6c data-vs-selectability principle. Exclude historical entries. | ISO 4217 |
| Languages | 6 (en, es, fr, de, it, ja) | **~180 (full ISO 639-1 catalog) with 11 marked `IsSupported=true`** per Decision 6c data-vs-selectability principle. The 11 supported = en, es, fr, de, it, ja (v1 carry-forward — Italian RESTORED per §1.31 audit) + nl, ko, zh, pt, pl (NEW additive per user re-affirmation 2026-05-17 — Dutch/Korean/Chinese Traditional/Portuguese Portugal/Polish) | CLDR + ISO 639-1 |
| Locales | 138 | **~700 (full CLDR BCP-47 catalog)** per user re-affirmation 2026-05-17; **18 selectable** (`IsSelectable=true`: en-US, en-CA, en-GB, en-AU, en-NZ, es-ES, es-MX, fr-FR, fr-CA, de-DE, it-IT, ja-JP, nl-NL, nl-BE, ko-KR, zh-TW, pt-PT, pl-PL) per Decision 6c data-vs-selectability principle | CLDR |
| Timezones | 309 | ~600 (full IANA) with ~150-200 `Selectable=true` per Decision 6c data-vs-selectability principle | IANA tzdb |

---

## Languages deferred from Phase 1 (with revisit triggers)

User's strategic framing (2026-05-17): *"it's reasonable / good to keep enemies of the US and neutral + high abuse countries out of our support range. frankly, i could see a scenario where we have to BLOCK india, russia, PR china, countries in the middle east / africa outright due to abuse / cyber attacks."* This deferred-list reflects that strategy. See "Phase 3 carry-forward inventory" section below for the active Edge geo-security carry-forward (per-country block-list + rate-limit override + geographic risk scoring) that operationalizes this stance.

| Lang | Deferred | Revisit trigger |
|---|---|---|
| Russian (ru-RU + ru-BY + ru-KZ) | Sanctions + payment infrastructure broken (Visa/Mastercard exited 2022) + elevated abuse rate. User-explicit: "skip due to abuse / sanctions." | When geopolitical situation normalizes AND payment infrastructure restored. Likely 5+ year horizon. |
| Chinese Mainland (zh-CN, Simplified) | ICP licensing required + data residency mandate + Great Firewall + payment friction (no Stripe) + IP/abuse concerns | When committing to China-specific deployment (separate compliance project — not just localization) |
| Chinese Hong Kong (zh-HK, Traditional) | Could be served by zh-TW fallback. Skip explicitly to avoid maintenance creep. | When HK becomes a priority market AND fallback to zh-TW becomes inadequate |
| Brazilian Portuguese (pt-BR) | User-explicit: abuse + low B2B willingness-to-pay | When BR commerce demand surfaces AND fraud controls mature |
| Hindi / Tamil / Bengali (hi-IN / ta-IN / bn-IN) | User-explicit: low B2B willingness-to-pay + elevated abuse potential | When India B2B market enters scope (user stated: probably never under current strategy) |
| Arabic variants (ar-SA / ar-AE / ar-EG) | RTL infrastructure cost + variable abuse risk by region | When MENA expansion is committed AND RTL UI infrastructure is built |
| Hebrew (he-IL) | RTL infrastructure cost (modest market size doesn't justify alone) | When RTL infrastructure exists for Arabic — he-IL becomes near-free addition |
| Turkish (tr-TR) | Currency volatility (TRY hyperinflation) + elevated abuse rate | When TR economic stability returns |
| Vietnamese / Thai / Indonesian / Malay (vi-VN / th-TH / id-ID / ms-MY) | B2B willingness-to-pay low + elevated abuse rate | When SEA B2B SaaS demand surfaces with fraud controls |
| Swedish / Norwegian / Danish / Finnish (sv-SE / no-NO / da-DK / fi-FI) | Nordic B2B markets default to English; localization is "nice to have" not deal-breaker | When Nordic-specific customer demands surface AND English-default proves insufficient |

**Note**: even though the locales themselves are PRESENT in the full ~700 CLDR catalog (so future expansion = JSON spec edit + Paraglide message file), the LANGUAGES backing them are not in the supported set for Phase 1. The deferred-language inventory above tracks the language-level decision (Russian language won't be added even if some Russian-locale entries exist in the CLDR catalog as non-selectable variants for Country.Locales completeness).

---

## In-flight rules.md predicates (drafted; apply at SHIP per user authorization)

These predicates DRAFT in this doc + the deliverable workspace; they ONLY get applied to `rules.md` at SHIP per the workflow.md §SHIP discipline. Section numbers below are PROPOSED — Planner verifies against the current `rules.md` (last seen with `§1` topping at `§1.21`) and adjusts at SHIP.

| # | Section family | Predicate (drafted) |
|---|---|---|
| §1.22 | Test Discipline | Versioned hash prefix on content-addressable IDs (`v1.` prefix mandatory). Evidence: per content-addressable hash factory → test asserts output starts with `"v1."`. Why: future normalization changes need flag-day-free migration; bare hex hashes break this. |
| §1.23 | Test Discipline | Reference data APPEND-ONLY with first-class `Deprecation` field + lookup APIs default to `activeOnly: false` (include deprecated). Every reference-data entity record has nullable `DeprecationInfo? Deprecation { get; init; }` property; every per-key lookup method + every `All` collection method on `I*Lookup` interfaces carries a `bool activeOnly = false` parameter (CHANGE 3 — single methods, not paired `By*`/`By*Active`). Evidence: per entity → Deprecation field present; per lookup interface → `activeOnly` parameter present on every getter + `All()` with default `false`. Why: silently removing deprecated codes invalidates content-addressable hashes referencing them + breaks historical lookups. Default `activeOnly: false` is the SAFER default — backend programmatic lookups (resolving historical references like a 2015 hash containing `YU`) MUST be able to look up `YU` without devs thinking about it. UI dropdowns consciously opt-in with `activeOnly: true`. Silent backend resolution failure is worse than visible UI bug (deprecated entry showing in dropdown = user reports it; missing historical reference = silent data corruption). |
| §1.24 | Test Discipline | Use NodaTime types for all timestamp storage; never `DateTime` / raw `DateTimeOffset`. Evidence: `grep -rEn 'DateTime(?!Offset)' server/**/*.cs` → expect zero hits in non-test code; `DateTimeOffset` hits expected only in BCL interop seams. Why: BCL DateTime DST math is wrong for historical instants; no Category-3 storage shape. |
| §1.25 | Test Discipline | Categorize every timestamp at design time (1=past instant, 2=future fixed, 3=future local-anchored); document the category in xmldoc. Evidence: every NodaTime-typed property carries `/// <remarks>Category: N (...)</remarks>` xmldoc. Why: conflating categories breaks DST transitions (Cat 3 stored as UTC fires wrong after the next transition). |
| §1.26 | Test Discipline | Inject `IClock` from `D2.Shared.Time`; never call `DateTimeOffset.UtcNow` / `SystemClock.Instance.GetCurrentInstant()` directly. Evidence: `grep` returns zero direct calls. Why: tests can't deterministically control time without `IClock`; `TestClock` is the test seam. |
| §1.27 | Test Discipline | IANA zone ids canonical; never offsets alone (`-05:00`); never Windows TZ names. Evidence: `grep -rE '"[+-]\d{2}:\d{2}"' server/**/*.cs` returns zero; `grep -rEn '"Eastern Standard Time"' server/**/*.cs` returns zero. Why: offsets lose DST info; Windows names don't exist on Linux. |
| §1.28 | Test Discipline | Use `Resolvers.LenientResolver` default for ambiguous/skipped DST local times. Evidence: every `ZonedDateTime.AtStrictly` / `AtLeniently` call site walked; `AtStrictly` rejected unless explicit user-input validation context. Why: spring-forward 2:30am doesn't exist; throwing is the wrong default for system code. |
| §1.29 | Test Discipline | Build-time consistency check: every IANA id in Geo.Default's catalog MUST resolve via NodaTime's bundled tzdb. Evidence: build-time test enumerates Geo.Default's timezone catalog + asserts each id resolves in `DateTimeZoneProviders.Tzdb`. Why: drift between Geo.Default + NodaTime tzdb = silent runtime failures on the math side. |
| §1.30 | Test Discipline | IANA timezone aliases follow §1.23 deprecation pattern (alias resolver maps old → new transparently). Evidence: `Timezone.Aliases` populated for every retired IANA id; lookup test asserts alias→canonical resolution. Why: `Asia/Saigon` lookup must still resolve even though `Asia/Ho_Chi_Minh` is the canonical. |
| §13.5 | Permission / Action Discipline | "Defer" is a smell; default = ship-in-same-change; deferring requires explicit user permission per occurrence. Evidence: any "defer" / "carry-forward to later phase" / "leave for follow-up" decision in journal → cite explicit user-authorization message. Why: silent defers accumulate as invisible debt; memory's "never defer without permission" rule already covers this but predicate codifies for audit-walk visibility. |
| §7.x | Naming, File Headers, Folder Casing | **`Region` is forbidden as a standalone type or bare property name in geo contexts** — clashes with UN M49 geopolitical regions / EU / NATO regions / AWS regions / etc. Use the `Subdivision*` prefix for geo-subdivision concepts: `SubdivisionISO31662Code` (structured ISO 3166-2 FK), `SubdivisionShortCode` (abbreviation). The `Subdivision*` prefix is unambiguous; bare `Region` / `Province` / `State` / `Prefecture` are not — all banned. (Note per CHANGE 2: `RequestContext` does NOT carry a free-form subdivision-name field — only the structured `SubdivisionISO31662Code`; unresolved upstream names live in the Phase 3 WhoIs entity's audit trail.) Evidence: `grep -rEn '\b[Rr]egion\b' server/ contracts/` returns hits only inside scoped exemptions (AWS region, K8s region — explicitly unrelated to geo). Why: per Decision 2. |
| §1.31 | Test Discipline | **V2 functional preservation.** Every capability available in v1 (`old/v1/D2-WORX/`) must be carried forward to v2 unless explicitly approved by user as out-of-scope with a documented revisit trigger. When designing a v2 replacement, the FIRST step is auditing the v1 surface for the same domain; the SECOND step is documenting the v1→v2 mapping (where each v1 capability lives in v2). Net-new v2 capabilities are welcome; they cannot REPLACE v1 capabilities without explicit user sign-off. Evidence: every v2 lib PLAN doc has a "v1 functional inventory → v2 mapping" table; per-step Auditor walks the table and any v1 capability not accounted for (carried / deferred-with-trigger / dropped-with-explicit-approval) = audit-finding. **Why**: v2 is a from-scratch rebuild using v1 as reference; the failure mode is leaner-than-v1 surface that silently breaks consumer migration. The catch: v1 is the floor, not the ceiling — drop nothing silently. **How to apply**: at every PLAN phase, walk v1's equivalent module + produce an inventory table mapping v1→v2; at every audit round, re-walk the inventory against the current code. **Meta-observation (added 2026-05-17)**: This rule earned its keep on its first audit. During the PLAN phase of deliverable 0008-geo-libs, §1.31 was drafted in PHASE_1.md by a Planner sub-agent. The same Planner's draft silently dropped 4 v1 capabilities that the user did NOT explicitly approve: (1) Italian language (`it`) from the Language seed, (2) `Country.Currencies` M:M (all accepted currencies per country) — broke real use cases (Switzerland EUR+CHF, Argentina USD+ARS, etc.), (3) `Country.Locales` M:M (all locales per country) — broke per-country locale-picker UX, (4) `Country.Territories` inverse navigation. A self-audit triggered when the user spotted #1 surfaced all 4. The rule was added to the same doc that violated it — proving (a) the rule is necessary, (b) summary-prose approval doesn't substitute for itemization, (c) the §13.6 itemization protocol is required as a structural fix. **Meta-observation case #2 (added 2026-05-17)**: During the same PHASE_1 PLAN authoring, a second misinterpretation surfaced — the orchestrator wrongly tied `Country.PrimaryLocale` nullability to `Locale.IsSelectable`, conflating DATA presence with UX serving availability. The user caught it by re-reading the doc: "i thought our plan was to add ALL languages but ONLY add translations for the specific languages we discussed. that means that, hypothetically, every single country SHOULD have a PRIMARY LOCALE." The architectural fix: separate data-from-selectability via a boolean flag uniformly across Language / Locale / Timezone (Decision 6c). This is the SECOND silent-misinterpretation §13.6 protocol caught — strengthening the case that itemized per-item consent on every spec decision is necessary, not optional. Pattern: agents tend to collapse separable concerns when summarizing; the user's re-read caught it both times. **Meta-observation case #3 (added 2026-05-17)**: This same round of doc enrichment caught TWO MORE first-pass agent design over-multiplications via user re-read: (a) **Paired `By*` / `By*Active` lookup methods** — first-pass design split deprecation-filter into two parallel methods per access pattern (`ByAlpha2` + `ByAlpha2Active`). User identified this should be a single method with `bool activeOnly = false` default. Cleaner API + safer default (backend programmatic gets all entries including deprecated, UI must opt-in to filter). (b) **`*Lite` / `*Full` method variants on lookup interfaces** — first-pass design exposed lite/full as method variants. User identified this is a package-import + wire-payload concern, NOT a lookup API concern. .NET backend always returns full graph (cheap in-memory pointers); TS lite vs full is determined by import path. Single method signature works for both. Pattern: agent's first-pass design tends to OVER-MULTIPLY (extra methods, extra states, extra nullability) when a simpler collapsed design is correct. Three documented cases now: Case #1: 4 silent drops (Italian + Country.Currencies + Country.Locales + Country.Territories) — Agent SHRANK data surface without consent; Case #2: PrimaryLocale-vs-IsSelectable conflation — Agent CONFLATED data presence with UX availability; Case #3: API over-multiplication — Agent OVER-MULTIPLIED method variants when single methods suffice. Three distinct failure modes, all caught by the same §13.6 itemization-and-re-read discipline. Each strengthens the case that per-item explicit consent (vs blanket "looks good") is structural, not optional. **Meta-observation case #4 (added 2026-05-18)**: This same round caught TWO MORE first-pass design issues via user re-read: (a) **Nullable nav types in C# Country record** — first-pass had `PrimaryLocale: Locale?` / `PrimaryLanguage: Language?` / `PrimaryCurrency: Currency?` (all nullable) — a holdover from when the doc conflated .NET and TS lite/full modeling. User clarified: ".NET backend always full graph (no lite mode); navs should be NON-nullable in C#." Only genuinely-nullable real-world facts (`SovereignCountry?`, `Deprecation?`) carry `?`. TS uses separate `{Entity}Lite` + `{Entity}Full` interfaces (Pattern B) for the import-path distinction. Cleaner type semantics in both languages. (b) **Missing `Currency.IsSupported` flag** — first-pass shipped Currency catalog without selectability flag, breaking the data-vs-selectability principle (Decision 6c). User caught: "we will need an is selectable for currency (we will only support a couple OOTB like locales)." Added `IsSupported: bool` + `ICurrencyLookup.AllSupported()` + 11 supported currencies (derived from supported locales' primary countries). Four documented failure modes now (silent drops / data-UX conflation / API over-multiplication / inconsistent application of locked principles). Pattern continues: agent's first-pass design tends toward over-multiplication AND inconsistent application of locked principles when synthesizing across many decision rounds. The §13.6 itemization-and-re-read discipline is what keeps each successive round caught. |
| §1.32 | Test Discipline | **Locale.IsSelectable build-time consistency.** Every `Locale` entity with `IsSelectable=true` MUST have a corresponding `contracts/messages/{IETFBCP47Tag}.json` Paraglide message file present in the repo at codegen time. The .NET SourceGen + TS emitter checks both directions: (a) every IsSelectable=true Locale → message file exists; (b) every message file → matching Locale entity exists with IsSelectable=true. Build fails on either mismatch. Evidence: codegen unit test in `D2.Shared.Geo.Default.Generator.Tests` enumerates `Locale.AllSelectable()` and asserts file presence; second test enumerates `contracts/messages/*.json` and asserts each maps to a selectable Locale. **Why**: V1 had three out-of-band lists drift silently — `PUBLIC_ENABLED_LOCALES` env var, Paraglide `settings.json`, and Paraglide message file presence — leading to selectable locales that had no translations + translated locales not exposed in UI. V2 collapses to one source-of-truth (`Locale.IsSelectable`) with codegen-enforced consistency. **How to apply**: at every codegen run + at every audit round, verify the two assertions pass. |
| §13.6 | Permission / Action Discipline | **Explicit per-item consent for drops, deferrals, and redactions.** When proposing to defer, drop, or redact ANY v1 functional surface OR ANY locked decision OR ANY user-stated requirement, the proposal MUST itemize every change EXPLICITLY — not bury it in summary prose. User approval requires per-item acknowledgement; broad statements like "looks good", "okay", or "lock that in" on a multi-page summary are NOT consent for any specific drop/defer/redact within it. Default action when in doubt: PRESERVE; deviation requires explicit per-item ASK. **Why**: this rule earned its keep in PHASE_1 of 0008-geo-libs — §1.31 was drafted, then PHASE_1's own initial draft silently dropped 4 v1 capabilities because the Planner agent buried them in summary tables that user approval of the doc could be read as accepting. The structural fix is itemization at decision time. **How to apply**: Planner/Implementer/Fixer agents present a numbered checklist of every drop/defer/redact and require explicit YES/NO/AMEND per item before proceeding. Per-step Auditor walks every "defer" / "drop" / "redact" decision in the journal + asserts the journal carries the per-item YES/NO/AMEND trail. |

**Predicate count**: 14 new predicates (9 in §1.22-1.30 + §1.31 strengthened + §1.32 new = 11 in §1 family; §13.5 + §13.6 = 2 in §13 family; §7.x = 1 in §7 family).

---

## Intentional drops from v1 (with rationale)

These are v1 capabilities that DO NOT carry forward to v2 — separate from "carry-forward to later phases" (revisit-trigger-bound) and separate from earlier silent-drop violations (which §1.31 caught + this PLAN restored). Each entry below carries: what v1 had + what v2 does instead + rationale + revisit trigger + the user-approval reference.

### Runtime reference-data update broadcast (v1's `IPubs.Update` + `ISubs.Updated` + `ICommands.ReqUpdate`)

**What v1 had**: pub/sub flow allowing services to detect ref-data version bump and refresh caches WITHOUT redeployment. The Geo service held the authoritative ref-data DB; clients subscribed to `Updated` events and called `ReqUpdate` to refresh their local cache.

**What v2 does instead**: deploy-to-update model. Reference data is build-time-baked into `D2.Shared.Geo.Default`. Any ref-data change requires a new lib release + consumer redeployment.

**Rationale** (user-stated): "we no longer have to sync with a db — it's just a lib"

**Revisit trigger**: if hot-update of reference data (without redeployment) becomes a real operational need — e.g., new ISO 3166-2 subdivision codes need to land within hours instead of days; emergency takedown of a deprecated entry.

**User-approved**: YES (item 6a confirmation, 2026-05-17)

### `Coordinates.GetParts()` + `StreetAddress.GetParts()` static helpers

**What v1 had**: internal static helpers (`Coordinates.cs:124`, `StreetAddress.cs:143`) that broke a value object into string-array parts for use in v1's atomic Location hash computation.

**What v2 does instead**: each VO computes its OWN hash (`Coordinates.HashId`, `StreetAddress.HashId`, `AdminLocation.HashId`); the aggregate is composed via `ComposeLocationHash(...)` free function (per Decision 4 + Decision 5). The `GetParts()` helpers have no consumer in v2 — superseded by per-VO hash + free composition function.

**Revisit trigger**: none. The functional outcome (deterministic content-addressable Location hash) is preserved via better mechanism — per-VO HashIds participate directly in `ComposeLocationHash` without needing string-array intermediates.

**User-approved**: YES (items 9a + 9b confirmations, 2026-05-17)

### `Region` / `SubdivisionName` free-form name field on RequestContext

**What v1 had**: `IRequestContext.Region` — free-form string captured from WhoIs `region` field; would hold "California" or "Bay Area" or "Some Obscure Region" depending on what IPinfo returned. An earlier v2 PLAN draft (per item 5a / item III from prior session) had renamed this to `SubdivisionName` to fit the `Subdivision*` prefix discipline, while keeping the same free-form-string semantics.

**What v2 does instead**: `IRequestContext.SubdivisionISO31662Code` (nullable) — only the structured ISO 3166-2 code is propagated. Unresolvable upstream names are NOT propagated to the application layer. Edge's WhoIs enrichment (Phase 3) attempts `subdivisionLookup.ResolveByName(ipInfo.Region, ipInfo.Country)` using the enhanced cascade (per CHANGE 1a — exact + startsWith + contains + Levenshtein ≤ 2; NFD-normalized). On success → `RequestContext.SubdivisionISO31662Code = result.ISO31662Code`. On failure → `RequestContext.SubdivisionISO31662Code = null`. The raw upstream name never reaches RequestContext.

**Rationale** (user-stated): with the full ~3,600 ISO 3166-2 catalog + enhanced ResolveByName cascade (accent normalization via NFD, substring matching, bounded Levenshtein distance), resolution rate is very high. Unresolvable names are typically:
- (a) Second-order subdivisions ISO 3166-2 doesn't cover (UK counties, Japanese wards, US counties, German Kreise — Phase 1 only ships first-order per Decision 16)
- (b) Non-administrative geographic regions ("Bay Area", "New England", "Midwest")
- (c) Garbage upstream data from misconfigured IPinfo accounts

None of (a)/(b)/(c) is actionable in the application layer. They don't drive feature gates, geo-fencing, or display logic — those need a structured code.

**Audit preservation**: Phase 3 Edge's WhoIs entity persists the FULL raw IPinfo response (including unresolved subdivision strings) as part of the cached row — debugging aid for "why didn't this resolve?" investigations + audit trail for compliance review. The data is NOT propagated to RequestContext but is available to operators investigating WhoIs-resolution gaps. See "Phase 3 (Edge WhoIs module) — v1 carry-forward inventory" section below for the WhoIs entity row.

**Revisit trigger**: if WhoIs upstream coverage degrades to where unresolved-but-actionable names become common (unlikely with full ISO 3166-2 catalog + enhanced cascade), reconsider exposing a fallback name field on RequestContext.

**User-approved**: YES (items 2a-2e confirmations, 2026-05-17 — supersedes earlier 5a / III rename decision)

---

## D2.Shared.Geo.Default catalog metadata (item 6b)

The Geo.Default lib exposes catalog metadata as static constants for operator visibility:

```csharp
namespace D2.Shared.Geo.Default;

public static class GeoCatalog
{
    /// <summary>Semver-like catalog version baked into emitted JSON spec at codegen time.
    /// Format: "YYYY.MM.DD.N" (date + within-day rev). Example: "2026.05.17.1".</summary>
    public const string CatalogVersion = "2026.05.17.1";

    /// <summary>Timestamp when this catalog snapshot was published (codegen time).
    /// v2 NEW (item 6b) — operator-visibility for stale deployments. Admin dashboards surface
    /// "Geo catalog: 2026.05.17.1 (published 2026-05-17)" so operators can spot services
    /// running stale catalog data that need redeployment.</summary>
    public static readonly Instant CatalogPublishedAt = Instant.FromUtc(2026, 5, 17, 0, 0, 0);
}
```

**Consumer pattern**: admin dashboards / health endpoints surface both `CatalogVersion` + `CatalogPublishedAt` per service instance. Operators spot drift across instances (e.g., one service shows `2026.05.17.1` published 2026-05-17, another shows `2026.04.30.2` published 2026-04-30 — the second instance needs redeployment).

**Rationale** (user-stated): "useful to identify if some service is running stale data somehow (need to be re-deployed)" — item 6b confirmation, 2026-05-17.

---

## Carry-forward to later phases

| Item | Phase | Revisit trigger |
|---|---|---|
| `LocationFix` (dynamic observed location) | Future | Dispatch service is being built |
| `IPhoneValidator` (Validator-DI pattern, libphonenumber-grade) | 2 (Contacts) | Phase 2 starts |
| `IEmailValidator` (Validator-DI pattern) | 2 (Contacts) | Phase 2 starts |
| Altitude on `Coordinates` | Future | Aerial / drone consumer surfaces |
| Second-order subdivision hierarchy (counties / wards / Kreise) | Future | Dispatch needs per-county routing |
| Currency historical entries (DEM, ITL, FRF, etc.) | Future | Historical-display use case surfaces |
| Country name localization beyond endonym (CLDR per-language matrix) | Future | Match-on-primary-lang proves insufficient |
| Runtime-loadable tzdb | Future | Faster-than-monthly tzdb turnaround needed |
| D2.Shared.Time exposing full NodaTime range (additional types) | Per-phase | Consumer demands |
| `@d2/geo-default-browser` slim variant | Future | Browser bundle becomes a real problem |
| Per-subdivision `PrimaryLanguageISO6391Code` override | Future | Catalan / Basque / Quebec French / etc. richer-display use case surfaces |
| `EncryptionDomains` per-message-type currency translation | Future | Multi-currency consumer ships |
| `Locale.ResolveByName` cascade (multi-result fuzzy) — same NFD + cascade pattern applied to Locale name fields | Future | UI locale search consumer surfaces (Phase 1 has no consumer; `Locale.ResolveSelectable(tag, lookup)` already handles BCP-47 tag fallback) |
| `Timezone.ResolveByName` cascade | Future | UI timezone search consumer surfaces (Phase 1 has no consumer; `ITimezoneLookup.AllByCountry` + `GetPrimaryForCountry` already handle the country-grouped picker UX) |
| `SearchByName(query, limit)` returning ranked `IReadOnlyList<T>` across any lookup | Future | UI search consumer surfaces wanting multi-result ranking (Phase 1 returns first-match; Phase N can layer ranked multi-result on the same cascade infrastructure) |

---

## Phase 2 (D2.Shared.Contacts) — v1 carry-forward inventory

This inventory (item 8a) anticipates Phase 2 — its purpose is so that when the Phase 2 PLAN doc is written, the Planner sub-agent inherits an already-audited list of every v1 capability that MUST be in scope (per §1.31). Phase 2 PLAN doc, when written, MUST inventory this list and confirm every item is in scope or explicitly user-approved as deferred per §13.6.

| v1 artifact | v1 file:line | Phase 2 location | Carry-forward action |
|---|---|---|---|
| `Contact` entity (11 fields: Id, CreatedAt, ContextKey, RelatedEntityId, IETFBCP47Tag, IANAIdentifier, ContactMethods, PersonalDetails, ProfessionalDetails, LocationHashId, Locale/Timezone navs) | `Geo.Domain/Entities/Contact.cs` | `D2.Shared.Contacts/Entities/Contact.cs` | Carry forward all 11 fields + immutable-update pattern |
| `ContactMethods` VO | `Geo.Domain/ValueObjects/ContactMethods.cs` | `D2.Shared.Contacts/ValueObjects/ContactMethods.cs` | Carry forward |
| `EmailAddress` VO | `Geo.Domain/ValueObjects/EmailAddress.cs` | `D2.Shared.Contacts/ValueObjects/EmailAddress.cs` | Carry forward |
| `PhoneNumber` VO | `Geo.Domain/ValueObjects/PhoneNumber.cs` | `D2.Shared.Contacts/ValueObjects/PhoneNumber.cs` | Carry forward + E.164 validation |
| `Personal` VO (9 fields incl. DateOfBirth + BiologicalSex) | `Geo.Domain/ValueObjects/Personal.cs` | `D2.Shared.Contacts/ValueObjects/Personal.cs` | Carry forward |
| `Professional` VO | `Geo.Domain/ValueObjects/Professional.cs` | `D2.Shared.Contacts/ValueObjects/Professional.cs` | Carry forward |
| `BiologicalSex` enum (4 values) | `Geo.Domain/Enums/BiologicalSex.cs` | `D2.Shared.Contacts/Enums/BiologicalSex.cs` | Carry forward |
| `GenerationalSuffix` enum (12 values) | `Geo.Domain/Enums/GenerationalSuffix.cs` | `D2.Shared.Contacts/Enums/GenerationalSuffix.cs` | Carry forward |
| `NameTitle` enum (16 values) | `Geo.Domain/Enums/NameTitle.cs` | `D2.Shared.Contacts/Enums/NameTitle.cs` | Carry forward |
| `CreateContacts` handler | `Geo.App/...` | `D2.Shared.Contacts/Handlers/C/CreateContacts.cs` | Reimplement |
| `DeleteContacts` handler | `Geo.App/...` | `D2.Shared.Contacts/Handlers/D/DeleteContacts.cs` | Reimplement |
| `DeleteContactsByExtKeys` handler | `Geo.App/...` | `D2.Shared.Contacts/Handlers/D/DeleteContactsByExtKeys.cs` | Reimplement |
| `GetContactsByExtKeys` handler | `Geo.App/...` | `D2.Shared.Contacts/Handlers/R/GetContactsByExtKeys.cs` | Reimplement |
| `GetContactsByIds` handler | `Geo.App/...` | `D2.Shared.Contacts/Handlers/R/GetContactsByIds.cs` | Reimplement |
| `UpdateContactsByExtKeys` handler | `Geo.App/...` | `D2.Shared.Contacts/Handlers/U/UpdateContactsByExtKeys.cs` | Reimplement (immutable-update pattern) |
| `ContactEviction` pub | `Geo.App/...IPubs.ContactEviction` | `D2.Shared.Contacts/Pubs/ContactEviction.cs` | Reimplement via D2.Shared.Messaging |
| `ContactsEvicted` sub | `Geo.Client/...ISubs.ContactsEvicted` | `D2.Shared.Contacts.Client/Subs/ContactsEvicted.cs` | Reimplement for cache invalidation |
| **`ApiKeyMappings` security control** | `Geo.App/GeoAppOptions.ApiKeyMappings` | `D2.Shared.Contacts/Options/ContactsOptions.cs` | **CRITICAL — recreate or contact RPCs become wide-open** |
| `ContactExpirationDuration` | `Geo.App/GeoAppOptions.ContactExpirationDuration` | `D2.Shared.Contacts/Options/ContactsOptions.cs` | Carry forward (default 4hr) |
| `AllowedContextKeys` client-side allowlist | `Geo.Client/GeoClientOptions.AllowedContextKeys` | `D2.Shared.Contacts.Client/Options/ContactsClientOptions.cs` | Carry forward — defense-in-depth |
| `ContactToCreateValidator` | `Geo.Client/...` | `D2.Shared.Contacts/Validators/ContactToCreateValidator.cs` | Carry forward |
| `CleanupOrphanedLocations` job + `JobLockTtlSeconds` | `Geo.App/...ICommands.CleanupOrphanedLocations` | `D2.Shared.Contacts/Jobs/CleanupOrphanedLocations.cs` | Carry forward — Contact ownership of orphan-creation justifies relocation |
| `IPhoneValidator` interface + Default impl | (Phase 2 per Decision 13) | `D2.Shared.Contacts/Validators/IPhoneNumberValidator.cs` | Add per Decision 13 pattern |
| `IEmailValidator` interface + Default impl | (Phase 2 per Decision 13) | `D2.Shared.Contacts/Validators/IEmailValidator.cs` | Add per Decision 13 pattern |

**Phase 2 PLAN doc, when written, MUST inventory this list and confirm every item is in scope or explicitly user-approved as deferred per §13.6.**

---

## Phase 3 (Edge WhoIs module) — v1 carry-forward inventory

This inventory (item 8b) anticipates Phase 3 — its purpose is so that when the Phase 3 PLAN doc is written, the Planner sub-agent inherits an already-audited list of every v1 WhoIs capability that MUST be in scope (per §1.31). Phase 3 PLAN doc, when written, MUST inventory this list and confirm every item is in scope or explicitly user-approved as deferred per §13.6.

| v1 artifact | v1 file:line | Phase 3 location | Carry-forward action |
|---|---|---|---|
| **NEW (not in v1)**: Per-country geo-IP block-list + per-country rate-limit-tier override + WhoIs-driven geographic risk scoring | n/a — user-flagged for v2 (2026-05-17) | `Edge/Security/Geo/CountryBlockList.cs` + `Edge/RateLimit/CountryRateLimitOverrides.cs` + `Edge/Fraud/GeographicRiskScorer.cs` | Net-new v2 capability. User strategic framing: *"we may need to BLOCK india, russia, PR china, countries in the middle east / africa outright due to abuse / cyber attacks."* Phase 3 PLAN MUST design: (a) block-list ingestion (config/admin-managed allowlist + denylist of ISO 3166-1 alpha-2 codes — denial returns HTTP 451 Unavailable For Legal Reasons or 403 Forbidden with telemetry), (b) `RateLimitTier` override semantics (per-country tier override on top of v2's 18-bucket RateLimitTier model — e.g., country=TR → forced `RateLimitTier.Strict`), (c) WhoIs-driven geographic risk scoring (input to fraud detection / step-up auth — combines country + AS/ISP reputation + VPN/proxy/Tor/relay flags into a normalized risk score). Operationalizes the deferred-language strategic framing (see "Languages deferred from Phase 1" section). |
| `WhoIs` entity (24 properties + factory methods + IPAddress validation + hash from IP\|year\|month) | `Geo.Domain/Entities/WhoIs.cs` | `Edge/WhoIs/Entities/WhoIs.cs` | Carry forward all 24 fields |
| - `HashId` (PK, content-addressable) | `WhoIs.cs:484-513` | same | Carry forward; consider `v1.` prefix per §1.22 |
| - `IPAddress` (normalized) | `WhoIs.cs` | same | Carry forward + validation |
| - `Year`, `Month` (temporal partition) | `WhoIs.cs` | same | Carry forward |
| - `ASN`, `ASName`, `ASDomain` | `WhoIs.cs` | same | Carry forward |
| - `ASType` (string?) | `WhoIs.cs` | same | **Phase 3 decision: keep string? or formalize as enum (ISP/Hosting/Education/Government/Business/Unknown)** |
| - `CarrierName`, `MCC`, `MNC` | `WhoIs.cs` | same | Carry forward |
| - 9× privacy flags (IsVPN/Tor/Proxy/Hosting/Relay/Anonymous/Anycast/Satellite/Mobile) | `WhoIs.cs` | same | Carry forward |
| - `PrivacyName` | `WhoIs.cs` | same | Carry forward |
| - `LocationHashId` FK | `WhoIs.cs` | same | Carry forward — references AdminLocation or composite Location hash |
| - `ASChanged`, `GeoChanged` (DateOnly?) | `WhoIs.cs` | same | Carry forward |
| `IIpInfoClient` interface | `Geo.App/...IIpInfoClient.cs` | `Edge/WhoIs/IpInfo/IIpInfoClient.cs` | Reimplement |
| `IpInfoResponse` record (Ip, Hostname, City, Region, Country, Postal, Latitude, Longitude, Org, Privacy) | `Geo.App/...IpInfoResponse.cs` | `Edge/WhoIs/IpInfo/IpInfoResponse.cs` | Carry forward |
| `IpInfoPrivacy` record (Vpn, Proxy, Tor, Relay, Hosting) | `Geo.App/...IpInfoPrivacy.cs` | `Edge/WhoIs/IpInfo/IpInfoPrivacy.cs` | Carry forward |
| `IpInfoClientWrapper` impl | `Geo.App/...IpInfoClientWrapper.cs` | `Edge/WhoIs/IpInfo/IpInfoClient.cs` | Reimplement with HttpClient + circuit breaker |
| `FindWhoIs` handler | `Geo.App/...IComplex.FindWhoIs` | `Edge/WhoIs/Handlers/X/FindWhoIs.cs` | Reimplement |
| `GetWhoIsByIds` handler | `Geo.App/...IQueries.GetWhoIsByIds` | `Edge/WhoIs/Handlers/R/GetWhoIsByIds.cs` | Reimplement |
| `CreateWhoIs` handler | `Geo.App/...ICommands.CreateWhoIs` | `Edge/WhoIs/Handlers/C/CreateWhoIs.cs` | Reimplement |
| `Populate` repository handler | `Geo.Infra/...IRead.Populate` | `Edge/WhoIs/Handlers/X/Populate.cs` | Reimplement (partial WhoIs → fully populated via IpInfo + Location create) |
| `PurgeStaleWhoIs` job + `WhoIsRetentionDays` (default 180) + `JobLockTtlSeconds` (default 300) | `Geo.App/...ICommands.PurgeStaleWhoIs` + `GeoAppOptions` | `Edge/WhoIs/Jobs/PurgeStaleWhoIs.cs` + `WhoIsOptions` | Carry forward with same defaults |
| `WhoIsExpirationDuration` (default 4hr) | `Geo.App/GeoAppOptions.WhoIsExpirationDuration` | `Edge/WhoIs/Options/WhoIsOptions.cs` | Carry forward |
| `WhoIsCacheExpiration` (default 8hr) | `Geo.Client/GeoClientOptions.WhoIsCacheExpiration` | `Edge/WhoIs/Options/WhoIsClientOptions.cs` (if Edge needs client-side cache) | Carry forward |
| `WhoIsCacheMaxEntries` (default 10,000 LRU) | `Geo.Client/GeoClientOptions.WhoIsCacheMaxEntries` | same | Carry forward |
| `CircuitBreakerFailureThreshold` (default 5) | `Geo.Client/GeoClientOptions` | `Edge/WhoIs/Options/IpInfoClientOptions.cs` (for IPinfo upstream) | Carry forward |
| `CircuitBreakerCooldownDuration` (default 30s) | `Geo.Client/GeoClientOptions` | same | Carry forward |

**Phase 3 Edge WhoIs PLAN doc, when written, MUST inventory this list and confirm every item is in scope or explicitly user-approved as deferred per §13.6. CRITICAL Phase 3 decisions: (1) ASType formalization (keep v1's string? or enum); (2) NEW geo-security capability set — per-country geo-IP block-list + per-country `RateLimitTier` override + WhoIs-driven geographic risk scoring (see top row of the inventory; operationalizes user's strategic framing about blocking high-abuse-risk countries per the "Languages deferred from Phase 1" section).**

---

## Open questions

(none — all locked during the multi-turn PLAN discussion)

---

## Final attestation block (template — filled at Step 8 SHIP)

> "I attest that this deliverable's process integrity has been verified against the deliverable completeness checklist in `rules.md` (Deliverable completeness checklist section). Every box is YES. The deliverable is ready for user REVIEW."

Spot-check links (to be filled at SHIP):

- Per-step journals (3-artifact model + clean big tables):
  - Step 1: `01-time/journal.md`
  - Step 2: `02-geo-abstractions/journal.md`
  - Step 3: `03-spec-and-codegen/journal.md`
  - Step 4: `04-geo-default/journal.md`
  - Step 5: `05-location/journal.md`
  - Step 6: `06-cross-cutting/journal.md`
- Final-review journal: `final-review/journal.md`
- Completeness Checklist: `final-review/completeness-checklist.md`

**14 drafted rules.md predicates** applied at SHIP per user authorization (§1.22-§1.30 + §1.31 strengthened + §1.32 + §13.5 + §13.6 + §7.x). Cross-cutting doc updates landed (PATTERNS.md additions including **Reference Data Philosophy** subsection; NEW TIMESTAMPS.md; NEW `contracts/geo/selectable-locales.spec.json` [with 18 selectable per 2026-05-17 expansion] + `contracts/geo/country-currencies-overrides.spec.json`; CLAUDE.md §3.5 Doc Update Map row; V2.md Phase 1 row enrichment; `Region` field DROPPED from `IRequestContext.spec.json` — entire field removed, no rename; per CHANGE 2 supersedes earlier rename decision). Phase 2 + Phase 3 carry-forward inventories embedded in this doc + the deliverable workspace README for downstream PLAN authors — including the NEW Phase 3 geo-security row.

**Final scope locked** per Decision 6c data-vs-selectability principle: ~180 ISO 639-1 language catalog with 11 supported; ~700 CLDR BCP-47 locale catalog with 18 selectable; **~180 ISO 4217 active currency catalog with 11 supported (USD/CAD/GBP/AUD/NZD/EUR/MXN/JPY/KRW/TWD/PLN)**; ~600 IANA timezone catalog with ~150-200 selectable; ~3,600 full ISO 3166-2 subdivisions; `Country.PrimaryLocale` ALWAYS set per CLDR (consumers check `PrimaryLocale.IsSelectable` for UX); .NET navs NON-nullable everywhere except genuinely-nullable real-world facts (`SovereignCountry?`, `Deprecation?`); TS uses Pattern B (separate `{Entity}Lite` + `{Entity}Full` interfaces); Phase 3 Edge geo-security carry-forward queued.

**Awaiting user REVIEW + SHIP authorization.** SHIP gate steps (per workflow.md §SHIP): commit the 0008 SHIP work + apply approved rules.md predicate additions + present this doc to user + copy snapshot to `docs/dev/deliverables/0008-geo-libs.md` + squash-merge `n/geo-libs` → `nova` + update V2.md §4 Phase 1 row to ✅ + archive `docs/v2/PHASE_1.md` → `docs/archive/PHASE_1_GEO_LIBS.md` when Phase 2 begins (not at this SHIP).

---

## Archive trigger

Archive `docs/v2/PHASE_1.md` → `docs/archive/PHASE_1_GEO_LIBS.md` when Phase 2 (Contacts) begins, per the V2.md §10 lifecycle rule + the precedent set by `docs/archive/PHASE_0_WIPE.md`. The archived snapshot stays as a frozen reference; the per-step deliverable workspace at `docs/wip/0008-geo-libs/` is left in-place for user to remove manually whenever they want.
