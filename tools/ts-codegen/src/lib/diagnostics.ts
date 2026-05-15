// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Severity level for an emitter diagnostic. `error` blocks generation;
 * `warning` allows generation to proceed but should surface in the
 * orchestrator's exit status.
 */
export type EmitDiagnosticSeverity = "error" | "warning";

/**
 * One diagnostic surfaced during emit. Mirrors the .NET
 * `EmitDiagnostic` record — same `id` / `severity` / `message`
 * (+ optional `filePath`) shape so cross-language tooling reads
 * identical fields.
 */
export interface EmitDiagnostic {
  readonly id: string;
  readonly severity: EmitDiagnosticSeverity;
  readonly message: string;
  readonly filePath?: string;
}

/**
 * Result of an emit pass — generated source plus the diagnostics that
 * fired. Mirrors .NET `EmitResult`.
 */
export interface EmitResult {
  readonly source: string;
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Diagnostic ID prefixes — REUSED from the .NET SourceGen catalogs since
 * the diagnostics describe spec-level violations (one spec → two emitters
 * with identical interpretation = identical diagnostic semantics).
 *
 * Catalog reference (kept here so consumers can `import` for assertions):
 *
 * - `D2CTX001-006`: context spec (auth-context / request-context).
 * - `D2SCP001-009`: auth-scopes spec.
 * - `D2AEC001-005`: auth-error-codes spec.
 * - `D2HDR001-007`: headers spec.
 * - `D2JWT001-006`: jwt-claims spec.
 */
export const DiagnosticIds = {
  // Context (auth-context + request-context).
  CTX_DUPLICATE_PROPERTY: "D2CTX001",
  CTX_INVALID_TYPE: "D2CTX002",
  CTX_INVALID_NAMESPACE: "D2CTX003",
  CTX_INVALID_NAME: "D2CTX004",
  CTX_EXTENDS_UNRESOLVED: "D2CTX005",
  CTX_MALFORMED_SPEC: "D2CTX006",

  // Auth scopes.
  SCP_DUPLICATE: "D2SCP001",
  SCP_INVALID_NAME: "D2SCP002",
  SCP_INVALID_SENSITIVITY: "D2SCP003",
  SCP_MALFORMED_SPEC: "D2SCP009",

  // Auth error codes.
  AEC_DUPLICATE_CODE: "D2AEC001",
  AEC_DUPLICATE_FACTORY: "D2AEC002",
  AEC_UNKNOWN_CATEGORY: "D2AEC003",
  AEC_INVALID_HTTP_STATUS: "D2AEC004",
  AEC_MALFORMED_SPEC: "D2AEC005",

  // Headers.
  HDR_MALFORMED_SPEC: "D2HDR001",
  HDR_UNKNOWN_TRANSPORT: "D2HDR002",
  HDR_INVALID_CONST_NAME: "D2HDR003",
  HDR_DUPLICATE: "D2HDR004",
  HDR_EMPTY_APPLICABILITY: "D2HDR005",
  HDR_UNKNOWN_CONVENTION: "D2HDR006",
  HDR_MISSING_SPEC: "D2HDR007",

  // Jwt claims.
  JWT_MALFORMED_SPEC: "D2JWT001",
  JWT_UNKNOWN_KIND: "D2JWT002",
  JWT_INVALID_CONST_NAME: "D2JWT003",
  JWT_DUPLICATE_CONST_NAME: "D2JWT004",
  JWT_MISSING_SPEC: "D2JWT005",
  JWT_EMPTY_VALUE: "D2JWT006",
} as const;

/**
 * Construct an `error`-severity diagnostic.
 */
export function diagError(
  id: string,
  message: string,
  filePath?: string,
): EmitDiagnostic {
  return filePath === undefined
    ? { id, severity: "error", message }
    : { id, severity: "error", message, filePath };
}

/**
 * Construct a `warning`-severity diagnostic.
 */
export function diagWarning(
  id: string,
  message: string,
  filePath?: string,
): EmitDiagnostic {
  return filePath === undefined
    ? { id, severity: "warning", message }
    : { id, severity: "warning", message, filePath };
}

/**
 * Pretty-print one diagnostic for console output.
 */
export function formatDiagnostic(d: EmitDiagnostic): string {
  const loc = d.filePath !== undefined ? ` ${d.filePath}` : "";
  return `${d.severity.toUpperCase()} ${d.id}${loc}: ${d.message}`;
}
