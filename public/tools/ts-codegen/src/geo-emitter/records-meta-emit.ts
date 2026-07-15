// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { buildHeader } from "../lib/file-emit.js";
import { tsPackagePath } from "../lib/paths.js";

import { appendEslintDisable } from "./emit-helpers.js";

/**
 * Emits `_records-meta.g.ts` to `@dcsv-io/d2-geo-default/src/generated/` — the
 * TS-side record-shape catalog consumed by the cross-language records
 * parity test (`geo-records.parity.test.ts`). Mirrors the .NET-side
 * fixture emitted by `GeoRecordsFixtureEmitter` so the parity test can
 * compare field name sets modulo casing.
 *
 * Each record type lists every field with its name + type-name + a
 * nullability flag. TS erases types at runtime, so the emitter writes
 * the shape as a static const-object rather than reflecting on
 * interface declarations.
 */
export function emitRecordsMeta(): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  const lines: string[] = [];
  lines.push(
    buildHeader("contracts/geo/*.spec.json (records-meta shape catalog)"),
  );
  appendEslintDisableTo(lines);
  lines.push("");
  lines.push("/** Cross-language records-meta catalog. Consumed by");
  lines.push(
    "  * `public/packages/typescript/contract-tests/tests/geo-records.parity.test.ts`. */",
  );
  lines.push("export interface RecordFieldMeta {");
  lines.push("  readonly name: string;");
  lines.push("  readonly type: string;");
  lines.push("  readonly nullable: boolean;");
  lines.push("}");
  lines.push("");
  lines.push(
    "export const GEO_RECORDS_META: Readonly<Record<string, readonly RecordFieldMeta[]>> = {",
  );
  for (const [recordName, fields] of Object.entries(RECORD_SHAPES)) {
    lines.push(`  ${recordName}: [`);
    for (const f of fields) {
      const name = JSON.stringify(f.name);
      const type = JSON.stringify(f.type);
      const obj = `{ name: ${name}, type: ${type}, nullable: ${f.nullable} }`;
      lines.push(`    ${obj},`);
    }
    lines.push("  ],");
  }
  lines.push("};");
  lines.push("");

  const path = tsPackagePath(
    "geo",
    "default",
    "src",
    "generated",
    "_records-meta.g.ts",
  );
  return { outputs: [{ path, source: lines.join("\n") }] };
}

function appendEslintDisableTo(lines: string[]): void {
  // appendEslintDisable mutates a StringBuilder; here we just inline the
  // expected line so the lib doesn't need a refactor for one call site.
  void appendEslintDisable;
  lines.push("/* eslint-disable */");
}

interface FieldMeta {
  readonly name: string;
  readonly type: string;
  readonly nullable: boolean;
}

/**
 * Static record-shape catalog. Each entry mirrors the field surface of
 * the `@dcsv-io/d2-geo-abstractions` interface of the same name. Field-name
 * casing here is TS-native (camelCase); the parity test compares to
 * the .NET PascalCase names with case-insensitive equality.
 *
 * Field types use shorthand: `List<T>` for ordered lists, `Set<T>` for
 * ReadonlySet-backed FKs, `Dictionary<K,V>` for keyed maps, bare names
 * (`string`, `number`, `boolean`, code/enum names) elsewhere.
 */
const RECORD_SHAPES: Readonly<Record<string, readonly FieldMeta[]>> = {
  Country: [
    { name: "iso31661Alpha2Code", type: "CountryCode", nullable: false },
    { name: "iso31661Alpha3Code", type: "string", nullable: false },
    { name: "iso31661NumericCode", type: "string", nullable: false },
    { name: "displayName", type: "string", nullable: false },
    { name: "officialName", type: "string", nullable: false },
    { name: "endonymDisplayName", type: "string", nullable: false },
    { name: "endonymOfficialName", type: "string", nullable: false },
    { name: "phoneNumberPrefix", type: "string", nullable: false },
    { name: "phoneNumberNationalFormat", type: "string", nullable: false },
    { name: "phoneNumberMinDigits", type: "number", nullable: true },
    { name: "phoneNumberMaxDigits", type: "number", nullable: false },
    { name: "firstDayOfWeek", type: "DayOfWeek", nullable: false },
    { name: "weekendStart", type: "DayOfWeek", nullable: false },
    { name: "weekendEnd", type: "DayOfWeek", nullable: false },
    { name: "measurementSystem", type: "MeasurementSystem", nullable: false },
    {
      name: "primaryLanguageIso6391Code",
      type: "LanguageCode",
      nullable: true,
    },
    { name: "primaryLanguage", type: "Language", nullable: true },
    {
      name: "primaryCurrencyIso4217AlphaCode",
      type: "CurrencyCode",
      nullable: true,
    },
    { name: "primaryCurrency", type: "Currency", nullable: true },
    { name: "primaryLocaleIetfBcp47Tag", type: "LocaleCode", nullable: true },
    { name: "primaryLocale", type: "Locale", nullable: true },
    {
      name: "sovereignCountryIso31661Alpha2Code",
      type: "CountryCode",
      nullable: true,
    },
    { name: "sovereignCountry", type: "Country", nullable: true },
    {
      name: "territoryIso31661Alpha2Codes",
      type: "Set<CountryCode>",
      nullable: false,
    },
    { name: "territories", type: "List<Country>", nullable: false },
    {
      name: "subdivisionIso31662Codes",
      type: "Set<SubdivisionCode>",
      nullable: false,
    },
    { name: "subdivisions", type: "List<Subdivision>", nullable: false },
    { name: "localeIetfBcp47Tags", type: "Set<LocaleCode>", nullable: false },
    { name: "locales", type: "List<Locale>", nullable: false },
    {
      name: "geopoliticalEntityShortCodes",
      type: "Set<GeopoliticalEntityCode>",
      nullable: false,
    },
    {
      name: "geopoliticalEntities",
      type: "List<GeopoliticalEntity>",
      nullable: false,
    },
    {
      name: "currencyIso4217AlphaCodes",
      type: "Set<CurrencyCode>",
      nullable: false,
    },
    {
      name: "currencies",
      type: "List<CountryCurrencyAcceptance>",
      nullable: false,
    },
    { name: "deprecation", type: "DeprecationInfo", nullable: true },
  ],
  Subdivision: [
    { name: "iso31662Code", type: "SubdivisionCode", nullable: false },
    { name: "shortCode", type: "string", nullable: false },
    { name: "displayName", type: "string", nullable: false },
    { name: "officialName", type: "string", nullable: false },
    { name: "endonymDisplayName", type: "string", nullable: false },
    { name: "endonymOfficialName", type: "string", nullable: false },
    { name: "countryIso31661Alpha2Code", type: "CountryCode", nullable: false },
    { name: "country", type: "Country", nullable: true },
    {
      name: "parentSubdivisionIso31662Code",
      type: "SubdivisionCode",
      nullable: true,
    },
    { name: "parentSubdivision", type: "Subdivision", nullable: true },
    { name: "type", type: "string", nullable: false },
    { name: "deprecation", type: "DeprecationInfo", nullable: true },
  ],
  Currency: [
    { name: "iso4217AlphaCode", type: "CurrencyCode", nullable: false },
    { name: "iso4217NumericCode", type: "string", nullable: false },
    { name: "displayName", type: "string", nullable: false },
    { name: "officialName", type: "string", nullable: false },
    { name: "decimalPlaces", type: "number", nullable: false },
    { name: "symbol", type: "string", nullable: false },
    { name: "isSupported", type: "boolean", nullable: false },
    {
      name: "acceptedInCountryIso31661Alpha2Codes",
      type: "Set<CountryCode>",
      nullable: false,
    },
    { name: "acceptedInCountries", type: "List<Country>", nullable: false },
    { name: "deprecation", type: "DeprecationInfo", nullable: true },
  ],
  Language: [
    { name: "iso6391Code", type: "LanguageCode", nullable: false },
    { name: "displayName", type: "string", nullable: false },
    { name: "endonym", type: "string", nullable: false },
    { name: "writingDirection", type: "WritingDirection", nullable: false },
    { name: "isSupported", type: "boolean", nullable: false },
    {
      name: "spokenInCountryIso31661Alpha2Codes",
      type: "Set<CountryCode>",
      nullable: false,
    },
    { name: "spokenInCountries", type: "List<Country>", nullable: false },
    { name: "localeIetfBcp47Tags", type: "Set<LocaleCode>", nullable: false },
    { name: "locales", type: "List<Locale>", nullable: false },
    { name: "deprecation", type: "DeprecationInfo", nullable: true },
  ],
  Locale: [
    { name: "ietfBcp47Tag", type: "LocaleCode", nullable: false },
    { name: "displayName", type: "string", nullable: false },
    { name: "endonym", type: "string", nullable: false },
    { name: "languageIso6391Code", type: "LanguageCode", nullable: true },
    { name: "language", type: "Language", nullable: true },
    { name: "countryIso31661Alpha2Code", type: "CountryCode", nullable: true },
    { name: "country", type: "Country", nullable: true },
    { name: "isSelectable", type: "boolean", nullable: false },
    { name: "firstDayOfWeek", type: "DayOfWeek", nullable: false },
    { name: "decimalSeparator", type: "string", nullable: false },
    { name: "thousandsSeparator", type: "string", nullable: false },
    { name: "dateFormatPattern", type: "DateFormatPattern", nullable: false },
    { name: "deprecation", type: "DeprecationInfo", nullable: true },
  ],
  Timezone: [
    { name: "ianaName", type: "TimezoneCode", nullable: false },
    { name: "displayName", type: "string", nullable: false },
    {
      name: "localizedDisplayNames",
      type: "Dictionary<string,string>",
      nullable: false,
    },
    { name: "currentStdOffsetMinutes", type: "number", nullable: false },
    { name: "currentDstOffsetMinutes", type: "number", nullable: true },
    { name: "currentStdAbbrev", type: "string", nullable: false },
    { name: "currentDstAbbrev", type: "string", nullable: true },
    {
      name: "primaryCountryIso31661Alpha2Code",
      type: "CountryCode",
      nullable: true,
    },
    { name: "primaryCountry", type: "Country", nullable: true },
    {
      name: "coApplicableCountryIso31661Alpha2Codes",
      type: "Set<CountryCode>",
      nullable: false,
    },
    { name: "coApplicableCountries", type: "List<Country>", nullable: false },
    { name: "selectable", type: "boolean", nullable: false },
    { name: "aliases", type: "List<string>", nullable: false },
    { name: "deprecation", type: "DeprecationInfo", nullable: true },
  ],
  GeopoliticalEntity: [
    { name: "shortCode", type: "GeopoliticalEntityCode", nullable: false },
    { name: "displayName", type: "string", nullable: false },
    { name: "type", type: "GeopoliticalEntityType", nullable: false },
    {
      name: "memberCountryIso31661Alpha2Codes",
      type: "Set<CountryCode>",
      nullable: false,
    },
    { name: "memberCountries", type: "List<Country>", nullable: false },
    { name: "deprecation", type: "DeprecationInfo", nullable: true },
  ],
  CountryCurrencyAcceptance: [
    { name: "iso4217AlphaCode", type: "CurrencyCode", nullable: false },
    { name: "level", type: "CurrencyAcceptanceLevel", nullable: false },
    { name: "currency", type: "Currency", nullable: true },
  ],
};
