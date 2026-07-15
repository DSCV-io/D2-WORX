// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { buildHeader } from "../../lib/file-emit.js";
import { StringBuilder } from "../../lib/string-builder.js";
import { appendEslintDisable, escapeStringLiteral } from "../emit-helpers.js";
import type { CurrencySpec, GeoSpecContext } from "../spec-types.js";

import { defaultGenPath } from "./paths.js";

/**
 * Emits `currencies.g.ts` — the per-currency DATA:
 *
 *   - `CurrencyLookup` — `byCode` Record indexed by `CurrencyCode`, `all`
 *     list. Filled in the first pass (module-init).
 *   - `Currencies.USD`, `Currencies.EUR`, ... — getter accessors reading
 *     through `CurrencyLookup.byCode`.
 *   - `wireCurrencyNav()` — wire-nav step. Mutates each Currency's
 *     `acceptedInCountries` + `acceptedInCountryIso31661Alpha2Codes`
 *     navs via cast.
 */
export function emitCurrencyData(context: GeoSpecContext): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  if (context.currencies === undefined) return { outputs: [] };

  const entries = [...context.currencies.entries].sort((a, b) =>
    a.iso4217AlphaCode.localeCompare(b.iso4217AlphaCode),
  );

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/currencies.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine(
    'import type { Country, Currency } from "@d2/geo-abstractions";',
  );
  sb.appendLine(
    'import { CountryCode, CurrencyCode } from "@d2/geo-abstractions";',
  );
  sb.appendLine();
  sb.appendLine(
    "// Sibling-lookup import — referenced inside wireCurrencyNav;",
  );
  sb.appendLine(
    "// safe under ESM cyclic-import rules because the wire-nav step runs",
  );
  sb.appendLine("// after all first-pass module inits complete.");
  sb.appendLine('import { CountryLookup } from "./countries.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * O(1) lookup over the currency catalog. First pass fills `byCode` /",
  );
  sb.appendLine(
    " * `all`; wire-nav step (`wireCurrencyNav`) wires `acceptedInCountries`",
  );
  sb.appendLine(" * + `acceptedInCountryIso31661Alpha2Codes`.");
  sb.appendLine(" */");
  sb.appendLine("export const CurrencyLookup: {");
  sb.increaseIndent();
  sb.appendLine(
    "/** Currency records keyed by ISO 4217 alpha (CurrencyCode brand). */",
  );
  sb.appendLine("readonly byCode: Record<string, Currency>;");
  sb.appendLine("all: readonly Currency[];");
  sb.decreaseIndent();
  sb.appendLine("} = {");
  sb.increaseIndent();
  sb.appendLine("byCode: {} as Record<string, Currency>,");
  sb.appendLine("all: [] as readonly Currency[],");
  sb.decreaseIndent();
  sb.appendLine("};");
  sb.appendLine();

  // -------- First pass: construct records --------
  sb.appendLine(
    "// ---- First pass: construct every Currency record with empty reverse navs. ----",
  );
  sb.appendLine("{");
  sb.increaseIndent();
  sb.appendLine("const byCode = CurrencyLookup.byCode;");
  sb.appendLine();
  const seen = new Set<string>();
  const emitted: CurrencySpec[] = [];
  for (const entry of entries) {
    const code = entry.iso4217AlphaCode;
    if (!isIdentifier(code) || seen.has(code)) continue;

    seen.add(code);
    emitted.push(entry);
    sb.appendLine(`byCode[CurrencyCode.${code}] = {`);
    sb.increaseIndent();
    sb.appendLine(`iso4217AlphaCode: CurrencyCode.${code} as CurrencyCode,`);
    sb.appendLine(
      `iso4217NumericCode: "${escapeStringLiteral(entry.iso4217NumericCode ?? "")}",`,
    );
    sb.appendLine(
      `displayName: "${escapeStringLiteral(entry.displayName ?? "")}",`,
    );
    sb.appendLine(
      `officialName: "${escapeStringLiteral(entry.displayName ?? "")}",`,
    );
    sb.appendLine(`decimalPlaces: ${entry.decimalPlaces ?? 0},`);
    sb.appendLine(`symbol: "${escapeStringLiteral(entry.symbol ?? "")}",`);
    sb.appendLine(`isSupported: ${entry.isSupported ? "true" : "false"},`);
    sb.appendLine(
      "acceptedInCountryIso31661Alpha2Codes: new Set<CountryCode>(),",
    );
    sb.appendLine("acceptedInCountries: [] as readonly Country[],");
    sb.decreaseIndent();
    sb.appendLine("};");
  }
  sb.appendLine();
  sb.appendLine("const all: Currency[] = [];");
  for (const entry of emitted)
    sb.appendLine(`all.push(byCode[CurrencyCode.${entry.iso4217AlphaCode}]!);`);

  sb.appendLine("CurrencyLookup.all = all;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  // -------- Currencies accessor --------
  sb.appendLine("/**");
  sb.appendLine(
    " * Per-currency accessors (`Currencies.USD` etc.) reading through",
  );
  sb.appendLine(" * `CurrencyLookup.byCode`.");
  sb.appendLine(" */");
  sb.appendLine("export const Currencies = {");
  sb.increaseIndent();
  for (const entry of emitted) {
    const code = entry.iso4217AlphaCode;
    sb.appendLine(
      `get ${code}(): Currency { return CurrencyLookup.byCode[CurrencyCode.${code}]!; },`,
    );
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();

  // -------- Wire-nav step: wireCurrencyNav --------
  sb.appendLine("/**");
  sb.appendLine(
    " * Wire-nav step of the two-pass populate pattern. Walks every Country's",
  );
  sb.appendLine(
    " * `currencies` list and back-fills each Currency's `acceptedInCountries`",
  );
  sb.appendLine(" * + `acceptedInCountryIso31661Alpha2Codes` reverse navs.");
  sb.appendLine(" */");
  sb.appendLine("export function wireCurrencyNav(): void {");
  sb.increaseIndent();
  sb.appendLine("const accumulator = new Map<CurrencyCode, Country[]>();");
  sb.appendLine("for (const country of CountryLookup.all) {");
  sb.increaseIndent();
  sb.appendLine("for (const cc of country.currencies) {");
  sb.increaseIndent();
  sb.appendLine("let list = accumulator.get(cc.iso4217AlphaCode);");
  sb.appendLine(
    "if (list === undefined) { list = []; accumulator.set(cc.iso4217AlphaCode, list); }",
  );
  sb.appendLine("list.push(country);");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine("for (const [code, countries] of accumulator) {");
  sb.increaseIndent();
  sb.appendLine("const rec = CurrencyLookup.byCode[code];");
  sb.appendLine("if (rec === undefined) continue;");
  sb.appendLine(
    "const mut = rec as { -readonly [K in keyof Currency]: Currency[K] };",
  );
  sb.appendLine("mut.acceptedInCountries = countries;");
  sb.appendLine("const codeSet = new Set<CountryCode>();");
  sb.appendLine(
    "for (const c of countries) codeSet.add(c.iso31661Alpha2Code);",
  );
  sb.appendLine("mut.acceptedInCountryIso31661Alpha2Codes = codeSet;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return {
    outputs: [
      { path: defaultGenPath("currencies.g.ts"), source: sb.toString() },
    ],
  };
}

function isIdentifier(s: string): boolean {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(s);
}
