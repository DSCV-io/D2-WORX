// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { truthy } from "@d2/utilities";

import { buildHeader } from "../../lib/file-emit.js";
import { StringBuilder } from "../../lib/string-builder.js";
import {
  appendEslintDisable,
  escapeStringLiteral,
  safeKey,
} from "../emit-helpers.js";
import type { GeoSpecContext, LocaleSpec } from "../spec-types.js";

import { defaultGenPath } from "./paths.js";

/**
 * Emits the per-locale DATA. Output: `locales.g.ts`.
 *
 *   - `LocaleLookup` — `byCode` Record indexed by LocaleCode (branded
 *     string); `byTag` Record indexed by raw IETF BCP 47 tag string for
 *     ergonomic wire-nav lookup from raw spec data; `all` list.
 *   - `Locales.en.US` etc. — nested const-object hierarchy returning
 *     the typed `LocaleCode` branded string at the leaf.
 *   - `wireLocaleNav()` — wire-nav step wires `language` + `country`
 *     nav refs.
 */
export function emitLocaleData(context: GeoSpecContext): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  if (context.locales === undefined) return { outputs: [] };

  const entries = [...context.locales.entries].sort((a, b) =>
    a.ietfBcp47Tag.localeCompare(b.ietfBcp47Tag),
  );

  // Build the canonical Language-enum key set from the languages catalog
  // so post-init Language refs skip codes that aren't members of the enum.
  const validLanguages = new Set<string>();
  if (context.languages !== undefined) {
    for (const l of context.languages.entries)
      validLanguages.add(l.iso6391Code);
  }

  // Build the valid-country member set.
  const validCountries = new Set<string>();
  if (context.countries !== undefined) {
    for (const c of context.countries.entries) {
      if (truthy(c.iso31661Alpha2Code) && isIdentifier(c.iso31661Alpha2Code))
        validCountries.add(c.iso31661Alpha2Code);
    }
  }

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/locales.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine(
    'import type { Locale, LocaleCode } from "@d2/geo-abstractions";',
  );
  sb.appendLine(
    'import { CountryCode, LanguageCode } from "@d2/geo-abstractions";',
  );
  sb.appendLine();
  sb.appendLine('import { CountryLookup } from "./countries.g.js";');
  sb.appendLine('import { LanguageLookup } from "./languages.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * O(1) lookup over the locale catalog. First pass fills `byCode` /",
  );
  sb.appendLine(
    " * `byTag` / `all`; wire-nav step (`wireLocaleNav`) wires `language` +",
  );
  sb.appendLine(" * `country` nav refs.");
  sb.appendLine(" */");
  sb.appendLine("export const LocaleLookup: {");
  sb.increaseIndent();
  sb.appendLine("readonly byCode: Record<string, Locale>;");
  sb.appendLine("readonly byTag: Record<string, Locale>;");
  sb.appendLine("all: readonly Locale[];");
  sb.decreaseIndent();
  sb.appendLine("} = {");
  sb.increaseIndent();
  sb.appendLine("byCode: {} as Record<string, Locale>,");
  sb.appendLine("byTag: {} as Record<string, Locale>,");
  sb.appendLine("all: [] as readonly Locale[],");
  sb.decreaseIndent();
  sb.appendLine("};");
  sb.appendLine();

  // First pass
  sb.appendLine(
    "// ---- First pass: construct every Locale record (nav refs undefined). ----",
  );
  sb.appendLine("{");
  sb.increaseIndent();
  sb.appendLine("const byCode = LocaleLookup.byCode;");
  sb.appendLine("const byTag = LocaleLookup.byTag;");
  sb.appendLine();
  for (const entry of entries) {
    sb.appendLine(`byCode["${escapeStringLiteral(entry.ietfBcp47Tag)}"] = {`);
    sb.increaseIndent();
    sb.appendLine(
      `ietfBcp47Tag: "${escapeStringLiteral(entry.ietfBcp47Tag)}" as LocaleCode,`,
    );
    sb.appendLine(`displayName: "${escapeStringLiteral(entry.name ?? "")}",`);
    sb.appendLine(
      `endonym: "${escapeStringLiteral(entry.endonym ?? entry.name ?? "")}",`,
    );

    // Code reps — init only.
    if (
      validLanguages.has(entry.languageISO6391Code) &&
      isIdentifier(entry.languageISO6391Code)
    ) {
      sb.appendLine(
        `languageIso6391Code: LanguageCode.${entry.languageISO6391Code} as LanguageCode,`,
      );
    }

    if (
      entry.countryISO31661Alpha2Code !== undefined &&
      validCountries.has(entry.countryISO31661Alpha2Code)
    ) {
      sb.appendLine(
        `countryIso31661Alpha2Code: CountryCode.${entry.countryISO31661Alpha2Code} as CountryCode,`,
      );
    }

    sb.appendLine(`isSelectable: ${entry.isSelectable ? "true" : "false"},`);
    sb.appendLine(
      `firstDayOfWeek: "${escapeStringLiteral(entry.firstDayOfWeek ?? "Monday")}",`,
    );
    sb.appendLine(
      `decimalSeparator: "${escapeStringLiteral(entry.decimalSeparator ?? ".")}",`,
    );
    sb.appendLine(
      `thousandsSeparator: "${escapeStringLiteral(entry.thousandsSeparator ?? "")}",`,
    );
    sb.appendLine(
      `dateFormatPattern: "${escapeStringLiteral(entry.dateFormatPattern ?? "YMD")}",`,
    );
    sb.decreaseIndent();
    sb.appendLine("};");
    sb.appendLine(
      `byTag["${escapeStringLiteral(entry.ietfBcp47Tag)}"] = byCode["${escapeStringLiteral(entry.ietfBcp47Tag)}"]!;`,
    );
  }
  sb.appendLine();
  sb.appendLine("const all: Locale[] = [];");
  for (const entry of entries)
    sb.appendLine(
      `all.push(byCode["${escapeStringLiteral(entry.ietfBcp47Tag)}"]!);`,
    );

  sb.appendLine("LocaleLookup.all = all;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  // Locales nested accessor
  emitNestedLocaleAccessor(sb, entries);
  sb.appendLine();

  // Wire-nav
  sb.appendLine("/**");
  sb.appendLine(
    " * Wire-nav step — wires `language` + `country` nav refs on every Locale.",
  );
  sb.appendLine(
    " * `language` left `undefined` when locale references a 3-letter code",
  );
  sb.appendLine(" * outside the LanguageCode enum.");
  sb.appendLine(" */");
  sb.appendLine("export function wireLocaleNav(): void {");
  sb.increaseIndent();
  sb.appendLine("for (const locale of LocaleLookup.all) {");
  sb.increaseIndent();
  sb.appendLine(
    "const mut = locale as { -readonly [K in keyof Locale]: Locale[K] };",
  );
  sb.appendLine("if (locale.languageIso6391Code !== undefined) {");
  sb.increaseIndent();
  sb.appendLine(
    "const lang = LanguageLookup.byCode[locale.languageIso6391Code];",
  );
  sb.appendLine("if (lang !== undefined) mut.language = lang;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine("if (locale.countryIso31661Alpha2Code !== undefined) {");
  sb.increaseIndent();
  sb.appendLine(
    "const country = CountryLookup.byCode[locale.countryIso31661Alpha2Code];",
  );
  sb.appendLine("if (country !== undefined) mut.country = country;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");

  return {
    outputs: [{ path: defaultGenPath("locales.g.ts"), source: sb.toString() }],
  };
}

interface TrieNode {
  segment?: string;
  leafTag?: string;
  children: Map<string, TrieNode>;
}

function emitNestedLocaleAccessor(
  sb: StringBuilder,
  entries: readonly LocaleSpec[],
): void {
  // First pass — record all strict prefixes.
  const prefixes = new Set<string>();
  for (const entry of entries) {
    const segs = entry.ietfBcp47Tag.split("-");
    for (let i = 1; i < segs.length; i++)
      prefixes.add(segs.slice(0, i).join("-"));
  }

  // Second pass — insert leaves into trie, skipping language-only tags
  // and prefix-collision tags.
  const root: TrieNode = { children: new Map() };
  for (const entry of entries) {
    const tag = entry.ietfBcp47Tag;
    const segs = tag.split("-");
    if (segs.length < 2) continue;

    if (prefixes.has(tag)) continue;

    let node = root;
    for (let i = 0; i < segs.length; i++) {
      const seg = segs[i]!;
      let child = node.children.get(seg);
      if (child === undefined) {
        child = { segment: seg, children: new Map() };
        node.children.set(seg, child);
      }
      node = child;
      if (i === segs.length - 1) node.leafTag = tag;
    }
  }

  sb.appendLine("/**");
  sb.appendLine(
    " * Nested accessor — `Locales.en.US` returns the typed `LocaleCode`",
  );
  sb.appendLine(
    ' * branded string `"en-US"`. 1-segment tags and prefix-collision tags',
  );
  sb.appendLine(" * are skipped; access them via `LocaleLookup.byTag`.");
  sb.appendLine(" */");
  sb.appendLine("export const Locales = {");
  sb.increaseIndent();
  emitTrieChildren(sb, root);
  sb.decreaseIndent();
  sb.appendLine("} as const;");
}

function emitTrieChildren(sb: StringBuilder, node: TrieNode): void {
  const keys = [...node.children.keys()].sort();
  for (const key of keys) {
    const child = node.children.get(key)!;
    emitTrieNode(sb, child);
  }
}

function emitTrieNode(sb: StringBuilder, node: TrieNode): void {
  const seg = node.segment ?? "";
  if (node.children.size === 0 && node.leafTag !== undefined) {
    sb.appendLine(
      `${safeKey(seg)}: "${escapeStringLiteral(node.leafTag)}" as import("@d2/geo-abstractions").LocaleCode,`,
    );
    return;
  }

  sb.appendLine(`${safeKey(seg)}: {`);
  sb.increaseIndent();
  emitTrieChildren(sb, node);
  sb.decreaseIndent();
  sb.appendLine("},");
}

function isIdentifier(s: string): boolean {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(s);
}
