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

/** One field-name entry from the `fields[]` sub-catalog. */
export interface DlqFieldEntry {
  readonly constName: string;
  readonly value: string;
  readonly doc: string;
}

/** One cause-string entry from the `causes[]` sub-catalog. */
export interface DlqCauseEntry {
  readonly constName: string;
  readonly value: string;
  readonly doc: string;
}

/** Top-level shape of `dlq-failure-metadata.spec.json`. */
export interface DlqFailureMetadataSpec {
  readonly fields: readonly DlqFieldEntry[];
  readonly causes: readonly DlqCauseEntry[];
}

/** Result of validating the spec. */
export interface ValidatedDlqFailureMetadata {
  readonly fields: readonly DlqFieldEntry[];
  readonly causes: readonly DlqCauseEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/** Validate the spec — surface invalid-constName, dup-constName/value, empty. */
export function validateDlqFailureMetadataSpec(
  spec: DlqFailureMetadataSpec,
): ValidatedDlqFailureMetadata {
  const diagnostics: EmitDiagnostic[] = [];
  const validFields: DlqFieldEntry[] = [];
  const validCauses: DlqCauseEntry[] = [];

  const seenFieldConstNames = new Set<string>();
  const seenFieldValues = new Set<string>();
  for (const entry of spec.fields) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DLQ_INVALID_CONST_NAME,
          `field has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (entry.value === undefined || entry.value.trim().length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DLQ_EMPTY_VALUE,
          `field '${entry.constName}' has empty wire value`,
        ),
      );
      continue;
    }
    if (seenFieldConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DLQ_DUPLICATE_FIELD_CONST_NAME,
          `field constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenFieldValues.has(entry.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DLQ_DUPLICATE_FIELD_VALUE,
          `field wire value '${entry.value}' is declared more than once`,
        ),
      );
      continue;
    }
    seenFieldConstNames.add(entry.constName);
    seenFieldValues.add(entry.value);
    validFields.push(entry);
  }

  const seenCauseConstNames = new Set<string>();
  const seenCauseValues = new Set<string>();
  for (const entry of spec.causes) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DLQ_INVALID_CONST_NAME,
          `cause has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (entry.value === undefined || entry.value.trim().length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DLQ_EMPTY_VALUE,
          `cause '${entry.constName}' has empty wire value`,
        ),
      );
      continue;
    }
    if (seenCauseConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DLQ_DUPLICATE_CAUSE,
          `cause constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenCauseValues.has(entry.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.DLQ_DUPLICATE_CAUSE,
          `cause wire value '${entry.value}' is declared more than once`,
        ),
      );
      continue;
    }
    seenCauseConstNames.add(entry.constName);
    seenCauseValues.add(entry.value);
    validCauses.push(entry);
  }

  return { fields: validFields, causes: validCauses, diagnostics };
}

/** Emit a combined `.g.ts` source carrying both sub-catalogs. */
export function emitDlqFailureMetadata(
  spec: DlqFailureMetadataSpec,
): EmitResult {
  const v = validateDlqFailureMetadataSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader(
      "contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json",
    ),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(" * Spec-derived DLQ failure-metadata wire-shape catalogs.");
  sb.appendLine(
    " * Mirrors .NET D2.Shared.Messaging.DlqFailureMetadataFields and",
  );
  sb.appendLine(
    " * D2.Shared.Messaging.RabbitMq.Subscribing.DlqFailureCauses (same wire values).",
  );
  sb.appendLine(" */");
  sb.appendLine();

  // Fields catalog.
  sb.appendLine("/**");
  sb.appendLine(
    " * JSON property-name constants for the DlqFailureMetadata record.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const DlqFailureMetadataFields = {");
  sb.increaseIndent();
  for (const e of v.fields) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type DlqFailureMetadataField =");
  sb.increaseIndent();
  sb.appendLine(
    "(typeof DlqFailureMetadataFields)[keyof typeof DlqFailureMetadataFields];",
  );
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine(
    "export const ALL_DLQ_FAILURE_METADATA_FIELDS: readonly string[] = [",
  );
  sb.increaseIndent();
  for (const e of v.fields) sb.appendLine(`"${escapeStringLiteral(e.value)}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();

  // Causes catalog.
  sb.appendLine("/**");
  sb.appendLine(
    " * Closed-enum cause-string constants for DlqFailureMetadata.Cause.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const DlqFailureCauses = {");
  sb.increaseIndent();
  for (const e of v.causes) {
    sb.appendLine("/**");
    for (const line of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type DlqFailureCause =");
  sb.increaseIndent();
  sb.appendLine("(typeof DlqFailureCauses)[keyof typeof DlqFailureCauses];");
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine("export const ALL_DLQ_FAILURE_CAUSES: readonly string[] = [");
  sb.increaseIndent();
  for (const e of v.causes) sb.appendLine(`"${escapeStringLiteral(e.value)}",`);
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
  "dlq-failure-metadata",
  "dlq-failure-metadata.spec.json",
);
const TARGET_PATH = tsPackagePath(
  "messaging-abstractions",
  "src",
  "dlq-failure-metadata.g.ts",
);

/** Run the dlq-failure-metadata emitter. */
export function runDlqFailureMetadataEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<DlqFailureMetadataSpec>(
    SPEC_PATH,
    DiagnosticIds.DLQ_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitDlqFailureMetadata(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("dlq-failure-metadata-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runDlqFailureMetadataEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
