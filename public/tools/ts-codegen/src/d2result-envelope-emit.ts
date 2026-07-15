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

/**
 * One field-name entry parsed from the d2result-envelope spec
 * (`contracts/d2result-envelope/d2result-envelope.spec.json`).
 */
export interface D2ResultEnvelopeFieldEntry {
  readonly constName: string;
  readonly value: string;
  readonly doc: string;
}

/** Top-level shape of `d2result-envelope.spec.json`. */
export interface D2ResultEnvelopeSpec {
  readonly fields: readonly D2ResultEnvelopeFieldEntry[];
}

/** Result of validating the spec. */
export interface ValidatedD2ResultEnvelope {
  readonly fields: readonly D2ResultEnvelopeFieldEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/** Validate the spec — surface invalid-constName, dup-constName/value, empty. */
export function validateD2ResultEnvelopeSpec(
  spec: D2ResultEnvelopeSpec,
): ValidatedD2ResultEnvelope {
  const diagnostics: EmitDiagnostic[] = [];
  const validFields: D2ResultEnvelopeFieldEntry[] = [];

  const seenConstNames = new Set<string>();
  const seenValues = new Set<string>();
  for (const entry of spec.fields) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DRE_INVALID_CONST_NAME,
          `field has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (entry.value === undefined || entry.value.trim().length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DRE_EMPTY_VALUE,
          `field '${entry.constName}' has empty wire value`,
        ),
      );
      continue;
    }
    if (seenConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DRE_DUPLICATE_FIELD_CONST_NAME,
          `field constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenValues.has(entry.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DRE_DUPLICATE_FIELD_VALUE,
          `field wire value '${entry.value}' is declared more than once`,
        ),
      );
      continue;
    }
    seenConstNames.add(entry.constName);
    seenValues.add(entry.value);
    validFields.push(entry);
  }

  return { fields: validFields, diagnostics };
}

/** Emit the `.g.ts` source carrying the field-name catalog. */
export function emitD2ResultEnvelope(spec: D2ResultEnvelopeSpec): EmitResult {
  const v = validateD2ResultEnvelopeSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader("contracts/d2result-envelope/d2result-envelope.spec.json"),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Spec-derived JSON property-name catalog for the D2Result Shape B",
  );
  sb.appendLine(
    " * wire envelope (the BFF gateway response shape that every frontend",
  );
  sb.appendLine(
    " * reads). Mirrors .NET DcsvIo.D2.Result.D2ResultEnvelopeFieldNames",
  );
  sb.appendLine(
    " * (same wire values). Every JSON serializer / deserializer references",
  );
  sb.appendLine(
    " * these constants for property names instead of inline string literals",
  );
  sb.appendLine(
    " * — cross-language wire drift on these field names is structurally",
  );
  sb.appendLine(" * impossible.");
  sb.appendLine(" */");
  sb.appendLine();

  sb.appendLine("/**");
  sb.appendLine(
    " * JSON property-name constants for the D2Result Shape B envelope.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const D2ResultEnvelopeFieldNames = {");
  sb.increaseIndent();
  for (const e of v.fields) {
    sb.appendLine("/**");
    for (const rawLine of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(rawLine)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type D2ResultEnvelopeFieldName =");
  sb.increaseIndent();
  sb.appendLine(
    "(typeof D2ResultEnvelopeFieldNames)[keyof typeof D2ResultEnvelopeFieldNames];",
  );
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine(
    "export const ALL_D2RESULT_ENVELOPE_FIELD_NAMES: readonly string[] = [",
  );
  sb.increaseIndent();
  for (const e of v.fields) sb.appendLine(`"${escapeStringLiteral(e.value)}",`);
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

const SPEC_PATH = contractsPath(
  "d2result-envelope",
  "d2result-envelope.spec.json",
);
const TARGET_PATH = tsPackagePath("result", "src", "d2result-envelope.g.ts");

/** Run the d2result-envelope emitter. */
export function runD2ResultEnvelopeEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<D2ResultEnvelopeSpec>(
    SPEC_PATH,
    DiagnosticIds.DRE_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitD2ResultEnvelope(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("d2result-envelope-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runD2ResultEnvelopeEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
