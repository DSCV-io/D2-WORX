// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Derives a country's `primaryLocaleIETFBCP47Tag` such that the resulting tag is
 * guaranteed to exist in the locales catalog (post-overlay-applied). The naive
 * `${primaryLanguageISO6391Code}-${iso31661Alpha2Code}` concatenation that
 * preceded this helper produced 20 country entries (CC, HK, MH, MO, MP, ME, NR,
 * NU, PH, PW, SG, SJ, TF, TK, TL, TV, TW, VU, WF, WS) that referenced locales
 * the catalog does not ship — D2GEO012 source-gen-time error.
 *
 * Algorithm (in order, first match wins):
 *
 *   1. Start with `{primaryLang639-1}-{regionAlpha2}` (the naive tag).
 *   2. Apply CLDR canonical script-subtag expansion via `likelySubtags`:
 *      e.g., `zh-HK` -> `zh-Hant-HK`, `zh-TW` -> `zh-Hant-TW`, `zh-MO` ->
 *      `zh-Hant-MO`, `sr-ME` -> `sr-Latn-ME`. If the expanded form is in the
 *      catalog, return it.
 *   3. Special-case `tl` (Tagalog — ISO 639-1) -> `fil` (Filipino) when the
 *      country's locale list carries a `fil-{region}` entry (PH, MP). Treat
 *      `fil` as the canonical successor of `tl` per CLDR.
 *   4. Special-case `no` (Norwegian — macro-language) -> `nb` (Bokmaal) for
 *      regions where CLDR ships `nb-{region}` instead (SJ).
 *   5. CLDR availableLocales validation: if the (possibly script-expanded) tag
 *      exists in CLDR availableLocales OR in the country's `localeIETFBCP47Tags`
 *      list (the post-overlay locales catalog), return it.
 *   6. Fallback: walk the country's `localeIETFBCP47Tags` array and pick the
 *      first match — preference order is `en-{region}` (English first; the
 *      strongest cross-region anchor) then the first entry in the array.
 *      Handles small-territory cases where the primary language is ISO 639-3
 *      and absent from our catalog (MH, NR, NU, PW, TK, TV, VU, WS, TL, WF).
 *   7. If nothing matches, return null. The pipeline-time validateLocaleRefs
 *      pass surfaces this as a hard error so the issue is caught at
 *      `pnpm tier-2:build` rather than deferred to .NET source-gen.
 *
 * The function is pure: no side effects, no I/O. All CLDR data is passed in
 * via parameters so unit tests can mock arbitrary catalog states.
 */

/** Shape consumed from the country's pipeline state — keeps the helper decoupled. */
export interface PrimaryLocaleDerivationInput {
  /** ISO 3166-1 alpha-2 country code (e.g., "HK"). */
  regionAlpha2: string;
  /**
   * Country's primary language code. Usually ISO 639-1 (`zh`, `en`) but CLDR
   * territoryInfo also surfaces ISO 639-3 codes (`fil`, `cmn`, `tet`, `niu`,
   * `pau`, `tkl`, `tvl`, `wls`, `tl`) for small-population territories. Null
   * when the country has no resolvable primary language.
   */
  primaryLanguageCode: string | null;
  /**
   * BCP 47 locale tags that the locales catalog ships for this country region
   * (derived from `Locale.regionSubtag === country.iso31661Alpha2Code` at the
   * Tier 2 cross-catalog merge step). Used for fallback resolution.
   */
  candidateLocaleTags: readonly string[];
}

/**
 * Language code aliases — when the country's primary language is the LEFT,
 * prefer the RIGHT before falling back to en-{region}. Each entry has a clear
 * CLDR / linguistic rationale; do not add aliases without evidence.
 *
 * - `tl` -> `fil`: ISO 639-1 Tagalog is the linguistic parent; Filipino (`fil`)
 *   is the official derived national language CLDR ships locales for (fil-PH).
 *   PH country spec carries `primaryLanguageISO6391Code: "tl"` but the locales
 *   catalog has no `tl-PH`; CLDR ships `fil-PH` instead.
 * - `no` -> `nb`: Norwegian (`no`) is a macro-language; CLDR ships Bokmaal
 *   (`nb`) locales (nb-SJ, nb-NO) rather than the macro form. SJ specifically
 *   has `nb-SJ` in the catalog.
 * - `cmn` -> `zh`: Mandarin Chinese (ISO 639-3 `cmn`) maps to the CLDR
 *   macro-language `zh` for catalog purposes (zh-Hans-SG for Singapore).
 */
const LANGUAGE_ALIASES: ReadonlyArray<readonly [string, string]> = [
  ["tl", "fil"],
  ["no", "nb"],
  ["cmn", "zh"],
];

/**
 * Pure derivation. Returns the chosen tag or null if no valid tag can be derived.
 * See module-level JSDoc for the algorithm.
 */
export function derivePrimaryLocaleTag(
  input: PrimaryLocaleDerivationInput,
  catalog: {
    /** CLDR likelySubtags map (source tag -> "lang-Script-Region"). */
    cldrLikelySubtags: ReadonlyMap<string, string>;
    /** Tags present in CLDR availableLocales `full` set. */
    cldrAvailableLocaleTags: ReadonlySet<string>;
  },
): string | null {
  const region = input.regionAlpha2;
  const candidatesByRegion = new Set(input.candidateLocaleTags);

  // Helper — is `tag` resolvable (catalog-shipped OR present in the country's
  // own locale list)? Either signals downstream consumers (esp. .NET source-gen
  // LocaleLookup) will find it.
  const isResolvable = (tag: string): boolean =>
    catalog.cldrAvailableLocaleTags.has(tag) || candidatesByRegion.has(tag);

  // Build the lang candidate list: original + any aliases.
  const langCandidates: string[] = [];
  if (input.primaryLanguageCode) {
    langCandidates.push(input.primaryLanguageCode);
    for (const [from, to] of LANGUAGE_ALIASES) {
      if (input.primaryLanguageCode === from && !langCandidates.includes(to)) {
        langCandidates.push(to);
      }
    }
  }

  // Pass 2 + Pass 5: try each language candidate. For each, try the script-
  // expanded form (via likelySubtags) first, then the plain `lang-region` form,
  // then any 3-part `{lang}-Script-{region}` tag the country's candidate list
  // ships (covers `zh-Hans-SG` etc. where the per-region likelySubtags entry is
  // absent but the script-specific catalog tag exists).
  for (const lang of langCandidates) {
    const plain = `${lang}-${region}`;

    // CLDR likelySubtags expansion: lookup keyed by the plain tag yields
    // `lang-Script-Region`. We preserve the script subtag and re-emit
    // `lang-Script-region`.
    const expanded = catalog.cldrLikelySubtags.get(plain);
    if (expanded) {
      const parts = expanded.split("-");
      if (parts.length === 3 && parts[0] && parts[1] && parts[2]) {
        const scriptTag = `${parts[0]}-${parts[1]}-${parts[2]}`;
        if (isResolvable(scriptTag)) return scriptTag;
      }
    }

    // Plain `lang-region` form — return if catalog ships it OR the country's
    // own locale list carries it.
    if (isResolvable(plain)) return plain;

    // Per-region script-expanded scan: walk candidate tags looking for any
    // 3-part `{lang}-Script-{region}` whose lang matches this candidate.
    // Handles the case where CLDR ships `zh-Hans-SG` but no `zh-SG`
    // likelySubtags entry. Sorted to keep deterministic when multiple scripts
    // exist (e.g., Hans before Hant alphabetically).
    const sortedCandidates = [...input.candidateLocaleTags].sort();
    for (const candidate of sortedCandidates) {
      const parts = candidate.split("-");
      if (parts.length === 3 && parts[0] === lang && parts[2] === region) {
        return candidate;
      }
    }
  }

  // Pass 6 fallback: pick from the country's own locales list. Preference:
  //   1. `en-{region}` if present (English is the strongest cross-region anchor)
  //   2. First entry in the sorted candidate list (deterministic)
  if (input.candidateLocaleTags.length > 0) {
    const englishTag = `en-${region}`;
    if (candidatesByRegion.has(englishTag)) return englishTag;
    // Sort defensively; Tier 2 already sorts but the helper shouldn't rely on
    // caller-side ordering for determinism.
    const sorted = [...input.candidateLocaleTags].sort();
    return sorted[0] ?? null;
  }

  // Pass 7: no derivation possible. Caller / validateLocaleRefs surfaces this.
  return null;
}
