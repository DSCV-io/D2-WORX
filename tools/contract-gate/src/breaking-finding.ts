// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Shared BreakingFinding type — the unit of output produced by each gate arm.
// Every arm returns an array of these; the CLI aggregates them into the final
// verdict.

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** The gate arm that produced a finding. */
export type GateArm = "proto" | "spec" | "i18n" | "openapi";

/** Severity of a breaking-change finding. */
export type FindingSeverity = "ERROR" | "WARN";

/**
 * A single breaking-change finding produced by one of the gate arms.
 */
export interface BreakingFinding {
  /** The arm that detected this finding. */
  readonly arm: GateArm;
  /** Severity level (all real breaking changes are ERROR). */
  readonly severity: FindingSeverity;
  /** Human-readable description of the break, including the file + key/field. */
  readonly message: string;
  /**
   * Optional: the source file path (relative to repo root) that contains the
   * break. May be undefined when the arm emits structured output directly
   * (e.g. buf's output already contains file references).
   */
  readonly file?: string;
}
