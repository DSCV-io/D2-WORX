// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { deriveDefaultRegionTag } from "../fetchers/cldr-likely-subtags.js";

/**
 * Computes the canonical "candidate locale tags" set the locales catalog will
 * ship — CLDR availableLocales `full` PLUS derived lang-Region tags via
 * likelySubtags (bare `en` -> `en-US`, `zh-Hans` -> `zh-Hans-CN`,
 * `zh-Hant` -> `zh-Hant-TW`). Mirrors the derivation block in
 * `write-locales.ts` so write-countries can predict the same locales-catalog
 * shape WITHOUT a dependency on write-locales running first.
 *
 * Pure function — no I/O. CLDR data passed in by caller.
 */
export function computeLocaleCatalogTags(input: {
  cldrAvailableLocaleFullTags: readonly string[];
  cldrLikelySubtags: ReadonlyMap<string, string>;
}): Set<string> {
  const tags = new Set<string>(input.cldrAvailableLocaleFullTags);
  for (const tag of input.cldrAvailableLocaleFullTags) {
    const parts = tag.split("-");
    if (parts.length === 1) {
      const defaultRegion = deriveDefaultRegionTag(
        tag,
        input.cldrLikelySubtags,
      );
      if (defaultRegion) tags.add(defaultRegion);
    } else if (
      parts.length === 2 &&
      parts[1] &&
      /^[A-Z][a-z]{3}$/.test(parts[1])
    ) {
      const expanded = input.cldrLikelySubtags.get(tag);
      if (expanded) {
        const expParts = expanded.split("-");
        if (
          expParts.length === 3 &&
          expParts[0] &&
          expParts[1] &&
          expParts[2]
        ) {
          tags.add(`${expParts[0]}-${expParts[1]}-${expParts[2]}`);
        }
      }
    }
  }
  return tags;
}

/**
 * Builds a per-region candidate-locale map from the post-derivation tag set.
 * Region subtag = the LAST 2-uppercase-letter segment of a tag. UN M49 numeric
 * regions (3-digit like `001`) are NOT included since they don't map to ISO
 * 3166-1 alpha-2 countries.
 */
export function indexLocaleTagsByRegion(
  tags: Iterable<string>,
): Map<string, string[]> {
  const byRegion = new Map<string, string[]>();
  for (const tag of tags) {
    const parts = tag.split("-");
    const last = parts[parts.length - 1];
    if (last && /^[A-Z]{2}$/.test(last)) {
      const list = byRegion.get(last) ?? [];
      list.push(tag);
      byRegion.set(last, list);
    }
  }
  for (const list of byRegion.values()) list.sort();
  return byRegion;
}
