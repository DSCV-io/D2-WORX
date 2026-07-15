// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { truthy } from "@dcsv-io/d2-utilities";

import { buildHeader, writeGeneratedFile } from "../lib/file-emit.js";
import { tsPackagePath } from "../lib/paths.js";
import { StringBuilder } from "../lib/string-builder.js";

import {
  appendEslintDisable,
  appendJsDoc,
  escapeStringLiteral,
  safeKey,
} from "./emit-helpers.js";
import type {
  CountrySpec,
  CurrencySpec,
  GeopoliticalEntitySpec,
  LanguageSpec,
} from "./spec-types.js";

/**
 * Emits the four "real enum" code catalogs to
 * `@dcsv-io/d2-geo-abstractions/src/generated/typed-codes/<name>.g.ts` (Country,
 * Currency, Language, GeopoliticalEntity) plus the related fixed-value enums
 * derived from the spec data (GeopoliticalEntityType, WritingDirection,
 * DateFormatPattern, CurrencyAcceptanceLevel).
 *
 * Output shape per enum (matches the pattern set by `auth-error-codes.g.ts`):
 *
 * ```ts
 * export const Country = { US: "US", CA: "CA", ... } as const;
 * export type Country = (typeof Country)[keyof typeof Country] & {
 *   readonly __brand: "Country";
 * };
 * export const CountrySchema = z
 *   .string()
 *   .refine((s): s is Country => ALL_COUNTRIES.has(s), { ... });
 * export const ALL_COUNTRIES: ReadonlySet<string> = new Set([...]);
 * ```
 *
 * Branding + Zod schemas are the established pattern for spec-derived
 * closed-set catalogs in `@dcsv-io/d2-geo-abstractions` (the package already lists
 * `zod` as a peer dependency).
 */

const SPEC_REF_COUNTRIES = "contracts/geo/countries.spec.json";
const SPEC_REF_CURRENCIES = "contracts/geo/currencies.spec.json";
const SPEC_REF_LANGUAGES = "contracts/geo/languages.spec.json";
const SPEC_REF_GEOPOLITICAL = "contracts/geo/geopolitical-entities.spec.json";
const SPEC_REF_LOCALES = "contracts/geo/locales.spec.json";

const GEN_DIR = (...parts: string[]): string =>
  tsPackagePath("geo", "abstractions", "src", "generated", ...parts);

/** Emit `CountryCode` real-enum + branded type + Zod schema + lookup set. */
export function emitCountryEnum(entries: readonly CountrySpec[]): {
  readonly path: string;
  readonly source: string;
} {
  const sorted = [...entries].sort((a, b) =>
    a.iso31661Alpha2Code.localeCompare(b.iso31661Alpha2Code),
  );
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(SPEC_REF_COUNTRIES));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine('import { z } from "zod";');
  sb.appendLine();
  emitConstObjectEnum(
    sb,
    "CountryCode",
    "ISO 3166-1 alpha-2 country code catalog. Branded string type — narrows to " +
      "exactly the 250-ish codes shipped in the spec. Mirrors .NET " +
      "`DcsvIo.D2.Geo.Abstractions.CountryCode` (real enum) byte-for-byte over " +
      "the wire (string-encoded alpha-2 in both runtimes). The bare `Country` " +
      "name is reserved for the spec-derived data record (interface).",
    sorted.map((c) => ({
      key: c.iso31661Alpha2Code,
      value: c.iso31661Alpha2Code,
      doc: c.displayName,
    })),
  );
  return {
    path: GEN_DIR("typed-codes", "country-code.g.ts"),
    source: sb.toString(),
  };
}

/** Emit `CurrencyCode` real-enum + branded type + Zod schema + lookup set. */
export function emitCurrencyEnum(entries: readonly CurrencySpec[]): {
  readonly path: string;
  readonly source: string;
} {
  const sorted = [...entries].sort((a, b) =>
    a.iso4217AlphaCode.localeCompare(b.iso4217AlphaCode),
  );
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(SPEC_REF_CURRENCIES));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine('import { z } from "zod";');
  sb.appendLine();
  emitConstObjectEnum(
    sb,
    "CurrencyCode",
    "ISO 4217 alpha currency code catalog (active + historical). Branded " +
      "string type. Mirrors .NET `DcsvIo.D2.Geo.Abstractions.CurrencyCode` " +
      "(real enum) byte-for-byte over the wire.",
    sorted.map((c) => ({
      key: c.iso4217AlphaCode,
      value: c.iso4217AlphaCode,
      doc: c.displayName,
    })),
  );
  return {
    path: GEN_DIR("typed-codes", "currency-code.g.ts"),
    source: sb.toString(),
  };
}

/** Emit `LanguageCode` real-enum + branded type + Zod schema + lookup set. */
export function emitLanguageEnum(entries: readonly LanguageSpec[]): {
  readonly path: string;
  readonly source: string;
} {
  const sorted = [...entries].sort((a, b) =>
    a.iso6391Code.localeCompare(b.iso6391Code),
  );
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(SPEC_REF_LANGUAGES));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine('import { z } from "zod";');
  sb.appendLine();
  emitConstObjectEnum(
    sb,
    "LanguageCode",
    "ISO 639-1 alpha-2 language code catalog. Branded string type. Mirrors " +
      ".NET `DcsvIo.D2.Geo.Abstractions.LanguageCode` (real enum) " +
      "byte-for-byte over the wire.",
    sorted.map((l) => ({
      key: l.iso6391Code,
      value: l.iso6391Code,
      doc: l.name,
    })),
  );
  return {
    path: GEN_DIR("typed-codes", "language-code.g.ts"),
    source: sb.toString(),
  };
}

/** Emit `GeopoliticalEntityCode` real-enum + branded type + Zod schema. */
export function emitGeopoliticalEntityEnum(
  entries: readonly GeopoliticalEntitySpec[],
): { readonly path: string; readonly source: string } {
  const sorted = [...entries].sort((a, b) =>
    a.shortCode.localeCompare(b.shortCode),
  );
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(SPEC_REF_GEOPOLITICAL));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine('import { z } from "zod";');
  sb.appendLine();
  emitConstObjectEnum(
    sb,
    "GeopoliticalEntityCode",
    "Catalog of supranational geopolitical short-codes (EU, NATO, USMCA, " +
      "continents, trade blocs, military alliances, etc.). Branded string " +
      "type. Mirrors .NET `DcsvIo.D2.Geo.Abstractions.GeopoliticalEntityCode` " +
      "byte-for-byte over the wire.",
    sorted.map((g) => ({
      key: g.shortCode,
      value: g.shortCode,
      doc: g.name,
    })),
  );
  return {
    path: GEN_DIR("typed-codes", "geopolitical-entity-code.g.ts"),
    source: sb.toString(),
  };
}

/**
 * Emit the closed-set typed enums that aren't strictly derived from a single
 * spec but live in the same generated tree because they describe field
 * shapes consumed by other emitted records. Output uses numeric-keyed
 * const-objects for `GeopoliticalEntityType` to preserve the exact integer
 * assignments stable across the wire and across .NET / TS; other enums are
 * string-valued.
 */
export function emitFixedEnums(): {
  readonly path: string;
  readonly source: string;
} {
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(SPEC_REF_GEOPOLITICAL));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine('import { z } from "zod";');
  sb.appendLine();

  // GeopoliticalEntityType — numeric enum with explicit values stable
  // across the wire and across .NET / TS. Const-object so the integer
  // assignments survive.
  appendJsDoc(
    sb,
    [
      "Type classification for `GeopoliticalEntity`. Numeric values are stable",
      "across the wire and across .NET / TS so the const-object form preserves",
      "the exact integer assignments. Categories: General Geopolitical (0-2),",
      "Economic (10-17), Political (20-25), Military (30-35).",
      "",
      "Mirrors .NET `DcsvIo.D2.Geo.Abstractions.GeopoliticalEntityType`",
      "byte-for-byte (same integer values, same names).",
    ].join("\n"),
  );
  sb.appendLine("export const GeopoliticalEntityType = {");
  sb.increaseIndent();
  const geTypeEntries: readonly { name: string; value: number }[] = [
    { name: "Continent", value: 0 },
    { name: "SubContinent", value: 1 },
    { name: "GeopoliticalRegion", value: 2 },
    { name: "FreeTradeAgreement", value: 10 },
    { name: "CustomsUnion", value: 11 },
    { name: "CommonMarket", value: 12 },
    { name: "EconomicUnion", value: 13 },
    { name: "MonetaryUnion", value: 14 },
    { name: "BilateralInvestmentTreaty", value: 15 },
    { name: "DevelopmentAgreement", value: 16 },
    { name: "ResourceSharingAgreement", value: 17 },
    { name: "PoliticalUnion", value: 20 },
    { name: "HumanRightsAgreement", value: 21 },
    { name: "EnvironmentalAgreement", value: 22 },
    { name: "GovernanceAndCooperationAgreement", value: 23 },
    { name: "PeaceTreaty", value: 24 },
    { name: "DemocracyPromotionAgreement", value: 25 },
    { name: "MilitaryAlliance", value: 30 },
    { name: "ArmsControlAgreement", value: 31 },
    { name: "StatusOfForcesAgreement", value: 32 },
    { name: "PeacekeepingAgreement", value: 33 },
    { name: "SecurityCooperationAgreement", value: 34 },
    { name: "NonAggressionPact", value: 35 },
  ];
  for (const e of geTypeEntries) sb.appendLine(`${e.name}: ${e.value},`);
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    "export type GeopoliticalEntityType = " +
      "(typeof GeopoliticalEntityType)[keyof typeof GeopoliticalEntityType];",
  );
  sb.appendLine();
  sb.appendLine("export const GeopoliticalEntityTypeSchema = z.union([");
  sb.increaseIndent();
  for (const e of geTypeEntries) sb.appendLine(`z.literal(${e.value}),`);
  sb.decreaseIndent();
  sb.appendLine("]);");
  sb.appendLine();

  emitStringEnumWithSchema(
    sb,
    "WritingDirection",
    ["LTR", "RTL"],
    [
      "Writing direction enum (left-to-right vs right-to-left). Surfaces on the",
      "`Language` record. Mirrors .NET",
      "`DcsvIo.D2.Geo.Abstractions.WritingDirection`.",
    ].join("\n"),
  );

  emitStringEnumWithSchema(
    sb,
    "DateFormatPattern",
    ["DMY", "MDY", "YMD"],
    [
      "Date format pattern enum. Surfaces on `Locale.dateFormatPattern`.",
      "Mirrors .NET `DcsvIo.D2.Geo.Abstractions.DateFormatPattern`.",
    ].join("\n"),
  );

  emitStringEnumWithSchema(
    sb,
    "CurrencyAcceptanceLevel",
    ["LegalTender", "WidelyAccepted", "Tourist"],
    [
      "Acceptance classification for a currency within a country (legal tender",
      "vs widely accepted vs tourist). Surfaces on `Country.currencies[]`.",
      "Mirrors .NET `DcsvIo.D2.Geo.Abstractions.CurrencyAcceptanceLevel`.",
    ].join("\n"),
  );

  emitStringEnumWithSchema(
    sb,
    "MeasurementSystem",
    ["Metric", "Imperial", "Mixed"],
    [
      "Measurement system enum. Surfaces on `Country.measurementSystem`.",
      "Mirrors .NET `DcsvIo.D2.Geo.Abstractions.MeasurementSystem`.",
    ].join("\n"),
  );

  emitStringEnumWithSchema(
    sb,
    "DayOfWeek",
    [
      "Monday",
      "Tuesday",
      "Wednesday",
      "Thursday",
      "Friday",
      "Saturday",
      "Sunday",
    ],
    [
      "Day-of-week enum mirroring .NET `GeoDayOfWeek` (string-valued on the",
      "wire). Surfaces on `Country.firstDayOfWeek` / `Country.weekendStart` /",
      "`Country.weekendEnd` / `Locale.firstDayOfWeek`.",
    ].join("\n"),
  );

  return { path: GEN_DIR("fixed-enums.g.ts"), source: sb.toString() };
}

/**
 * Emit a `Locale.resolveSelectable()` shape — kept here with the enums so the
 * branded `LocaleCode` type referenced in its signature is in scope (the
 * concrete impl lives in `@dcsv-io/d2-geo-default`; this just emits the API shape).
 * Currently a stub — the impl + selectable-locale data lands in
 * `@dcsv-io/d2-geo-default` once selectable-locale cascade data is emitted.
 */
export function emitLocaleHelpersStub(): {
  readonly path: string;
  readonly source: string;
} {
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(SPEC_REF_LOCALES));
  appendEslintDisable(sb);
  sb.appendLine();
  appendJsDoc(
    sb,
    [
      "Locale helper-API shape stubs — actual implementation lands in",
      "`@dcsv-io/d2-geo-default` once the selectable-locale list + cascade data is",
      "emitted. Re-exported here purely to pin the public-facing function",
      "signatures so consumers can import them via `@dcsv-io/d2-geo-abstractions`",
      "regardless of which package supplies the impl.",
    ].join("\n"),
  );
  sb.appendLine();
  sb.appendLine(
    "// (no exports yet — populated when `@dcsv-io/d2-geo-default` ships.)",
  );
  sb.appendLine("export {};");
  return { path: GEN_DIR("locale-helpers.g.ts"), source: sb.toString() };
}

/**
 * Internal helper — emit a const-object string enum with branded type, Zod
 * schema, and `ALL_*` validation set. Used for the four real-enum catalogs.
 */
function emitConstObjectEnum(
  sb: StringBuilder,
  typeName: string,
  doc: string,
  entries: readonly { key: string; value: string; doc?: string }[],
): void {
  appendJsDoc(sb, doc);
  sb.appendLine(`export const ${typeName} = {`);
  sb.increaseIndent();
  for (const e of entries) {
    if (truthy(e.doc)) {
      sb.appendLine(`/** ${e.doc!.replace(/\*\//g, "*\\/")} */`);
    }
    sb.appendLine(`${safeKey(e.key)}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    `export type ${typeName} = ` +
      `(typeof ${typeName})[keyof typeof ${typeName}] & { readonly __brand: "${typeName}" };`,
  );
  sb.appendLine();
  const setName = `ALL_${camelToScreaming(typeName)}_SET`;
  sb.appendLine(`export const ${setName}: ReadonlySet<string> = new Set([`);
  sb.increaseIndent();
  for (const e of entries) sb.appendLine(`"${escapeStringLiteral(e.value)}",`);
  sb.decreaseIndent();
  sb.appendLine("]);");
  sb.appendLine();
  sb.appendLine(`export const ${typeName}Schema = z`);
  sb.increaseIndent();
  sb.appendLine(".string()");
  sb.appendLine(
    `.refine((s): s is ${typeName} => ${setName}.has(s),` +
      ` { message: "value is not a known ${typeName} code" });`,
  );
  sb.decreaseIndent();
  sb.appendLine();
}

function emitStringEnumWithSchema(
  sb: StringBuilder,
  typeName: string,
  values: readonly string[],
  doc: string,
): void {
  appendJsDoc(sb, doc);
  sb.appendLine(`export const ${typeName} = {`);
  sb.increaseIndent();
  for (const v of values) sb.appendLine(`${v}: "${v}",`);
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    `export type ${typeName} = (typeof ${typeName})[keyof typeof ${typeName}];`,
  );
  sb.appendLine();
  sb.appendLine(`export const ${typeName}Schema = z.enum([`);
  sb.increaseIndent();
  for (const v of values) sb.appendLine(`"${v}",`);
  sb.decreaseIndent();
  sb.appendLine("]);");
  sb.appendLine();
}

/** `Country` → `COUNTRY`, `GeopoliticalEntity` → `GEOPOLITICAL_ENTITY`. */
function camelToScreaming(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1_$2").toUpperCase();
}

/** Write all emitted enum files to disk. Returns the list of paths written. */
export function writeEnumOutputs(
  outputs: readonly { path: string; source: string }[],
): readonly string[] {
  const paths: string[] = [];
  for (const { path, source } of outputs) {
    writeGeneratedFile(path, source);
    paths.push(path);
  }
  return paths;
}
