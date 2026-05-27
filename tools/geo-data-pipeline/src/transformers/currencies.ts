// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CldrCurrencyEntry } from "../fetchers/cldr-currencies.js";
import type { CurrencyDataPayload } from "../fetchers/cldr-supplemental.js";
import type { CurrencyCodeRow } from "../fetchers/datasets-currency-codes.js";

const DEFAULT_DECIMAL_PLACES = 2;

/**
 * Known ISO 4217 alpha-code displayName overrides for currencies that share a
 * numeric code with a sibling code AND collide on displayName because CLDR
 * doesn't carry the historical sibling (so we fall back to the datasets/
 * currency-codes generic name).
 *
 * Mirrors CLDR's disambiguation pattern for the codes it does carry (e.g.,
 * ZWD = "Zimbabwean Dollar (1980-2008)", ZWN = "Zimbabwean Dollar (2006-2008)",
 * ZWR = "Zimbabwean Dollar (2008)"). When a sibling isn't in CLDR, its only
 * source is datasets/currency-codes which is generic — datasets lists ZWC as
 * "Rhodesian Dollar" identical to RHD, so the two collide. The override below
 * disambiguates ZWC as the Zimbabwe-Rhodesia successor entity (1970-1980)
 * distinct from RHD (Southern Rhodesia, 1968-1980).
 *
 * Only add entries here for codes that:
 *  (a) share a numeric code with another currency, AND
 *  (b) lack a CLDR-disambiguated displayName.
 * If CLDR already disambiguates (e.g., ZWD/ZWN/ZWR), no override needed.
 */
export const KNOWN_NUMERIC_REUSE_DISPLAYNAME_OVERRIDES: ReadonlyMap<
  string,
  string
> = new Map([["ZWC", "Zimbabwe Rhodesian Dollar"]]);

/**
 * Mirrors the Currency entity shape. `IsSupported` is a Tier 2 field (derived from the
 * selectable-locale set) and not part of this pipeline-raw shape.
 */
export interface CurrencyPartial {
  /** ISO 4217 alpha-3 code, UPPERCASE. Primary key. */
  iso4217AlphaCode: string;
  /** ISO 4217 numeric code (3-digit string, zero-padded). Null when source omits it. */
  iso4217NumericCode: string | null;
  /** English display name from CLDR (authoritative) with datasets/currency-codes fallback. */
  displayName: string;
  /**
   * CLDR `symbol-alt-narrow` (most compact glyph, e.g. "$", "€", "؋").
   * Falls back to `symbol` which may be the alpha code.
   */
  symbol: string | null;
  /**
   * Decimal places from CLDR currencyData.fractions._digits, with datasets minorUnit
   * fallback, defaulting to 2.
   */
  decimalPlaces: number;
  /**
   * True when CLDR shows any active (no _to) usage in any region.
   * False when all uses are retired.
   */
  isActive: boolean;
  /** Localized display names across the 11 supported locales (CLDR cldr-numbers-full). */
  localizedDisplayNames: Record<string, string>;
  /**
   * Currency's display name in the primary issuing country's primary language.
   * Null when no derivable home language.
   */
  endonymDisplayName: string | null;
  /**
   * ISO 3166-1 alpha-2 of the country whose primary language was used to derive the
   * endonym. Null when no derivation possible.
   */
  endonymSourceCountryISO31661Alpha2Code: string | null;
  /**
   * Inverse-nav from Country.activeLegalTenderCurrencies: every country×period this
   * currency was/is used. Sorted by fromDate asc.
   */
  usageHistory: CurrencyUsageEntry[];
  _provenance: {
    sources: string[];
    extractedAt: string;
  };
}

export interface CurrencyUsageEntry {
  /**
   * ISO 3166-1 alpha-2 code. May be ISO 3166-3 transitional (e.g., "SU" Soviet Union,
   * "DD" East Germany) for dissolved entities.
   */
  countryISO31661Alpha2Code: string;
  /** CLDR `_from` (YYYY-MM-DD). Null when CLDR omits the start. */
  fromDate: string | null;
  /** CLDR `_to` (YYYY-MM-DD). Null = still active. */
  toDate: string | null;
  /**
   * False when CLDR `_tender: "false"` (e.g., USS — US dollar Same-day,
   * non-legal-tender). True by default.
   */
  isLegalTender: boolean;
}

export interface BuildCurrenciesContext {
  /**
   * All datasets/currency-codes rows (per-COUNTRY, deduped to per-currency inside the
   * transformer).
   */
  datasetsRows: readonly CurrencyCodeRow[];
  /** CLDR currencyData.region — full per-country, per-currency date-ranged history. */
  cldrRegion: CurrencyDataPayload["supplemental"]["currencyData"]["region"];
  /** CLDR currencyData.fractions — per-currency decimal places + rounding. */
  cldrFractions: NonNullable<
    CurrencyDataPayload["supplemental"]["currencyData"]["fractions"]
  >;
  /**
   * Per-locale CLDR currencies file (cldr-numbers-full). Maps locale → currency-code → entry.
   */
  cldrLocaleCurrencies: Map<string, Map<string, CldrCurrencyEntry>>;
  /** Map of country ISO 3166-1 alpha-2 → primary ISO 639-1 language. From countries.spec.json. */
  countryToPrimaryLang: Map<string, string>;
}

/**
 * Build the full currency catalog from the merged sources.
 *
 * Strategy:
 *  1. Discover the universe of ISO 4217 codes from BOTH datasets/currency-codes AND CLDR
 *     currencyData (one is per-country with retired flags; the other is the temporal-history
 *     source). Union.
 *  2. For each code, dedupe datasets rows + invert CLDR region data into usageHistory.
 *  3. Compute isActive from the inverted history (any _to-less entry = active).
 *  4. Look up CLDR English displayName + symbol (preferred over datasets/currency-codes naming).
 *  5. Lookup per-locale display name across the 11 supported locales.
 *  6. Derive endonym by resolving the most-likely primary issuing country's primary language.
 */
export function buildCurrencyEntries(
  ctx: BuildCurrenciesContext,
): CurrencyPartial[] {
  // -- 1. Dedupe datasets rows by alpha code; preserve first row's metadata + retired flag --
  const datasetsByCode = new Map<
    string,
    {
      name: string;
      numericCode: string | null;
      minorUnit: number | null;
      anyActiveRow: boolean;
    }
  >();
  for (const row of ctx.datasetsRows) {
    if (!row.alphabeticCode) continue; // skip ancient rows missing alpha code
    const existing = datasetsByCode.get(row.alphabeticCode);
    const isActive = row.withdrawalDate.length === 0;
    if (!existing) {
      datasetsByCode.set(row.alphabeticCode, {
        name: row.currencyName,
        numericCode: row.numericCode || null,
        minorUnit: parseMinorUnit(row.minorUnit),
        anyActiveRow: isActive,
      });
    } else if (isActive) {
      existing.anyActiveRow = true;
    }
  }

  // -- 2. Invert CLDR region data → currency code → usage entries --
  const usageByCurrency = new Map<string, CurrencyUsageEntry[]>();
  for (const [countryCode, entries] of Object.entries(ctx.cldrRegion)) {
    for (const entry of entries) {
      // Each entry is a single-key object: { "USD": { "_from": "...", "_to": "..." } }
      for (const [currencyCode, dates] of Object.entries(entry)) {
        const list = usageByCurrency.get(currencyCode) ?? [];
        list.push({
          countryISO31661Alpha2Code: countryCode,
          fromDate: dates._from ?? null,
          toDate: dates._to ?? null,
          isLegalTender: dates._tender !== "false",
        });
        usageByCurrency.set(currencyCode, list);
      }
    }
  }
  // Sort each usage list by fromDate ascending (nulls last)
  for (const list of usageByCurrency.values()) {
    list.sort((a, b) => {
      if (a.fromDate === null && b.fromDate === null) return 0;
      if (a.fromDate === null) return 1;
      if (b.fromDate === null) return -1;
      return a.fromDate.localeCompare(b.fromDate);
    });
  }

  // -- 3. Union of currency codes from BOTH sources --
  const allCodes = new Set<string>([
    ...datasetsByCode.keys(),
    ...usageByCurrency.keys(),
  ]);

  // -- 4. Build CurrencyPartial for each code --
  const entries: CurrencyPartial[] = [];
  for (const code of allCodes) {
    const datasets = datasetsByCode.get(code);
    const usage = usageByCurrency.get(code) ?? [];

    // English display name: prefer hand-curated override (for numeric-reuse
    // sibling pairs CLDR doesn't disambiguate) > CLDR > datasets/currency-codes
    const cldrEn = ctx.cldrLocaleCurrencies.get("en")?.get(code);
    const englishName =
      KNOWN_NUMERIC_REUSE_DISPLAYNAME_OVERRIDES.get(code) ??
      cldrEn?.displayName ??
      datasets?.name;
    if (!englishName) continue; // can't ship a currency with no name in either source

    // Symbol: prefer CLDR narrow > CLDR symbol > null
    const symbol = cldrEn?.symbolNarrow ?? cldrEn?.symbol ?? null;

    // Decimal places: prefer CLDR fractions._digits > datasets minorUnit > DEFAULT
    const cldrFraction =
      ctx.cldrFractions[code] ?? ctx.cldrFractions["DEFAULT"];
    const decimalPlaces =
      parseIntStrict(cldrFraction?._digits) ??
      datasets?.minorUnit ??
      DEFAULT_DECIMAL_PLACES;

    // IsActive: any current (no _to) usage = active. If no CLDR history, fall back to datasets
    // (treat as active when ANY datasets row had no WithdrawalDate).
    const cldrShowsActive = usage.some((u) => u.toDate === null);
    const isActive =
      usage.length > 0 ? cldrShowsActive : (datasets?.anyActiveRow ?? false);

    // Localized display names
    const localizedDisplayNames: Record<string, string> = {};
    for (const [locale, byCode] of ctx.cldrLocaleCurrencies.entries()) {
      const name = byCode.get(code)?.displayName;
      if (name) localizedDisplayNames[locale] = name;
    }

    // Endonym derivation: pick the FIRST active (no _to) tender=true usage entry as "home
    // country," look up its primary language, then the localized name in that lang.
    // For retired-only currencies, fall back to the earliest historical entry.
    const homeUsage = pickHomeCountryUsage(usage);
    let endonymDisplayName: string | null = null;
    let endonymSourceCountry: string | null = null;
    if (homeUsage) {
      const homeCountry = homeUsage.countryISO31661Alpha2Code;
      const homeLang = ctx.countryToPrimaryLang.get(homeCountry);
      if (homeLang) {
        const homeLocalized = ctx.cldrLocaleCurrencies
          .get(homeLang)
          ?.get(code)?.displayName;
        if (homeLocalized) {
          endonymDisplayName = homeLocalized;
          endonymSourceCountry = homeCountry;
        }
      }
    }

    const sources: string[] = [];
    if (datasets) sources.push("datasets/currency-codes");
    if (cldrEn) sources.push("cldr-numbers-full");
    if (usage.length > 0) sources.push("cldr-currencyData");

    entries.push({
      iso4217AlphaCode: code,
      iso4217NumericCode: datasets?.numericCode ?? null,
      displayName: englishName,
      symbol,
      decimalPlaces,
      isActive,
      localizedDisplayNames,
      endonymDisplayName,
      endonymSourceCountryISO31661Alpha2Code: endonymSourceCountry,
      usageHistory: usage,
      _provenance: {
        sources,
        extractedAt: new Date().toISOString(),
      },
    });
  }

  entries.sort((a, b) => a.iso4217AlphaCode.localeCompare(b.iso4217AlphaCode));
  return entries;
}

/**
 * Picks the "home country" for endonym derivation:
 *   - First prefer an active (no _to) legal-tender entry — the live primary issuer
 *   - If none, fall back to the earliest historical entry (oldest fromDate)
 * Returns null when usage is empty.
 */
export function pickHomeCountryUsage(
  usage: readonly CurrencyUsageEntry[],
): CurrencyUsageEntry | null {
  const activeTender = usage.find((u) => u.toDate === null && u.isLegalTender);
  if (activeTender) return activeTender;
  const earliest = usage.find((u) => u.fromDate !== null);
  return earliest ?? usage[0] ?? null;
}

export function parseMinorUnit(value: string): number | null {
  if (!value) return null;
  if (value === "N.A.") return null;
  const n = Number(value);
  return Number.isInteger(n) && n >= 0 && n <= 6 ? n : null;
}

export function parseIntStrict(value: string | undefined): number | null {
  if (!value) return null;
  const n = Number(value);
  return Number.isInteger(n) && n >= 0 && n <= 6 ? n : null;
}
