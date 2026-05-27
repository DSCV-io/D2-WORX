// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-numbers-full";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";

/**
 * CLDR `cldr-numbers-full/main/{locale}/currencies.json` shape:
 *
 * ```
 * {
 *   "main": {
 *     "en": {
 *       "identity": { "language": "en" },
 *       "numbers": {
 *         "currencies": {
 *           "USD": {
 *             "displayName": "US Dollar",
 *             "displayName-count-one": "US dollar",
 *             "displayName-count-other": "US dollars",
 *             "symbol": "$",
 *             "symbol-alt-narrow": "$"
 *           },
 *           "EUR": {
 *             "displayName": "Euro",
 *             "symbol": "€",
 *             "symbol-alt-narrow": "€"
 *           },
 *           "AFN": {
 *             "displayName": "Afghan Afghani",
 *             "symbol": "AFN",
 *             "symbol-alt-narrow": "؋"
 *           }
 *         }
 *       }
 *     }
 *   }
 * }
 * ```
 *
 * Keys are ISO 4217 alpha-3 codes. Display names include retired currencies with
 * year-range suffixes (e.g., "Afghan Afghani (1927–2002)" for AFA).
 * `symbol` defaults to the alpha-3 code if no glyph is established.
 * `symbol-alt-narrow` is the most compact form (e.g., "؋" instead of "AFN").
 */
export interface CldrCurrenciesPayload {
  main: Record<
    string,
    {
      identity: { language: string };
      numbers: {
        currencies: Record<
          string,
          {
            displayName?: string;
            "displayName-count-one"?: string;
            "displayName-count-other"?: string;
            symbol?: string;
            "symbol-alt-narrow"?: string;
          }
        >;
      };
    }
  >;
}

export interface CldrCurrencyEntry {
  displayName: string | null;
  symbol: string | null;
  symbolNarrow: string | null;
}

export interface CldrCurrenciesFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  locale: string;
  byCurrencyCode: Map<string, CldrCurrencyEntry>;
}

export async function fetchCldrCurrencies(
  locale: string,
  options?: {
    ttlHours?: number;
  },
): Promise<CldrCurrenciesFetchResult> {
  // upstream URL — cannot wrap
  const url = `https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-numbers-full/main/${locale}/currencies.json`;
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url,
    license: SOURCE_LICENSE,
    cacheKey: `${locale}-currencies.json`,
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(
    fetched.body.toString("utf8"),
  ) as CldrCurrenciesPayload;
  const raw = payload.main[locale]?.numbers?.currencies ?? {};
  const byCurrencyCode = new Map<string, CldrCurrencyEntry>();
  for (const [code, entry] of Object.entries(raw)) {
    byCurrencyCode.set(code, {
      displayName: entry.displayName ?? null,
      symbol: entry.symbol ?? null,
      symbolNarrow: entry["symbol-alt-narrow"] ?? null,
    });
  }
  return {
    locale,
    byCurrencyCode,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}
