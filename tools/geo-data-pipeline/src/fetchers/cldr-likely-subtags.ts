// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-core";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";
// upstream URL — cannot wrap
const SOURCE_URL =
  "https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-core/supplemental/likelySubtags.json";

/**
 * CLDR's `likelySubtags.json` — the canonical lang → default lang-Script-Region mapping.
 *
 * ```
 * {
 *   "supplemental": {
 *     "likelySubtags": {
 *       "en":  "en-Latn-US",
 *       "ja":  "ja-Jpan-JP",
 *       "zh":  "zh-Hans-CN",
 *       "pt":  "pt-Latn-BR",
 *       "ar":  "ar-Arab-EG",
 *       "zh-Hant": "zh-Hant-TW",
 *       "sr-Cyrl": "sr-Cyrl-RS",
 *       ...
 *     }
 *   }
 * }
 * ```
 *
 * Used to derive the "default region" for a bare language tag — e.g., "en" implies
 * "en-US", "pt" implies "pt-BR" (since CLDR's availableLocales.json deliberately omits
 * lang-DefaultRegion tags as redundant).
 */
export interface LikelySubtagsPayload {
  supplemental: {
    likelySubtags: Record<string, string>;
  };
}

export interface LikelySubtagsFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  /** Map from source tag → expanded `<lang>-<Script>-<REGION>` tag. */
  bySourceTag: Map<string, string>;
}

export async function fetchCldrLikelySubtags(options?: {
  ttlHours?: number;
}): Promise<LikelySubtagsFetchResult> {
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url: SOURCE_URL,
    license: SOURCE_LICENSE,
    cacheKey: "likelySubtags.json",
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(
    fetched.body.toString("utf8"),
  ) as LikelySubtagsPayload;
  const bySourceTag = new Map<string, string>();
  for (const [src, expanded] of Object.entries(
    payload.supplemental.likelySubtags,
  )) {
    bySourceTag.set(src, expanded);
  }
  return {
    bySourceTag,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}

/**
 * Returns the lang-Region tag derived from CLDR likelySubtags for the given source tag.
 * E.g., "en" → "en-US", "pt" → "pt-BR", "zh" → "zh-CN", "zh-Hant" → "zh-TW".
 * Returns null when CLDR has no expansion or the result has no region subtag.
 *
 * Note: the expansion is the FULL lang-Script-Region form. We then drop the script subtag
 * unless it differs from the language's default script — that's left to the caller.
 */
export function deriveDefaultRegionTag(
  source: string,
  bySourceTag: ReadonlyMap<string, string>,
): string | null {
  const expanded = bySourceTag.get(source);
  if (!expanded) return null;
  // Parse expanded: <lang>-<Script>-<REGION>
  const parts = expanded.split("-");
  if (parts.length !== 3) return null;
  const [lang, , region] = parts;
  if (!lang || !region) return null;
  return `${lang}-${region}`;
}
