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

const SUPPORTED_HTTP_STATUSES = new Set([
  200, 206, 207, 400, 401, 403, 404, 409, 413, 429, 500, 503,
]);

const CODE_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/** Top-level shape of `error-codes.spec.json`. */
export interface ErrorCodesSpec {
  readonly errorCodes: readonly ErrorCodeEntry[];
}

/** One error-code entry parsed from the spec. */
export interface ErrorCodeEntry {
  readonly code: string;
  readonly httpStatus: number;
  readonly doc: string;
}

/** Result of validating the spec. */
export interface ValidatedErrorCodes {
  readonly entries: readonly ErrorCodeEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Validate the spec — surface duplicate codes / unsupported HTTP statuses /
 * invalid SCREAMING_SNAKE shape / missing doc. Returns the valid subset for
 * downstream emit. Mirrors the .NET emitter's predicate set so cross-language
 * drift between the two validation surfaces is structurally impossible.
 */
export function validateErrorCodesSpec(
  spec: ErrorCodesSpec,
): ValidatedErrorCodes {
  const diagnostics: EmitDiagnostic[] = [];
  const validEntries: ErrorCodeEntry[] = [];
  const seenCodes = new Set<string>();

  for (const entry of spec.errorCodes) {
    if (!CODE_NAME_RE.test(entry.code)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EC_INVALID_CODE,
          `invalid error code '${entry.code}' — must match ${CODE_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (seenCodes.has(entry.code)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EC_DUPLICATE_CODE,
          `duplicate error code '${entry.code}'`,
        ),
      );
      continue;
    }
    seenCodes.add(entry.code);
    if (!SUPPORTED_HTTP_STATUSES.has(entry.httpStatus)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EC_INVALID_HTTP_STATUS,
          `unsupported httpStatus ${entry.httpStatus} on '${entry.code}' (supported: ${[
            ...SUPPORTED_HTTP_STATUSES,
          ]
            .sort((a, b) => a - b)
            .join(", ")})`,
        ),
      );
      continue;
    }
    if (entry.doc === undefined || entry.doc.trim().length === 0) {
      diagnostics.push(
        diagError(
          DiagnosticIds.EC_MISSING_DOC,
          `error code '${entry.code}' is missing the required 'doc' summary text`,
        ),
      );
      continue;
    }
    validEntries.push(entry);
  }
  return { entries: validEntries, diagnostics };
}

/**
 * Emit the `error-codes.g.ts` source. Stateless and unit-testable.
 * Preserves spec order so spec edits map to predictable diffs in the
 * emitted file (mirrors the .NET emitter's preserve-order discipline).
 */
export function emitErrorCodes(spec: ErrorCodesSpec): EmitResult {
  const v = validateErrorCodesSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/error-codes/error-codes.spec.json"));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Standardized error codes surfaced as `D2Result.errorCode`. Mirrors",
  );
  sb.appendLine(
    " * .NET `D2.Shared.Result.ErrorCodes` byte-for-byte (single spec source",
  );
  sb.appendLine(
    " * emits both sides; cross-language drift is structurally impossible).",
  );
  sb.appendLine(" */");
  sb.appendLine("export const ErrorCodes = {");
  sb.increaseIndent();
  for (const e of v.entries) {
    sb.appendLine("/**");
    for (const rawLine of e.doc.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(rawLine)}`);
    sb.appendLine(" */");
    sb.appendLine(`${e.code}: "${escapeStringLiteral(e.code)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type ErrorCode =");
  sb.increaseIndent();
  sb.appendLine("(typeof ErrorCodes)[keyof typeof ErrorCodes];");
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine("/** All declared error codes in spec order. */");
  sb.appendLine("export const ALL_ERROR_CODES: readonly string[] = [");
  sb.increaseIndent();
  for (const e of v.entries) sb.appendLine(`"${escapeStringLiteral(e.code)}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(" * HTTP status declared in the spec for a given error code.");
  sb.appendLine(" * Returns 500 for unknown codes (defensive default).");
  sb.appendLine(" */");
  sb.appendLine("export function getErrorHttpStatus(code: string): number {");
  sb.increaseIndent();
  sb.appendLine("switch (code) {");
  sb.increaseIndent();
  for (const e of v.entries)
    sb.appendLine(
      `case "${escapeStringLiteral(e.code)}": return ${e.httpStatus};`,
    );
  sb.appendLine("default: return 500;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
  return { source: sb.toString(), diagnostics: v.diagnostics };
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

const SPEC_PATH = contractsPath("error-codes", "error-codes.spec.json");
const TARGET_PATH = tsPackagePath("result", "src", "error-codes.g.ts");

/**
 * Run the error-codes emitter. Per-spec mtime check skips emit when
 * the output is newer than the spec; pass `force=true` to bypass.
 */
export function runErrorCodesEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET_PATH, [SPEC_PATH])) return [];
  const loadResult = loadSpec<ErrorCodesSpec>(
    SPEC_PATH,
    DiagnosticIds.EC_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const result = emitErrorCodes(loadResult.spec);
  if (result.diagnostics.some((d) => d.severity === "error"))
    return result.diagnostics;

  writeGeneratedFile(TARGET_PATH, result.source);
  return result.diagnostics;
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("error-codes-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runErrorCodesEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
