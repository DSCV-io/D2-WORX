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

const VALID_CATEGORIES = new Set([
  "validation_failure",
  "infrastructure_unavailable",
  "policy_denied",
]);
const SUPPORTED_HTTP_STATUSES = new Set([401, 503]);

export interface ErrorCodesSpec {
  readonly errorCodes: readonly ErrorCodeEntry[];
}

export interface ErrorCodeEntry {
  readonly code: string;
  readonly httpStatus: number;
  readonly category: string;
  readonly userMessageKey: string;
  readonly factoryName: string;
  readonly doc?: string;
}

export interface ValidatedErrorCodes {
  readonly entries: readonly ErrorCodeEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Validate the spec — surface duplicate codes / duplicate factories /
 * unknown categories / unsupported HTTP statuses. Returns the valid
 * subset for downstream emit.
 */
export function validateErrorCodesSpec(
  spec: ErrorCodesSpec,
): ValidatedErrorCodes {
  const diagnostics: EmitDiagnostic[] = [];
  const validEntries: ErrorCodeEntry[] = [];
  const seenCodes = new Set<string>();
  const seenFactories = new Set<string>();

  for (const entry of spec.errorCodes) {
    if (seenCodes.has(entry.code)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.AEC_DUPLICATE_CODE,
          `duplicate error code '${entry.code}'`,
        ),
      );
      continue;
    }
    seenCodes.add(entry.code);
    if (seenFactories.has(entry.factoryName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.AEC_DUPLICATE_FACTORY,
          `duplicate factory name '${entry.factoryName}'`,
        ),
      );
      continue;
    }
    seenFactories.add(entry.factoryName);
    if (!VALID_CATEGORIES.has(entry.category)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.AEC_UNKNOWN_CATEGORY,
          `unknown category '${entry.category}' on '${entry.code}' (valid: ${[
            ...VALID_CATEGORIES,
          ]
            .sort()
            .join(", ")})`,
        ),
      );
      continue;
    }
    if (!SUPPORTED_HTTP_STATUSES.has(entry.httpStatus)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.AEC_INVALID_HTTP_STATUS,
          `unsupported httpStatus ${entry.httpStatus} on '${entry.code}' (supported: ${[
            ...SUPPORTED_HTTP_STATUSES,
          ]
            .sort()
            .join(", ")})`,
        ),
      );
      continue;
    }
    validEntries.push(entry);
  }
  return { entries: validEntries, diagnostics };
}

export function emitAuthErrorCodes(spec: ErrorCodesSpec): EmitResult {
  const v = validateErrorCodesSpec(spec);
  if (v.diagnostics.length > 0)
    return { source: "", diagnostics: v.diagnostics };
  const sorted = [...v.entries].sort((a, b) => a.code.localeCompare(b.code));

  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader("contracts/auth-error-codes/auth-error-codes.spec.json"),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Machine-readable error codes for auth runtime failures. Mirrors .NET",
  );
  sb.appendLine(
    " * D2.Shared.Auth.Errors.AuthErrorCodes (same string values).",
  );
  sb.appendLine(" */");
  sb.appendLine("export const AuthErrorCodes = {");
  sb.increaseIndent();
  for (const e of sorted) sb.appendLine(`${e.code}: "${e.code}",`);
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine("export type AuthErrorCode =");
  sb.increaseIndent();
  sb.appendLine("(typeof AuthErrorCodes)[keyof typeof AuthErrorCodes];");
  sb.decreaseIndent();
  sb.appendLine();
  sb.appendLine("export const ALL_AUTH_ERROR_CODES: readonly string[] = [");
  sb.increaseIndent();
  for (const e of sorted) sb.appendLine(`"${e.code}",`);
  sb.decreaseIndent();
  sb.appendLine("];");
  sb.appendLine();
  // HTTP status lookup.
  sb.appendLine("/**");
  sb.appendLine(" * HTTP status declared in the spec for an AUTH_* code.");
  sb.appendLine(" * Returns 500 for unknown codes (defensive default).");
  sb.appendLine(" */");
  sb.appendLine(
    "export function getAuthErrorHttpStatus(code: string): number {",
  );
  sb.increaseIndent();
  sb.appendLine("switch (code) {");
  sb.increaseIndent();
  for (const e of sorted)
    sb.appendLine(`case "${e.code}": return ${e.httpStatus};`);
  sb.appendLine("default: return 500;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();
  return { source: sb.toString(), diagnostics: [] };
}

const SPEC_PATH = contractsPath(
  "auth-error-codes",
  "auth-error-codes.spec.json",
);
const TARGET = tsPackagePath(
  "auth-abstractions",
  "src",
  "auth-error-codes.g.ts",
);

export function runAuthErrorCodesEmit(
  force = false,
): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET, [SPEC_PATH])) return [];
  const loadResult = loadSpec<ErrorCodesSpec>(
    SPEC_PATH,
    DiagnosticIds.AEC_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;
  const result = emitAuthErrorCodes(loadResult.spec);
  if (result.diagnostics.length > 0) return result.diagnostics;
  writeGeneratedFile(TARGET, result.source);
  return [];
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("auth-error-codes-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runAuthErrorCodesEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
