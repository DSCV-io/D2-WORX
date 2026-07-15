// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Scalar registry — maps TypeSpec built-in scalar names to their
// equivalent types in C#, proto3, and TypeScript.
//
// The core set plus the six temporal scalars are seeded here. Temporal scalars
// (utcDateTime, offsetDateTime, plainDate, plainTime, plainDateTime, duration)
// map to their lossless wire forms: the two instant-bearing scalars become
// DateTimeOffset (ISO-8601 "O", offset preserved); the offset-free scalars
// (plainDate / plainTime / plainDateTime) and duration become string
// (offset-free ISO / ISO-8601 P…T…). The wire form is NOT the domain form —
// the handler body maps wire ↔ NodaTime / Temporal at the boundary, never the
// emitter. Zone-bearing values (IANA name must survive) use the composite wire
// records declared in contracts/typespec/common/temporal.tsp, NOT a scalar.
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

  // ---- Temporal --------------------------------------------------------------
  // Instant-bearing scalars → DateTimeOffset on the C# wire (System.Text.Json
  // serializes with the "O" round-trip format — offset preserved, lossless to
  // 100ns ticks). proto3 + TS carry the ISO-8601 string. The NodaTime domain
  // target is Instant (utcDateTime) / OffsetDateTime (offsetDateTime); IANA-
  // bearing values use the ZonedInstantWire composite, not offsetDateTime.
  utcDateTime: { cs: "DateTimeOffset", proto: "string", ts: "string" },
  offsetDateTime: { cs: "DateTimeOffset", proto: "string", ts: "string" },
  // Offset-FREE scalars → string. Inventing a +00:00 offset on a wall-clock /
  // date-only / time-only value silently corrupts its meaning, so the lossless
  // wire shape is an offset-free ISO string round-tripped by NodaTime's
  // LocalDatePattern.Iso / LocalTimePattern.ExtendedIso /
  // LocalDateTimePattern.ExtendedIso (and Temporal's PlainDate/PlainTime/
  // PlainDateTime on the TS side).
  plainDate: { cs: "string", proto: "string", ts: "string" },
  plainTime: { cs: "string", proto: "string", ts: "string" },
  plainDateTime: { cs: "string", proto: "string", ts: "string" },
  // Elapsed interval → ISO-8601 "P…T…" string (DurationPattern.Roundtrip ↔
  // Temporal.Duration). NodaTime Duration, never the BCL TimeSpan.
  duration: { cs: "string", proto: "string", ts: "string" },
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
