// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Scalar registry — maps TypeSpec built-in scalar names to their
// equivalent types in C#, proto3, and TypeScript.
//
// Only the unambiguous core set is seeded here. Temporal scalars
// (utcDateTime, plainDate, plainTime, offsetDateTime, duration) require
// NodaTime / DateTimeOffset decisions that belong to the DTO emitter step;
// they are deferred to prevent premature mapping commits.
//
// Unmapped scalars ALWAYS fail loud. resolveScalar() throws when a scalar is
// not in the registry; callers convert this to a D2TSP001 diagnostic so tsp
// compile exits non-zero. NEVER return a silent fallback.

/** Mapping from one TypeSpec scalar to its target-language equivalents. */
export interface ScalarMapping {
  /** C# type name (e.g. "string", "int", "long", "double"). */
  readonly cs: string;
  /** proto3 field type (e.g. "string", "int32", "int64", "double"). */
  readonly proto: string;
  /** TypeScript type (e.g. "string", "number", "bigint", "boolean"). */
  readonly ts: string;
}

// Frozen map — never mutated at runtime. Any key not present here triggers
// the D2TSP001 loud failure via resolveScalar().
const REGISTRY: Readonly<Record<string, ScalarMapping>> = Object.freeze({
  // ---- String ----------------------------------------------------------------
  string: { cs: "string", proto: "string", ts: "string" },

  // ---- Boolean ---------------------------------------------------------------
  boolean: { cs: "bool", proto: "bool", ts: "boolean" },

  // ---- Bytes -----------------------------------------------------------------
  bytes: { cs: "byte[]", proto: "bytes", ts: "Uint8Array" },

  // ---- Signed integers -------------------------------------------------------
  // TypeSpec `integer` is the abstract integer base; map to int64 / long as the
  // safe catch-all (the concrete-int scalars below are preferred for codegen).
  integer: { cs: "long", proto: "int64", ts: "bigint" },
  int8: { cs: "sbyte", proto: "int32", ts: "number" },
  int16: { cs: "short", proto: "int32", ts: "number" },
  int32: { cs: "int", proto: "int32", ts: "number" },
  int64: { cs: "long", proto: "int64", ts: "bigint" },

  // ---- Unsigned integers -----------------------------------------------------
  uint8: { cs: "byte", proto: "uint32", ts: "number" },
  uint16: { cs: "ushort", proto: "uint32", ts: "number" },
  uint32: { cs: "uint", proto: "uint32", ts: "number" },
  uint64: { cs: "ulong", proto: "uint64", ts: "bigint" },

  // ---- Safe integer (IEEE 754 double-safe range: -(2^53-1) to 2^53-1) -------
  safeint: { cs: "long", proto: "int64", ts: "number" },

  // ---- Floating point --------------------------------------------------------
  float: { cs: "double", proto: "double", ts: "number" },
  float32: { cs: "float", proto: "float", ts: "number" },
  float64: { cs: "double", proto: "double", ts: "number" },
  // TypeSpec `numeric` is the abstract numeric base; double is the safe default.
  numeric: { cs: "double", proto: "double", ts: "number" },
  // `decimal` and `decimal128` have no exact proto3 equivalent; use string as
  // the wire representation (lossless serialization via ToString("G")).
  decimal: { cs: "decimal", proto: "string", ts: "string" },
  decimal128: { cs: "decimal", proto: "string", ts: "string" },

  // ---- URL -------------------------------------------------------------------
  url: { cs: "string", proto: "string", ts: "string" },
});

/**
 * Resolve a TypeSpec scalar name to its target-language mapping.
 *
 * @throws {Error} when the scalar is not in the registry (D2TSP001 — loud failure).
 *   Callers convert this error into a typed diagnostic via $lib.reportDiagnostic.
 */
export function resolveScalar(scalarName: string): ScalarMapping {
  const mapping = REGISTRY[scalarName];
  if (mapping === undefined)
    throw new Error(
      `D2TSP001: unmapped TypeSpec scalar '${scalarName}' — no C#/proto/TS mapping in the scalar registry`,
    );
  return mapping;
}

/**
 * Returns true when the scalar name has a registry entry.
 * Use for guard checks before calling resolveScalar() in contexts where
 * the missing-scalar diagnostic needs more context than the raw throw.
 */
export function hasScalar(scalarName: string): boolean {
  return Object.prototype.hasOwnProperty.call(REGISTRY, scalarName);
}
