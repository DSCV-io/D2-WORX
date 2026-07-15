// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Hand-written meta-records + name-resolution helpers.
export type { DeprecationInfo } from "./deprecation-info.js";
export { normalize } from "./name-resolution/name-normalizer.js";
export { compare, isWithin } from "./name-resolution/levenshtein-comparer.js";

// Hand-written API contract interfaces.
export type { IGeoReference } from "./interfaces/i-geo-reference.js";
export type { IGeoNameResolver } from "./name-resolution/i-geo-name-resolver.js";

// Spec-derived closed-set code enums (`*Code` suffix names the typed
// identifier; the bare singular is the record) + Zod schemas + closed-set
// validation tables.
export {
  CountryCode,
  CountryCodeSchema,
  ALL_COUNTRY_CODE_SET,
} from "./generated/typed-codes/country-code.g.js";
export {
  CurrencyCode,
  CurrencyCodeSchema,
  ALL_CURRENCY_CODE_SET,
} from "./generated/typed-codes/currency-code.g.js";
export {
  LanguageCode,
  LanguageCodeSchema,
  ALL_LANGUAGE_CODE_SET,
} from "./generated/typed-codes/language-code.g.js";
export {
  GeopoliticalEntityCode,
  GeopoliticalEntityCodeSchema,
  ALL_GEOPOLITICAL_ENTITY_CODE_SET,
} from "./generated/typed-codes/geopolitical-entity-code.g.js";

// Spec-derived open-set wrapper-code branded string types — the wrapper
// names already disambiguate from the bare record name (e.g.
// `SubdivisionCode` vs `Subdivision`).
export {
  SubdivisionCodeSchema,
  SUBDIVISION_CODE_SET,
  asSubdivisionCode,
  type SubdivisionCode,
} from "./generated/typed-codes/subdivision-code.g.js";
export {
  LocaleCodeSchema,
  LOCALE_CODE_SET,
  asLocaleCode,
  type LocaleCode,
} from "./generated/typed-codes/locale-code.g.js";
export {
  TimezoneCodeSchema,
  TIMEZONE_CODE_SET,
  asTimezoneCode,
  type TimezoneCode,
} from "./generated/typed-codes/timezone-code.g.js";

// Spec-derived fixed-value enums (writing direction, date format,
// day-of-week, measurement system, currency acceptance level,
// geopolitical entity type).
export {
  WritingDirection,
  WritingDirectionSchema,
  DateFormatPattern,
  DateFormatPatternSchema,
  DayOfWeek,
  DayOfWeekSchema,
  MeasurementSystem,
  MeasurementSystemSchema,
  CurrencyAcceptanceLevel,
  CurrencyAcceptanceLevelSchema,
  GeopoliticalEntityType,
  GeopoliticalEntityTypeSchema,
} from "./generated/fixed-enums.g.js";

// Spec-derived record shapes — single shape per entity. Recursive nav
// refs populated in the wire-nav step of the data emitter under
// @dcsv-io/d2-geo-default.
export type {
  Country,
  CountryCurrencyAcceptance,
} from "./generated/country.g.js";
export type { Subdivision } from "./generated/subdivision.g.js";
export type { Currency } from "./generated/currency.g.js";
export type { Language } from "./generated/language.g.js";
export type { Locale } from "./generated/locale.g.js";
export type { Timezone } from "./generated/timezone.g.js";
export type { GeopoliticalEntity } from "./generated/geopolitical-entity.g.js";

// Catalog metadata (version + published-at snapshot).
export {
  CATALOG_VERSION,
  CATALOG_PUBLISHED_AT,
} from "./generated/geo-catalog.g.js";
