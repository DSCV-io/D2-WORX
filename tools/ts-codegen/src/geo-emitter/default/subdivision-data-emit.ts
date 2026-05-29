// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { falsey, truthy } from "@d2/utilities";

import { buildHeader } from "../../lib/file-emit.js";
import { StringBuilder } from "../../lib/string-builder.js";
import {
  appendEslintDisable,
  escapeStringLiteral,
  safeKey,
} from "../emit-helpers.js";
import type { GeoSpecContext, SubdivisionSpec } from "../spec-types.js";

import { defaultGenPath } from "./paths.js";

/**
 * Emits the per-subdivision DATA. Output: `subdivisions.g.ts`. Mirrors the
 * .NET `SubdivisionDataEmitter` (split into `SubdivisionLookup` +
 * `SubdivisionsNested` — collapsed into one TS file for sibling-import
 * locality).
 *
 *   - `SubdivisionLookup` — `byCode` (Record indexed by string code),
 *     `byCountry` (Record indexed by raw alpha-2 → readonly list), `all`
 *     list. Filled in the first pass.
 *   - `Subdivisions.US.NY` etc. — nested accessor returning the typed
 *     `SubdivisionCode` branded string.
 *   - `wireSubdivisionNav()` — wire-nav step. Wires each Subdivision's
 *     `country` + `parentSubdivision` nav refs.
 */
export function emitSubdivisionData(context: GeoSpecContext): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  if (context.subdivisions === undefined) return { outputs: [] };

  // Build the valid-country member set so we can defensively skip
  // subdivisions referencing countries that aren't in the catalog.
  const validCountryMembers = new Set<string>();
  if (context.countries !== undefined) {
    for (const c of context.countries.entries) {
      if (truthy(c.iso31661Alpha2Code) && isIdentifier(c.iso31661Alpha2Code))
        validCountryMembers.add(c.iso31661Alpha2Code);
    }
  }

  const allEntries = [...context.subdivisions.entries].sort((a, b) =>
    a.iso31662Code.localeCompare(b.iso31662Code),
  );

  const entries: SubdivisionSpec[] = [];
  for (const s of allEntries) {
    if (
      falsey(s.countryISO31661Alpha2Code) ||
      !validCountryMembers.has(s.countryISO31661Alpha2Code)
    ) {
      continue;
    }
    entries.push(s);
  }

  const validSubdivisionCodes = new Set<string>();
  for (const s of entries) validSubdivisionCodes.add(s.iso31662Code);

  // Group by country alpha-2 for the nested accessor + byCountry index.
  const grouped = new Map<string, SubdivisionSpec[]>();
  for (const entry of entries) {
    const c = entry.countryISO31661Alpha2Code;
    const list = grouped.get(c) ?? [];
    list.push(entry);
    grouped.set(c, list);
  }

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/subdivisions.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine(
    'import type { Subdivision, SubdivisionCode } from "@d2/geo-abstractions";',
  );
  sb.appendLine('import { CountryCode } from "@d2/geo-abstractions";');
  sb.appendLine();
  sb.appendLine('import { CountryLookup } from "./countries.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * O(1) lookup over the subdivision catalog. First pass fills `byCode` /",
  );
  sb.appendLine(
    " * `byCountry` / `all`; wire-nav step (`wireSubdivisionNav`) wires the",
  );
  sb.appendLine(
    " * `country` + `parentSubdivision` nav refs on each Subdivision.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const SubdivisionLookup: {");
  sb.increaseIndent();
  sb.appendLine("readonly byCode: Record<string, Subdivision>;");
  sb.appendLine("readonly byCountry: Record<string, readonly Subdivision[]>;");
  sb.appendLine("all: readonly Subdivision[];");
  sb.decreaseIndent();
  sb.appendLine("} = {");
  sb.increaseIndent();
  sb.appendLine("byCode: {} as Record<string, Subdivision>,");
  sb.appendLine("byCountry: {} as Record<string, readonly Subdivision[]>,");
  sb.appendLine("all: [] as readonly Subdivision[],");
  sb.decreaseIndent();
  sb.appendLine("};");
  sb.appendLine();

  // First pass
  sb.appendLine(
    "// ---- First pass: construct every Subdivision record (country nav undefined). ----",
  );
  sb.appendLine("{");
  sb.increaseIndent();
  sb.appendLine("const byCode = SubdivisionLookup.byCode;");
  sb.appendLine("const byCountry = SubdivisionLookup.byCountry;");
  sb.appendLine();
  for (const entry of entries) {
    const key = entry.iso31662Code;
    sb.appendLine(`byCode["${escapeStringLiteral(key)}"] = {`);
    sb.increaseIndent();
    sb.appendLine(
      `iso31662Code: "${escapeStringLiteral(entry.iso31662Code)}" as SubdivisionCode,`,
    );
    sb.appendLine(
      `shortCode: "${escapeStringLiteral(entry.shortCode ?? "")}",`,
    );
    sb.appendLine(
      `displayName: "${escapeStringLiteral(entry.displayName ?? "")}",`,
    );
    sb.appendLine(
      `officialName: "${escapeStringLiteral(entry.officialName ?? "")}",`,
    );
    sb.appendLine(
      `endonymDisplayName: "` +
        `${escapeStringLiteral(entry.endonymDisplayName ?? entry.displayName ?? "")}",`,
    );
    sb.appendLine(
      `endonymOfficialName: "` +
        `${escapeStringLiteral(entry.endonymDisplayName ?? entry.officialName ?? "")}",`,
    );
    sb.appendLine(
      `countryIso31661Alpha2Code: CountryCode.${entry.countryISO31661Alpha2Code} as CountryCode,`,
    );
    if (
      entry.parentISO31662Code !== undefined &&
      validSubdivisionCodes.has(entry.parentISO31662Code)
    ) {
      sb.appendLine(
        `parentSubdivisionIso31662Code: "${escapeStringLiteral(entry.parentISO31662Code)}"` +
          ` as SubdivisionCode,`,
      );
    }
    sb.appendLine(`type: "${escapeStringLiteral(entry.type ?? "")}",`);
    sb.decreaseIndent();
    sb.appendLine("};");
  }
  sb.appendLine();

  // Populate byCountry index, keyed by raw alpha-2 string for stability
  // across const-object brand quirks.
  const countries = [...grouped.keys()].sort();
  for (const country of countries) {
    if (!isIdentifier(country)) continue;

    sb.appendLine(`byCountry["${escapeStringLiteral(country)}"] = [`);
    sb.increaseIndent();
    for (const entry of grouped.get(country)!) {
      sb.appendLine(`byCode["${escapeStringLiteral(entry.iso31662Code)}"]!,`);
    }
    sb.decreaseIndent();
    sb.appendLine("];");
  }
  sb.appendLine();
  sb.appendLine("const all: Subdivision[] = [];");
  for (const entry of entries)
    sb.appendLine(
      `all.push(byCode["${escapeStringLiteral(entry.iso31662Code)}"]!);`,
    );

  sb.appendLine("SubdivisionLookup.all = all;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  // Subdivisions nested accessor
  sb.appendLine("/**");
  sb.appendLine(" * Nested accessor — `Subdivisions.US.NY` returns the typed");
  sb.appendLine(' * `SubdivisionCode` branded string `"US-NY"`. Mirrors .NET');
  sb.appendLine(" * `D2.Shared.Geo.Default.Subdivisions.US.NY`.");
  sb.appendLine(" */");
  sb.appendLine("export const Subdivisions = {");
  sb.increaseIndent();
  for (const country of countries) {
    sb.appendLine(`${safeKey(country)}: {`);
    sb.increaseIndent();
    for (const entry of grouped.get(country)!) {
      sb.appendLine(
        `${safeKey(entry.shortCode)}: "${escapeStringLiteral(entry.iso31662Code)}"` +
          ` as SubdivisionCode,`,
      );
    }
    sb.decreaseIndent();
    sb.appendLine("},");
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();

  // Wire-nav step
  sb.appendLine("/**");
  sb.appendLine(
    " * Wire-nav step — wires `country` + `parentSubdivision` nav refs on",
  );
  sb.appendLine(" * every Subdivision.");
  sb.appendLine(" */");
  sb.appendLine("export function wireSubdivisionNav(): void {");
  sb.increaseIndent();
  sb.appendLine("for (const sub of SubdivisionLookup.all) {");
  sb.increaseIndent();
  sb.appendLine(
    "const mut = sub as { -readonly [K in keyof Subdivision]: Subdivision[K] };",
  );
  sb.appendLine(
    "const country = CountryLookup.byCode[sub.countryIso31661Alpha2Code];",
  );
  sb.appendLine("if (country !== undefined) mut.country = country;");
  sb.appendLine();
  sb.appendLine("if (sub.parentSubdivisionIso31662Code !== undefined) {");
  sb.increaseIndent();
  sb.appendLine(
    "const parent = SubdivisionLookup.byCode[sub.parentSubdivisionIso31662Code];",
  );
  sb.appendLine("if (parent !== undefined) mut.parentSubdivision = parent;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");

  return {
    outputs: [
      { path: defaultGenPath("subdivisions.g.ts"), source: sb.toString() },
    ],
  };
}

function isIdentifier(s: string): boolean {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(s);
}
