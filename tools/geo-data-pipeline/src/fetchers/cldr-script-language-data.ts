// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-core";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";

// scriptMetadata.json lives at cldr-core/ root (NOT under supplemental/)
// upstream URL — cannot wrap
const URL_SCRIPT_METADATA =
  "https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-core/scriptMetadata.json";
// languageData.json lives under supplemental/
// upstream URL — cannot wrap
const URL_LANGUAGE_DATA =
  "https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-core/supplemental/languageData.json";

/**
 * scriptMetadata.json — per ISO 15924 script code:
 *
 * ```
 * {
 *   "scriptMetadata": {
 *     "Arab": { "rtl": "YES", "originCountry": "SA", "likelyLanguage": "ar", "rank": 8, ... },
 *     "Latn": { "rtl": "NO", "originCountry": "IT", "likelyLanguage": "en", "rank": 2, ... },
 *     "Hebr": { "rtl": "YES", "originCountry": "IL", "likelyLanguage": "he", ... }
 *   }
 * }
 * ```
 *
 * We consume `rtl` (YES/NO) to derive Language.WritingDirection (RTL/LTR).
 */
export interface ScriptMetadataPayload {
  scriptMetadata: Record<string, {
    rtl?: "YES" | "NO";
    originCountry?: string;
    likelyLanguage?: string;
    [k: string]: unknown;
  }>;
}

/**
 * languageData.json — per ISO 639-1 lang code:
 *
 * ```
 * {
 *   "supplemental": {
 *     "languageData": {
 *       "ja": { "_scripts": ["Jpan"] },
 *       "ar": { "_scripts": ["Arab"] },
 *       "en": { "_scripts": ["Latn"] }
 *     }
 *   }
 * }
 * ```
 *
 * `_scripts` is ordered by usage — first entry is the primary script for the language.
 * Some entries have alternate forms like `<lang>-alt-secondary` for less-common scripts.
 */
export interface LanguageDataPayload {
  supplemental: {
    languageData: Record<string, {
      _scripts?: string[];
      _territories?: string[];
      [k: string]: unknown;
    }>;
  };
}

export interface ScriptLanguageDataFetchResult
  extends Pick<CachedFetch, "provenance" | "fromCache"> {
  scriptMetadata: ScriptMetadataPayload["scriptMetadata"];
  languageData: LanguageDataPayload["supplemental"]["languageData"];
  scriptMetadataProvenance: CachedFetch["provenance"];
  languageDataProvenance: CachedFetch["provenance"];
}

export async function fetchScriptAndLanguageData(options?: {
  ttlHours?: number;
}): Promise<ScriptLanguageDataFetchResult> {
  const [scripts, langs] = await Promise.all([
    fetchAndCache({
      source: SOURCE_NAME,
      url: URL_SCRIPT_METADATA,
      license: SOURCE_LICENSE,
      cacheKey: "scriptMetadata.json",
      ttlHours: options?.ttlHours,
    }),
    fetchAndCache({
      source: SOURCE_NAME,
      url: URL_LANGUAGE_DATA,
      license: SOURCE_LICENSE,
      cacheKey: "languageData.json",
      ttlHours: options?.ttlHours,
    }),
  ]);
  const smPayload = JSON.parse(scripts.body.toString("utf8")) as ScriptMetadataPayload;
  const ldPayload = JSON.parse(langs.body.toString("utf8")) as LanguageDataPayload;
  return {
    scriptMetadata: smPayload.scriptMetadata,
    languageData: ldPayload.supplemental.languageData,
    provenance: scripts.provenance, // primary; both also exposed separately
    scriptMetadataProvenance: scripts.provenance,
    languageDataProvenance: langs.provenance,
    fromCache: scripts.fromCache && langs.fromCache,
  };
}

/**
 * Returns "RTL" if the language's primary script is right-to-left, else "LTR".
 * Null when the language is unknown or no scripts are mapped.
 */
export function deriveWritingDirection(
  iso639_1: string,
  scriptMetadata: ScriptMetadataPayload["scriptMetadata"],
  languageData: LanguageDataPayload["supplemental"]["languageData"],
): "LTR" | "RTL" | null {
  const entry = languageData[iso639_1];
  const primaryScript = entry?._scripts?.[0];
  if (!primaryScript) return null;
  const meta = scriptMetadata[primaryScript];
  if (!meta) return null;
  return meta.rtl === "YES" ? "RTL" : "LTR";
}
