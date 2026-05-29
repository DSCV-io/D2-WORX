// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  fetchCldrSubdivisions,
  type CldrSubdivisionsFetchResult,
} from "../fetchers/cldr-subdivisions.js";
import {
  computeOrder,
  fetchDebianIso31662,
} from "../fetchers/debian-iso-codes.js";
import { fetchSubdivisionEndonyms } from "../fetchers/wikidata-endonyms.js";
import type { FetchProvenance } from "../util/cache.js";
import { getEndonymLanguageList } from "../util/endonym-languages.js";

/**
 * Final Subdivision spec entry shape.
 *
 * Source-priority hierarchy (post-2026-05-23 investigation; see
 * `contracts/geo/KNOWN_WARNINGS.md` for the full rationale):
 *
 *   1. **English displayName / officialName**: Wikidata.en (P300 SPARQL labels) is the
 *      authoritative source — matches Wikipedia 1:1, correctly aligned with the
 *      current ISO 3166-2 code set, ~99% coverage of currently-active codes. Falls
 *      back to debian/iso-codes' `name` field when Wikidata lacks an `en` label
 *      (~140 small territories / less-trafficked entries).
 *   2. **Existence (which codes ship)**: debian/iso-codes' `iso_3166-2.json` is the
 *      authoritative current-codes list. Any CLDR-only code that is NOT in Debian is
 *      a CLDR ZOMBIE (retired-by-ISO-reassignment label that CLDR didn't drop) — we
 *      filter these at this layer and emit a D2GEO011 diagnostic per drop so the
 *      operator knows what was dropped.
 *   3. **Localized non-English labels**: Wikidata SPARQL labels (preferred — broader
 *      coverage than CLDR for ja/zh/ko/etc.); CLDR retained only as a secondary
 *      seed for the 11 supported locales before Wikidata overrides on conflict.
 *   4. **Endonym (country's primary-language label)**: Wikidata via the standard
 *      primary-language lookup, EXCEPT Norway (`NO`) where Wikidata has no
 *      unified `no` locale — the Norwegian cascade is `nb → nn → no → da → sv`.
 *
 * Hand-rolled overlays at `contracts/geo/overlays/subdivisions.overlays.spec.json`
 * apply LAST (Tier 2 build time) as explicit overrides, but are intended to stay
 * empty unless Wikidata.en is wrong AND the Debian fallback is also unacceptable.
 */
export interface SubdivisionPartial {
  /** Canonical ISO 3166-2 code (e.g., "US-CA"). PK. */
  iso31662Code: string;
  /** Suffix after the dash, e.g. "CA" for US-CA. Useful for compact display. */
  shortCode: string;
  /** Owning country (ISO 3166-1 alpha-2). FK to Country catalog. */
  countryISO31661Alpha2Code: string;
  /**
   * English display name. PRIMARY source: Wikidata.en label (P300); FALLBACK:
   * debian/iso-codes' `name` field. CLDR's English subdivision labels are NOT
   * used as displayName authority (stale for many post-2020 ISO reassignments —
   * see Iran IR-22/IR-23/IR-00, Norway 2020 county merges, Estonia EE-44/57/59).
   */
  displayName: string;
  /**
   * English official name. Mirrors displayName — Wikidata + Debian don't
   * distinguish official-vs-display for subdivisions; we keep the field for
   * Country-catalog parity.
   */
  officialName: string;
  /**
   * Country-primary-language endonym. Looked up from Wikidata's primary-language
   * label for the country, with a special cascade for Norway (`nb → nn → no →
   * da → sv` — Wikidata stores Norwegian under multiple Bokmål/Nynorsk variants
   * and lacks a unified `no` locale for many entries). Null when no source has a
   * label for the country's primary language.
   */
  endonymDisplayName: string | null;
  /**
   * Localized names across the 11 supported languages. Wikidata wins on conflict
   * with CLDR (broader coverage); CLDR contributions filled in where Wikidata
   * lacks a label for a supported locale.
   */
  localizedDisplayNames: Record<string, string>;
  /**
   * ISO 3166-2 subdivision type per the standard. From debian iso-codes.
   * Examples: "State", "Province", "Region", "Prefecture", "Canton", "Parish",
   * "Rayon", "Department", "County", "Governorate", "Autonomous community", etc.
   */
  type: string;
  /**
   * Parent ISO 3166-2 code (e.g., "GB-ENG" for many UK second-order entries).
   * Null for first-order subdivisions directly under the country.
   */
  parentISO31662Code: string | null;
  /**
   * 1 for first-order (no parent), 2 for second-order (parent has no parent), 3+ for deeper.
   * The catalog ships ALL orders (data layer); consumers filter on this field to restrict
   * to a subset (e.g., first-order only for a country picker UI).
   */
  order: number;
}

/**
 * Per-country breakdown of subdivision orders. Generated alongside the spec to
 * answer "which countries have second/third-order data, and how much?"
 */
export interface CountryOrderReport {
  countryISO31661Alpha2Code: string;
  firstOrder: number;
  secondOrder: number;
  thirdPlusOrder: number;
  total: number;
}

/**
 * Diagnostic row for a CLDR-zombie code dropped during transformation.
 * Surfaced via D2GEO011 warning + the missing-wikidata-en pipeline log so the
 * operator can confirm what was dropped on each refresh.
 */
export interface CldrZombieWarning {
  /** The CLDR-only ISO 3166-2 code (uppercase, hyphenated) that was dropped. */
  iso31662Code: string;
  /** Owning country (ISO 3166-1 alpha-2). */
  countryISO31661Alpha2Code: string;
  /** CLDR's English label for the dropped code (informational). */
  cldrEnLabel: string | null;
}

/**
 * Diagnostic row for a Debian-present code that lacks a Wikidata `en` label.
 * Wikidata.en is the primary displayName source; when it's missing we fall back
 * to the Debian `name` field. Operator reviews these to decide if a
 * `contracts/geo/overlays/subdivisions.overlays.spec.json` override is warranted
 * for awkward Debian fallbacks.
 */
export interface MissingWikidataEnRow {
  iso31662Code: string;
  countryISO31661Alpha2Code: string;
  /** The Debian `name` value used as fallback. */
  debianFallbackName: string;
  /** Suggested operator action — informational only. */
  suggestedAction: string;
}

/**
 * Per-country count of cases where Wikidata.en and Debian.name disagree.
 * Informational — surfaces the magnitude of label drift between the two
 * canonical sources at refresh time so the operator can gauge data churn.
 */
export interface PerCountryDivergenceRow {
  countryISO31661Alpha2Code: string;
  /** Number of subdivisions where Wikidata.en ≠ debian.name. */
  divergentCount: number;
  /** Total subdivisions in the country. */
  totalCount: number;
}

const SUPPORTED_LANGUAGE_CODES = [
  "en",
  "es",
  "fr",
  "de",
  "it",
  "ja",
  "nl",
  "ko",
  "zh",
  "pt",
  "pl",
] as const;

/**
 * Norwegian endonym cascade. Wikidata stores Norwegian labels under multiple
 * locale tags and has no unified `no` locale for many subdivisions. Order:
 * `nb` (Bokmål) → `nn` (Nynorsk) → `no` (generic Norwegian) → `da` (Danish,
 * mutually intelligible) → `sv` (Swedish, also intelligible). Applied ONLY
 * when the country is Norway (`NO`); other countries use the standard
 * primary-language lookup against the country's mapped language.
 */
const NORWEGIAN_ENDONYM_CASCADE = ["nb", "nn", "no", "da", "sv"] as const;

export interface SubdivisionsLoadResult {
  entries: SubdivisionPartial[];
  /** Per-country order breakdown for the report section of the spec. */
  countryOrderReports: CountryOrderReport[];
  /** Counts of debian entries with no CLDR match (interesting drift between sources). */
  debianOnly: number;
  cldrOnly: number;
  /** All fetch provenances for the spec's sources[] block. */
  provenance: FetchProvenance[];
  /** Wikidata fill counts (informational; not a regression signal). */
  wikidataEndonymFills: number;
  wikidataLocalizedAdds: number;
  /** Codes dropped because CLDR shipped them but Debian no longer does. */
  cldrZombieWarnings: CldrZombieWarning[];
  /**
   * Debian-present codes where Wikidata lacks an `en` label and we fell back to
   * the Debian `name` field. Operator-triage signal — write to
   * `logs/missing-wikidata-en.json` for review.
   */
  missingWikidataEn: MissingWikidataEnRow[];
  /**
   * Per-country count of subdivisions where Wikidata.en disagrees with Debian.
   * Informational — gauges label drift across refresh cycles.
   */
  perCountryDivergence: PerCountryDivergenceRow[];
}

export async function loadSubdivisions(
  countryPrimaryLanguageMap: Map<string, string>,
): Promise<SubdivisionsLoadResult> {
  console.error(`[fetch] debian/iso-codes iso_3166-2.json`);
  const debian = await fetchDebianIso31662();

  console.error(`[fetch] CLDR subdivisions for 11 supported languages`);
  const cldrByLocale = new Map<string, CldrSubdivisionsFetchResult>();
  const provenance: FetchProvenance[] = [debian.provenance];
  for (const locale of SUPPORTED_LANGUAGE_CODES) {
    const r = await fetchCldrSubdivisions(locale);
    cldrByLocale.set(locale, r);
    provenance.push(r.provenance);
  }

  // Build per-debian-entry spec entries
  const entries: SubdivisionPartial[] = [];
  const allCldrCodes = new Set<string>();
  for (const r of cldrByLocale.values()) {
    for (const k of Object.keys(r.rawIdToName)) {
      allCldrCodes.add(k); // CLDR concatenated form e.g. "usca"
    }
  }
  const seenCodes = new Set<string>();
  let unresolvedOrder = 0;

  for (const debEntry of debian.entries) {
    const isoCode = debEntry.code.toUpperCase();
    if (seenCodes.has(isoCode)) continue;
    seenCodes.add(isoCode);

    const parts = isoCode.split("-");
    const countryCode = parts[0];
    const shortCode = parts.slice(1).join("-"); // preserves multi-dash second-order codes
    if (!countryCode || !shortCode) continue;

    const order = computeOrder(debEntry, debian.byCode);
    if (order === null) {
      unresolvedOrder++;
      continue;
    }

    // CLDR lookups — seed localizedDisplayNames per supported locale. Wikidata
    // enrichment below replaces / extends these per-entry.
    const cldrConcatenated = isoCode.toLowerCase().split("-").join("");
    const localizedDisplayNames: Record<string, string> = {};
    for (const locale of SUPPORTED_LANGUAGE_CODES) {
      const name = cldrByLocale.get(locale)?.rawIdToName[cldrConcatenated];
      if (name) localizedDisplayNames[locale] = name;
    }

    // Provisional displayName / officialName + endonym from the
    // Debian + CLDR-seeded set — overridden below by Wikidata enrichment.
    // Falling back to debEntry.name keeps the entry valid even if Wikidata has
    // no label at all for the code (rare; ~140 small territories).
    const debianFallbackName = debEntry.name;
    const displayName = debianFallbackName;
    const officialName = displayName;
    const primaryLang = countryPrimaryLanguageMap.get(countryCode) ?? null;
    const endonymDisplayName = primaryLang
      ? (localizedDisplayNames[primaryLang] ?? null)
      : null;

    entries.push({
      iso31662Code: isoCode,
      shortCode,
      countryISO31661Alpha2Code: countryCode,
      displayName,
      officialName,
      endonymDisplayName,
      localizedDisplayNames,
      type: debEntry.type,
      parentISO31662Code: debEntry.parent
        ? debEntry.parent.toUpperCase()
        : null,
      order,
    });
  }

  if (unresolvedOrder > 0) {
    console.error(
      `  [WARN] ${unresolvedOrder} debian entries dropped due to unresolvable parent chain`,
    );
  }

  // Tally entries CLDR has but debian doesn't (and vice versa). The CLDR-only
  // set drives the D2GEO011 zombie filter — codes CLDR ships that Debian no
  // longer considers current ISO 3166-2 codes (likely retired by reassignment).
  const debianConcatenatedCodes = new Set(
    debian.entries.map((e) =>
      e.code.toUpperCase().toLowerCase().split("-").join(""),
    ),
  );
  let cldrOnly = 0;
  const cldrZombieWarnings: CldrZombieWarning[] = [];
  for (const cldrCode of allCldrCodes) {
    if (debianConcatenatedCodes.has(cldrCode)) continue;
    cldrOnly++;
    // Reconstruct human-form ISO 3166-2 code from CLDR's concatenated form.
    // CLDR uses lowercase no-hyphen (e.g. "ir31"); ISO form is "IR-31".
    if (cldrCode.length < 3) continue;
    const country = cldrCode.slice(0, 2).toUpperCase();
    const suffix = cldrCode.slice(2).toUpperCase();
    const isoForm = `${country}-${suffix}`;
    const cldrEnLabel = cldrByLocale.get("en")?.rawIdToName[cldrCode] ?? null;
    cldrZombieWarnings.push({
      iso31662Code: isoForm,
      countryISO31661Alpha2Code: country,
      cldrEnLabel,
    });
  }
  let debianOnly = 0;
  for (const c of debianConcatenatedCodes)
    if (!allCldrCodes.has(c)) debianOnly++;

  // Emit D2GEO011 warnings + a single summary line. Sorted for stable output.
  cldrZombieWarnings.sort((a, b) =>
    a.iso31662Code.localeCompare(b.iso31662Code),
  );
  if (cldrZombieWarnings.length > 0) {
    console.error(
      `  [D2GEO011] dropped ${cldrZombieWarnings.length} CLDR-zombie codes ` +
        `(CLDR ships them but debian/iso-codes no longer considers them current — ` +
        `likely retired by ISO 3166-2 reassignment). Per-code detail follows; ` +
        `refresh with pnpm geo:refresh to surface any new zombies.`,
    );
    for (const z of cldrZombieWarnings) {
      console.error(
        `    [D2GEO011] subdivision '${z.iso31662Code}' in ${z.countryISO31661Alpha2Code}: ` +
          `CLDR label="${z.cldrEnLabel ?? "(none)"}"; not in current Debian iso-codes; filtered.`,
      );
    }
  }

  // Per-country order breakdown
  const reportMap = new Map<string, CountryOrderReport>();
  for (const e of entries) {
    let row = reportMap.get(e.countryISO31661Alpha2Code);
    if (!row) {
      row = {
        countryISO31661Alpha2Code: e.countryISO31661Alpha2Code,
        firstOrder: 0,
        secondOrder: 0,
        thirdPlusOrder: 0,
        total: 0,
      };
      reportMap.set(e.countryISO31661Alpha2Code, row);
    }
    if (e.order === 1) row.firstOrder++;
    else if (e.order === 2) row.secondOrder++;
    else row.thirdPlusOrder++;
    row.total++;
  }
  const countryOrderReports = [...reportMap.values()].sort(
    (a, b) => b.total - a.total,
  );

  // Wikidata SPARQL endonym layer (CC0). Replaces CLDR's sparse non-English subdivision
  // names with Wikidata's much broader coverage (89-97% per language vs CLDR's near-zero
  // for ja/zh/ko). For our specific endonym use case (per-subdivision name in country's
  // primary language), Wikidata covers ~98%+ of major-country subdivisions.
  //
  // Wikidata.en is ALSO the primary source for displayName / officialName (post
  // 2026-05-23 investigation — CLDR.en drifts after ISO reassignments; Wikidata.en
  // tracks Wikipedia/ISO authority).
  const endonymLangs = await getEndonymLanguageList();
  console.error(
    `[fetch] Wikidata subdivision endonyms (${endonymLangs.length} languages)`,
  );
  const wikidata = await fetchSubdivisionEndonyms(endonymLangs);
  for (const fetch of wikidata.fetches) {
    provenance.push(fetch.provenance);
  }

  let wikidataEndonymFills = 0;
  let wikidataLocalizedAdds = 0;
  const missingWikidataEn: MissingWikidataEnRow[] = [];
  const perCountryDivergenceMap = new Map<string, PerCountryDivergenceRow>();
  for (const entry of entries) {
    const wEntry = wikidata.byCode.get(entry.iso31662Code);

    if (wEntry) {
      // Merge Wikidata labels into localizedDisplayNames (Wikidata wins on conflict;
      // CLDR's near-empty non-English data is replaced with Wikidata's comprehensive set).
      for (const [lang, label] of Object.entries(wEntry.labelsByLang)) {
        if (entry.localizedDisplayNames[lang] !== label) {
          if (!entry.localizedDisplayNames[lang]) wikidataLocalizedAdds++;
          entry.localizedDisplayNames[lang] = label;
        }
      }

      // PRIMARY DISPLAY NAME: Wikidata.en overrides the Debian fallback assigned
      // above. See KNOWN_WARNINGS.md for the source-priority rationale.
      const wikidataEn = entry.localizedDisplayNames["en"];
      if (wikidataEn) {
        entry.displayName = wikidataEn;
        entry.officialName = wikidataEn;
      }

      // Re-derive endonymDisplayName from Wikidata. Norway requires a special
      // cascade because Wikidata stores Norwegian under nb/nn (with no unified
      // `no` for many entries); other countries use the standard primary-language
      // lookup.
      const endonym = deriveEndonymWithCascade(
        wEntry.labelsByLang,
        entry.countryISO31661Alpha2Code,
        countryPrimaryLanguageMap,
      );
      if (endonym && endonym !== entry.endonymDisplayName) {
        if (!entry.endonymDisplayName) wikidataEndonymFills++;
        entry.endonymDisplayName = endonym;
      }
    }

    // Triage rows — fired AFTER the Wikidata.en override attempt so the
    // displayName field reflects the active fallback decision.
    if (!entry.localizedDisplayNames["en"]) {
      missingWikidataEn.push({
        iso31662Code: entry.iso31662Code,
        countryISO31661Alpha2Code: entry.countryISO31661Alpha2Code,
        debianFallbackName: entry.displayName,
        suggestedAction:
          "If the Debian fallback is awkward, consider adding an overlay entry " +
          "at contracts/geo/overlays/subdivisions.overlays.spec.json.",
      });
    }

    // Divergence tracking: count subdivisions where Wikidata.en disagrees with
    // the Debian fallback. Both populated AND different = a divergence row.
    let row = perCountryDivergenceMap.get(entry.countryISO31661Alpha2Code);
    if (!row) {
      row = {
        countryISO31661Alpha2Code: entry.countryISO31661Alpha2Code,
        divergentCount: 0,
        totalCount: 0,
      };
      perCountryDivergenceMap.set(entry.countryISO31661Alpha2Code, row);
    }
    row.totalCount++;
    // Look up the Debian-originally-assigned name from the entries list. We
    // re-derive it here rather than caching to avoid extra storage; the debian
    // entry was the displayName at the moment of `entries.push` BEFORE the
    // Wikidata override, so we look it up in debian.entries directly.
    const debEntry = debian.byCode.get(entry.iso31662Code);
    const debianName = debEntry?.name ?? entry.displayName;
    const wikidataEn = wEntry?.labelsByLang["en"];
    if (wikidataEn && wikidataEn !== debianName) {
      row.divergentCount++;
    }
  }
  console.error(
    `  [wikidata] subdivision endonym fills: ${wikidataEndonymFills}; ` +
      `localized name adds: ${wikidataLocalizedAdds}; ` +
      `Debian-fallback-only (no Wikidata.en): ${missingWikidataEn.length}`,
  );

  // Per-country divergence summary — informational only (helps the operator
  // gauge data drift). Sort by divergentCount desc so the largest drift
  // surfaces first.
  const perCountryDivergence = [...perCountryDivergenceMap.values()]
    .filter((r) => r.divergentCount > 0)
    .sort((a, b) => b.divergentCount - a.divergentCount);
  if (perCountryDivergence.length > 0) {
    const topRows = perCountryDivergence.slice(0, 10);
    console.error(
      `  [divergence] ${perCountryDivergence.length} countries have ≥1 subdivision where ` +
        `Wikidata.en ≠ debian.name. Top 10 by divergent count:`,
    );
    for (const r of topRows) {
      console.error(
        `    ${r.countryISO31661Alpha2Code}: ` +
          `${r.divergentCount}/${r.totalCount} subdivisions diverge`,
      );
    }
  }

  entries.sort((a, b) => a.iso31662Code.localeCompare(b.iso31662Code));

  return {
    entries,
    countryOrderReports,
    debianOnly,
    cldrOnly,
    provenance,
    wikidataEndonymFills,
    wikidataLocalizedAdds,
    cldrZombieWarnings,
    missingWikidataEn,
    perCountryDivergence,
  };
}

/**
 * Derives the country-primary-language endonym from a Wikidata `labelsByLang`
 * map. Norway gets the special cascade `nb → nn → no → da → sv` because
 * Wikidata stores Norwegian labels under multiple locale tags and lacks a
 * unified `no` locale for many subdivisions. All other countries use the
 * standard primary-language lookup against the country's mapped language.
 *
 * Returns undefined when no candidate label exists.
 */
export function deriveEndonymWithCascade(
  labelsByLang: Record<string, string>,
  country: string,
  countryPrimaryLanguageMap: Map<string, string>,
): string | undefined {
  if (country === "NO") {
    for (const tag of NORWEGIAN_ENDONYM_CASCADE) {
      const label = labelsByLang[tag];
      if (label) return label;
    }
    return undefined;
  }
  const primaryLang = countryPrimaryLanguageMap.get(country);
  if (!primaryLang) return undefined;
  return labelsByLang[primaryLang];
}
