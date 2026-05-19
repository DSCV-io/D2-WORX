// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "cldr-dates-full";
const SOURCE_LICENSE = "Unicode-3.0 (Unicode License)";

/**
 * CLDR `cldr-dates-full/main/{locale}/ca-gregorian.json` shape (extract):
 *
 * ```
 * {
 *   "main": {
 *     "en": {
 *       "dates": {
 *         "calendars": {
 *           "gregorian": {
 *             "dateFormats": {
 *               "full":   "EEEE, MMMM d, y",
 *               "long":   "MMMM d, y",
 *               "medium": "MMM d, y",
 *               "short":  "M/d/yy"     ← we read this to derive DMY/MDY/YMD
 *             }
 *           }
 *         }
 *       }
 *     }
 *   }
 * }
 * ```
 *
 * The `short` pattern uses CLDR pattern letters: `y` = year, `M` = month, `d` = day-of-month
 * (case-sensitive — lowercase m means minute in CLDR; uppercase D means day-of-year, etc.).
 * Derivation: scan the pattern for the FIRST occurrence of `y` / `M` / `d`, sort by position →
 * the order of those three letters IS the date format pattern.
 *   "M/d/yy"  → M, d, y → MDY
 *   "dd.MM.y" → d, M, y → DMY
 *   "y/MM/dd" → y, M, d → YMD
 */
/**
 * CLDR `dateFormats.short` is normally a string. For some locales (e.g. `haw`) it
 * comes wrapped as `{ "_value": "d/M/yy", "_numbers": "M=romanlow" }` — a non-default
 * numbering-system annotation. We accept either form and extract the pattern string.
 */
type CldrShortDatePattern = string | { _value?: string };

export interface CldrCaGregorianPayload {
  main: Record<string, {
    dates: {
      calendars: {
        gregorian: {
          dateFormats: {
            full?: string;
            long?: string;
            medium?: string;
            short?: CldrShortDatePattern;
          };
        };
      };
    };
  }>;
}

export type DateFormatPattern = "DMY" | "MDY" | "YMD";

export interface CldrDatesFetchResult extends Pick<CachedFetch, "provenance" | "fromCache"> {
  locale: string;
  /** Raw CLDR short date pattern (e.g., "M/d/yy"). */
  shortPattern: string;
  /** Derived enum value (DMY / MDY / YMD). */
  dateFormatPattern: DateFormatPattern;
}

export async function fetchCldrDates(locale: string, options?: {
  ttlHours?: number;
}): Promise<CldrDatesFetchResult> {
  // upstream URL — cannot wrap
  const url = `https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-dates-full/main/${locale}/ca-gregorian.json`;
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url,
    license: SOURCE_LICENSE,
    cacheKey: `${locale}-ca-gregorian.json`,
    ttlHours: options?.ttlHours,
  });
  const payload = JSON.parse(fetched.body.toString("utf8")) as CldrCaGregorianPayload;
  const raw = payload.main[locale]?.dates?.calendars?.gregorian?.dateFormats?.short;
  const shortPattern = extractShortPattern(raw);
  if (!shortPattern) {
    throw new Error(`CLDR ca-gregorian.json for ${locale} missing dateFormats.short`);
  }

  return {
    locale,
    shortPattern,
    dateFormatPattern: deriveDateFormatPattern(shortPattern),
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}

export function extractShortPattern(raw: CldrShortDatePattern | undefined): string | null {
  if (typeof raw === "string") return raw;
  if (raw && typeof raw === "object" && typeof raw._value === "string") return raw._value;
  return null;
}

/**
 * Scans `pattern` for the first index of each CLDR pattern letter
 * (`y`, `M`, `d` — case-sensitive), sorts by index, and returns the resulting ordering
 * as DMY / MDY / YMD.
 *
 * Throws when any of the three letters is missing — CLDR short patterns always include all
 * three. (Some locales use `r` for related-Gregorian year; we still expect y in the standard
 * Gregorian calendar's dateFormats.short.)
 */
export function deriveDateFormatPattern(pattern: string): DateFormatPattern {
  const yIdx = pattern.indexOf("y");
  const mIdx = pattern.indexOf("M");
  const dIdx = pattern.indexOf("d");
  if (yIdx < 0 || mIdx < 0 || dIdx < 0) {
    throw new Error(`Cannot derive DateFormatPattern from pattern "${pattern}" — missing y/M/d`);
  }
  const order = [
    { letter: "y" as const, idx: yIdx },
    { letter: "M" as const, idx: mIdx },
    { letter: "d" as const, idx: dIdx },
  ].sort((a, b) => a.idx - b.idx).map((x) => x.letter).join("");
  switch (order) {
    case "Mdy": return "MDY";
    case "dMy": return "DMY";
    case "yMd": return "YMD";
    case "ydM": return "YMD"; // edge case: y first, then d, then M (rare; treat as Y-first)
    case "Myd": return "MDY"; // edge case: M, y, d
    case "dyM": return "DMY"; // edge case: d, y, M
    default:
      // All 6 permutations covered above; this is unreachable for well-formed input.
      throw new Error(`Unexpected date-letter order "${order}" from pattern "${pattern}"`);
  }
}
