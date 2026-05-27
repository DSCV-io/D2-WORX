// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import Papa from "papaparse";
import { fetchAndCache, type CachedFetch } from "../util/cache.js";

const SOURCE_NAME = "datasets-currency-codes";
// upstream URL — cannot wrap
const SOURCE_URL =
  "https://raw.githubusercontent.com/datasets/currency-codes/main/data/codes-all.csv";
const SOURCE_LICENSE =
  "PDDL-1.0 (Open Knowledge Foundation Public Domain Dedication)";
const CACHE_KEY = "codes-all.csv";

/**
 * One row of datasets/currency-codes — per-COUNTRY, not per-currency. Multiple rows for the
 * same currency (every Eurozone country has its own EUR row; USD has US + UM + EC + ...).
 *
 * Sample:
 *   Entity,Currency,AlphabeticCode,NumericCode,MinorUnit,WithdrawalDate
 *   AFGHANISTAN,Afghani,AFN,971,2,
 *   ÅLAND ISLANDS,Euro,EUR,978,2,
 *   ZIMBABWE,Zimbabwe Dollar,,,,2009-04
 *
 * WithdrawalDate is non-empty for retired currencies. Some retired rows have empty
 * AlphabeticCode/NumericCode (older currencies whose codes were reused).
 */
export interface CurrencyCodeRow {
  entityName: string; // ISO 4217 country name (uppercase)
  currencyName: string; // English currency name (e.g., "Afghani", "Euro")
  alphabeticCode: string; // ISO 4217 alpha-3 (may be empty for ancient retired)
  numericCode: string; // ISO 4217 numeric (3-digit string)
  minorUnit: string; // Number of decimal places ("0", "2", "3"). "N.A." for some.
  withdrawalDate: string; // "YYYY-MM" when retired; empty when active
}

export interface CurrencyCodesFetchResult extends Pick<
  CachedFetch,
  "provenance" | "fromCache"
> {
  rows: CurrencyCodeRow[];
}

export async function fetchDatasetsCurrencyCodes(options?: {
  ttlHours?: number;
}): Promise<CurrencyCodesFetchResult> {
  const fetched = await fetchAndCache({
    source: SOURCE_NAME,
    url: SOURCE_URL,
    license: SOURCE_LICENSE,
    cacheKey: CACHE_KEY,
    ttlHours: options?.ttlHours,
  });
  const csvText = fetched.body.toString("utf8");
  const parsed = Papa.parse<Record<string, string>>(csvText, {
    header: true,
    skipEmptyLines: true,
  });
  const rows: CurrencyCodeRow[] = parsed.data.map((r) => ({
    entityName: (r["Entity"] ?? "").trim(),
    currencyName: (r["Currency"] ?? "").trim(),
    alphabeticCode: (r["AlphabeticCode"] ?? "").trim().toUpperCase(),
    numericCode: (r["NumericCode"] ?? "").trim(),
    minorUnit: (r["MinorUnit"] ?? "").trim(),
    withdrawalDate: (r["WithdrawalDate"] ?? "").trim(),
  }));
  return {
    rows,
    provenance: fetched.provenance,
    fromCache: fetched.fromCache,
  };
}
