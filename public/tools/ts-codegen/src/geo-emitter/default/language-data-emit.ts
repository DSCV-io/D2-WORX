// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { buildHeader } from "../../lib/file-emit.js";
import { StringBuilder } from "../../lib/string-builder.js";
import { appendEslintDisable, escapeStringLiteral } from "../emit-helpers.js";
import type { GeoSpecContext, LanguageSpec } from "../spec-types.js";

import { defaultGenPath } from "./paths.js";

/**
 * Emits `languages.g.ts` — the per-language DATA:
 *
 *   - `LanguageLookup` — `byCode` Record indexed by `LanguageCode`, `all`
 *     list. Filled in the first pass.
 *   - `Languages.en`, `Languages.fr`, ... — getter accessors reading
 *     through `LanguageLookup.byCode`.
 *   - `wireLanguageNav()` — wire-nav step. Mutates each Language's
 *     `spokenInCountries` + `locales` nav refs + paired typed code sets.
 */
export function emitLanguageData(context: GeoSpecContext): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  if (context.languages === undefined) return { outputs: [] };

  const entries = [...context.languages.entries].sort((a, b) =>
    a.iso6391Code.localeCompare(b.iso6391Code),
  );

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/languages.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine(
    'import type { Country, Language, Locale, LocaleCode } from "@dcsv-io/d2-geo-abstractions";',
  );
  sb.appendLine(
    'import { CountryCode, LanguageCode } from "@dcsv-io/d2-geo-abstractions";',
  );
  sb.appendLine();
  sb.appendLine(
    "// Sibling-lookup imports — referenced inside wireLanguageNav.",
  );
  sb.appendLine('import { CountryLookup } from "./countries.g.js";');
  sb.appendLine('import { LocaleLookup } from "./locales.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * O(1) lookup over the language catalog. First pass fills `byCode` /",
  );
  sb.appendLine(
    " * `all`; wire-nav step wires `spokenInCountries` / `locales` reverse",
  );
  sb.appendLine(" * navs + paired typed code sets.");
  sb.appendLine(" */");
  sb.appendLine("export const LanguageLookup: {");
  sb.increaseIndent();
  sb.appendLine(
    "/** Language records keyed by ISO 639-1 (LanguageCode brand). */",
  );
  sb.appendLine("readonly byCode: Record<string, Language>;");
  sb.appendLine("all: readonly Language[];");
  sb.decreaseIndent();
  sb.appendLine("} = {");
  sb.increaseIndent();
  sb.appendLine("byCode: {} as Record<string, Language>,");
  sb.appendLine("all: [] as readonly Language[],");
  sb.decreaseIndent();
  sb.appendLine("};");
  sb.appendLine();

  // First pass
  sb.appendLine(
    "// ---- First pass: construct every Language record with empty reverse navs. ----",
  );
  sb.appendLine("{");
  sb.increaseIndent();
  sb.appendLine("const byCode = LanguageLookup.byCode;");
  sb.appendLine();
  const seen = new Set<string>();
  const emitted: LanguageSpec[] = [];
  for (const entry of entries) {
    const code = entry.iso6391Code;
    if (!isIdentifier(code) || seen.has(code)) continue;

    seen.add(code);
    emitted.push(entry);
    sb.appendLine(`byCode[LanguageCode.${code}] = {`);
    sb.increaseIndent();
    sb.appendLine(`iso6391Code: LanguageCode.${code} as LanguageCode,`);
    sb.appendLine(`displayName: "${escapeStringLiteral(entry.name ?? "")}",`);
    sb.appendLine(
      `endonym: "${escapeStringLiteral(entry.endonym ?? entry.name ?? "")}",`,
    );
    sb.appendLine(
      `writingDirection: "${escapeStringLiteral(entry.writingDirection ?? "LTR")}",`,
    );
    sb.appendLine(`isSupported: ${entry.isSupported ? "true" : "false"},`);
    sb.appendLine(
      "spokenInCountryIso31661Alpha2Codes: new Set<CountryCode>(),",
    );
    sb.appendLine("spokenInCountries: [] as readonly Country[],");
    sb.appendLine("localeIetfBcp47Tags: new Set<LocaleCode>(),");
    sb.appendLine("locales: [] as readonly Locale[],");
    sb.decreaseIndent();
    sb.appendLine("};");
  }
  sb.appendLine();
  sb.appendLine("const all: Language[] = [];");
  for (const entry of emitted)
    sb.appendLine(`all.push(byCode[LanguageCode.${entry.iso6391Code}]!);`);

  sb.appendLine("LanguageLookup.all = all;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  // Languages accessor
  sb.appendLine("/**");
  sb.appendLine(" * Per-language accessors (`Languages.en` etc.).");
  sb.appendLine(" */");
  sb.appendLine("export const Languages = {");
  sb.increaseIndent();
  for (const entry of emitted) {
    const code = entry.iso6391Code;
    sb.appendLine(
      `get ${code}(): Language { return LanguageLookup.byCode[LanguageCode.${code}]!; },`,
    );
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();

  // Wire-nav
  sb.appendLine("/**");
  sb.appendLine(
    " * Wire-nav step — back-fills `spokenInCountries` (via Country.primaryLanguage)",
  );
  sb.appendLine(
    " * + `locales` (via Locale.language) and paired typed code sets.",
  );
  sb.appendLine(" */");
  sb.appendLine("export function wireLanguageNav(): void {");
  sb.increaseIndent();
  sb.appendLine("const spokenInByLang = new Map<LanguageCode, Country[]>();");
  sb.appendLine("for (const country of CountryLookup.all) {");
  sb.increaseIndent();
  sb.appendLine("const lang = country.primaryLanguage;");
  sb.appendLine("if (lang === undefined) continue;");
  sb.appendLine("let list = spokenInByLang.get(lang.iso6391Code);");
  sb.appendLine(
    "if (list === undefined) { list = []; spokenInByLang.set(lang.iso6391Code, list); }",
  );
  sb.appendLine("list.push(country);");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine("for (const [code, list] of spokenInByLang) {");
  sb.increaseIndent();
  sb.appendLine("const rec = LanguageLookup.byCode[code];");
  sb.appendLine("if (rec === undefined) continue;");
  sb.appendLine(
    "const mut = rec as { -readonly [K in keyof Language]: Language[K] };",
  );
  sb.appendLine("mut.spokenInCountries = list;");
  sb.appendLine("const codeSet = new Set<CountryCode>();");
  sb.appendLine("for (const c of list) codeSet.add(c.iso31661Alpha2Code);");
  sb.appendLine("mut.spokenInCountryIso31661Alpha2Codes = codeSet;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
  sb.appendLine("const localesByLang = new Map<LanguageCode, Locale[]>();");
  sb.appendLine("for (const locale of LocaleLookup.all) {");
  sb.increaseIndent();
  sb.appendLine("const lang = locale.language;");
  sb.appendLine("if (lang === undefined) continue;");
  sb.appendLine("let list = localesByLang.get(lang.iso6391Code);");
  sb.appendLine(
    "if (list === undefined) { list = []; localesByLang.set(lang.iso6391Code, list); }",
  );
  sb.appendLine("list.push(locale);");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine("for (const [code, list] of localesByLang) {");
  sb.increaseIndent();
  sb.appendLine("const rec = LanguageLookup.byCode[code];");
  sb.appendLine("if (rec === undefined) continue;");
  sb.appendLine(
    "const mut = rec as { -readonly [K in keyof Language]: Language[K] };",
  );
  sb.appendLine("mut.locales = list;");
  sb.appendLine("const codeSet = new Set<LocaleCode>();");
  sb.appendLine("for (const l of list) codeSet.add(l.ietfBcp47Tag);");
  sb.appendLine("mut.localeIetfBcp47Tags = codeSet;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");

  return {
    outputs: [
      { path: defaultGenPath("languages.g.ts"), source: sb.toString() },
    ],
  };
}

function isIdentifier(s: string): boolean {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(s);
}
