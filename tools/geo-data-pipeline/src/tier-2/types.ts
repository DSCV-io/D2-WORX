// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Tier 2 codegen-ready output shapes — the canonical record shapes the platform
 * consumes for Country / Locale / Currency / Language / Subdivision / Timezone /
 * GeopoliticalEntity. These JSON files at contracts/geo/*.spec.json are consumed
 * directly by codegen (.NET SourceGen + TS emitter — Tier 3) and translated into
 * concrete entity types. No transformation between this JSON and the concrete
 * types beyond the codegen's serialization step.
 *
 * Three-tier story: Tier 1 (`src-data/`) — pipeline ingestion output; Tier 2
 * (this layer) — denormalized + reorganized in the platform's preferred style;
 * Tier 3 — generated C# / TS code produced by codegen consuming Tier 2.
 *
 * Everything here uses camelCase field names (matching the codegen-emitted property
 * names lowered to camelCase per language convention).
 */

export type DayOfWeek =
  | "Sunday"
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday";

export type MeasurementSystem = "Metric" | "Imperial" | "Mixed";
export type WritingDirection = "LTR" | "RTL";
export type DateFormatPattern = "DMY" | "MDY" | "YMD";
export type CurrencyAcceptanceLevel = "LegalTender" | "WidelyAccepted" | "Tourist";

/** Country record — codegen-ready shape. */
export interface CountrySpec {
  iso31661Alpha2Code: string;
  iso31661Alpha3Code: string;
  iso31661NumericCode: string;
  displayName: string;
  officialName: string;
  endonymDisplayName: string | null;
  phoneNumberPrefix: string | null;
  phoneNumberNationalFormat: string | null;
  phoneNumberMinDigits: number | null;
  phoneNumberMaxDigits: number | null;
  firstDayOfWeek: DayOfWeek;
  weekendStart: DayOfWeek;
  weekendEnd: DayOfWeek;
  measurementSystem: MeasurementSystem;
  primaryLanguageISO6391Code: string | null;
  primaryCurrencyISO4217AlphaCode: string | null;
  primaryLocaleIETFBCP47Tag: string | null;
  sovereignCountryISO31661Alpha2Code: string | null;
  geopoliticalEntityShortCodes: string[];
  subdivisionISO31662Codes: string[];
  timezoneIanaIdentifiers: string[];
  localeIETFBCP47Tags: string[];
  spokenLanguageISO6391Codes: string[];
  territoryISO31661Alpha2Codes: string[];
  currencies: CountryCurrencyAcceptance[];
}

export interface CountryCurrencyAcceptance {
  iso4217AlphaCode: string;
  level: CurrencyAcceptanceLevel;
}

/** Subdivision record. */
export interface SubdivisionSpec {
  iso31662Code: string;
  shortCode: string;
  displayName: string;
  officialName: string;
  endonymDisplayName: string | null;
  countryISO31661Alpha2Code: string;
  parentISO31662Code: string | null;
  type: string | null;
  order: number | null;
}

/** Currency record. */
export interface CurrencySpec {
  iso4217AlphaCode: string;
  iso4217NumericCode: string | null;
  displayName: string;
  decimalPlaces: number;
  symbol: string | null;
  isActive: boolean;
  isSupported: boolean;
}

/** Language record. */
export interface LanguageSpec {
  iso6391Code: string;
  name: string;
  endonym: string | null;
  writingDirection: WritingDirection;
  isSupported: boolean;
  spokenInCountryISO31661Alpha2Codes: string[];
}

/** Locale record — region-derived fields denormalized from Country. */
export interface LocaleSpec {
  ietfBcp47Tag: string;
  name: string;
  endonym: string | null;
  languageISO6391Code: string;
  countryISO31661Alpha2Code: string | null;
  isSelectable: boolean;
  firstDayOfWeek: DayOfWeek;
  decimalSeparator: string;
  thousandsSeparator: string;
  dateFormatPattern: DateFormatPattern;
}

/** Timezone record. */
export interface TimezoneSpec {
  ianaIdentifier: string;
  displayName: string;
  currentStdOffsetMinutes: number;
  currentDstOffsetMinutes: number | null;
  currentStdAbbrev: string;
  currentDstAbbrev: string | null;
  countryISO31661Alpha2Code: string | null;
  coApplicableCountryISO31661Alpha2Codes: string[];
  aliases: string[];
}

/**
 * GeopoliticalEntity — hand-rolled at contracts/geo/geopolitical-entities.spec.json
 * and lives as a Tier 2 PEER to the pipeline-generated Tier 2 specs (codegen treats
 * it identically; only $generated / $source distinguish provenance). Tier 2 builder
 * just validates + passes through; no upstream source for supranational groupings.
 */
export interface GeopoliticalEntitySpec {
  shortCode: string;
  name: string;
  type: string;
  countryISO31661Alpha2Codes: string[];
}

/** Wrapper for any Tier 2 catalog file with consistent header convention. */
export interface CatalogFileWrapper<TEntry> {
  $generated: boolean;
  $source: "pipeline-derived" | "manual";
  $schema: string;
  $note: string;
  catalogVersion: string;
  generatedAt: string;
  entries: TEntry[];
}
