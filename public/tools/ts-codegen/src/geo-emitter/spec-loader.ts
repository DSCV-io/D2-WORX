// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { basename, resolve } from "node:path";

import { falsey } from "@dcsv-io/d2-utilities";

import {
  diagError,
  type EmitDiagnostic,
  DiagnosticIds,
} from "../lib/diagnostics.js";
import { contractsPath } from "../lib/paths.js";

import type {
  CountrySpec,
  CountryCurrencyAcceptanceSpec,
  CurrencySpec,
  GeoSpecContext,
  GeopoliticalEntitySpec,
  LanguageSpec,
  LocaleSpec,
  SpecEnvelope,
  SpecMetadata,
  SubdivisionSpec,
  TimezoneSpec,
} from "./spec-types.js";

/**
 * Pure logic for parsing the geo Tier-2 JSON spec files into typed DTOs
 * (`./spec-types.ts`). Mirrors .NET `DcsvIo.D2.Geo.SourceGen.SpecLoader`
 * field-for-field — both runtimes consume the same JSON, so the
 * deserialization surface is identical by construction. JSON-shape failures
 * surface as `D2GEO001`; missing-metadata failures surface as `D2GEO006`.
 * Semantic checks (FK resolution, vocabulary discipline, catalog uniqueness)
 * belong in the per-entity emitters that consume the context.
 *
 * Spec mirroring under `./spec-types.ts` is permitted as the codegen-internal
 * carve-out — those DTOs do not leak across a package boundary and serve only
 * as typed input for emitter consumption.
 */

const _COUNTRIES_FILE = "countries.spec.json";
const _SUBDIVISIONS_FILE = "subdivisions.spec.json";
const _CURRENCIES_FILE = "currencies.spec.json";
const _LANGUAGES_FILE = "languages.spec.json";
const _LOCALES_FILE = "locales.spec.json";
const _TIMEZONES_FILE = "timezones.spec.json";
const _GEOPOLITICAL_FILE = "geopolitical-entities.spec.json";

/** Canonical list of expected spec file basenames. */
export const GEO_SPEC_FILES: readonly string[] = [
  _COUNTRIES_FILE,
  _SUBDIVISIONS_FILE,
  _CURRENCIES_FILE,
  _LANGUAGES_FILE,
  _LOCALES_FILE,
  _TIMEZONES_FILE,
  _GEOPOLITICAL_FILE,
];

/** Absolute filesystem paths to every expected geo spec file. */
export function getGeoSpecPaths(): readonly string[] {
  return GEO_SPEC_FILES.map((f) => contractsPath("geo", f));
}

/**
 * Load + parse every geo spec file under `contracts/geo/`. Returns a fully
 * populated `GeoSpecContext`; any per-file failure pushes a diagnostic onto
 * the result and leaves the corresponding context slot `undefined`.
 */
export function loadGeoSpecs(): {
  readonly context: GeoSpecContext;
  readonly diagnostics: readonly EmitDiagnostic[];
} {
  const diagnostics: EmitDiagnostic[] = [];
  const ctx: {
    countries?: SpecEnvelope<CountrySpec>;
    subdivisions?: SpecEnvelope<SubdivisionSpec>;
    currencies?: SpecEnvelope<CurrencySpec>;
    languages?: SpecEnvelope<LanguageSpec>;
    locales?: SpecEnvelope<LocaleSpec>;
    timezones?: SpecEnvelope<TimezoneSpec>;
    geopoliticalEntities?: SpecEnvelope<GeopoliticalEntitySpec>;
  } = {};

  for (const fileName of GEO_SPEC_FILES) {
    const path = contractsPath("geo", fileName);
    let raw: string;
    try {
      raw = readFileSync(path, "utf8");
    } catch (e) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GEO_MISSING_SPEC,
          `failed to read geo spec: ${(e as Error).message}`,
          path,
        ),
      );
      continue;
    }

    let root: unknown;
    try {
      root = JSON.parse(raw);
    } catch (e) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GEO_MALFORMED_SPEC,
          `${fileName}: JSON parse failed — ${(e as Error).message}`,
          path,
        ),
      );
      continue;
    }
    if (root === null || typeof root !== "object" || Array.isArray(root)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GEO_MALFORMED_SPEC,
          `${fileName}: root must be a JSON object`,
          path,
        ),
      );
      continue;
    }
    const rootObj = root as Record<string, unknown>;

    const metadata = parseMetadata(rootObj, fileName, diagnostics);
    if (metadata === undefined) continue;

    const entriesRaw = rootObj["entries"];
    if (!Array.isArray(entriesRaw)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GEO_MALFORMED_SPEC,
          `${fileName}: missing required 'entries' array at root`,
          path,
        ),
      );
      continue;
    }

    const baseName = basename(path);
    switch (baseName) {
      case _COUNTRIES_FILE:
        ctx.countries = {
          metadata,
          entries: parseEntries(
            entriesRaw,
            fileName,
            diagnostics,
            parseCountry,
          ),
        };
        break;
      case _SUBDIVISIONS_FILE:
        ctx.subdivisions = {
          metadata,
          entries: parseEntries(
            entriesRaw,
            fileName,
            diagnostics,
            parseSubdivision,
          ),
        };
        break;
      case _CURRENCIES_FILE:
        ctx.currencies = {
          metadata,
          entries: parseEntries(
            entriesRaw,
            fileName,
            diagnostics,
            parseCurrency,
          ),
        };
        break;
      case _LANGUAGES_FILE:
        ctx.languages = {
          metadata,
          entries: parseEntries(
            entriesRaw,
            fileName,
            diagnostics,
            parseLanguage,
          ),
        };
        break;
      case _LOCALES_FILE:
        ctx.locales = {
          metadata,
          entries: parseEntries(entriesRaw, fileName, diagnostics, parseLocale),
        };
        break;
      case _TIMEZONES_FILE:
        ctx.timezones = {
          metadata,
          entries: parseEntries(
            entriesRaw,
            fileName,
            diagnostics,
            parseTimezone,
          ),
        };
        break;
      case _GEOPOLITICAL_FILE:
        ctx.geopoliticalEntities = {
          metadata,
          entries: parseEntries(
            entriesRaw,
            fileName,
            diagnostics,
            parseGeopoliticalEntity,
          ),
        };
        break;
      default:
        // Unreachable — GEO_SPEC_FILES enumerates the dispatch.
        break;
    }
  }

  return { context: ctx, diagnostics };
}

function parseMetadata(
  root: Record<string, unknown>,
  fileName: string,
  diagnostics: EmitDiagnostic[],
): SpecMetadata | undefined {
  const catalogVersion = root["catalogVersion"];
  if (typeof catalogVersion !== "string" || falsey(catalogVersion)) {
    diagnostics.push(
      diagError(
        DiagnosticIds.GEO_MISSING_CATALOG_METADATA,
        `${fileName}: missing required string 'catalogVersion'`,
      ),
    );
    return undefined;
  }

  const isGeneratedRaw = root["$generated"];
  const isGenerated =
    typeof isGeneratedRaw === "boolean" ? isGeneratedRaw : false;

  const sourceRaw = root["$source"];
  const source = typeof sourceRaw === "string" ? sourceRaw : "";

  const generatedAtRaw = root["generatedAt"];
  const generatedAt =
    typeof generatedAtRaw === "string" && !falsey(generatedAtRaw)
      ? generatedAtRaw
      : undefined;

  const lastEditedAtRaw = root["lastEditedAt"];
  const lastEditedAt =
    typeof lastEditedAtRaw === "string" && !falsey(lastEditedAtRaw)
      ? lastEditedAtRaw
      : undefined;

  if (isGenerated && generatedAt === undefined) {
    diagnostics.push(
      diagError(
        DiagnosticIds.GEO_MISSING_CATALOG_METADATA,
        `${fileName}: missing required string 'generatedAt'`,
      ),
    );
    return undefined;
  }
  if (!isGenerated && lastEditedAt === undefined) {
    diagnostics.push(
      diagError(
        DiagnosticIds.GEO_MISSING_CATALOG_METADATA,
        `${fileName}: missing required string 'lastEditedAt'`,
      ),
    );
    return undefined;
  }

  const md: {
    catalogVersion: string;
    isGenerated: boolean;
    source: string;
    generatedAt?: string;
    lastEditedAt?: string;
  } = { catalogVersion, isGenerated, source };
  if (generatedAt !== undefined) md.generatedAt = generatedAt;
  if (lastEditedAt !== undefined) md.lastEditedAt = lastEditedAt;
  return md;
}

type EntryParser<T> = (
  raw: Record<string, unknown>,
  fileName: string,
  index: number,
) => { entry?: T; diagnostic?: EmitDiagnostic };

function parseEntries<T>(
  raw: readonly unknown[],
  fileName: string,
  diagnostics: EmitDiagnostic[],
  parser: EntryParser<T>,
): readonly T[] {
  const out: T[] = [];
  for (let i = 0; i < raw.length; i++) {
    const element = raw[i];
    if (
      element === null ||
      typeof element !== "object" ||
      Array.isArray(element)
    ) {
      diagnostics.push(
        diagError(
          DiagnosticIds.GEO_MALFORMED_SPEC,
          `${fileName}: entries[${i}] must be a JSON object`,
        ),
      );
      continue;
    }
    const result = parser(element as Record<string, unknown>, fileName, i);
    if (result.diagnostic !== undefined) {
      diagnostics.push(result.diagnostic);
      continue;
    }
    if (result.entry !== undefined) out.push(result.entry);
  }
  return out;
}

function reqString(
  obj: Record<string, unknown>,
  key: string,
): string | undefined {
  const v = obj[key];
  if (typeof v !== "string" || falsey(v)) return undefined;
  return v;
}

function optString(
  obj: Record<string, unknown>,
  key: string,
): string | undefined {
  const v = obj[key];
  return typeof v === "string" && !falsey(v) ? v : undefined;
}

function optInt(obj: Record<string, unknown>, key: string): number | undefined {
  const v = obj[key];
  return typeof v === "number" && Number.isFinite(v) && Number.isInteger(v)
    ? v
    : undefined;
}

function optBool(
  obj: Record<string, unknown>,
  key: string,
): boolean | undefined {
  const v = obj[key];
  return typeof v === "boolean" ? v : undefined;
}

function strList(obj: Record<string, unknown>, key: string): readonly string[] {
  const v = obj[key];
  if (!Array.isArray(v)) return [];
  return v.filter((x) => typeof x === "string" && !falsey(x)) as string[];
}

function missing(
  fileName: string,
  index: number,
  field: string,
): EmitDiagnostic {
  return diagError(
    DiagnosticIds.GEO_MALFORMED_SPEC,
    `${fileName}: entries[${index}] missing required string '${field}'`,
  );
}

function parseCountry(
  raw: Record<string, unknown>,
  fileName: string,
  index: number,
): { entry?: CountrySpec; diagnostic?: EmitDiagnostic } {
  const alpha2 = reqString(raw, "iso31661Alpha2Code");
  if (alpha2 === undefined)
    return { diagnostic: missing(fileName, index, "iso31661Alpha2Code") };
  const alpha3 = reqString(raw, "iso31661Alpha3Code");
  if (alpha3 === undefined)
    return { diagnostic: missing(fileName, index, "iso31661Alpha3Code") };
  const numeric = reqString(raw, "iso31661NumericCode");
  if (numeric === undefined)
    return { diagnostic: missing(fileName, index, "iso31661NumericCode") };
  const displayName = reqString(raw, "displayName");
  if (displayName === undefined)
    return { diagnostic: missing(fileName, index, "displayName") };
  const officialName = reqString(raw, "officialName");
  if (officialName === undefined)
    return { diagnostic: missing(fileName, index, "officialName") };
  const firstDay = reqString(raw, "firstDayOfWeek");
  if (firstDay === undefined)
    return { diagnostic: missing(fileName, index, "firstDayOfWeek") };
  const weekendStart = reqString(raw, "weekendStart");
  if (weekendStart === undefined)
    return { diagnostic: missing(fileName, index, "weekendStart") };
  const weekendEnd = reqString(raw, "weekendEnd");
  if (weekendEnd === undefined)
    return { diagnostic: missing(fileName, index, "weekendEnd") };
  const measurementSystem = reqString(raw, "measurementSystem");
  if (measurementSystem === undefined)
    return { diagnostic: missing(fileName, index, "measurementSystem") };

  const currenciesRaw = raw["currencies"];
  const currencies: CountryCurrencyAcceptanceSpec[] = [];
  if (Array.isArray(currenciesRaw)) {
    for (const ce of currenciesRaw) {
      if (ce === null || typeof ce !== "object" || Array.isArray(ce)) continue;
      const co = ce as Record<string, unknown>;
      const code = optString(co, "iso4217AlphaCode");
      const level = optString(co, "level");
      if (code !== undefined && level !== undefined)
        currencies.push({ iso4217AlphaCode: code, level });
    }
  }

  const entry: CountrySpec = {
    iso31661Alpha2Code: alpha2,
    iso31661Alpha3Code: alpha3,
    iso31661NumericCode: numeric,
    displayName,
    officialName,
    ...(optString(raw, "endonymDisplayName") !== undefined && {
      endonymDisplayName: optString(raw, "endonymDisplayName"),
    }),
    ...(optString(raw, "phoneNumberPrefix") !== undefined && {
      phoneNumberPrefix: optString(raw, "phoneNumberPrefix"),
    }),
    ...(optString(raw, "phoneNumberNationalFormat") !== undefined && {
      phoneNumberNationalFormat: optString(raw, "phoneNumberNationalFormat"),
    }),
    ...(optInt(raw, "phoneNumberMinDigits") !== undefined && {
      phoneNumberMinDigits: optInt(raw, "phoneNumberMinDigits"),
    }),
    ...(optInt(raw, "phoneNumberMaxDigits") !== undefined && {
      phoneNumberMaxDigits: optInt(raw, "phoneNumberMaxDigits"),
    }),
    firstDayOfWeek: firstDay,
    weekendStart,
    weekendEnd,
    measurementSystem,
    ...(optString(raw, "primaryLanguageISO6391Code") !== undefined && {
      primaryLanguageISO6391Code: optString(raw, "primaryLanguageISO6391Code"),
    }),
    ...(optString(raw, "primaryCurrencyISO4217AlphaCode") !== undefined && {
      primaryCurrencyISO4217AlphaCode: optString(
        raw,
        "primaryCurrencyISO4217AlphaCode",
      ),
    }),
    ...(optString(raw, "primaryLocaleIETFBCP47Tag") !== undefined && {
      primaryLocaleIETFBCP47Tag: optString(raw, "primaryLocaleIETFBCP47Tag"),
    }),
    ...(optString(raw, "sovereignCountryISO31661Alpha2Code") !== undefined && {
      sovereignCountryISO31661Alpha2Code: optString(
        raw,
        "sovereignCountryISO31661Alpha2Code",
      ),
    }),
    geopoliticalEntityShortCodes: strList(raw, "geopoliticalEntityShortCodes"),
    subdivisionISO31662Codes: strList(raw, "subdivisionISO31662Codes"),
    timezoneIanaIdentifiers: strList(raw, "timezoneIanaIdentifiers"),
    localeIETFBCP47Tags: strList(raw, "localeIETFBCP47Tags"),
    spokenLanguageISO6391Codes: strList(raw, "spokenLanguageISO6391Codes"),
    territoryISO31661Alpha2Codes: strList(raw, "territoryISO31661Alpha2Codes"),
    currencies,
  };
  return { entry };
}

function parseSubdivision(
  raw: Record<string, unknown>,
  fileName: string,
  index: number,
): { entry?: SubdivisionSpec; diagnostic?: EmitDiagnostic } {
  const code = reqString(raw, "iso31662Code");
  if (code === undefined)
    return { diagnostic: missing(fileName, index, "iso31662Code") };
  const shortCode = reqString(raw, "shortCode");
  if (shortCode === undefined)
    return { diagnostic: missing(fileName, index, "shortCode") };
  const displayName = reqString(raw, "displayName");
  if (displayName === undefined)
    return { diagnostic: missing(fileName, index, "displayName") };
  const officialName = reqString(raw, "officialName");
  if (officialName === undefined)
    return { diagnostic: missing(fileName, index, "officialName") };
  const countryCode = reqString(raw, "countryISO31661Alpha2Code");
  if (countryCode === undefined)
    return {
      diagnostic: missing(fileName, index, "countryISO31661Alpha2Code"),
    };

  const entry: SubdivisionSpec = {
    iso31662Code: code,
    shortCode,
    displayName,
    officialName,
    ...(optString(raw, "endonymDisplayName") !== undefined && {
      endonymDisplayName: optString(raw, "endonymDisplayName"),
    }),
    countryISO31661Alpha2Code: countryCode,
    ...(optString(raw, "parentISO31662Code") !== undefined && {
      parentISO31662Code: optString(raw, "parentISO31662Code"),
    }),
    ...(optString(raw, "type") !== undefined && {
      type: optString(raw, "type"),
    }),
    ...(optInt(raw, "order") !== undefined && { order: optInt(raw, "order") }),
  };
  return { entry };
}

function parseCurrency(
  raw: Record<string, unknown>,
  fileName: string,
  index: number,
): { entry?: CurrencySpec; diagnostic?: EmitDiagnostic } {
  const alpha = reqString(raw, "iso4217AlphaCode");
  if (alpha === undefined)
    return { diagnostic: missing(fileName, index, "iso4217AlphaCode") };
  const displayName = reqString(raw, "displayName");
  if (displayName === undefined)
    return { diagnostic: missing(fileName, index, "displayName") };
  const entry: CurrencySpec = {
    iso4217AlphaCode: alpha,
    ...(optString(raw, "iso4217NumericCode") !== undefined && {
      iso4217NumericCode: optString(raw, "iso4217NumericCode"),
    }),
    displayName,
    decimalPlaces: optInt(raw, "decimalPlaces") ?? 0,
    ...(optString(raw, "symbol") !== undefined && {
      symbol: optString(raw, "symbol"),
    }),
    isActive: optBool(raw, "isActive") ?? false,
    isSupported: optBool(raw, "isSupported") ?? false,
  };
  return { entry };
}

function parseLanguage(
  raw: Record<string, unknown>,
  fileName: string,
  index: number,
): { entry?: LanguageSpec; diagnostic?: EmitDiagnostic } {
  const code = reqString(raw, "iso6391Code");
  if (code === undefined)
    return { diagnostic: missing(fileName, index, "iso6391Code") };
  const name = reqString(raw, "name");
  if (name === undefined)
    return { diagnostic: missing(fileName, index, "name") };
  const writingDirection = reqString(raw, "writingDirection");
  if (writingDirection === undefined)
    return { diagnostic: missing(fileName, index, "writingDirection") };

  const entry: LanguageSpec = {
    iso6391Code: code,
    name,
    ...(optString(raw, "endonym") !== undefined && {
      endonym: optString(raw, "endonym"),
    }),
    writingDirection,
    isSupported: optBool(raw, "isSupported") ?? false,
    spokenInCountryISO31661Alpha2Codes: strList(
      raw,
      "spokenInCountryISO31661Alpha2Codes",
    ),
  };
  return { entry };
}

function parseLocale(
  raw: Record<string, unknown>,
  fileName: string,
  index: number,
): { entry?: LocaleSpec; diagnostic?: EmitDiagnostic } {
  const tag = reqString(raw, "ietfBcp47Tag");
  if (tag === undefined)
    return { diagnostic: missing(fileName, index, "ietfBcp47Tag") };
  const name = reqString(raw, "name");
  if (name === undefined)
    return { diagnostic: missing(fileName, index, "name") };
  const languageCode = reqString(raw, "languageISO6391Code");
  if (languageCode === undefined)
    return { diagnostic: missing(fileName, index, "languageISO6391Code") };
  const firstDay = reqString(raw, "firstDayOfWeek");
  if (firstDay === undefined)
    return { diagnostic: missing(fileName, index, "firstDayOfWeek") };
  const decimalSeparator = reqString(raw, "decimalSeparator");
  if (decimalSeparator === undefined)
    return { diagnostic: missing(fileName, index, "decimalSeparator") };
  const dateFormat = reqString(raw, "dateFormatPattern");
  if (dateFormat === undefined)
    return { diagnostic: missing(fileName, index, "dateFormatPattern") };

  // thousandsSeparator may legitimately be empty (e.g. some locales).
  const thousandsRaw = raw["thousandsSeparator"];
  const thousandsSeparator =
    typeof thousandsRaw === "string" ? thousandsRaw : "";

  const entry: LocaleSpec = {
    ietfBcp47Tag: tag,
    name,
    ...(optString(raw, "endonym") !== undefined && {
      endonym: optString(raw, "endonym"),
    }),
    languageISO6391Code: languageCode,
    ...(optString(raw, "countryISO31661Alpha2Code") !== undefined && {
      countryISO31661Alpha2Code: optString(raw, "countryISO31661Alpha2Code"),
    }),
    isSelectable: optBool(raw, "isSelectable") ?? false,
    firstDayOfWeek: firstDay,
    decimalSeparator,
    thousandsSeparator,
    dateFormatPattern: dateFormat,
  };
  return { entry };
}

function parseTimezone(
  raw: Record<string, unknown>,
  fileName: string,
  index: number,
): { entry?: TimezoneSpec; diagnostic?: EmitDiagnostic } {
  const iana = reqString(raw, "ianaIdentifier");
  if (iana === undefined)
    return { diagnostic: missing(fileName, index, "ianaIdentifier") };
  const displayName = reqString(raw, "displayName");
  if (displayName === undefined)
    return { diagnostic: missing(fileName, index, "displayName") };
  const stdOffset = optInt(raw, "currentStdOffsetMinutes");
  if (stdOffset === undefined)
    return { diagnostic: missing(fileName, index, "currentStdOffsetMinutes") };

  const entry: TimezoneSpec = {
    ianaIdentifier: iana,
    displayName,
    currentStdOffsetMinutes: stdOffset,
    ...(optInt(raw, "currentDstOffsetMinutes") !== undefined && {
      currentDstOffsetMinutes: optInt(raw, "currentDstOffsetMinutes"),
    }),
    currentStdAbbrev: optString(raw, "currentStdAbbrev") ?? "",
    ...(optString(raw, "currentDstAbbrev") !== undefined && {
      currentDstAbbrev: optString(raw, "currentDstAbbrev"),
    }),
    ...(optString(raw, "countryISO31661Alpha2Code") !== undefined && {
      countryISO31661Alpha2Code: optString(raw, "countryISO31661Alpha2Code"),
    }),
    coApplicableCountryISO31661Alpha2Codes: strList(
      raw,
      "coApplicableCountryISO31661Alpha2Codes",
    ),
    aliases: strList(raw, "aliases"),
  };
  return { entry };
}

function parseGeopoliticalEntity(
  raw: Record<string, unknown>,
  fileName: string,
  index: number,
): { entry?: GeopoliticalEntitySpec; diagnostic?: EmitDiagnostic } {
  const shortCode = reqString(raw, "shortCode");
  if (shortCode === undefined)
    return { diagnostic: missing(fileName, index, "shortCode") };
  const name = reqString(raw, "name");
  if (name === undefined)
    return { diagnostic: missing(fileName, index, "name") };
  const type = reqString(raw, "type");
  if (type === undefined)
    return { diagnostic: missing(fileName, index, "type") };

  const entry: GeopoliticalEntitySpec = {
    shortCode,
    name,
    type,
    countryISO31661Alpha2Codes: strList(raw, "countryISO31661Alpha2Codes"),
  };
  return { entry };
}

// Re-export path helper so the orchestrator can list inputs without
// duplicating the file enumeration.
export function getGeoSpecAbsolutePaths(): readonly string[] {
  return GEO_SPEC_FILES.map((f) => resolve(contractsPath("geo", f)));
}
