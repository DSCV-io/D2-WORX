// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-core-supplemental";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";
const URL_BASE =
  "https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-core/supplemental";

/**
 * Known CLDR supplemental files we consume. Each is a small JSON file (1-100KB)
 * under cldr-core/supplemental/. Naming the files explicitly here keeps a
 * single source of truth for which supplemental data we depend on.
 */
export type CldrSupplementalFile =
  | "weekData"
  | "measurementData"
  | "currencyData"
  | "territoryInfo"
  | "languageData"
  | "telephoneCodeData";

export interface CldrSupplementalFetchResult<TPayload = unknown> extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  file: CldrSupplementalFile;
  payload: TPayload;
}

export async function fetchCldrSupplemental<TPayload = unknown>(
  file: CldrSupplementalFile,
  options?: { ttlHours?: number },
): Promise<CldrSupplementalFetchResult<TPayload>> {
  const url = `${URL_BASE}/${file}.json`;
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url,
    license: SOURCE_LICENSE,
    cacheKey: `${file}.json`,
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(fetched.body.toString("utf8")) as TPayload;
  return {
    file,
    payload,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}

// -----------------------------------------------------------------------------
// Typed shapes for the supplemental files we currently consume
// -----------------------------------------------------------------------------

/**
 * weekData.json shape:
 * {
 *   "supplemental": {
 *     "version": { ... },
 *     "weekData": {
 *       "minDays": { "001": "1", "US": "1", ... },
 *       "firstDay": { "001": "mon", "US": "sun", "AE": "sat", ... },
 *       "weekendStart": { "001": "sat", "EG": "fri", ... },
 *       "weekendEnd": { "001": "sun", "EG": "sat", ... }
 *     }
 *   }
 * }
 *
 * Day codes: sun, mon, tue, wed, thu, fri, sat
 * Region codes: ISO 3166-1 alpha-2 + "001" (default for unspecified territories)
 */
export interface WeekDataPayload {
  supplemental: {
    weekData: {
      minDays: Record<string, string>;
      firstDay: Record<string, string>;
      weekendStart?: Record<string, string>;
      weekendEnd?: Record<string, string>;
    };
  };
}

/**
 * measurementData.json shape:
 * {
 *   "supplemental": {
 *     "measurementData": {
 *       "measurementSystem": {
 *         "001": "metric", "US": "ussystem", "LR": "ussystem", "MM": "metric", ...
 *       },
 *       "measurementSystem-category-temperature": {
 *         "001": "metric", "US": "ussystem", ...
 *       },
 *       "paperSize": { "001": "A4", "US": "US-Letter", ... }
 *     }
 *   }
 * }
 */
export interface MeasurementDataPayload {
  supplemental: {
    measurementData: {
      measurementSystem: Record<string, string>;
      ["measurementSystem-category-temperature"]?: Record<string, string>;
      paperSize?: Record<string, string>;
    };
  };
}

/**
 * currencyData.json shape (Country.Currencies source — legal tender + dates):
 * {
 *   "supplemental": {
 *     "currencyData": {
 *       "fractions": { ... },
 *       "region": {
 *         "US": [
 *           { "USD": { "_from": "1792-01-01" } }
 *         ],
 *         "DE": [
 *           { "DEM": { "_from": "1948-06-20", "_to": "2002-02-28" } },
 *           { "EUR": { "_from": "1999-01-01" } }
 *         ],
 *         "AR": [
 *           { "ARP": { "_from": "1983-06-01", "_to": "1985-06-14" } },
 *           { "ARA": { "_from": "1985-06-15", "_to": "1992-12-31" } },
 *           { "ARS": { "_from": "1992-01-01" } }
 *         ],
 *         ...
 *       }
 *     }
 *   }
 * }
 *
 * Each region entry is an array of single-key objects. Active currencies have
 * `_from` but no `_to`. Historical currencies have both. `_tender: false` flag
 * marks non-legal-tender (e.g., USS in US).
 */
export interface CurrencyDataPayload {
  supplemental: {
    currencyData: {
      /**
       * Per-currency fractional / rounding metadata:
       * ```
       * "fractions": {
       *   "DEFAULT": { "_rounding": "0", "_digits": "2" },
       *   "JPY":     { "_rounding": "0", "_digits": "0" },
       *   "KWD":     { "_rounding": "0", "_digits": "3" },
       *   "CHF":     { "_rounding": "5", "_digits": "2" }
       * }
       * ```
       * `_digits` = number of decimal places. `_rounding` = nearest rounding increment.
       * `_cashDigits` / `_cashRounding` exist for cash-vs-accounting differences (CHF, etc.).
       * "DEFAULT" entry applies to currencies not explicitly listed.
       */
      fractions?: Record<
        string,
        {
          _rounding?: string;
          _digits?: string;
          _cashRounding?: string;
          _cashDigits?: string;
        }
      >;
      region: Record<
        string,
        Array<
          Record<string, { _from?: string; _to?: string; _tender?: string }>
        >
      >;
    };
  };
}

/**
 * telephoneCodeData.json shape:
 * Newer CLDR releases REMOVED this file in favor of libphonenumber. Older releases:
 * {
 *   "supplemental": {
 *     "telephoneCodeData": {
 *       "AC": [ { "telephoneCountryCode": { "_code": "247" } } ],
 *       ...
 *     }
 *   }
 * }
 *
 * As of CLDR 38+, this is deprecated. We skip it and use libphonenumber instead.
 */

/**
 * territoryInfo.json shape (GDP + population + language stats per region):
 * {
 *   "supplemental": {
 *     "territoryInfo": {
 *       "US": {
 *         "_gdp": "21433000000000",
 *         "_literacyPercent": "99",
 *         "_population": "331002647",
 *         "languagePopulation": {
 *           "en": { "_populationPercent": "78.6", "_officialStatus": "de_facto_official" },
 *           "es": { "_populationPercent": "13.4" },
 *           ...
 *         }
 *       }
 *     }
 *   }
 * }
 */
export interface TerritoryInfoPayload {
  supplemental: {
    territoryInfo: Record<
      string,
      {
        _gdp?: string;
        _literacyPercent?: string;
        _population?: string;
        languagePopulation?: Record<
          string,
          {
            _populationPercent?: string;
            _officialStatus?: string;
            _writingPercent?: string;
          }
        >;
      }
    >;
  };
}
