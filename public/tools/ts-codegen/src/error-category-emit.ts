// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  diagError,
  type EmitDiagnostic,
  type EmitResult,
  DiagnosticIds,
  formatDiagnostic,
} from "./lib/diagnostics.js";
import {
  buildHeader,
  isOutputUpToDate,
  writeGeneratedFile,
} from "./lib/file-emit.js";
import { contractsPath, tsPackagePath } from "./lib/paths.js";
import { loadSpec } from "./lib/spec-loader.js";
import { StringBuilder } from "./lib/string-builder.js";

/** One error-category entry parsed from the spec. */
export interface ErrorCategoryEntry {
  readonly wire: string;
  readonly doc: string;
}

/** Top-level shape of `error-category.spec.json`. */
export interface ErrorCategorySpec {
  readonly categories: readonly ErrorCategoryEntry[];
}

/** Result of validating the spec. */
export interface ValidatedErrorCategories {
  readonly categories: readonly ErrorCategoryEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

const WIRE_RE = /^[a-z][a-z0-9_]*$/;

/**
 * Validate the spec — surface invalid-wire, duplicate wire, empty doc. Mirrors
 * the .NET ErrorCategoryEmitter validation order.
 */
export function validateErrorCategorySpec(
  spec: ErrorCategorySpec,
): ValidatedErrorCategories {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: ErrorCategoryEntry[] = [];
  const seenWires = new Set<string>();

  for (const entry of spec.categories) {
    if (!WIRE_RE.test(entry.wire)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ECAT_INVALID_WIRE,
          `error category has invalid wire '${entry.wire}' — ` +
            `must match ${WIRE_RE.source}`,
        ),
      );
      continue;
    }
    if (entry.doc === undefined || entry.doc.trim().length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ECAT_EMPTY_DOC,
          `error category '${entry.wire}' has empty or whitespace-only doc`,
        ),
      );
      continue;
    }
    if (seenWires.has(entry.wire)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.ECAT_DUPLICATE_WIRE,
          `error category wire '${entry.wire}' is declared more than once`,
        ),
      );
      continue;
    }
    seenWires.add(entry.wire);
    valid.push(entry);
  }

  return { categories: valid, diagnostics };
}

/**
 * Map a snake_case wire string to its PascalCase member name. Mirrors the .NET
 * ErrorCategoryEmitter.WireToMemberName transform (validation_failure ->
 * ValidationFailure) so the const-object keys line up with the .NET enum members.
 */
export function wireToMemberName(wire: string): string {
  return wire
    .split("_")
    .filter((seg) => seg.length > 0)
    .map((seg) => seg.charAt(0).toUpperCase() + seg.slice(1).toLowerCase())
    .join("");
}

/** Emit the error-category `.g.ts` source. */
export function emitErrorCategory(spec: ErrorCategorySpec): EmitResult {
  const v = validateErrorCategorySpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  // Sort by wire string so the emitted union + maps are deterministic and
  // byte-stable with the .NET ErrorCategory enum's ordinal member order.
  const sorted = [...v.categories].sort((a, b) =>
    a.wire < b.wire ? -1 : a.wire > b.wire ? 1 : 0,
  );

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader("contracts/error-category/error-category.spec.json"),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Closed semantic/telemetry classification for error codes. Each value",
  );
  sb.appendLine(
    " * is the snake_case wire string carried on the result envelope. Mirrors",
  );
  sb.appendLine(
    " * the .NET DcsvIo.D2.ErrorCodes.Category.ErrorCategory enum (same wire",
  );
  sb.appendLine(" * values).");
  sb.appendLine(" */");
  sb.appendLine("export type ErrorCategory =");
  sb.increaseIndent();
  sorted.forEach((e, i) => {
    const suffix = i === sorted.length - 1 ? ";" : "";
    sb.appendLine(`| "${escapeStringLiteral(e.wire)}"${suffix}`);
  });
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * PascalCase member name -> wire string. Mirrors the .NET ErrorCategory",
  );
  sb.appendLine(
    " * enum members (the cross-runtime parity fixture compares this map).",
  );
  sb.appendLine(" */");
  sb.appendLine("export const ErrorCategoryWire = {");
  sb.increaseIndent();
  for (const e of sorted) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(" */");
    sb.appendLine(
      `${wireToMemberName(e.wire)}: "${escapeStringLiteral(e.wire)}",`,
    );
  }
  sb.decreaseIndent();
  sb.appendLine("} as const satisfies Record<string, ErrorCategory>;");
  sb.appendLine();
  sb.appendLine(
    "/** All ErrorCategory wire strings in canonical (ordinal wire) order. */",
  );
  sb.appendLine(
    "export const ALL_ERROR_CATEGORIES: readonly ErrorCategory[] = [",
  );
  sb.increaseIndent();
  for (const e of sorted) sb.appendLine(`"${escapeStringLiteral(e.wire)}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

const SPEC_PATH = contractsPath("error-category", "error-category.spec.json");
const TARGET_PATH = tsPackagePath(
  "error-category",
  "src",
  "generated",
  "error-category.g.ts",
);

/** Run the error-category emitter. */
export function runErrorCategoryEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<ErrorCategorySpec>(
    SPEC_PATH,
    DiagnosticIds.ECAT_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitErrorCategory(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("error-category-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runErrorCategoryEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
