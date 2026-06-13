// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
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
 * One property-name entry parsed from a wire-shape spec
 * (`contracts/tk-message/tk-message.spec.json` or
 * `contracts/input-error/input-error.spec.json`).
 */
export interface WireShapeProperty {
  readonly constName: string;
  readonly value: string;
  readonly doc: string;
}

/** Top-level shape of a wire-shape `*.spec.json`. */
export interface WireShapeSpec {
  readonly properties: readonly WireShapeProperty[];
}

/** Result of validating the spec — surfaces drift / duplicate / shape errors. */
export interface ValidatedWireShape {
  readonly properties: readonly WireShapeProperty[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/**
 * Validate the spec — surface invalid-constName, duplicate constName,
 * duplicate wire value. Mirrors the .NET emitter's predicate set (same
 * spec → same predicate surface byte-for-byte).
 */
export function validateWireShapeSpec(spec: WireShapeSpec): ValidatedWireShape {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: WireShapeProperty[] = [];
  const seenConstNames = new Set<string>();
  const seenValues = new Set<string>();

  for (const entry of spec.properties) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.WS_INVALID_CONST_NAME,
          `property has invalid constName '${entry.constName}' — ` +
            `must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (seenConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.WS_DUPLICATE_PROPERTY_CONST_NAME,
          `property constName '${entry.constName}' is declared more than once`,
        ),
      );
      continue;
    }
    if (seenValues.has(entry.value)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.WS_DUPLICATE_PROPERTY_VALUE,
          `property wire value '${entry.value}' is declared more than once`,
        ),
      );
      continue;
    }
    seenConstNames.add(entry.constName);
    seenValues.add(entry.value);
    valid.push(entry);
  }

  return { properties: valid, diagnostics };
}

/**
 * Options for emitting a wire-shape catalog. Each consumer (tk-message,
 * input-error) passes its catalog-specific identifiers; the emitter
 * stays shape-agnostic.
 */
export interface EmitOptions {
  /**
   * Spec relative path used in the auto-generated header
   * (e.g. `contracts/tk-message/tk-message.spec.json`).
   */
  readonly specRelativePath: string;
  /** Exported `as const` object identifier (e.g. `TkMessageWireShape`). */
  readonly catalogName: string;
  /** Human-readable shape name used in the JSDoc summary (e.g. `TKMessage`). */
  readonly catalogDescription: string;
}

/**
 * Emit the wire-shape `.g.ts` source. Stateless and unit-testable. Preserves
 * spec order so spec edits map to predictable diffs in the emitted file.
 */
export function emitWireShape(
  spec: WireShapeSpec,
  opts: EmitOptions,
): EmitResult {
  const v = validateWireShapeSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(buildHeader(opts.specRelativePath));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    ` * Spec-derived JSON property-name catalog for the ${opts.catalogDescription} ` +
      `wire shape. Every JSON serializer / deserializer in TypeScript references ` +
      `these constants for property names instead of inline string literals — ` +
      `drift between the wire and the code is structurally impossible.`,
  );
  sb.appendLine(" *");
  sb.appendLine(
    ` * Cross-language parity: the SAME spec drives the .NET-side catalog via ` +
      `D2.Shared.WireShapes.SourceGen. Both sides emit the same property names ` +
      `byte-for-byte; cross-language wire drift is impossible.`,
  );
  sb.appendLine(" */");
  sb.appendLine(`export const ${opts.catalogName} = {`);
  sb.increaseIndent();
  for (const e of v.properties) {
    sb.appendLine("/**");
    for (const rawLine of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(rawLine)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

// ---------------------------------------------------------------------------
// Per-catalog runners — one per wire shape, each is a thin glue layer over
// the shared emit helper above.
// ---------------------------------------------------------------------------

const TK_MESSAGE_SPEC_PATH = contractsPath(
  "tk-message",
  "tk-message.spec.json",
);
const TK_MESSAGE_TARGET_PATH = tsPackagePath(
  "i18n-abstractions",
  "src",
  "generated",
  "tk-message.g.ts",
);

const INPUT_ERROR_SPEC_PATH = contractsPath(
  "input-error",
  "input-error.spec.json",
);
const INPUT_ERROR_TARGET_PATH = tsPackagePath(
  "result",
  "src",
  "input-error.g.ts",
);

/**
 * Run the tk-message emitter. Per-spec mtime check skips emit when the
 * output is newer than the spec; pass `force=true` to bypass.
 */
export function runTkMessageEmit(force = false): readonly EmitDiagnostic[] {
  if (
    !force &&
    isOutputUpToDate(TK_MESSAGE_TARGET_PATH, [TK_MESSAGE_SPEC_PATH])
  )
    return [];
  const loadResult = loadSpec<WireShapeSpec>(
    TK_MESSAGE_SPEC_PATH,
    DiagnosticIds.WS_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitWireShape(loadResult.spec, {
    specRelativePath: "contracts/tk-message/tk-message.spec.json",
    catalogName: "TkMessageWireShape",
    catalogDescription: "TKMessage",
  });
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TK_MESSAGE_TARGET_PATH, result.source);
  return result.diagnostics;
}

/**
 * Run the input-error emitter. Per-spec mtime check skips emit when the
 * output is newer than the spec; pass `force=true` to bypass.
 */
export function runInputErrorEmit(force = false): readonly EmitDiagnostic[] {
  if (
    !force &&
    isOutputUpToDate(INPUT_ERROR_TARGET_PATH, [INPUT_ERROR_SPEC_PATH])
  )
    return [];
  const loadResult = loadSpec<WireShapeSpec>(
    INPUT_ERROR_SPEC_PATH,
    DiagnosticIds.WS_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitWireShape(loadResult.spec, {
    specRelativePath: "contracts/input-error/input-error.spec.json",
    catalogName: "InputErrorWireShape",
    catalogDescription: "InputError",
  });
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(INPUT_ERROR_TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("wire-shape-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = [...runTkMessageEmit(force), ...runInputErrorEmit(force)];
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
