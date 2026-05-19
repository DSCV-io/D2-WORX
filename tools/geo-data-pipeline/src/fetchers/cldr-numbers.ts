// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-numbers-full";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";

/**
 * CLDR `cldr-numbers-full/main/{locale}/numbers.json` shape (extract):
 *
 * ```
 * {
 *   "main": {
 *     "en": {
 *       "identity": { "language": "en" },
 *       "numbers": {
 *         "defaultNumberingSystem": "latn",
 *         "symbols-numberSystem-latn": {
 *           "decimal": ".",
 *           "group": ",",
 *           "percentSign": "%",
 *           "minusSign": "-",
 *           ...
 *         },
 *         "decimalFormats-numberSystem-latn": { "standard": "#,##0.###" },
 *         ...
 *       }
 *     }
 *   }
 * }
 * ```
 *
 * Some locales (Arabic, Persian, Bengali) default to non-latin numbering systems
 * (`arab`, `arabext`, `beng`, `deva`); the `defaultNumberingSystem` field names which
 * `symbols-numberSystem-{system}` block carries the canonical separators for that locale.
 */
export interface CldrNumbersPayload {
  main: Record<string, {
    identity: { language: string };
    numbers: {
      defaultNumberingSystem?: string;
      [k: string]: unknown;
    };
  }>;
}

export interface CldrNumbersFetchResult extends Pick<CachedFetch, "provenance" | "fromCache"> {
  locale: string;
  defaultNumberingSystem: string;
  decimalSeparator: string;
  thousandsSeparator: string;
  percentSign: string;
  minusSign: string;
}

const FALLBACK_NUMBERING_SYSTEM = "latn";
const FALLBACK_DECIMAL = ".";
const FALLBACK_GROUP = ",";
const FALLBACK_PERCENT = "%";
const FALLBACK_MINUS = "-";

export async function fetchCldrNumbers(locale: string, options?: {
  ttlHours?: number;
}): Promise<CldrNumbersFetchResult> {
  // upstream URL — cannot wrap
  const url = `https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-numbers-full/main/${locale}/numbers.json`;
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url,
    license: SOURCE_LICENSE,
    cacheKey: `${locale}-numbers.json`,
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(fetched.body.toString("utf8")) as CldrNumbersPayload;
  const numbers = payload.main[locale]?.numbers;
  if (!numbers) {
    throw new Error(`CLDR numbers.json for ${locale} missing main.${locale}.numbers block`);
  }

  const system = numbers.defaultNumberingSystem ?? FALLBACK_NUMBERING_SYSTEM;
  const symbolsKey = `symbols-numberSystem-${system}`;
  const fallbackKey = `symbols-numberSystem-${FALLBACK_NUMBERING_SYSTEM}`;
  const symbols = (numbers[symbolsKey] ?? numbers[fallbackKey]) as
    | { decimal?: string; group?: string; percentSign?: string; minusSign?: string }
    | undefined;

  return {
    locale,
    defaultNumberingSystem: system,
    decimalSeparator: symbols?.decimal ?? FALLBACK_DECIMAL,
    thousandsSeparator: symbols?.group ?? FALLBACK_GROUP,
    percentSign: symbols?.percentSign ?? FALLBACK_PERCENT,
    minusSign: symbols?.minusSign ?? FALLBACK_MINUS,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}
