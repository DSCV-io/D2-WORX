// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { truthy } from "@d2/utilities";

import { buildHeader } from "../lib/file-emit.js";
import { tsPackagePath } from "../lib/paths.js";
import { StringBuilder } from "../lib/string-builder.js";

import { appendEslintDisable, appendJsDoc } from "./emit-helpers.js";

/**
 * Emits the TS record-shape interfaces (one shape per catalog) into
 * `@d2/geo-abstractions/src/generated/<entity>.g.ts`. Shapes mirror the
 * .NET sealed records defined in `D2.Shared.Geo.Abstractions/Generated/`
 * field-for-field, modulo TS casing.
 *
 * Naming convention:
 * - Identifier types: `CountryCode`, `CurrencyCode`, `LanguageCode`,
 *   `GeopoliticalEntityCode`, `SubdivisionCode`, `LocaleCode`,
 *   `TimezoneCode`.
 * - Records: bare singular (`Country`, `Subdivision`, `Currency`,
 *   `Language`, `Locale`, `Timezone`, `GeopoliticalEntity`).
 * - PK fields are named for what the value IS — `iso31661Alpha2Code`,
 *   `ietfBcp47Tag`, `ianaName`, etc.
 *
 * Universal dual-representation rule — every relationship carries BOTH:
 * - **Code rep** — typed `TCode` field (`ReadonlySet<TCode>` for set FKs)
 *   for O(1) `.has()` membership checks.
 * - **Nav rep** — `TRecord` field (`readonly TRecord[]` for set FKs) for
 *   ordered iteration / property access.
 *
 * Per the workspace `undefined`-over-`null` convention, nullable single
 * primaries use `?:` not `T | null`. Records returned over the wire from
 * .NET (where the field is `T?`) normalize JSON `null` to `undefined` at
 * the Zod deserialization boundary.
 */

const GEN_DIR = (...parts: string[]): string =>
  tsPackagePath("geo", "abstractions", "src", "generated", ...parts);

const SPEC_REFS: Readonly<Record<string, string>> = {
  countries: "contracts/geo/countries.spec.json",
  subdivisions: "contracts/geo/subdivisions.spec.json",
  currencies: "contracts/geo/currencies.spec.json",
  languages: "contracts/geo/languages.spec.json",
  locales: "contracts/geo/locales.spec.json",
  timezones: "contracts/geo/timezones.spec.json",
  geopoliticalEntities: "contracts/geo/geopolitical-entities.spec.json",
};

interface RecordShapeFile {
  readonly path: string;
  readonly source: string;
}

/** Emit `country.g.ts` (Country + CountryCurrencyAcceptance). */
export function emitCountryRecords(): RecordShapeFile {
  const sb = startFile(
    SPEC_REFS["countries"]!,
    ["CountryCode", "CurrencyCode", "LanguageCode", "GeopoliticalEntityCode"],
    ["SubdivisionCode", "LocaleCode"],
    ["CurrencyAcceptanceLevel", "DayOfWeek", "MeasurementSystem"],
  );
  sb.appendLine('import type { Currency } from "./currency.g.js";');
  sb.appendLine('import type { Locale } from "./locale.g.js";');
  sb.appendLine('import type { Language } from "./language.g.js";');
  sb.appendLine('import type { Subdivision } from "./subdivision.g.js";');
  sb.appendLine(
    'import type { GeopoliticalEntity } from "./geopolitical-entity.g.js";',
  );
  sb.appendLine(
    'import type { DeprecationInfo } from "../deprecation-info.js";',
  );
  sb.appendLine();

  appendJsDoc(
    sb,
    [
      "Per-country M:M `currencies[]` entry. Carries the typed currency",
      "code + acceptance level + embedded `Currency` nav record (wired in",
      "the wire-nav step of the two-pass populate pattern).",
    ].join("\n"),
  );
  sb.appendLine("export interface CountryCurrencyAcceptance {");
  sb.increaseIndent();
  sb.appendLine("readonly iso4217AlphaCode: CurrencyCode;");
  sb.appendLine("readonly level: CurrencyAcceptanceLevel;");
  sb.appendLine("readonly currency?: Currency;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  appendJsDoc(
    sb,
    [
      "`Country` record — single shape per entity. Universal dual-rep for",
      "every relationship: typed code field (`ReadonlySet<TCode>` for set",
      "FKs) AND nav record list. Nav refs (`primaryLanguage`,",
      "`subdivisions`, `territories`, ...) populated in the wire-nav step",
      "via one-time cast. Mirrors .NET",
      "`D2.Shared.Geo.Abstractions.Country` field-for-field (modulo TS",
      "casing).",
    ].join("\n"),
  );
  sb.appendLine("export interface Country {");
  sb.increaseIndent();
  // Scalars.
  sb.appendLine("readonly iso31661Alpha2Code: CountryCode;");
  sb.appendLine("readonly iso31661Alpha3Code: string;");
  sb.appendLine("readonly iso31661NumericCode: string;");
  sb.appendLine("readonly displayName: string;");
  sb.appendLine("readonly officialName: string;");
  sb.appendLine("readonly endonymDisplayName: string;");
  sb.appendLine("readonly endonymOfficialName: string;");
  sb.appendLine("readonly phoneNumberPrefix: string;");
  sb.appendLine("readonly phoneNumberNationalFormat: string;");
  sb.appendLine("readonly phoneNumberMinDigits?: number;");
  sb.appendLine("readonly phoneNumberMaxDigits: number;");
  sb.appendLine("readonly firstDayOfWeek: DayOfWeek;");
  sb.appendLine("readonly weekendStart: DayOfWeek;");
  sb.appendLine("readonly weekendEnd: DayOfWeek;");
  sb.appendLine("readonly measurementSystem: MeasurementSystem;");
  // Single FK code reps + nav reps.
  sb.appendLine("readonly primaryLanguageIso6391Code?: LanguageCode;");
  sb.appendLine("readonly primaryLanguage?: Language;");
  sb.appendLine("readonly primaryCurrencyIso4217AlphaCode?: CurrencyCode;");
  sb.appendLine("readonly primaryCurrency?: Currency;");
  sb.appendLine("readonly primaryLocaleIetfBcp47Tag?: LocaleCode;");
  sb.appendLine("readonly primaryLocale?: Locale;");
  sb.appendLine("readonly sovereignCountryIso31661Alpha2Code?: CountryCode;");
  sb.appendLine("readonly sovereignCountry?: Country;");
  // Set FK code reps + nav reps.
  sb.appendLine(
    "readonly territoryIso31661Alpha2Codes: ReadonlySet<CountryCode>;",
  );
  sb.appendLine("readonly territories: readonly Country[];");
  sb.appendLine(
    "readonly subdivisionIso31662Codes: ReadonlySet<SubdivisionCode>;",
  );
  sb.appendLine("readonly subdivisions: readonly Subdivision[];");
  sb.appendLine("readonly localeIetfBcp47Tags: ReadonlySet<LocaleCode>;");
  sb.appendLine("readonly locales: readonly Locale[];");
  sb.appendLine(
    "readonly geopoliticalEntityShortCodes: ReadonlySet<GeopoliticalEntityCode>;",
  );
  sb.appendLine(
    "readonly geopoliticalEntities: readonly GeopoliticalEntity[];",
  );
  sb.appendLine(
    "readonly currencyIso4217AlphaCodes: ReadonlySet<CurrencyCode>;",
  );
  sb.appendLine("readonly currencies: readonly CountryCurrencyAcceptance[];");
  sb.appendLine("readonly deprecation?: DeprecationInfo;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return { path: GEN_DIR("country.g.ts"), source: sb.toString() };
}

/** Emit `subdivision.g.ts` (Subdivision). */
export function emitSubdivisionRecords(): RecordShapeFile {
  const sb = startFile(
    SPEC_REFS["subdivisions"]!,
    ["CountryCode"],
    ["SubdivisionCode"],
    [],
  );
  sb.appendLine('import type { Country } from "./country.g.js";');
  sb.appendLine(
    'import type { DeprecationInfo } from "../deprecation-info.js";',
  );
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "`Subdivision` record — ISO 3166-2 administrative subdivision.",
      "Vocabulary discipline: every field uses `subdivision` (never",
      "`state` / `province` / `region`); display labels live on the",
      "`type` field. Nav refs `country` + `parentSubdivision` populated",
      "in the wire-nav step. Mirrors .NET",
      "`D2.Shared.Geo.Abstractions.Subdivision` field-for-field.",
    ].join("\n"),
  );
  sb.appendLine("export interface Subdivision {");
  sb.increaseIndent();
  sb.appendLine("readonly iso31662Code: SubdivisionCode;");
  sb.appendLine("readonly shortCode: string;");
  sb.appendLine("readonly displayName: string;");
  sb.appendLine("readonly officialName: string;");
  sb.appendLine("readonly endonymDisplayName: string;");
  sb.appendLine("readonly endonymOfficialName: string;");
  sb.appendLine("readonly countryIso31661Alpha2Code: CountryCode;");
  sb.appendLine("readonly country?: Country;");
  sb.appendLine("readonly parentSubdivisionIso31662Code?: SubdivisionCode;");
  sb.appendLine("readonly parentSubdivision?: Subdivision;");
  sb.appendLine("readonly type: string;");
  sb.appendLine("readonly deprecation?: DeprecationInfo;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return { path: GEN_DIR("subdivision.g.ts"), source: sb.toString() };
}

/** Emit `currency.g.ts` (Currency). */
export function emitCurrencyRecords(): RecordShapeFile {
  const sb = startFile(
    SPEC_REFS["currencies"]!,
    ["CountryCode", "CurrencyCode"],
    [],
    [],
  );
  sb.appendLine('import type { Country } from "./country.g.js";');
  sb.appendLine(
    'import type { DeprecationInfo } from "../deprecation-info.js";',
  );
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "`Currency` record. Reverse-nav `acceptedInCountries` populated",
      "in the wire-nav step from every country's `currencies` list.",
      "Mirrors .NET `D2.Shared.Geo.Abstractions.Currency` field-for-field.",
    ].join("\n"),
  );
  sb.appendLine("export interface Currency {");
  sb.increaseIndent();
  sb.appendLine("readonly iso4217AlphaCode: CurrencyCode;");
  sb.appendLine("readonly iso4217NumericCode: string;");
  sb.appendLine("readonly displayName: string;");
  sb.appendLine("readonly officialName: string;");
  sb.appendLine("readonly decimalPlaces: number;");
  sb.appendLine("readonly symbol: string;");
  sb.appendLine("readonly isSupported: boolean;");
  sb.appendLine(
    "readonly acceptedInCountryIso31661Alpha2Codes: ReadonlySet<CountryCode>;",
  );
  sb.appendLine("readonly acceptedInCountries: readonly Country[];");
  sb.appendLine("readonly deprecation?: DeprecationInfo;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return { path: GEN_DIR("currency.g.ts"), source: sb.toString() };
}

/** Emit `language.g.ts` (Language). */
export function emitLanguageRecords(): RecordShapeFile {
  const sb = startFile(
    SPEC_REFS["languages"]!,
    ["CountryCode", "LanguageCode"],
    ["LocaleCode"],
    ["WritingDirection"],
  );
  sb.appendLine('import type { Country } from "./country.g.js";');
  sb.appendLine('import type { Locale } from "./locale.g.js";');
  sb.appendLine(
    'import type { DeprecationInfo } from "../deprecation-info.js";',
  );
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "`Language` record. Reverse-navs `spokenInCountries` + `locales`",
      "populated in the wire-nav step. Mirrors .NET",
      "`D2.Shared.Geo.Abstractions.Language` field-for-field.",
    ].join("\n"),
  );
  sb.appendLine("export interface Language {");
  sb.increaseIndent();
  sb.appendLine("readonly iso6391Code: LanguageCode;");
  sb.appendLine("readonly displayName: string;");
  sb.appendLine("readonly endonym: string;");
  sb.appendLine("readonly writingDirection: WritingDirection;");
  sb.appendLine("readonly isSupported: boolean;");
  sb.appendLine(
    "readonly spokenInCountryIso31661Alpha2Codes: ReadonlySet<CountryCode>;",
  );
  sb.appendLine("readonly spokenInCountries: readonly Country[];");
  sb.appendLine("readonly localeIetfBcp47Tags: ReadonlySet<LocaleCode>;");
  sb.appendLine("readonly locales: readonly Locale[];");
  sb.appendLine("readonly deprecation?: DeprecationInfo;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return { path: GEN_DIR("language.g.ts"), source: sb.toString() };
}

/** Emit `locale.g.ts` (Locale). */
export function emitLocaleRecords(): RecordShapeFile {
  const sb = startFile(
    SPEC_REFS["locales"]!,
    ["CountryCode", "LanguageCode"],
    ["LocaleCode"],
    ["DateFormatPattern", "DayOfWeek"],
  );
  sb.appendLine('import type { Country } from "./country.g.js";');
  sb.appendLine('import type { Language } from "./language.g.js";');
  sb.appendLine(
    'import type { DeprecationInfo } from "../deprecation-info.js";',
  );
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "`Locale` record. `language` / `languageIso6391Code` are `undefined`",
      "when the locale references a 3-letter ISO 639-2 / 639-3 code outside",
      'the ISO 639-1 `LanguageCode` enum (see KNOWN_WARNINGS.md "Language',
      'enum scope"). `country` / `countryIso31661Alpha2Code` are `undefined`',
      "for language-only tags (no region subtag). Both nav refs populated",
      "in the wire-nav step. Mirrors .NET",
      "`D2.Shared.Geo.Abstractions.Locale` field-for-field.",
    ].join("\n"),
  );
  sb.appendLine("export interface Locale {");
  sb.increaseIndent();
  sb.appendLine("readonly ietfBcp47Tag: LocaleCode;");
  sb.appendLine("readonly displayName: string;");
  sb.appendLine("readonly endonym: string;");
  sb.appendLine("readonly languageIso6391Code?: LanguageCode;");
  sb.appendLine("readonly language?: Language;");
  sb.appendLine("readonly countryIso31661Alpha2Code?: CountryCode;");
  sb.appendLine("readonly country?: Country;");
  sb.appendLine("readonly isSelectable: boolean;");
  sb.appendLine("readonly firstDayOfWeek: DayOfWeek;");
  sb.appendLine("readonly decimalSeparator: string;");
  sb.appendLine("readonly thousandsSeparator: string;");
  sb.appendLine("readonly dateFormatPattern: DateFormatPattern;");
  sb.appendLine("readonly deprecation?: DeprecationInfo;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return { path: GEN_DIR("locale.g.ts"), source: sb.toString() };
}

/** Emit `timezone.g.ts` (Timezone). */
export function emitTimezoneRecords(): RecordShapeFile {
  const sb = startFile(
    SPEC_REFS["timezones"]!,
    ["CountryCode"],
    ["TimezoneCode"],
    [],
  );
  sb.appendLine('import type { Country } from "./country.g.js";');
  sb.appendLine(
    'import type { DeprecationInfo } from "../deprecation-info.js";',
  );
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "`Timezone` record. `primaryCountry` is `undefined` for `Etc/*`",
      "pseudo-zones. `coApplicableCountries` carries other countries",
      "sharing the same IANA zone (beyond the primary). Both nav refs",
      "populated in the wire-nav step. Mirrors .NET",
      "`D2.Shared.Geo.Abstractions.Timezone` field-for-field.",
    ].join("\n"),
  );
  sb.appendLine("export interface Timezone {");
  sb.increaseIndent();
  sb.appendLine("readonly ianaName: TimezoneCode;");
  sb.appendLine("readonly displayName: string;");
  sb.appendLine(
    "readonly localizedDisplayNames: Readonly<Record<string, string>>;",
  );
  sb.appendLine("readonly currentStdOffsetMinutes: number;");
  sb.appendLine("readonly currentDstOffsetMinutes?: number;");
  sb.appendLine("readonly currentStdAbbrev: string;");
  sb.appendLine("readonly currentDstAbbrev?: string;");
  sb.appendLine("readonly primaryCountryIso31661Alpha2Code?: CountryCode;");
  sb.appendLine("readonly primaryCountry?: Country;");
  sb.appendLine(
    "readonly coApplicableCountryIso31661Alpha2Codes: ReadonlySet<CountryCode>;",
  );
  sb.appendLine("readonly coApplicableCountries: readonly Country[];");
  sb.appendLine("readonly selectable: boolean;");
  sb.appendLine("readonly aliases: readonly string[];");
  sb.appendLine("readonly deprecation?: DeprecationInfo;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return { path: GEN_DIR("timezone.g.ts"), source: sb.toString() };
}

/**
 * Emit `geopolitical-entity.g.ts` (GeopoliticalEntity).
 * Sister-imports `GeopoliticalEntityType` from `./fixed-enums.g.ts`.
 */
export function emitGeopoliticalEntityRecords(): RecordShapeFile {
  const sb = startFile(
    SPEC_REFS["geopoliticalEntities"]!,
    ["CountryCode", "GeopoliticalEntityCode"],
    [],
    ["GeopoliticalEntityType"],
  );
  sb.appendLine('import type { Country } from "./country.g.js";');
  sb.appendLine(
    'import type { DeprecationInfo } from "../deprecation-info.js";',
  );
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "`GeopoliticalEntity` record. Member country dual rep:",
      "`memberCountryIso31661Alpha2Codes` for O(1) `.has()` checks,",
      "`memberCountries` for ordered iteration. Nav populated in the",
      "wire-nav step. Mirrors .NET",
      "`D2.Shared.Geo.Abstractions.GeopoliticalEntity` field-for-field.",
    ].join("\n"),
  );
  sb.appendLine("export interface GeopoliticalEntity {");
  sb.increaseIndent();
  sb.appendLine("readonly shortCode: GeopoliticalEntityCode;");
  sb.appendLine("readonly displayName: string;");
  sb.appendLine("readonly type: GeopoliticalEntityType;");
  sb.appendLine(
    "readonly memberCountryIso31661Alpha2Codes: ReadonlySet<CountryCode>;",
  );
  sb.appendLine("readonly memberCountries: readonly Country[];");
  sb.appendLine("readonly deprecation?: DeprecationInfo;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return { path: GEN_DIR("geopolitical-entity.g.ts"), source: sb.toString() };
}

/**
 * Internal helper — produce a `StringBuilder` pre-populated with the header,
 * eslint-disable, and the appropriate `import type` lines from the typed-codes
 * + fixed-enums modules.
 */
function startFile(
  specRef: string,
  enumImports: readonly string[],
  wrapperCodeImports: readonly string[],
  fixedEnumImports: readonly string[],
): StringBuilder {
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(specRef));
  appendEslintDisable(sb);
  sb.appendLine();
  for (const enumName of enumImports) {
    const fileBase = camelToKebab(enumName);
    sb.appendLine(
      `import type { ${enumName} } from "./typed-codes/${fileBase}.g.js";`,
    );
  }
  for (const wrapperName of wrapperCodeImports) {
    const fileBase = camelToKebab(wrapperName);
    sb.appendLine(
      `import type { ${wrapperName} } from "./typed-codes/${fileBase}.g.js";`,
    );
  }
  if (truthy(fixedEnumImports)) {
    sb.appendLine(
      `import type { ${fixedEnumImports.join(", ")} } from "./fixed-enums.g.js";`,
    );
  }
  return sb;
}

function camelToKebab(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
}
