// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for the scalar registry — direct-unit (§1.28 / §1.2 adversarial).
//
// Covers:
//   - Every seeded scalar returns the correct { cs, proto, ts } mapping.
//   - resolveScalar throws (D2TSP001 loud failure) for an unknown scalar.
//   - hasScalar returns true for known, false for unknown.

import { describe, it, expect } from "vitest";
import { resolveScalar, hasScalar } from "../src/lib/scalar-registry.js";

// Table-driven verification of every seeded scalar.
const EXPECTED_MAPPINGS: Array<{
  name: string;
  cs: string;
  proto: string;
  ts: string;
}> = [
  // String
  { name: "string", cs: "string", proto: "string", ts: "string" },
  // Boolean
  { name: "boolean", cs: "bool", proto: "bool", ts: "boolean" },
  // Bytes
  { name: "bytes", cs: "byte[]", proto: "bytes", ts: "Uint8Array" },
  // Signed integers
  { name: "integer", cs: "long", proto: "int64", ts: "bigint" },
  { name: "int8", cs: "sbyte", proto: "int32", ts: "number" },
  { name: "int16", cs: "short", proto: "int32", ts: "number" },
  { name: "int32", cs: "int", proto: "int32", ts: "number" },
  { name: "int64", cs: "long", proto: "int64", ts: "bigint" },
  // Unsigned integers
  { name: "uint8", cs: "byte", proto: "uint32", ts: "number" },
  { name: "uint16", cs: "ushort", proto: "uint32", ts: "number" },
  { name: "uint32", cs: "uint", proto: "uint32", ts: "number" },
  { name: "uint64", cs: "ulong", proto: "uint64", ts: "bigint" },
  // Safe integer
  { name: "safeint", cs: "long", proto: "int64", ts: "number" },
  // Floats
  { name: "float", cs: "double", proto: "double", ts: "number" },
  { name: "float32", cs: "float", proto: "float", ts: "number" },
  { name: "float64", cs: "double", proto: "double", ts: "number" },
  { name: "numeric", cs: "double", proto: "double", ts: "number" },
  { name: "decimal", cs: "decimal", proto: "string", ts: "string" },
  { name: "decimal128", cs: "decimal", proto: "string", ts: "string" },
  // URL
  { name: "url", cs: "string", proto: "string", ts: "string" },
  // Temporal — instant-bearing → DateTimeOffset; offset-free → string.
  { name: "utcDateTime", cs: "DateTimeOffset", proto: "string", ts: "string" },
  {
    name: "offsetDateTime",
    cs: "DateTimeOffset",
    proto: "string",
    ts: "string",
  },
  { name: "plainDate", cs: "string", proto: "string", ts: "string" },
  { name: "plainTime", cs: "string", proto: "string", ts: "string" },
  { name: "plainDateTime", cs: "string", proto: "string", ts: "string" },
  { name: "duration", cs: "string", proto: "string", ts: "string" },
];

describe("resolveScalar_MappedScalars", () => {
  for (const { name, cs, proto, ts } of EXPECTED_MAPPINGS) {
    it(`resolveScalar('${name}') returns { cs: '${cs}', proto: '${proto}', ts: '${ts}' }`, () => {
      const mapping = resolveScalar(name);
      expect(mapping.cs).toBe(cs);
      expect(mapping.proto).toBe(proto);
      expect(mapping.ts).toBe(ts);
    });
  }
});

describe("resolveScalar_UnmappedScalar_ThrowsLoudly", () => {
  it("throws for an unknown scalar name", () => {
    expect(() => resolveScalar("madeUpScalar")).toThrow(
      "D2TSP001: unmapped TypeSpec scalar 'madeUpScalar'",
    );
  });

  it("throws for an empty string scalar name", () => {
    expect(() => resolveScalar("")).toThrow("D2TSP001");
  });

  it("throws for a genuinely-unknown scalar — the loud-fail mechanism is intact after temporal was added", () => {
    // The temporal scalars are now mapped; the loud-fail contract must STILL fire
    // for any genuinely-unknown scalar (the registry ADDS entries; it must not
    // silence D2TSP001). This is the NV-3 regression guard.
    expect(() => resolveScalar("notARealScalar")).toThrow("D2TSP001");
  });

  it("never returns a silent fallback — throw message includes the scalar name", () => {
    const unknownName = "totallyBogusScalar";
    let caught: unknown;
    try {
      resolveScalar(unknownName);
    } catch (e) {
      caught = e;
    }
    expect(caught).toBeInstanceOf(Error);
    expect((caught as Error).message).toContain(unknownName);
  });
});

describe("hasScalar", () => {
  it("returns true for every seeded scalar name", () => {
    for (const { name } of EXPECTED_MAPPINGS)
      expect(hasScalar(name), `hasScalar('${name}') should be true`).toBe(true);
  });

  it("returns false for an unknown scalar", () => {
    expect(hasScalar("madeUpScalar")).toBe(false);
  });

  it("returns false for empty string", () => {
    expect(hasScalar("")).toBe(false);
  });

  it("returns true for the now-mapped temporal scalars", () => {
    expect(hasScalar("utcDateTime")).toBe(true);
    expect(hasScalar("offsetDateTime")).toBe(true);
    expect(hasScalar("plainDate")).toBe(true);
    expect(hasScalar("plainTime")).toBe(true);
    expect(hasScalar("plainDateTime")).toBe(true);
    expect(hasScalar("duration")).toBe(true);
  });
});
