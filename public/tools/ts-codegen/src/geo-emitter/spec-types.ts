// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Typed deserialization shapes for the seven geo Tier-2 spec files under
 * `contracts/geo/*.spec.json`. Mirrors the .NET `DcsvIo.D2.Geo.SourceGen.Spec`
 * DTO catalog field-for-field — both runtimes consume the same JSON, so the
 * deserialization surface is identical by construction. The TS side uses
 * `readonly` everywhere because spec data is immutable at every layer.
 *
 * Spec mirroring here is permitted as the codegen-internal carve-out — these
 * DTOs do not leak across a package boundary and serve only as typed input
 * for the per-entity emitters.
 */

/**
 * Common envelope wrapping every spec file — `$generated` / `$source` /
 * `catalogVersion` plus either `generatedAt` (pipeline-derived) or
 * `lastEditedAt` (hand-rolled).
 */
export interface SpecMetadata {
  readonly catalogVersion: string;
  readonly generatedAt?: string;
  readonly lastEditedAt?: string;
  readonly isGenerated: boolean;
  readonly source: string;
}

/** Catalog envelope — metadata + entries. */
export interface SpecEnvelope<T> {
  readonly metadata: SpecMetadata;
  readonly entries: readonly T[];
}

/** One per-country M:M `currencies` entry. */
export interface CountryCurrencyAcceptanceSpec {
  readonly iso4217AlphaCode: string;
  readonly level: string;
}

/** One row in `countries.spec.json`. */
export interface CountrySpec {
  readonly iso31661Alpha2Code: string;
  readonly iso31661Alpha3Code: string;
  readonly iso31661NumericCode: string;
  readonly displayName: string;
  readonly officialName: string;
  readonly endonymDisplayName?: string;
  readonly phoneNumberPrefix?: string;
  readonly phoneNumberNationalFormat?: string;
  readonly phoneNumberMinDigits?: number;
  readonly phoneNumberMaxDigits?: number;
  readonly firstDayOfWeek: string;
  readonly weekendStart: string;
  readonly weekendEnd: string;
  readonly measurementSystem: string;
  readonly primaryLanguageISO6391Code?: string;
  readonly primaryCurrencyISO4217AlphaCode?: string;
  readonly primaryLocaleIETFBCP47Tag?: string;
  readonly sovereignCountryISO31661Alpha2Code?: string;
  readonly geopoliticalEntityShortCodes: readonly string[];
  readonly subdivisionISO31662Codes: readonly string[];
  readonly timezoneIanaIdentifiers: readonly string[];
  readonly localeIETFBCP47Tags: readonly string[];
  readonly spokenLanguageISO6391Codes: readonly string[];
  readonly territoryISO31661Alpha2Codes: readonly string[];
  readonly currencies: readonly CountryCurrencyAcceptanceSpec[];
}

/** One row in `subdivisions.spec.json`. */
export interface SubdivisionSpec {
  readonly iso31662Code: string;
  readonly shortCode: string;
  readonly displayName: string;
  readonly officialName: string;
  readonly endonymDisplayName?: string;
  readonly countryISO31661Alpha2Code: string;
  readonly parentISO31662Code?: string;
  readonly type?: string;
  readonly order?: number;
}

/** One row in `currencies.spec.json`. */
export interface CurrencySpec {
  readonly iso4217AlphaCode: string;
  readonly iso4217NumericCode?: string;
  readonly displayName: string;
  readonly decimalPlaces: number;
  readonly symbol?: string;
  readonly isActive: boolean;
  readonly isSupported: boolean;
}

/** One row in `languages.spec.json`. */
export interface LanguageSpec {
  readonly iso6391Code: string;
  readonly name: string;
  readonly endonym?: string;
  readonly writingDirection: string;
  readonly isSupported: boolean;
  readonly spokenInCountryISO31661Alpha2Codes: readonly string[];
}

/** One row in `locales.spec.json`. */
export interface LocaleSpec {
  readonly ietfBcp47Tag: string;
  readonly name: string;
  readonly endonym?: string;
  readonly languageISO6391Code: string;
  readonly countryISO31661Alpha2Code?: string;
  readonly isSelectable: boolean;
  readonly firstDayOfWeek: string;
  readonly decimalSeparator: string;
  readonly thousandsSeparator: string;
  readonly dateFormatPattern: string;
}

/** One row in `timezones.spec.json`. */
export interface TimezoneSpec {
  readonly ianaIdentifier: string;
  readonly displayName: string;
  readonly currentStdOffsetMinutes: number;
  readonly currentDstOffsetMinutes?: number;
  readonly currentStdAbbrev: string;
  readonly currentDstAbbrev?: string;
  readonly countryISO31661Alpha2Code?: string;
  readonly coApplicableCountryISO31661Alpha2Codes: readonly string[];
  readonly aliases: readonly string[];
}

/** One row in `geopolitical-entities.spec.json`. */
export interface GeopoliticalEntitySpec {
  readonly shortCode: string;
  readonly name: string;
  readonly type: string;
  readonly countryISO31661Alpha2Codes: readonly string[];
}

/** Aggregate context — every catalog optionally populated. */
export interface GeoSpecContext {
  readonly countries?: SpecEnvelope<CountrySpec>;
  readonly subdivisions?: SpecEnvelope<SubdivisionSpec>;
  readonly currencies?: SpecEnvelope<CurrencySpec>;
  readonly languages?: SpecEnvelope<LanguageSpec>;
  readonly locales?: SpecEnvelope<LocaleSpec>;
  readonly timezones?: SpecEnvelope<TimezoneSpec>;
  readonly geopoliticalEntities?: SpecEnvelope<GeopoliticalEntitySpec>;
}
