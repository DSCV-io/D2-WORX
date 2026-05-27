// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { falsey, truthy } from "@d2/utilities";

import { buildHeader } from "../../lib/file-emit.js";
import { StringBuilder } from "../../lib/string-builder.js";
import { appendEslintDisable, escapeStringLiteral } from "../emit-helpers.js";
import type {
  CountrySpec,
  GeoSpecContext,
  LanguageSpec,
} from "../spec-types.js";

import { defaultGenPath } from "./paths.js";

/**
 * Emits the per-country DATA — single shape per entity + two-pass populate
 * + universal dual-rep code+nav fields. Mirrors the .NET
 * `CountryDataEmitter` field-for-field. Output is `countries.g.ts`:
 *
 *   - `CountryLookup` — `byCode` / `byIso31661Alpha2` / `byIso31661Alpha3`
 *     / `all` populated in the first pass (module-init).
 *   - `Countries.US`, `Countries.CA`, ... — getter accessors reading
 *     through `CountryLookup.byCode`.
 *   - `wireCountryNav()` — wire-nav step the coordinator invokes after
 *     every catalog's first pass has run; mutates nav refs via cast.
 *
 * First pass builds every `Country` record with scalars + every code-rep
 * field populated (typed codes — `CountryCode`, `LanguageCode`, etc., and
 * `Set<TCode>` for set FKs); nav-rep fields start as `undefined` for
 * nullable singles and `[]` for lists. Wire-nav step wires nav-rep refs
 * via one-time cast (TS `readonly` is compile-time only).
 *
 * Three sovereign-but-uninhabited territories (AQ Antarctica, BV Bouvet
 * Island, HM Heard & McDonald) carry `undefined` for primaryLanguage /
 * primaryCurrency / primaryLocale — per the workspace `undefined`-over-
 * `null` convention.
 */
export function emitCountryData(context: GeoSpecContext): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  if (context.countries === undefined) return { outputs: [] };

  const entries = [...context.countries.entries].sort((a, b) =>
    a.iso31661Alpha2Code.localeCompare(b.iso31661Alpha2Code),
  );

  const validLanguages = buildLanguageKeySet(context.languages?.entries);

  const validCurrencies = new Set<string>();
  if (context.currencies !== undefined) {
    for (const c of context.currencies.entries) {
      if (isIdentifier(c.iso4217AlphaCode))
        validCurrencies.add(c.iso4217AlphaCode);
    }
  }

  const validGpes = new Set<string>();
  if (context.geopoliticalEntities !== undefined) {
    for (const g of context.geopoliticalEntities.entries) {
      if (isIdentifier(g.shortCode)) validGpes.add(g.shortCode);
    }
  }

  return {
    outputs: [
      {
        path: defaultGenPath("countries.g.ts"),
        source: emitCountries(
          entries,
          validLanguages,
          validCurrencies,
          validGpes,
        ),
      },
    ],
  };
}

function emitCountries(
  entries: readonly CountrySpec[],
  validLanguages: ReadonlySet<string>,
  validCurrencies: ReadonlySet<string>,
  validGpes: ReadonlySet<string>,
): string {
  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/countries.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine(
    'import type { Country, CountryCurrencyAcceptance, GeopoliticalEntity, Locale, LocaleCode, Subdivision, SubdivisionCode } from "@d2/geo-abstractions";',
  );
  sb.appendLine(
    'import { CountryCode, CurrencyCode, GeopoliticalEntityCode, LanguageCode } from "@d2/geo-abstractions";',
  );
  sb.appendLine();
  sb.appendLine(
    "// Sibling-lookup imports — referenced inside wireCountryNav;",
  );
  sb.appendLine(
    "// the coordinator guarantees every first pass has completed before any",
  );
  sb.appendLine("// wireNav runs, so circular-import init order is safe.");
  sb.appendLine('import { CurrencyLookup } from "./currencies.g.js";');
  sb.appendLine(
    'import { GeopoliticalEntityLookup } from "./geopolitical-entities.g.js";',
  );
  sb.appendLine('import { LanguageLookup } from "./languages.g.js";');
  sb.appendLine('import { LocaleLookup } from "./locales.g.js";');
  sb.appendLine('import { SubdivisionLookup } from "./subdivisions.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * O(1) lookup tables over the country catalog. First pass (module-init)",
  );
  sb.appendLine(
    " * materializes every `Country` record with scalars + code-rep fields",
  );
  sb.appendLine(
    " * populated and nav refs at defaults (`undefined` / `[]`); wire-nav",
  );
  sb.appendLine(
    " * step (`wireCountryNav`) mutates nav refs via one-time cast (TS",
  );
  sb.appendLine(" * `readonly` is compile-time only). Invoked by the");
  sb.appendLine(
    " * `GeoDataInitializer` coordinator after every catalog's first pass.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const CountryLookup: {");
  sb.increaseIndent();
  sb.appendLine(
    "/** Country records keyed by ISO 3166-1 alpha-2 (CountryCode brand). */",
  );
  sb.appendLine("readonly byCode: Record<string, Country>;");
  sb.appendLine("readonly byIso31661Alpha2: Record<string, Country>;");
  sb.appendLine("readonly byIso31661Alpha3: Record<string, Country>;");
  sb.appendLine("all: readonly Country[];");
  sb.decreaseIndent();
  sb.appendLine("} = {");
  sb.increaseIndent();
  sb.appendLine("byCode: {} as Record<string, Country>,");
  sb.appendLine("byIso31661Alpha2: {} as Record<string, Country>,");
  sb.appendLine("byIso31661Alpha3: {} as Record<string, Country>,");
  sb.appendLine("all: [] as readonly Country[],");
  sb.decreaseIndent();
  sb.appendLine("};");
  sb.appendLine();

  // -------- First pass: build records (module-init top-level block) --------
  sb.appendLine(
    "// ---- First pass: construct every Country record with default/empty nav values. ----",
  );
  sb.appendLine("{");
  sb.increaseIndent();
  sb.appendLine("const byCode = CountryLookup.byCode;");
  sb.appendLine("const byAlpha2 = CountryLookup.byIso31661Alpha2;");
  sb.appendLine("const byAlpha3 = CountryLookup.byIso31661Alpha3;");
  sb.appendLine();
  for (const entry of entries)
    emitCountryRecordConstruction(
      sb,
      entry,
      validLanguages,
      validCurrencies,
      validGpes,
    );

  sb.appendLine();
  sb.appendLine("const all: Country[] = [];");
  for (const entry of entries)
    sb.appendLine(
      `all.push(byCode[CountryCode.${entry.iso31661Alpha2Code}]!);`,
    );

  sb.appendLine("CountryLookup.all = all;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  // -------- Countries accessor --------
  sb.appendLine("/**");
  sb.appendLine(
    " * Per-country `Country` accessors keyed by ISO 3166-1 alpha-2 — read",
  );
  sb.appendLine(
    " * through `CountryLookup.byCode` so consumers always observe a",
  );
  sb.appendLine(
    " * fully-materialized record. Nav refs are populated in the wire-nav",
  );
  sb.appendLine(" * step by `wireCountryNav`.");
  sb.appendLine(" */");
  sb.appendLine("export const Countries = {");
  sb.increaseIndent();
  for (const entry of entries) {
    const code = entry.iso31661Alpha2Code;
    sb.appendLine(`/** ${escapeJsDocText(entry.displayName)} (${code}). */`);
    sb.appendLine(
      `get ${code}(): Country { return CountryLookup.byCode[CountryCode.${code}]!; },`,
    );
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();

  // -------- Wire-nav step: wireCountryNav --------
  sb.appendLine("/**");
  sb.appendLine(" * Wire-nav step of the two-pass populate pattern.");
  sb.appendLine(
    " * Mutates the recorded `Country` nav properties via one-time cast.",
  );
  sb.appendLine(
    " * The cast is confined to this module-init code — consumer code",
  );
  sb.appendLine(
    " * must treat record fields as `readonly` (compile-time enforced).",
  );
  sb.appendLine(" * Invoked by the GeoDataInitializer coordinator.");
  sb.appendLine(" */");
  sb.appendLine("export function wireCountryNav(): void {");
  sb.increaseIndent();
  for (const entry of entries)
    emitCountryNavWire(sb, entry, validLanguages, validGpes);

  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return sb.toString();
}

function emitCountryRecordConstruction(
  sb: StringBuilder,
  entry: CountrySpec,
  validLanguages: ReadonlySet<string>,
  validCurrencies: ReadonlySet<string>,
  validGpes: ReadonlySet<string>,
): void {
  const c = entry.iso31661Alpha2Code;
  sb.appendLine(`byCode[CountryCode.${c}] = {`);
  sb.increaseIndent();
  sb.appendLine(`iso31661Alpha2Code: CountryCode.${c} as CountryCode,`);
  sb.appendLine(
    `iso31661Alpha3Code: "${escapeStringLiteral(entry.iso31661Alpha3Code)}",`,
  );
  sb.appendLine(
    `iso31661NumericCode: "${escapeStringLiteral(entry.iso31661NumericCode)}",`,
  );
  sb.appendLine(`displayName: "${escapeStringLiteral(entry.displayName)}",`);
  sb.appendLine(`officialName: "${escapeStringLiteral(entry.officialName)}",`);
  sb.appendLine(
    `endonymDisplayName: "${escapeStringLiteral(entry.endonymDisplayName ?? entry.displayName)}",`,
  );
  sb.appendLine(
    `endonymOfficialName: "${escapeStringLiteral(entry.endonymDisplayName ?? entry.officialName)}",`,
  );
  sb.appendLine(
    `phoneNumberPrefix: "${escapeStringLiteral(entry.phoneNumberPrefix ?? "")}",`,
  );
  sb.appendLine(
    `phoneNumberNationalFormat: "${escapeStringLiteral(entry.phoneNumberNationalFormat ?? "")}",`,
  );
  if (entry.phoneNumberMinDigits !== undefined)
    sb.appendLine(`phoneNumberMinDigits: ${entry.phoneNumberMinDigits},`);

  sb.appendLine(`phoneNumberMaxDigits: ${entry.phoneNumberMaxDigits ?? 0},`);

  // String-valued enums — literals are assignable to the type directly.
  sb.appendLine(
    `firstDayOfWeek: "${escapeStringLiteral(entry.firstDayOfWeek)}",`,
  );
  sb.appendLine(`weekendStart: "${escapeStringLiteral(entry.weekendStart)}",`);
  sb.appendLine(`weekendEnd: "${escapeStringLiteral(entry.weekendEnd)}",`);
  sb.appendLine(
    `measurementSystem: "${escapeStringLiteral(entry.measurementSystem)}",`,
  );

  // ---- Primary single FK code reps (init-only) ----
  const primLang = entry.primaryLanguageISO6391Code;
  if (
    primLang !== undefined &&
    validLanguages.has(primLang) &&
    isIdentifier(primLang)
  ) {
    sb.appendLine(
      `primaryLanguageIso6391Code: LanguageCode.${primLang} as LanguageCode,`,
    );
  }

  const primCur = entry.primaryCurrencyISO4217AlphaCode;
  if (primCur !== undefined && validCurrencies.has(primCur))
    sb.appendLine(
      `primaryCurrencyIso4217AlphaCode: CurrencyCode.${primCur} as CurrencyCode,`,
    );

  const primLoc = entry.primaryLocaleIETFBCP47Tag;
  if (truthy(primLoc)) {
    sb.appendLine(
      `primaryLocaleIetfBcp47Tag: "${escapeStringLiteral(primLoc!)}" as LocaleCode,`,
    );
  }

  const sov = entry.sovereignCountryISO31661Alpha2Code;
  if (sov !== undefined && isIdentifier(sov))
    sb.appendLine(
      `sovereignCountryIso31661Alpha2Code: CountryCode.${sov} as CountryCode,`,
    );

  // ---- Set FK code reps (Set<TCode>, init-only) ----
  emitCountrySet(
    sb,
    "territoryIso31661Alpha2Codes",
    entry.territoryISO31661Alpha2Codes,
  );
  emitLocaleSet(sb, "localeIetfBcp47Tags", entry.localeIETFBCP47Tags);
  emitGpeSet(
    sb,
    "geopoliticalEntityShortCodes",
    entry.geopoliticalEntityShortCodes,
    validGpes,
  );
  emitCurrencySet(
    sb,
    "currencyIso4217AlphaCodes",
    entry.currencies,
    validCurrencies,
  );

  // ---- Nested currency-acceptance list (required init) ----
  sb.appendLine("currencies: [");
  sb.increaseIndent();
  for (const cc of entry.currencies) {
    if (falsey(cc.iso4217AlphaCode)) continue;
    if (!validCurrencies.has(cc.iso4217AlphaCode)) continue;

    sb.appendLine("{");
    sb.increaseIndent();
    sb.appendLine(
      `iso4217AlphaCode: CurrencyCode.${cc.iso4217AlphaCode} as CurrencyCode,`,
    );
    sb.appendLine(
      `level: "${escapeStringLiteral(cc.level || "LegalTender")}",`,
    );
    sb.decreaseIndent();
    sb.appendLine("},");
  }
  sb.decreaseIndent();
  sb.appendLine("] as CountryCurrencyAcceptance[],");

  // ---- Nav refs + subdivision code set — wire-nav populates these ----
  sb.appendLine("subdivisionIso31662Codes: new Set<SubdivisionCode>(),");
  sb.appendLine("geopoliticalEntities: [] as readonly GeopoliticalEntity[],");
  sb.appendLine("subdivisions: [] as readonly Subdivision[],");
  sb.appendLine("locales: [] as readonly Locale[],");
  sb.appendLine("territories: [] as readonly Country[],");
  sb.decreaseIndent();
  sb.appendLine("};");

  sb.appendLine(
    `byAlpha2["${escapeStringLiteral(entry.iso31661Alpha2Code)}"] = byCode[CountryCode.${c}]!;`,
  );
  if (truthy(entry.iso31661Alpha3Code)) {
    sb.appendLine(
      `byAlpha3["${escapeStringLiteral(entry.iso31661Alpha3Code)}"] = byCode[CountryCode.${c}]!;`,
    );
  }
}

function emitCountrySet(
  sb: StringBuilder,
  fieldName: string,
  codes: readonly string[] | undefined,
): void {
  const list = (codes ?? []).filter((s) => truthy(s) && isIdentifier(s));
  if (falsey(list)) {
    sb.appendLine(`${fieldName}: new Set<CountryCode>(),`);
    return;
  }

  sb.appendLine(`${fieldName}: new Set<CountryCode>([`);
  sb.increaseIndent();
  for (const c of list) sb.appendLine(`CountryCode.${c} as CountryCode,`);

  sb.decreaseIndent();
  sb.appendLine("]),");
}

function emitLocaleSet(
  sb: StringBuilder,
  fieldName: string,
  tags: readonly string[] | undefined,
): void {
  const list = (tags ?? []).filter((s) => truthy(s));
  if (falsey(list)) {
    sb.appendLine(`${fieldName}: new Set<LocaleCode>(),`);
    return;
  }

  sb.appendLine(`${fieldName}: new Set<LocaleCode>([`);
  sb.increaseIndent();
  for (const t of list)
    sb.appendLine(`"${escapeStringLiteral(t)}" as LocaleCode,`);

  sb.decreaseIndent();
  sb.appendLine("]),");
}

function emitGpeSet(
  sb: StringBuilder,
  fieldName: string,
  codes: readonly string[] | undefined,
  validGpes: ReadonlySet<string>,
): void {
  const list = (codes ?? []).filter((s) => truthy(s) && validGpes.has(s));
  if (falsey(list)) {
    sb.appendLine(`${fieldName}: new Set<GeopoliticalEntityCode>(),`);
    return;
  }

  sb.appendLine(`${fieldName}: new Set<GeopoliticalEntityCode>([`);
  sb.increaseIndent();
  for (const g of list)
    sb.appendLine(`GeopoliticalEntityCode.${g} as GeopoliticalEntityCode,`);

  sb.decreaseIndent();
  sb.appendLine("]),");
}

function emitCurrencySet(
  sb: StringBuilder,
  fieldName: string,
  acceptance: readonly { readonly iso4217AlphaCode: string }[],
  validCurrencies: ReadonlySet<string>,
): void {
  const seen = new Set<string>();
  const list: string[] = [];
  for (const cc of acceptance) {
    if (falsey(cc.iso4217AlphaCode)) continue;
    if (!validCurrencies.has(cc.iso4217AlphaCode)) continue;

    if (seen.has(cc.iso4217AlphaCode)) continue;

    seen.add(cc.iso4217AlphaCode);
    list.push(cc.iso4217AlphaCode);
  }

  if (falsey(list)) {
    sb.appendLine(`${fieldName}: new Set<CurrencyCode>(),`);
    return;
  }

  sb.appendLine(`${fieldName}: new Set<CurrencyCode>([`);
  sb.increaseIndent();
  for (const c of list) sb.appendLine(`CurrencyCode.${c} as CurrencyCode,`);

  sb.decreaseIndent();
  sb.appendLine("]),");
}

function emitCountryNavWire(
  sb: StringBuilder,
  entry: CountrySpec,
  validLanguages: ReadonlySet<string>,
  validGpes: ReadonlySet<string>,
): void {
  const c = entry.iso31661Alpha2Code;
  sb.appendLine(`// ${c}`);
  sb.appendLine("{");
  sb.increaseIndent();
  sb.appendLine(`const rec = CountryLookup.byCode[CountryCode.${c}]!;`);
  sb.appendLine(
    "const mut = rec as { -readonly [K in keyof Country]: Country[K] };",
  );

  const primLang = entry.primaryLanguageISO6391Code;
  if (
    primLang !== undefined &&
    validLanguages.has(primLang) &&
    isIdentifier(primLang)
  ) {
    sb.appendLine(
      `mut.primaryLanguage = LanguageLookup.byCode[LanguageCode.${primLang}];`,
    );
  }

  const primCur = entry.primaryCurrencyISO4217AlphaCode;
  if (primCur !== undefined && isIdentifier(primCur)) {
    sb.appendLine(
      `mut.primaryCurrency = CurrencyLookup.byCode[CurrencyCode.${primCur}];`,
    );
  }

  const primLoc = entry.primaryLocaleIETFBCP47Tag;
  if (truthy(primLoc)) {
    sb.appendLine(
      `mut.primaryLocale = LocaleLookup.byTag["${escapeStringLiteral(primLoc!)}"];`,
    );
  }

  const sov = entry.sovereignCountryISO31661Alpha2Code;
  if (sov !== undefined && isIdentifier(sov)) {
    sb.appendLine(
      `mut.sovereignCountry = CountryLookup.byCode[CountryCode.${sov}];`,
    );
  }

  const localeTags = entry.localeIETFBCP47Tags.filter((t) => truthy(t));
  if (truthy(localeTags)) {
    sb.appendLine("mut.locales = [");
    sb.increaseIndent();
    for (const tag of localeTags)
      sb.appendLine(`LocaleLookup.byTag["${escapeStringLiteral(tag)}"]!,`);

    sb.decreaseIndent();
    sb.appendLine("];");
  }

  const terrs = entry.territoryISO31661Alpha2Codes.filter(
    (t) => truthy(t) && isIdentifier(t),
  );
  if (truthy(terrs)) {
    sb.appendLine("mut.territories = [");
    sb.increaseIndent();
    for (const t of terrs)
      sb.appendLine(`CountryLookup.byCode[CountryCode.${t}]!,`);

    sb.decreaseIndent();
    sb.appendLine("];");
  }

  const gpes = entry.geopoliticalEntityShortCodes.filter(
    (g) => truthy(g) && validGpes.has(g),
  );
  if (truthy(gpes)) {
    sb.appendLine("mut.geopoliticalEntities = [");
    sb.increaseIndent();
    for (const g of gpes) {
      sb.appendLine(
        `GeopoliticalEntityLookup.byCode[GeopoliticalEntityCode.${g}]!,`,
      );
    }

    sb.decreaseIndent();
    sb.appendLine("];");
  }

  sb.appendLine(
    `{ const subs = SubdivisionLookup.byCountry["${c}"]; if (subs !== undefined) { mut.subdivisions = subs; const codeSet = new Set<SubdivisionCode>(); for (const s of subs) codeSet.add(s.iso31662Code); mut.subdivisionIso31662Codes = codeSet; } }`,
  );

  // Wire nested Currency nav on each acceptance entry.
  if (truthy(entry.currencies)) {
    sb.appendLine("for (const cc of mut.currencies) {");
    sb.increaseIndent();
    sb.appendLine(
      "(cc as { -readonly [K in keyof CountryCurrencyAcceptance]: CountryCurrencyAcceptance[K] }).currency = CurrencyLookup.byCode[cc.iso4217AlphaCode];",
    );
    sb.decreaseIndent();
    sb.appendLine("}");
  }

  sb.decreaseIndent();
  sb.appendLine("}");
}

function escapeJsDocText(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

function isIdentifier(s: string): boolean {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(s);
}

function buildLanguageKeySet(
  langs: readonly LanguageSpec[] | undefined,
): ReadonlySet<string> {
  const s = new Set<string>();
  if (langs === undefined) return s;

  for (const l of langs) s.add(l.iso6391Code);

  return s;
}
