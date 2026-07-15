// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { buildHeader } from "../../lib/file-emit.js";
import { StringBuilder } from "../../lib/string-builder.js";
import { appendEslintDisable } from "../emit-helpers.js";

import { defaultGenPath } from "./paths.js";

/**
 * Emits `geo-data-initializer.g.ts` — the coordinator module that drives
 * the wire-nav step of the two-pass populate pattern. Mirrors the .NET
 * `GeoDataInitializerEmitter` (which uses a `[ModuleInitializer]`
 * attribute). On the TS side we replicate this with a top-level call —
 * importing the coordinator module triggers the wire-nav sequence
 * exactly once (ESM modules are cached, so a second import is a no-op).
 *
 * Sequence (mirrors the .NET coordinator):
 *
 *   1. First pass — happens automatically as each sibling lookup module
 *      reaches its top-level first-pass block during the static import
 *      graph evaluation. No coordinator action needed because every
 *      first pass only touches its own catalog.
 *   2. Wire-nav step — runs in dependency order:
 *      - SubdivisionLookup.wireSubdivisionNav  (Country → Subdivision.country)
 *      - CountryLookup.wireCountryNav          (consumes Subdivision/Currency/Locale/Language)
 *      - LocaleLookup.wireLocaleNav            (depends on Country + Language first pass)
 *      - CurrencyLookup.wireCurrencyNav        (depends on Country.currencies)
 *      - LanguageLookup.wireLanguageNav        (depends on Country.primaryLanguage +
 *        Locale.language)
 *      - TimezoneLookup.wireTimezoneNav        (depends on Country)
 *      - GeopoliticalEntityLookup.wireGeopoliticalEntityNav  (depends on Country)
 *
 * The coordinator carries an `initializeGeoData()` function PLUS a
 * top-level call that runs it. Idempotent — the function checks an
 * internal flag and short-circuits on subsequent invocations.
 */
export function emitGeoDataInitializer(): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/*.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine('import { wireCountryNav } from "./countries.g.js";');
  sb.appendLine('import { wireCurrencyNav } from "./currencies.g.js";');
  sb.appendLine(
    'import { wireGeopoliticalEntityNav } from "./geopolitical-entities.g.js";',
  );
  sb.appendLine('import { wireLanguageNav } from "./languages.g.js";');
  sb.appendLine('import { wireLocaleNav } from "./locales.g.js";');
  sb.appendLine('import { wireSubdivisionNav } from "./subdivisions.g.js";');
  sb.appendLine('import { wireTimezoneNav } from "./timezones.g.js";');
  sb.appendLine();
  sb.appendLine("let _initialized = false;");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(" * Coordinator for the two-pass populate pattern.");
  sb.appendLine(" * Idempotent — short-circuits on subsequent calls via the");
  sb.appendLine(
    " * `_initialized` flag. Each catalog's first pass runs as a top-level",
  );
  sb.appendLine(
    " * block during this module's import-graph evaluation; the wire-nav",
  );
  sb.appendLine(" * step (this function) wires the cross-catalog nav refs.");
  sb.appendLine(" */");
  sb.appendLine("export function initializeGeoData(): void {");
  sb.increaseIndent();
  sb.appendLine("if (_initialized) return;");
  sb.appendLine("_initialized = true;");
  sb.appendLine();
  sb.appendLine("// Wire-nav step — wire nav refs in dependency order.");
  sb.appendLine("// SubdivisionLookup.byCountry must be populated before");
  sb.appendLine(
    "// CountryLookup.wireCountryNav consumes it for Country.subdivisions.",
  );
  sb.appendLine("wireSubdivisionNav();");
  sb.appendLine("wireCountryNav();");
  sb.appendLine("wireLocaleNav();");
  sb.appendLine("wireCurrencyNav();");
  sb.appendLine("wireLanguageNav();");
  sb.appendLine("wireTimezoneNav();");
  sb.appendLine("wireGeopoliticalEntityNav();");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
  sb.appendLine(
    "// Top-level call — importing this module guarantees the catalogs are",
  );
  sb.appendLine(
    "// fully wired before consumer code reads them. ESM module caching",
  );
  sb.appendLine("// makes repeat imports a no-op.");
  sb.appendLine("initializeGeoData();");

  return {
    outputs: [
      {
        path: defaultGenPath("geo-data-initializer.g.ts"),
        source: sb.toString(),
      },
    ],
  };
}
