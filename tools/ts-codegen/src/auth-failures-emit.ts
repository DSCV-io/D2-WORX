// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type ErrorCodesSpec,
  validateErrorCodesSpec,
} from "./auth-error-codes-emit.js";
import {
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
 * Emit `AuthFailures` static factory functions. Each entry produces a
 * function returning `D2Result.fail(...)` with the right errorCode +
 * statusCode + default messageKey. Reuses the validation walk from
 * auth-error-codes-emit.ts (filter-and-skip duplicates / unknown
 * categories / unsupported HTTP statuses) so emitter symmetry mirrors
 * .NET FailureFactoriesEmitter.
 */
export function emitAuthFailures(spec: ErrorCodesSpec): EmitResult {
  const v = validateErrorCodesSpec(spec);
  // Note: auth-error-codes-emit surfaces the diagnostics; this emitter
  // silently filters (matches .NET behavior). Returns no diagnostics here.
  const sorted = [...v.entries].sort((a, b) => a.code.localeCompare(b.code));
  const sb = new StringBuilder();
  sb.appendLine(
    buildHeader("contracts/auth-error-codes/auth-error-codes.spec.json"),
  );
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine(
    'import { D2Result, serviceUnavailable, unauthorized, tk } from "@d2/result";',
  );
  sb.appendLine('import { AuthErrorCodes } from "./auth-error-codes.g.js";');
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Pre-built D2Result failures for inbound auth runtime — JWT validation",
  );
  sb.appendLine(
    " * rejections, session liveness outages, JWKS upstream failures.",
  );
  sb.appendLine(
    " * Mirrors .NET D2.Shared.Auth.Errors.AuthFailures factory shape.",
  );
  sb.appendLine(" */");
  sb.appendLine("export const AuthFailures = {");
  sb.increaseIndent();
  for (const e of sorted) {
    const factory =
      e.category === "infrastructure_unavailable"
        ? "serviceUnavailable"
        : "unauthorized";
    const fnName = camelCase(e.factoryName);
    sb.appendLine(`/** ${escapeJsDoc(e.doc ?? "")} */`);
    sb.appendLine(`${fnName}(traceId?: string): D2Result {`);
    sb.increaseIndent();
    sb.appendLine(`return ${factory}({`);
    sb.increaseIndent();
    sb.appendLine(`messages: [tk("${e.userMessageKey}")],`);
    sb.appendLine(`errorCode: AuthErrorCodes.${e.code},`);
    sb.appendLine(`traceId,`);
    sb.decreaseIndent();
    sb.appendLine(`});`);
    sb.decreaseIndent();
    sb.appendLine(`},`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  return { source: sb.toString(), diagnostics: [] };
}

function camelCase(pascal: string): string {
  return pascal.length === 0
    ? pascal
    : pascal[0]!.toLowerCase() + pascal.slice(1);
}

function escapeJsDoc(s: string): string {
  return s.replaceAll("*/", "* /");
}

const SPEC_PATH = contractsPath(
  "auth-error-codes",
  "auth-error-codes.spec.json",
);
const TARGET = tsPackagePath("auth", "abstractions", "src", "auth-failures.g.ts");

export function runAuthFailuresEmit(force = false): readonly EmitDiagnostic[] {
  if (!force && isOutputUpToDate(TARGET, [SPEC_PATH])) return [];
  const loadResult = loadSpec<ErrorCodesSpec>(
    SPEC_PATH,
    DiagnosticIds.AEC_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;
  const result = emitAuthFailures(loadResult.spec);
  if (result.diagnostics.length > 0) return result.diagnostics;
  writeGeneratedFile(TARGET, result.source);
  return [];
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("auth-failures-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runAuthFailuresEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
