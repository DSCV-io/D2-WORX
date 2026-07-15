<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# Known warnings — `contracts/geo/`

Build-time + pipeline-time warnings emitted by the geo source-gens (`DcsvIo.D2.Geo.SourceGen` and `tools/ts-codegen/src/geo-emitter/`) and by the Tier-1 (geo data pipeline stage 1 — raw transform) transformer (`tools/geo-data-pipeline/src/transformers/subdivisions.ts`) that are EXPECTED and intentionally accepted. Cross-reference any new warning against this doc:

- **In the list, with matching parameters** → expected; no action needed
- **In the list, with DIFFERENT parameters** (different entity ids, different normalized name) → escalate, may be new bug
- **NOT in the list** → escalate as new bug, investigate before suppressing

## Subdivision source-priority hierarchy (2026-05-23 architecture)

The subdivisions transformer uses this priority order for English displayName / officialName:

1. **Wikidata.en (P300 SPARQL label)** — PRIMARY. Tracks Wikipedia 1:1, correctly reflects ISO 3166-2 reassignments, covers ~99% of currently-active codes (5,324 / 5,360 in the current cache). Wins on conflict with all other sources.
2. **debian/iso-codes `name` field** — FALLBACK. Used for the ~140 small territories Wikidata lacks an `en` label for (microstate territorial entries, less-trafficked dependencies, etc.). Also the authority for WHICH codes exist (current-codes list).
3. **CLDR `cldr-subdivisions-full/en-subdivisions.json`** — NO LONGER USED for English displayName. CLDR is stale for many post-2020 ISO reassignments (Iran 2020-11-24, Norway 2020 county mergers, Estonia EE county restructuring). Retained only as a secondary seed for the 11 supported localized labels before Wikidata overrides on conflict.
4. **Hand-rolled overlay at `contracts/geo/overlays/subdivisions.overlays.spec.json`** — LAST. Applied at Tier-2 (geo pipeline stage 2 — spec assembly) build time as explicit override. Intended to stay empty unless Wikidata.en is wrong AND the Debian fallback is also unacceptable.

For non-English labels (the 10 other supported locales: es/fr/de/it/ja/nl/ko/zh/pt/pl), Wikidata wins over CLDR on conflict.

For endonyms (country-primary-language label), Wikidata is the source. Norway gets a special cascade `nb → nn → no → da → sv` because Wikidata stores Norwegian under multiple Bokmål/Nynorsk variants and lacks a unified `no` locale for many subdivisions.

## D2GEO010 — catalog-uniqueness duplicates

D2GEO010 fires when two entities in the same catalog have normalized names that collide (NFD + strip combining marks + lowercase + trim → same string). These are warnings, not build-blocking errors; the resolver fail-closes at runtime via ambiguity detection (returns null + reports the collision via diagnostics).

### Expected dupes — DO NOT ESCALATE (10 total)

| Catalog        | Pair                | Normalized name | Category                  | Why expected                                                                                                                                                                                                                                                                | Escalation trigger                                                                          |
| -------------- | ------------------- | --------------- | ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `subdivisions` | `BF-03` / `BF-KAD`  | `kadiogo`       | parent-child legitimate   | Burkina Faso Centre Region (BF-03) + Kadiogo Province (BF-KAD, child of BF-03). The child province's endonym matches the parent Region's display name.                                                                                                                      | If a third entity joins the pair, or if either side's label changes upstream.               |
| `subdivisions` | `ES-NA` / `ES-NC`   | `navarra`       | parent-child legitimate   | Navarra Province (ES-NA, historical/legacy ISO code) + Navarra Chartered Community (ES-NC, modern autonomous-community parent). Spain restructured Navarra as a chartered community in 1982 but the ISO code ES-NA persists.                                                | If upstream finally retires ES-NA and the pair becomes a single entry.                      |
| `subdivisions` | `GN-B` / `GN-BK`    | `boke`          | parent-child legitimate   | Guinea Boké Region (GN-B) + Boké Prefecture (GN-BK, child of GN-B). Region/prefecture share the namesake city's label.                                                                                                                                                      | If a third entity joins, or if either label changes upstream.                               |
| `subdivisions` | `GN-F` / `GN-FA`    | `faranah`       | parent-child legitimate   | Guinea Faranah Region (GN-F) + Faranah Prefecture (GN-FA, child of GN-F). Same Region/prefecture pattern.                                                                                                                                                                   | Same as GN-B / GN-BK.                                                                       |
| `subdivisions` | `GN-K` / `GN-KA`    | `kankan`        | parent-child legitimate   | Guinea Kankan Region (GN-K) + Kankan Prefecture (GN-KA, child of GN-K).                                                                                                                                                                                                     | Same as GN-B / GN-BK.                                                                       |
| `subdivisions` | `GN-L` / `GN-LA`    | `labe`          | parent-child legitimate   | Guinea Labé Region (GN-L) + Labé Prefecture (GN-LA, child of GN-L).                                                                                                                                                                                                         | Same as GN-B / GN-BK.                                                                       |
| `subdivisions` | `GN-N` / `GN-NZ`    | `nzerekore`     | parent-child legitimate   | Guinea Nzérékoré Region (GN-N) + Nzérékoré Prefecture (GN-NZ, child of GN-N).                                                                                                                                                                                               | Same as GN-B / GN-BK.                                                                       |
| `subdivisions` | `ID-PA` / `ID-PP`   | `papua`         | parent-child legitimate   | Indonesia Papua Province (ID-PA) + Papua Islands geographical grouping (ID-PP). Both legitimately labelled "Papua" in CLDR; child/parent semantics depend on the consumer.                                                                                                  | If upstream resolves the grouping ambiguity, or if a third "Papua" entity appears.          |
| `subdivisions` | `MT-45` / `MT-46`   | `rabat`         | real-world ambiguity      | Malta has TWO localities both named Rabat — one on Gozo (MT-45) and one on Malta (MT-46). Both genuinely "Rabat" in Maltese. Resolver returns null (ambiguous) for bare "Rabat" input; caller must specify island via context.                                              | If Malta administratively renames one of the localities; otherwise this is permanent.       |
| `subdivisions` | `MT-65` / `MT-66`   | `zebbug`        | real-world ambiguity      | Same Malta dual-Żebbuġ situation: Żebbuġ on Gozo (MT-65) + Żebbuġ on Malta (MT-66). Both legitimately named Żebbuġ.                                                                                                                                                         | Same as MT-45 / MT-46.                                                                      |

### Recount note (post 2026-05-23 architecture flip)

The above 10 expected dupes survive the switch to Wikidata.en authority — they are inherent to ISO 3166-2 modeling (8 parent-child) and real-world ambiguity (2 Malta), neither of which Wikidata.en changes. The prior 11th expected entry (Iran IR-22 CLDR-stale "Markazi" label) no longer fires because Wikidata.en correctly returns "Hormozgan Province" (the canonical post-2020-11-24 reassignment label) — that finding auto-resolves and falls off the list.

Re-walk this list after each `pnpm geo:refresh`. The actual D2GEO010 count is reported in the source-gen build output; comparing it to the table above is the standard drift-detection step.

### Escalation triggers (when to investigate)

- **Count increases beyond 10** — new collision class surfaced. Investigate before suppressing.
- **An entry leaves the list** — upstream data shifted (e.g., a rename eliminated a collision). Update this doc in the same change.
- **An entry's normalized form changes** — algorithm change OR upstream label change. Verify which and update either the doc or the algorithm.
- **A new D2GEO00X warning code appears** that isn't documented here — investigate, document if expected.

## D2GEO011 — CLDR-zombie codes filtered

D2GEO011 fires when CLDR's `cldr-subdivisions-full/en-subdivisions.json` ships a subdivision code that debian/iso-codes no longer considers a current ISO 3166-2 code (the code was retired by ISO via reassignment, and CLDR didn't drop the stale label). The Tier-1 transformer DROPS these codes from the catalog output (Debian is the authority for which codes exist) and emits one D2GEO011 warning per drop so the operator can confirm what was dropped.

### Expected behavior

- The diagnostic fires at TRANSFORM time (`tools/geo-data-pipeline/src/transformers/subdivisions.ts`) — NOT at codegen time.
- Output format: `[D2GEO011] subdivision '{code}' in {country}: CLDR label="{cldrEnLabel}"; not in current Debian iso-codes; filtered.`
- A summary line precedes the per-code detail: `[D2GEO011] dropped {N} CLDR-zombie codes`.
- Known zombies (per current upstream caches): IR-31, IR-32 (post-2020 Iran reassignment); NO-01/02/04/05/06/07/08/09/10/12/14/16/17/19/20 (post-2020 Norway county merger); EE-44/57/59 (Estonia county restructuring); plus ~330 others across many countries (mostly historic reassignments).
- The dropped codes carry NO Tier-2 spec entry — they don't ship to consumers. The resolver / lookup tables won't surface them; any caller passing a retired code as input gets a NotFound.

### Escalation trigger

- **A code expected to be CURRENT appears as a zombie** — investigate. Either Wikidata has the code (and is correct) but Debian doesn't (Debian is stale → file Debian iso-codes upstream report) OR Wikidata + Debian both say the code is retired and CLDR is the holdout (CLDR is stale → expected D2GEO011 behavior).
- **The zombie count drops significantly between refreshes** — CLDR may have shipped a cleanup pass; sanity-check that no currently-active codes accidentally appear in the zombie list now.

## Missing Wikidata.en — operator triage log

Wikidata.en covers ~99% of currently-active subdivision codes (5,324 / 5,360 in the current cache); the remaining ~140 fall back to debian/iso-codes' `name` field. The pipeline writes the full triage list to `tools/geo-data-pipeline/logs/missing-wikidata-en.json` (gitignored as a refresh artifact) on every run of `pnpm write:subdivisions` / `pnpm geo:refresh`.

### When to review

- After every `pnpm geo:refresh`.
- Before a major release.

### Triage flow

For each row in the log:

1. Inspect the `debianFallbackName` field.
2. If the fallback is canonical English (e.g., "Kabul Province", "Tashkent Region") → no action; leave the fallback in place.
3. If the fallback is awkward (e.g., non-canonical transliteration with macrons that conflict with modern usage, an outdated colonial-era name, etc.) → add an overlay entry at `contracts/geo/overlays/subdivisions.overlays.spec.json` with `reason: "Wikidata.en missing; Debian fallback awkward — preferring <chosen-form>"`.

## Per-country divergence summary

After each refresh, the pipeline emits a per-country count of subdivisions where Wikidata.en disagrees with debian/iso-codes' `name` field. Output via console.error in the `[divergence]` log lines (top 10 countries by divergent count).

This is purely informational — divergence is the normal case (Wikidata uses modern English-canonical forms with Province / Region suffixes; Debian uses the ISO 3166-2 raw name field which is often unsuffixed or carries macrons / transliteration variants). The summary helps the operator gauge data churn over time.

## Pinned canonical truths regression test

`tools/geo-data-pipeline/tests/unit/transformers-subdivisions-pinned-truths.test.ts` pins ~25 canonical truths that the post-refresh `subdivisions.spec.json` MUST satisfy. Examples: IR-22 contains "Hormoz", IR-00 contains "Markazi", NO-30 contains "Viken", US-CA contains "California". The assertions are substring-tolerant (so "Tehran" matches "Tehran Province") to allow upstream format drift while still catching wholesale shifts.

### Escalation flow when this fails

1. **Inspect the failing code's row** in `subdivisions.spec.json`.
2. **Verify the underlying upstream change** — Wikidata SPARQL label, debian/iso-codes' entry, and (if relevant) CLDR alignment.
3. **One of two outcomes**:
   - Upstream cache is stale (rare — 24h TTL covers most cases) → delete the relevant `.cache/wikidata/` file and re-run `pnpm geo:refresh`.
   - Upstream is genuinely wrong (and Debian fallback isn't acceptable) → add an overlay entry at `contracts/geo/overlays/subdivisions.overlays.spec.json` per the source-priority hierarchy above.

A failure here is a PROCESS SIGNAL, not a code bug — the pipeline correctly pulled what upstream provided; that upstream is unexpected.

## Manual refresh cadence guidance

There is NO scheduled refresh job. `pnpm geo:refresh` is OPERATOR-INTENTIONAL — run when the operator decides.

### Recommended cadence

- **Monthly review** OR **before a major release** — whichever comes first.
- After each refresh, walk: D2GEO011 zombie count, missing-wikidata-en log, per-country divergence summary, pinned-truths test results, full D2GEO010 count (compare to the 10 expected dupes above).

### Out-of-cycle refresh triggers

- A pinned-truth assertion fails on someone's local run (re-pull may have new data).
- A new ISO 3166-2 reassignment is publicly announced.
- A consumer reports an obviously-wrong displayName / endonym.

## Design rationale: allow-listed instead of forced-fix

- **Parent-child cases (8 of 10)** — inherent to how ISO 3166-2 models hierarchical subdivisions (region + sub-region share names). NOT a data bug; fixing would mean renaming legitimate entities. The resolver fail-closes at runtime, which is the correct behavior for genuinely ambiguous lookups.
- **Real-world ambiguity (2 of 10 — Rabat, Żebbuġ)** — Malta really has two Rabats and two Żebbuġs. No upstream fix possible. The resolver fail-closes correctly; consumers must disambiguate via island context.
- **CLDR zombies (D2GEO011)** — filtered automatically at transform time. The pipeline is the fix; the warning is the audit trail.
- **CLDR English drift (formerly IR-22 overlay)** — resolved architecturally by the 2026-05-23 source-priority flip (Wikidata.en primary). No overlay needed; the `contracts/geo/overlays/subdivisions.overlays.spec.json` file is intentionally empty — add an entry only when Wikidata.en is wrong and the Debian fallback is also unacceptable.

## Language enum scope: ISO 639-1 only (current limitation)

The `LanguageCode` enum in `DcsvIo.D2.Geo.Abstractions` covers only ISO 639-1 (2-letter codes, ~184 entries). Some Tier-2 spec data references languages with 3-letter codes that exist in ISO 639-2 / 639-3 but not 639-1:

- **~162 locales** reference 3-letter language codes — e.g. `tzm-Tfng-MA` (Standard Moroccan Tamazight), `vai-Vaii-LR` (Vai), `agq-CM` (Aghem). The raw language string is preserved on the spec; the typed `Locale.Language` nav is `null` / `undefined` for those entries.
- **8 countries** carry a primary language outside 639-1: `MP/fil` (Filipino), `NU/niu` (Niuean), `PW/pau` (Palauan), `SG/cmn` (Mandarin), `TK/tkl` (Tokelauan), `TL/tet` (Tetum), `TV/tvl` (Tuvaluan), `WF/wls` (Wallisian). The typed `Country.PrimaryLanguage` nav is `null` / `undefined` for those entries.

**Current handling**: `Country.PrimaryLanguage` and `Locale.Language` are nullable; emitted as `null` (.NET) / `undefined` (TS) when the spec references a non-639-1 code. The raw 3-letter language string remains accessible via the spec-derived scalar field on the same record.

**Adjacent nullability surfaced by the same audit**:

- `Country.PrimaryCurrency` is nullable for AQ (Antarctica) which has no single primary currency.
- `Country.PrimaryLocale` is nullable for the three uninhabited sovereign territories (AQ, BV, HM) whose CLDR data lacks a primary locale.
- `Country.PhoneNumberMinDigits` is nullable for the seven territories that lack subscriber-number data (AQ, BV, GS, HM, PN, …) — the spec preserves null end-to-end so consumers can detect "no data" vs "0 digits".

When the count of legitimate duplicates evolves (new countries added, ISO renumberings, etc.), update this doc IN THE SAME COMMIT as the change. Agents reading new D2GEO010 / D2GEO011 warnings should be able to look up every expected case here.

## Geo name-resolver cache — ambiguity-sentinel behavior

`DefaultGeoNameResolver` (`.NET`) and `tryResolveCountryByName` / `tryResolveSubdivisionByName` (TS `@dcsv-io/d2-geo-default`) build their normalized-name → record cache on first lookup (cache-aside discipline). When two or more records normalize to the same name, the cache stores a single AMBIGUOUS sentinel at that key rather than picking one record arbitrarily. Pass-1 lookups hitting the sentinel return `D2Result.NotFound` (with the `TK.Geo.Errors.NAME_RESOLUTION_AMBIGUOUS` translation key). Pass-2 / 3 / 4 walks exclude ambiguous entries from the candidate pool — they cannot become a fuzzy-match winner.

This is the runtime defense-in-depth that pairs with the codegen-time `D2GEO010` warning (catalog-uniqueness duplicates listed above). The duplicates are accepted at codegen time and resolved fail-closed at runtime — consumers passing an ambiguous name (e.g. `"Papua"` for the ID-PA / ID-PP pair) get `NotFound`, never a wrong-record answer. The cache build orders catalog entries deterministically (by ISO code, `StringComparer.Ordinal`) so two processes building the cache independently mark the same set of keys as ambiguous.
