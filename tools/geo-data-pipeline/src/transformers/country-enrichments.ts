// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  fetchCldrSupplemental,
  type CurrencyDataPayload,
  type MeasurementDataPayload,
  type WeekDataPayload,
} from "../fetchers/cldr-supplemental.js";
import type { FetchProvenance } from "../util/cache.js";

const DEFAULT_REGION = "001"; // CLDR world-default fallback key

const CLDR_DAY_TO_TITLE: Record<string, DayOfWeek> = {
  sun: "Sunday",
  mon: "Monday",
  tue: "Tuesday",
  wed: "Wednesday",
  thu: "Thursday",
  fri: "Friday",
  sat: "Saturday",
};

export type DayOfWeek =
  | "Sunday"
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday";

export type MeasurementSystem = "Metric" | "Imperial" | "Mixed";

const CLDR_MEASUREMENT_TO_OURS: Record<string, MeasurementSystem> = {
  metric: "Metric",
  US: "Imperial",
  UK: "Mixed",
};

export interface CountryWeekEnrichment {
  firstDayOfWeek: DayOfWeek;
  weekendStart: DayOfWeek;
  weekendEnd: DayOfWeek;
}

export interface CountryMeasurementEnrichment {
  measurementSystem: MeasurementSystem;
}

export interface ActiveCurrencyEntry {
  isoAlphaCode: string;
  fromDate: string | null;
}

export interface EnrichmentsLoadResult {
  weekData: WeekDataPayload["supplemental"]["weekData"];
  measurementData: MeasurementDataPayload["supplemental"]["measurementData"];
  currencyData: CurrencyDataPayload["supplemental"]["currencyData"];
  provenance: FetchProvenance[];
}

export async function loadCountryEnrichments(): Promise<EnrichmentsLoadResult> {
  const [week, meas, curr] = await Promise.all([
    fetchCldrSupplemental<WeekDataPayload>("weekData"),
    fetchCldrSupplemental<MeasurementDataPayload>("measurementData"),
    fetchCldrSupplemental<CurrencyDataPayload>("currencyData"),
  ]);
  return {
    weekData: week.payload.supplemental.weekData,
    measurementData: meas.payload.supplemental.measurementData,
    currencyData: curr.payload.supplemental.currencyData,
    provenance: [week.provenance, meas.provenance, curr.provenance],
  };
}

/**
 * Per-country week settings with `001` world-default fallback per CLDR convention.
 * Returns null for unmapped day codes — fail loud rather than silently miscategorize.
 */
export function deriveWeekEnrichment(
  weekData: WeekDataPayload["supplemental"]["weekData"],
  alpha2: string,
): CountryWeekEnrichment | null {
  const firstDayRaw = weekData.firstDay[alpha2] ?? weekData.firstDay[DEFAULT_REGION];
  const weekendStartRaw =
    weekData.weekendStart?.[alpha2] ?? weekData.weekendStart?.[DEFAULT_REGION] ?? "sat";
  const weekendEndRaw =
    weekData.weekendEnd?.[alpha2] ?? weekData.weekendEnd?.[DEFAULT_REGION] ?? "sun";
  const firstDay = firstDayRaw ? CLDR_DAY_TO_TITLE[firstDayRaw] : null;
  const weekendStart = CLDR_DAY_TO_TITLE[weekendStartRaw];
  const weekendEnd = CLDR_DAY_TO_TITLE[weekendEndRaw];
  if (!firstDay || !weekendStart || !weekendEnd) return null;
  return { firstDayOfWeek: firstDay, weekendStart, weekendEnd };
}

/**
 * MeasurementSystem with `001` world-default fallback. CLDR has only ~5 region overrides
 * (US, LR, MM use US/UK system); everywhere else inherits metric default.
 */
export function deriveMeasurementEnrichment(
  measurementData: MeasurementDataPayload["supplemental"]["measurementData"],
  alpha2: string,
): CountryMeasurementEnrichment | null {
  const raw =
    measurementData.measurementSystem[alpha2] ??
    measurementData.measurementSystem[DEFAULT_REGION] ??
    "metric";
  const system = CLDR_MEASUREMENT_TO_OURS[raw];
  if (!system) return null;
  return { measurementSystem: system };
}

/**
 * Per-country active legal-tender currencies (source for Country.Currencies M:M).
 * Filters out:
 *   - Historical entries (`_to` present = retired)
 *   - Non-tender accounting/clearing currencies (`_tender === "false"` e.g., CHE/CHW in CH)
 *
 * **Preserves CLDR's native array order** — CLDR conveys "primary first, supplementary
 * after" semantics through array order, NOT through `_from` chronology. Examples:
 *   - NA: CLDR = [NAD(1993), ZAR(1961)] → NAD primary (newer but listed first)
 *   - LS: CLDR = [ZAR(1961), LSL(1980)] → ZAR conventionally listed first (older)
 *   - VE: CLDR = [VES(2018), VED(2021)] → VES primary
 * Sorting by `_from` would destroy this signal. Caller treats `[0]` as the CLDR-suggested
 * primary; cross-check against datasets is the authoritative tiebreaker for actual primary.
 */
export function deriveActiveCurrencies(
  currencyData: CurrencyDataPayload["supplemental"]["currencyData"],
  alpha2: string,
): ActiveCurrencyEntry[] {
  const regionEntries = currencyData.region[alpha2];
  if (!regionEntries) return [];

  const active: ActiveCurrencyEntry[] = [];
  for (const entry of regionEntries) {
    for (const [code, meta] of Object.entries(entry)) {
      if (meta._to) continue; // historical — retired
      if (meta._tender === "false") continue; // non-tender (CHE/CHW/USS/etc.)
      active.push({ isoAlphaCode: code, fromDate: meta._from ?? null });
    }
  }
  // NO sort — preserves CLDR's native array order
  return active;
}

/**
 * Returns true ONLY when CLDR has the currency code listed for the country with `_to`
 * (retired) AND has NO active (no-`_to`) entry for the same code. Handles "rejoined"
 * cases correctly — Mali used XOF (1958-1962), left for MLF, then rejoined XOF in 1984.
 * CLDR lists both entries; the code is NOT retired because an active entry exists.
 */
export function isCurrencyRetiredInCldr(
  currencyData: CurrencyDataPayload["supplemental"]["currencyData"],
  alpha2: string,
  currencyCode: string,
): boolean {
  const regionEntries = currencyData.region[alpha2];
  if (!regionEntries) return false;
  let hasHistorical = false;
  let hasActive = false;
  for (const entry of regionEntries) {
    const meta = entry[currencyCode];
    if (!meta) continue;
    if (meta._to) hasHistorical = true;
    else hasActive = true;
  }
  return hasHistorical && !hasActive;
}
