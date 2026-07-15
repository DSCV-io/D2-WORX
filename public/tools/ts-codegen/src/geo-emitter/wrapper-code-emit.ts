// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { buildHeader } from "../lib/file-emit.js";
import { tsPackagePath } from "../lib/paths.js";
import { StringBuilder } from "../lib/string-builder.js";

import {
  appendEslintDisable,
  appendJsDoc,
  escapeStringLiteral,
} from "./emit-helpers.js";
import type {
  LocaleSpec,
  SubdivisionSpec,
  TimezoneSpec,
} from "./spec-types.js";

/**
 * Emits the three wrapper-code catalogs that are too large to emit as a real
 * const-object enum but still need strict closed-set validation:
 * `SubdivisionCode` (~3,600 ISO 3166-2 codes), `LocaleCode` (~700 BCP-47
 * tags), `TimezoneCode` (~600 IANA identifiers).
 *
 * Per the strict wire-code policy, the Zod refine rejects unknown values —
 * the closed-set validation table mirrors .NET `FrozenSet` /
 * `JsonConverter`-rejection semantics. The branded string type narrows the TS
 * type system so a bare `string` cannot be passed where a `LocaleCode` is
 * expected without an explicit assertion or schema parse.
 *
 * Each emitter writes one file to
 * `@d2/geo-abstractions/src/generated/typed-codes/<name>.g.ts`.
 */

const SPEC_REF_SUBDIVISIONS = "contracts/geo/subdivisions.spec.json";
const SPEC_REF_LOCALES = "contracts/geo/locales.spec.json";
const SPEC_REF_TIMEZONES = "contracts/geo/timezones.spec.json";

const GEN_DIR = (...parts: string[]): string =>
  tsPackagePath("geo", "abstractions", "src", "generated", ...parts);

/** Emit `SubdivisionCode` branded type + Zod schema + validation set. */
export function emitSubdivisionCode(entries: readonly SubdivisionSpec[]): {
  readonly path: string;
  readonly source: string;
} {
  const sorted = [...entries]
    .map((e) => e.iso31662Code)
    .sort((a, b) => a.localeCompare(b));
  return {
    path: GEN_DIR("typed-codes", "subdivision-code.g.ts"),
    source: emitWrapperCode(
      "SubdivisionCode",
      SPEC_REF_SUBDIVISIONS,
      sorted,
      [
        "ISO 3166-2 subdivision code (e.g. `US-CA`, `JP-13`, `GB-LND`). Closed",
        "set — the Zod schema rejects unknown codes (strict deserialization).",
        "Mirrors .NET `D2.Shared.Geo.Abstractions.SubdivisionCode` wrapper /",
        "JsonConverter byte-for-byte over the wire.",
      ].join("\n"),
    ),
  };
}

/** Emit `LocaleCode` branded type + Zod schema + validation set. */
export function emitLocaleCode(entries: readonly LocaleSpec[]): {
  readonly path: string;
  readonly source: string;
} {
  const sorted = [...entries]
    .map((e) => e.ietfBcp47Tag)
    .sort((a, b) => a.localeCompare(b));
  return {
    path: GEN_DIR("typed-codes", "locale-code.g.ts"),
    source: emitWrapperCode(
      "LocaleCode",
      SPEC_REF_LOCALES,
      sorted,
      [
        "IETF BCP-47 locale tag (e.g. `en-US`, `pt-BR`, `zh-Hant-TW`). Closed",
        "set — the Zod schema rejects unknown tags (strict deserialization).",
        "Mirrors .NET `D2.Shared.Geo.Abstractions.LocaleCode` wrapper /",
        "JsonConverter byte-for-byte over the wire.",
      ].join("\n"),
    ),
  };
}

/** Emit `TimezoneCode` branded type + Zod schema + validation set. */
export function emitTimezoneCode(entries: readonly TimezoneSpec[]): {
  readonly path: string;
  readonly source: string;
} {
  const sorted = [...entries]
    .map((e) => e.ianaIdentifier)
    .sort((a, b) => a.localeCompare(b));
  return {
    path: GEN_DIR("typed-codes", "timezone-code.g.ts"),
    source: emitWrapperCode(
      "TimezoneCode",
      SPEC_REF_TIMEZONES,
      sorted,
      [
        "IANA timezone identifier (e.g. `America/New_York`, `Asia/Tokyo`,",
        "`Europe/London`). Closed set — the Zod schema rejects unknown",
        "identifiers (strict deserialization). Mirrors .NET",
        "`D2.Shared.Geo.Abstractions.TimezoneCode` wrapper struct /",
        "JsonConverter byte-for-byte over the wire.",
      ].join("\n"),
    ),
  };
}

function emitWrapperCode(
  typeName: string,
  specRef: string,
  values: readonly string[],
  doc: string,
): string {
  const sb = new StringBuilder();
  sb.appendLine(buildHeader(specRef));
  appendEslintDisable(sb);
  sb.appendLine();
  sb.appendLine('import { z } from "zod";');
  sb.appendLine();
  appendJsDoc(sb, doc);
  sb.appendLine(
    `export type ${typeName} = string & { readonly __brand: "${typeName}" };`,
  );
  sb.appendLine();
  const setName = `${typeName.toUpperCase().replace(/([A-Z])([A-Z][a-z])/g, "$1_$2")}_SET`;
  // Simpler approach — just `<TYPE_NAME>_SET` derived by camel-to-screaming.
  const safeSetName = camelToScreaming(typeName) + "_SET";
  sb.appendLine(`export const ${safeSetName}: ReadonlySet<string> = new Set([`);
  // Reference unused-name to suppress lint complaining about computed alt.
  void setName;
  sb.increaseIndent();
  for (const v of values) sb.appendLine(`"${escapeStringLiteral(v)}",`);
  sb.decreaseIndent();
  sb.appendLine("]);");
  sb.appendLine();
  sb.appendLine(`export const ${typeName}Schema = z`);
  sb.increaseIndent();
  sb.appendLine(".string()");
  sb.appendLine(
    `.refine((s): s is ${typeName} => ${safeSetName}.has(s),` +
      ` { message: "value is not a known ${typeName}" });`,
  );
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Type assertion helper — narrows a string to the branded type WITHOUT",
  );
  sb.appendLine(
    " * runtime validation. Use only when the caller already proved the value",
  );
  sb.appendLine(
    " * came from a trusted source (e.g. another spec-derived constant); for",
  );
  sb.appendLine(
    ` * untrusted input always use \`${typeName}Schema.parse(...)\`.`,
  );
  sb.appendLine(" */");
  sb.appendLine(
    `export function as${typeName}(value: string): ${typeName} { return value as ${typeName}; }`,
  );
  sb.appendLine();
  return sb.toString();
}

function camelToScreaming(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1_$2").toUpperCase();
}
