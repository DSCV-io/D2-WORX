// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey, truthy } from "@d2/utilities";

import { buildHeader } from "../../lib/file-emit.js";
import { StringBuilder } from "../../lib/string-builder.js";
import { appendEslintDisable, escapeStringLiteral } from "../emit-helpers.js";
import type { GeopoliticalEntitySpec, GeoSpecContext } from "../spec-types.js";

import { defaultGenPath } from "./paths.js";

/**
 * Emits `geopolitical-entities.g.ts` — the per-entity DATA.
 *
 *   - `GeopoliticalEntityLookup` — `byCode` Record indexed by
 *     `GeopoliticalEntityCode`, `all` list. Filled in the first pass.
 *   - `GeopoliticalEntities.EU` etc. — getter accessors.
 *   - `wireGeopoliticalEntityNav()` — wire-nav step. Resolves
 *     `memberCountryIso31661Alpha2Codes` to `memberCountries` via cast.
 */
export function emitGeopoliticalEntityData(context: GeoSpecContext): {
  readonly outputs: readonly {
    readonly path: string;
    readonly source: string;
  }[];
} {
  if (context.geopoliticalEntities === undefined) return { outputs: [] };

  const entries = [...context.geopoliticalEntities.entries].sort((a, b) =>
    a.shortCode.localeCompare(b.shortCode),
  );

  // Build the valid-country member set so we can defensively skip
  // member codes referencing countries not in the catalog.
  const validCountries = new Set<string>();
  if (context.countries !== undefined) {
    for (const c of context.countries.entries) {
      if (truthy(c.iso31661Alpha2Code) && isIdentifier(c.iso31661Alpha2Code))
        validCountries.add(c.iso31661Alpha2Code);
    }
  }

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/geo/geopolitical-entities.spec.json"));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine(
    'import type { Country, GeopoliticalEntity } from "@d2/geo-abstractions";',
  );
  sb.appendLine(
    "import { CountryCode, GeopoliticalEntityCode, GeopoliticalEntityType }" +
      ' from "@d2/geo-abstractions";',
  );
  sb.appendLine();
  sb.appendLine('import { CountryLookup } from "./countries.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(" * O(1) lookup over the geopolitical-entity catalog.");
  sb.appendLine(" */");
  sb.appendLine("export const GeopoliticalEntityLookup: {");
  sb.increaseIndent();
  sb.appendLine(
    "/** GeopoliticalEntity records keyed by short code (GeopoliticalEntityCode brand). */",
  );
  sb.appendLine("readonly byCode: Record<string, GeopoliticalEntity>;");
  sb.appendLine("all: readonly GeopoliticalEntity[];");
  sb.decreaseIndent();
  sb.appendLine("} = {");
  sb.increaseIndent();
  sb.appendLine("byCode: {} as Record<string, GeopoliticalEntity>,");
  sb.appendLine("all: [] as readonly GeopoliticalEntity[],");
  sb.decreaseIndent();
  sb.appendLine("};");
  sb.appendLine();

  // First pass
  sb.appendLine(
    "// ---- First pass: construct every GeopoliticalEntity record. ----",
  );
  sb.appendLine("{");
  sb.increaseIndent();
  sb.appendLine("const byCode = GeopoliticalEntityLookup.byCode;");
  sb.appendLine();
  const seen = new Set<string>();
  const emitted: GeopoliticalEntitySpec[] = [];
  for (const entry of entries) {
    if (!isIdentifier(entry.shortCode) || seen.has(entry.shortCode)) continue;

    seen.add(entry.shortCode);
    emitted.push(entry);
    sb.appendLine(`byCode[GeopoliticalEntityCode.${entry.shortCode}] = {`);
    sb.increaseIndent();
    sb.appendLine(
      `shortCode: GeopoliticalEntityCode.${entry.shortCode} as GeopoliticalEntityCode,`,
    );
    sb.appendLine(`displayName: "${escapeStringLiteral(entry.name ?? "")}",`);
    sb.appendLine(`type: GeopoliticalEntityType.${entry.type || "Continent"},`);

    const codes = (entry.countryISO31661Alpha2Codes ?? []).filter(
      (c) => truthy(c) && validCountries.has(c),
    );
    if (falsey(codes)) {
      sb.appendLine(
        "memberCountryIso31661Alpha2Codes: new Set<CountryCode>(),",
      );
    } else {
      sb.appendLine("memberCountryIso31661Alpha2Codes: new Set<CountryCode>([");
      sb.increaseIndent();
      for (const c of codes) sb.appendLine(`CountryCode.${c} as CountryCode,`);
      sb.decreaseIndent();
      sb.appendLine("]),");
    }
    sb.appendLine("memberCountries: [] as readonly Country[],");
    sb.decreaseIndent();
    sb.appendLine("};");
  }
  sb.appendLine();
  sb.appendLine("const all: GeopoliticalEntity[] = [];");
  for (const entry of emitted)
    sb.appendLine(
      `all.push(byCode[GeopoliticalEntityCode.${entry.shortCode}]!);`,
    );

  sb.appendLine("GeopoliticalEntityLookup.all = all;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  // Accessors
  sb.appendLine("/**");
  sb.appendLine(" * Per-entity accessors (`GeopoliticalEntities.EU` etc.).");
  sb.appendLine(" */");
  sb.appendLine("export const GeopoliticalEntities = {");
  sb.increaseIndent();
  for (const entry of emitted) {
    const code = entry.shortCode;
    sb.appendLine(
      `get ${code}(): GeopoliticalEntity { return GeopoliticalEntityLookup.byCode` +
        `[GeopoliticalEntityCode.${code}]!; },`,
    );
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();

  // Wire-nav
  sb.appendLine("/**");
  sb.appendLine(
    " * Wire-nav step — resolves each entity's `memberCountryIso31661Alpha2Codes`",
  );
  sb.appendLine(" * via `CountryLookup` and populates `memberCountries`.");
  sb.appendLine(" */");
  sb.appendLine("export function wireGeopoliticalEntityNav(): void {");
  sb.increaseIndent();
  sb.appendLine("for (const entity of GeopoliticalEntityLookup.all) {");
  sb.increaseIndent();
  sb.appendLine(
    "const mut = entity as { -readonly [K in keyof GeopoliticalEntity]: GeopoliticalEntity[K] };",
  );
  sb.appendLine("const list: Country[] = [];");
  sb.appendLine("for (const cc of entity.memberCountryIso31661Alpha2Codes) {");
  sb.increaseIndent();
  sb.appendLine("const country = CountryLookup.byCode[cc];");
  sb.appendLine("if (country !== undefined) list.push(country);");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine("mut.memberCountries = list;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return {
    outputs: [
      {
        path: defaultGenPath("geopolitical-entities.g.ts"),
        source: sb.toString(),
      },
    ],
  };
}

function isIdentifier(s: string): boolean {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(s);
}
