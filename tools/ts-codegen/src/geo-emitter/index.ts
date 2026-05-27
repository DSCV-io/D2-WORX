// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { type EmitDiagnostic } from "../lib/diagnostics.js";
import { isOutputUpToDate, writeGeneratedFile } from "../lib/file-emit.js";
import { tsPackagePath } from "../lib/paths.js";

import { assertCatalogUniqueness } from "./catalog-uniqueness.js";
import {
  emitCountryEnum,
  emitCurrencyEnum,
  emitFixedEnums,
  emitGeopoliticalEntityEnum,
  emitLanguageEnum,
  emitLocaleHelpersStub,
} from "./enum-emit.js";
import { emitGeoCatalog } from "./geo-catalog-emit.js";
import {
  emitCountryRecords,
  emitCurrencyRecords,
  emitGeopoliticalEntityRecords,
  emitLanguageRecords,
  emitLocaleRecords,
  emitSubdivisionRecords,
  emitTimezoneRecords,
} from "./record-shape-emit.js";
import { emitRecordsMeta } from "./records-meta-emit.js";
import { getGeoSpecPaths, loadGeoSpecs } from "./spec-loader.js";
import { validateVocabulary } from "./vocabulary-guard.js";
import {
  emitLocaleCode,
  emitSubdivisionCode,
  emitTimezoneCode,
} from "./wrapper-code-emit.js";

// Catalog DATA emitters (write to @d2/geo-default/src/generated/).
import { emitCountryData } from "./default/country-data-emit.js";
import { emitCurrencyData } from "./default/currency-data-emit.js";
import { emitGeoDataInitializer } from "./default/geo-data-initializer-emit.js";
import { emitGeopoliticalEntityData } from "./default/geopolitical-entity-data-emit.js";
import { emitLanguageData } from "./default/language-data-emit.js";
import { emitLocaleData } from "./default/locale-data-emit.js";
import { emitSubdivisionData } from "./default/subdivision-data-emit.js";
import { emitTimezoneData } from "./default/timezone-data-emit.js";

/**
 * Geo codegen orchestrator. Single entrypoint that the top-level
 * `tools/ts-codegen/src/orchestrator.ts` calls — loads the seven
 * `contracts/geo/*.spec.json` Tier-2 files, validates them (catalog
 * uniqueness, vocabulary discipline), then emits both:
 *   - TYPE files into `@d2/geo-abstractions/src/generated/` (record shapes,
 *     branded code types, Zod schemas, closed-set validation tables); and
 *   - DATA files into `@d2/geo-default/src/generated/` (per-catalog
 *     `Record<Code, Entity>` + nested const-object hierarchies for
 *     Subdivisions / Locales / Timezones).
 *
 * Each emitter group fails fast on a missing catalog — the orchestrator
 * keeps emitting whichever catalogs ARE present so a partial-spec
 * environment can still type-check the generated files.
 */
export function runGeoEmit(force: boolean): readonly EmitDiagnostic[] {
  const specPaths = getGeoSpecPaths();
  const targetSummaryPath = tsPackagePath(
    "geo-abstractions",
    "src",
    "generated",
    "geo-catalog.g.ts",
  );

  // Up-to-date short-circuit — if the canonical "last file written" output
  // is newer than every spec, skip the work. `geo-catalog.g.ts` is always the
  // last file emitted so its mtime is the freshness signal for the batch.
  if (!force && isOutputUpToDate(targetSummaryPath, specPaths)) return [];

  const { context, diagnostics: loadDiagnostics } = loadGeoSpecs();
  const diagnostics: EmitDiagnostic[] = [...loadDiagnostics];

  // Vocabulary discipline — collect every PascalCase / camelCase field name
  // referenced across the spec shapes (the .NET-side validates field names
  // automatically; the TS side hand-rolls the field list since spec JSON
  // shape doesn't carry a separate identifier surface). The lists below are
  // the canonical spec field identifiers per the Plan §3 entity-shape table.
  const allFieldNames: readonly string[] = [
    // Country
    "iso31661Alpha2Code",
    "iso31661Alpha3Code",
    "iso31661NumericCode",
    "displayName",
    "officialName",
    "endonymDisplayName",
    "phoneNumberPrefix",
    "phoneNumberNationalFormat",
    "phoneNumberMinDigits",
    "phoneNumberMaxDigits",
    "firstDayOfWeek",
    "weekendStart",
    "weekendEnd",
    "measurementSystem",
    "primaryLanguageISO6391Code",
    "primaryCurrencyISO4217AlphaCode",
    "primaryLocaleIETFBCP47Tag",
    "sovereignCountryISO31661Alpha2Code",
    "geopoliticalEntityShortCodes",
    "subdivisionISO31662Codes",
    "timezoneIanaIdentifiers",
    "localeIETFBCP47Tags",
    "spokenLanguageISO6391Codes",
    "territoryISO31661Alpha2Codes",
    "currencies",
    // Subdivision
    "iso31662Code",
    "shortCode",
    "parentISO31662Code",
    "type",
    "order",
    // Currency
    "iso4217AlphaCode",
    "iso4217NumericCode",
    "decimalPlaces",
    "symbol",
    "isActive",
    "isSupported",
    // Language
    "iso6391Code",
    "name",
    "endonym",
    "writingDirection",
    "spokenInCountryISO31661Alpha2Codes",
    // Locale
    "ietfBcp47Tag",
    "languageISO6391Code",
    "countryISO31661Alpha2Code",
    "isSelectable",
    "decimalSeparator",
    "thousandsSeparator",
    "dateFormatPattern",
    // Timezone
    "ianaIdentifier",
    "currentStdOffsetMinutes",
    "currentDstOffsetMinutes",
    "currentStdAbbrev",
    "currentDstAbbrev",
    "coApplicableCountryISO31661Alpha2Codes",
    "aliases",
    // GeopoliticalEntity
    "countryISO31661Alpha2Codes",
  ];
  diagnostics.push(...validateVocabulary("geo-emitter", allFieldNames));

  // Catalog uniqueness gate — fail-closed on duplicate normalized names.
  diagnostics.push(...assertCatalogUniqueness(context));

  // Abort if any errors surfaced — never write partial / inconsistent output.
  if (diagnostics.some((d) => d.severity === "error")) return diagnostics;

  // Type-only outputs — record shapes + branded code types + fixed enums +
  // catalog metadata.
  const outputs: { readonly path: string; readonly source: string }[] = [];

  if (context.countries !== undefined)
    outputs.push(emitCountryEnum(context.countries.entries));
  if (context.currencies !== undefined)
    outputs.push(emitCurrencyEnum(context.currencies.entries));
  if (context.languages !== undefined)
    outputs.push(emitLanguageEnum(context.languages.entries));
  if (context.geopoliticalEntities !== undefined)
    outputs.push(
      emitGeopoliticalEntityEnum(context.geopoliticalEntities.entries),
    );

  outputs.push(emitFixedEnums());
  outputs.push(emitLocaleHelpersStub());

  if (context.subdivisions !== undefined)
    outputs.push(emitSubdivisionCode(context.subdivisions.entries));
  if (context.locales !== undefined)
    outputs.push(emitLocaleCode(context.locales.entries));
  if (context.timezones !== undefined)
    outputs.push(emitTimezoneCode(context.timezones.entries));

  // Record shapes — fixed surface; emitted whether or not the corresponding
  // catalog is populated, so consumers always have the types in scope.
  outputs.push(emitCountryRecords());
  outputs.push(emitSubdivisionRecords());
  outputs.push(emitCurrencyRecords());
  outputs.push(emitLanguageRecords());
  outputs.push(emitLocaleRecords());
  outputs.push(emitTimezoneRecords());
  outputs.push(emitGeopoliticalEntityRecords());

  // geo-catalog.g.ts ALWAYS written last among the type outputs — its
  // mtime gates the `isOutputUpToDate` short-circuit above.
  outputs.push(emitGeoCatalog(context));

  // Catalog DATA emission into @d2/geo-default/src/generated/. The
  // Default-target outputs go into a separate package directory, so they
  // don't share the up-to-date mtime gate (the geo-catalog.g.ts in
  // geo-abstractions is what the gate checks). Empty arrays are returned
  // when the relevant spec catalog isn't present.
  for (const { outputs: defaultOutputs } of [
    emitCountryData(context),
    emitCurrencyData(context),
    emitLanguageData(context),
    emitGeopoliticalEntityData(context),
    emitSubdivisionData(context),
    emitLocaleData(context),
    emitTimezoneData(context),
  ]) {
    for (const o of defaultOutputs) outputs.push(o);
  }

  // Coordinator (wire-nav driver) — emitted last in the data-emit batch
  // so consumers always see the coordinator after every catalog file
  // has materialized.
  for (const o of emitGeoDataInitializer().outputs) outputs.push(o);

  // Records-meta catalog — TS-side shape mirror for the cross-language
  // records parity test. Emits a single `_records-meta.g.ts` file that
  // mirrors the .NET-side `geo-records.fixture.json` shape.
  for (const o of emitRecordsMeta().outputs) outputs.push(o);

  for (const { path, source } of outputs) {
    writeGeneratedFile(path, source);
  }

  return diagnostics;
}
