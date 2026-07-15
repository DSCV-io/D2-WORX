// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey, truthy } from "@dcsv-io/d2-utilities";

import { buildHeader } from "../../lib/file-emit.js";
import { StringBuilder } from "../../lib/string-builder.js";
import {
  appendEslintDisable,
  escapeStringLiteral,
  safeKey,
} from "../emit-helpers.js";
import type { GeoSpecContext, TimezoneSpec } from "../spec-types.js";

import { defaultGenPath } from "./paths.js";

/**
 * Emits the per-timezone DATA. Output: `timezones.g.ts`.
 *
 *   - `TimezoneLookup` — `byCode` Record indexed by IANA-id string;
 *     `all` list. Filled in the first pass.
 *   - `Timezones.America.New_York` etc. — nested const-object hierarchy.
 *   - `wireTimezoneNav()` — wire-nav step wires `primaryCountry` +
 *     `coApplicableCountries` nav refs on each.
 */
export function emitTimezoneData(context: GeoSpecContext): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  if (context.timezones === undefined) return { outputs: [] };

  const entries = [...context.timezones.entries].sort((a, b) =>
    a.ianaIdentifier.localeCompare(b.ianaIdentifier),
  );

  // Build the valid-country member set so we can defensively skip
  // timezones referencing countries not in the catalog.
  const validCountries = new Set<string>();
  if (context.countries !== undefined) {
    for (const c of context.countries.entries) {
      if (truthy(c.iso31661Alpha2Code) && isIdentifier(c.iso31661Alpha2Code))
        validCountries.add(c.iso31661Alpha2Code);
    }
  }

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/timezones.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine(
    'import type { Country, Timezone, TimezoneCode } from "@dcsv-io/d2-geo-abstractions";',
  );
  sb.appendLine('import { CountryCode } from "@dcsv-io/d2-geo-abstractions";');
  sb.appendLine();
  sb.appendLine('import { CountryLookup } from "./countries.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(" * O(1) lookup over the timezone catalog.");
  sb.appendLine(" */");
  sb.appendLine("export const TimezoneLookup: {");
  sb.increaseIndent();
  sb.appendLine("readonly byCode: Record<string, Timezone>;");
  sb.appendLine("all: readonly Timezone[];");
  sb.decreaseIndent();
  sb.appendLine("} = {");
  sb.increaseIndent();
  sb.appendLine("byCode: {} as Record<string, Timezone>,");
  sb.appendLine("all: [] as readonly Timezone[],");
  sb.decreaseIndent();
  sb.appendLine("};");
  sb.appendLine();

  // First pass
  sb.appendLine(
    "// ---- First pass: construct every Timezone record (country undefined). ----",
  );
  sb.appendLine("{");
  sb.increaseIndent();
  sb.appendLine("const byCode = TimezoneLookup.byCode;");
  sb.appendLine();
  for (const entry of entries) {
    sb.appendLine(`byCode["${escapeStringLiteral(entry.ianaIdentifier)}"] = {`);
    sb.increaseIndent();
    sb.appendLine(
      `ianaName: "${escapeStringLiteral(entry.ianaIdentifier)}" as TimezoneCode,`,
    );
    sb.appendLine(
      `displayName: "${escapeStringLiteral(entry.displayName ?? "")}",`,
    );
    sb.appendLine(
      "localizedDisplayNames: {} as Readonly<Record<string, string>>,",
    );
    sb.appendLine(
      `currentStdOffsetMinutes: ${entry.currentStdOffsetMinutes ?? 0},`,
    );
    if (entry.currentDstOffsetMinutes !== undefined)
      sb.appendLine(
        `currentDstOffsetMinutes: ${entry.currentDstOffsetMinutes},`,
      );

    sb.appendLine(
      `currentStdAbbrev: "${escapeStringLiteral(entry.currentStdAbbrev ?? "")}",`,
    );
    if (entry.currentDstAbbrev !== undefined)
      sb.appendLine(
        `currentDstAbbrev: "${escapeStringLiteral(entry.currentDstAbbrev)}",`,
      );

    if (
      entry.countryISO31661Alpha2Code !== undefined &&
      validCountries.has(entry.countryISO31661Alpha2Code)
    ) {
      sb.appendLine(
        `primaryCountryIso31661Alpha2Code: CountryCode.${entry.countryISO31661Alpha2Code}` +
          ` as CountryCode,`,
      );
    }

    // CoApplicable codes set (init, required).
    const coCodes = (entry.coApplicableCountryISO31661Alpha2Codes ?? []).filter(
      (c) => truthy(c) && validCountries.has(c),
    );
    if (falsey(coCodes)) {
      sb.appendLine(
        "coApplicableCountryIso31661Alpha2Codes: new Set<CountryCode>(),",
      );
    } else {
      sb.appendLine(
        "coApplicableCountryIso31661Alpha2Codes: new Set<CountryCode>([",
      );
      sb.increaseIndent();
      for (const c of coCodes)
        sb.appendLine(`CountryCode.${c} as CountryCode,`);
      sb.decreaseIndent();
      sb.appendLine("]),");
    }

    sb.appendLine("coApplicableCountries: [] as readonly Country[],");
    sb.appendLine("selectable: true,");

    const aliases = entry.aliases ?? [];
    if (falsey(aliases)) {
      sb.appendLine("aliases: [] as readonly string[],");
    } else {
      sb.appendLine("aliases: [");
      sb.increaseIndent();
      for (const a of aliases) sb.appendLine(`"${escapeStringLiteral(a)}",`);

      sb.decreaseIndent();
      sb.appendLine("] as readonly string[],");
    }
    sb.decreaseIndent();
    sb.appendLine("};");
  }
  sb.appendLine();
  sb.appendLine("const all: Timezone[] = [];");
  for (const entry of entries)
    sb.appendLine(
      `all.push(byCode["${escapeStringLiteral(entry.ianaIdentifier)}"]!);`,
    );

  sb.appendLine("TimezoneLookup.all = all;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  // Timezones nested accessor
  emitNestedTimezoneAccessor(sb, entries);
  sb.appendLine();

  // Wire-nav
  sb.appendLine("/**");
  sb.appendLine(
    " * Wire-nav step — wires `primaryCountry` + `coApplicableCountries`",
  );
  sb.appendLine(
    " * nav refs (skipped for Etc/* pseudo-zones lacking a primary).",
  );
  sb.appendLine(" */");
  sb.appendLine("export function wireTimezoneNav(): void {");
  sb.increaseIndent();
  sb.appendLine("for (const tz of TimezoneLookup.all) {");
  sb.increaseIndent();
  sb.appendLine(
    "const mut = tz as { -readonly [K in keyof Timezone]: Timezone[K] };",
  );
  sb.appendLine("if (tz.primaryCountryIso31661Alpha2Code !== undefined) {");
  sb.increaseIndent();
  sb.appendLine(
    "const primary = CountryLookup.byCode[tz.primaryCountryIso31661Alpha2Code];",
  );
  sb.appendLine("if (primary !== undefined) mut.primaryCountry = primary;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine("if (tz.coApplicableCountryIso31661Alpha2Codes.size > 0) {");
  sb.increaseIndent();
  sb.appendLine("const list: Country[] = [];");
  sb.appendLine(
    "for (const cc of tz.coApplicableCountryIso31661Alpha2Codes) {",
  );
  sb.increaseIndent();
  sb.appendLine("const country = CountryLookup.byCode[cc];");
  sb.appendLine("if (country !== undefined) list.push(country);");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine("mut.coApplicableCountries = list;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");

  return {
    outputs: [
      { path: defaultGenPath("timezones.g.ts"), source: sb.toString() },
    ],
  };
}

interface TrieNode {
  segment?: string;
  leafIana?: string;
  children: Map<string, TrieNode>;
}

function emitNestedTimezoneAccessor(
  sb: StringBuilder,
  entries: readonly TimezoneSpec[],
): void {
  const root: TrieNode = { children: new Map() };
  for (const entry of entries) {
    const segs = entry.ianaIdentifier.split("/");
    if (segs.length < 2) continue;

    let node = root;
    for (let i = 0; i < segs.length; i++) {
      const seg = segs[i]!;
      let child = node.children.get(seg);
      if (child === undefined) {
        child = { segment: seg, children: new Map() };
        node.children.set(seg, child);
      }
      node = child;
      if (i === segs.length - 1) node.leafIana = entry.ianaIdentifier;
    }
  }

  sb.appendLine("/**");
  sb.appendLine(
    " * Nested accessor — `Timezones.America.New_York` returns the typed",
  );
  sb.appendLine(" * `TimezoneCode` branded string.");
  sb.appendLine(" */");
  sb.appendLine("export const Timezones = {");
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
  if (node.children.size === 0 && node.leafIana !== undefined) {
    sb.appendLine(
      `${safeKey(seg)}: "${escapeStringLiteral(node.leafIana)}"` +
        ` as import("@dcsv-io/d2-geo-abstractions").TimezoneCode,`,
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
