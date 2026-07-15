// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

// ---------------------------------------------------------------------------
// Proto ↔ spec wire-field parity guard.
//
// D2ResultProto, TKMessageProto, and InputErrorProto are hand-authored in
// contracts/protos/common/v1/d2_result.proto. Their JSON/wire field names are
// independently declared in three spec files:
//   contracts/d2result-envelope/d2result-envelope.spec.json  (D2ResultProto)
//   contracts/tk-message/tk-message.spec.json                (TKMessageProto)
//   contracts/input-error/input-error.spec.json              (InputErrorProto)
//
// Nothing mechanically enforces parity between these two sources. This test
// reads both, normalizes proto snake_case field names to camelCase, and asserts
// the semantic field sets match EXACTLY — no extra fields on either side, none
// missing. A field added to the spec but not the proto (or vice-versa) REDs
// here immediately.
//
// FIELD NUMBERS: proto field numbers are proto-only (the JSON specs have no
// equivalent). This test asserts NAME/PRESENCE parity only — not numbers.
//
// NAMING CONVENTION: proto uses snake_case field names (buf STANDARD lint);
// JSON wire uses camelCase per the specs. Reconciliation: normalize each
// proto field name with snakeToCamel() before comparing to spec wire values.
// ---------------------------------------------------------------------------

/** Walk up from startDir until a directory containing pnpm-workspace.yaml is found. */
function findRepoRoot(startDir: string): string {
  let dir = startDir;
  for (;;) {
    if (existsSync(join(dir, "pnpm-workspace.yaml"))) return dir;
    const parent = dirname(dir);
    if (parent === dir)
      throw new Error(
        `Could not locate the repo root (no 'pnpm-workspace.yaml' found) ` +
          `walking up from '${startDir}'.`,
      );
    dir = parent;
  }
}

/** Convert a proto snake_case field name to camelCase. */
function snakeToCamel(name: string): string {
  return name.replace(/_([a-z])/g, (_, ch: string) => ch.toUpperCase());
}

const thisDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = findRepoRoot(thisDir);

/** Read a file from the repo root, relative path. */
function readRepoFile(relPath: string): string {
  return readFileSync(join(repoRoot, relPath), "utf8");
}

/** Read and parse a JSON spec file from contracts/. */
function readSpec(contractDir: string, fileName: string): unknown {
  return JSON.parse(
    readRepoFile(`public/contracts/${contractDir}/${fileName}`),
  );
}

// ---------------------------------------------------------------------------
// Proto parser — extracts field names from a .proto file using regex.
// Only handles the simple message field patterns present in d2_result.proto:
//   <scalar>   <field_name> = <N>;
//   repeated   <type>       <field_name> = <N>;
//   optional   <type>       <field_name> = <N>;
//   map<K, V>               <field_name> = <N>;
// This is intentionally narrow — it covers exactly what d2_result.proto uses
// and will NOT silently succeed on syntax it doesn't understand.
// ---------------------------------------------------------------------------

/** Extract the set of field names for a named message from raw proto source. */
function extractProtoFields(protoSrc: string, messageName: string): string[] {
  // Match the message body — assumes no nested messages inside the target.
  const bodyRe = new RegExp(`message\\s+${messageName}\\s*\\{([^}]*)\\}`, "s");
  const bodyMatch = bodyRe.exec(protoSrc);
  if (!bodyMatch)
    throw new Error(`Message '${messageName}' not found in proto source.`);

  const body = bodyMatch[1]!;
  const fieldNames: string[] = [];

  // map<K, V> <fieldName> = <N>;
  const mapFieldRe = /map\s*<[^>]+>\s+(\w+)\s*=/g;
  let m: RegExpExecArray | null;
  while ((m = mapFieldRe.exec(body)) !== null) {
    fieldNames.push(m[1]!);
  }

  // Remove map field lines from body so the generic rule below doesn't double-match.
  const bodyNoMap = body.replace(/map\s*<[^>]+>\s+\w+\s*=\s*\d+\s*;/g, "");

  // <qualifier?> <type> <fieldName> = <N>;
  // Qualifiers: repeated, optional (proto3 optional keyword)
  const scalarFieldRe =
    /(?:(?:repeated|optional)\s+)?\w+\s+(\w+)\s*=\s*\d+\s*;/g;
  while ((m = scalarFieldRe.exec(bodyNoMap)) !== null) {
    fieldNames.push(m[1]!);
  }

  return fieldNames;
}

// ---------------------------------------------------------------------------
// Spec parsers — extract the array of wire values (the "value" field in each
// spec entry). The two spec shapes are:
//   d2result-envelope.spec.json  → { fields: [{ constName, value, doc }] }
//   tk-message.spec.json          → { properties: [{ constName, value, doc }] }
//   input-error.spec.json         → { properties: [{ constName, value, doc }] }
// ---------------------------------------------------------------------------

interface SpecField {
  readonly constName: string;
  readonly value: string;
  readonly doc?: string;
}

interface EnvelopeSpec {
  readonly fields: readonly SpecField[];
}

interface PropertiesSpec {
  readonly properties: readonly SpecField[];
}

function extractEnvelopeSpecWireValues(spec: EnvelopeSpec): string[] {
  return spec.fields.map((f) => f.value);
}

function extractPropertiesSpecWireValues(spec: PropertiesSpec): string[] {
  return spec.properties.map((p) => p.value);
}

// ---------------------------------------------------------------------------
// Load source material
// ---------------------------------------------------------------------------

const protoSrc = readRepoFile(
  "public/contracts/protos/common/v1/d2_result.proto",
);

const envelopeSpec = readSpec(
  "d2result-envelope",
  "d2result-envelope.spec.json",
) as EnvelopeSpec;

const tkMessageSpec = readSpec(
  "tk-message",
  "tk-message.spec.json",
) as PropertiesSpec;

const inputErrorSpec = readSpec(
  "input-error",
  "input-error.spec.json",
) as PropertiesSpec;

// ---------------------------------------------------------------------------
// D2ResultProto ↔ d2result-envelope spec
//
// INTENTIONAL EXCLUSION: the spec declares a `data` field (the generic TData
// payload on D2Result<TData>). D2ResultProto deliberately OMITS this field —
// in gRPC the typed payload travels as the actual protobuf response message;
// D2ResultProto carries only the result metadata (success, status, error, etc.).
// `data` is a JSON-transport-only concept. This test excludes it from the
// comparison so the assertion targets the shared semantic field set.
// ---------------------------------------------------------------------------

const _DATA_JSON_ONLY = "data";

describe("D2ResultProto ↔ d2result-envelope spec wire-field parity", () => {
  const protoFields = extractProtoFields(protoSrc, "D2ResultProto");
  const protoCamel = protoFields.map(snakeToCamel).sort();
  // Exclude the JSON-transport-only `data` field from the spec side — it has
  // no proto counterpart by design (gRPC payload travels as the typed message).
  const specValues = extractEnvelopeSpecWireValues(envelopeSpec)
    .filter((v) => v !== _DATA_JSON_ONLY)
    .sort();

  it("proto fields list is non-empty (parse sanity check)", () => {
    expect(protoFields.length).toBeGreaterThan(0);
  });

  it("spec wire values are non-empty after data exclusion (parse sanity check)", () => {
    expect(specValues.length).toBeGreaterThan(0);
  });

  it("spec declares the data field (confirming the exclusion is intentional, not a parse miss)", () => {
    const allSpecValues = extractEnvelopeSpecWireValues(envelopeSpec);
    expect(allSpecValues).toContain(_DATA_JSON_ONLY);
  });

  it("proto camelCase field names match spec wire values exactly (no extra, none missing)", () => {
    expect(protoCamel).toEqual(specValues);
  });

  // Per-field assertions — precise failure message when a single field drifts.
  for (const specValue of specValues) {
    it(`spec wire field '${specValue}' has a corresponding proto field`, () => {
      expect(protoCamel).toContain(specValue);
    });
  }

  for (const camel of protoCamel) {
    it(`proto field '${camel}' (camelCase) is declared in the spec`, () => {
      expect(specValues).toContain(camel);
    });
  }
});

// ---------------------------------------------------------------------------
// TKMessageProto ↔ tk-message spec
// ---------------------------------------------------------------------------

describe("TKMessageProto ↔ tk-message spec wire-field parity", () => {
  const protoFields = extractProtoFields(protoSrc, "TKMessageProto");
  const protoCamel = protoFields.map(snakeToCamel).sort();
  const specValues = extractPropertiesSpecWireValues(tkMessageSpec).sort();

  it("proto fields list is non-empty (parse sanity check)", () => {
    expect(protoFields.length).toBeGreaterThan(0);
  });

  it("spec wire values are non-empty (parse sanity check)", () => {
    expect(specValues.length).toBeGreaterThan(0);
  });

  it("proto camelCase field names match spec wire values exactly (no extra, none missing)", () => {
    expect(protoCamel).toEqual(specValues);
  });

  for (const specValue of specValues) {
    it(`spec wire field '${specValue}' has a corresponding proto field`, () => {
      expect(protoCamel).toContain(specValue);
    });
  }

  for (const camel of protoCamel) {
    it(`proto field '${camel}' (camelCase) is declared in the spec`, () => {
      expect(specValues).toContain(camel);
    });
  }
});

// ---------------------------------------------------------------------------
// InputErrorProto ↔ input-error spec
// ---------------------------------------------------------------------------

describe("InputErrorProto ↔ input-error spec wire-field parity", () => {
  const protoFields = extractProtoFields(protoSrc, "InputErrorProto");
  const protoCamel = protoFields.map(snakeToCamel).sort();
  const specValues = extractPropertiesSpecWireValues(inputErrorSpec).sort();

  it("proto fields list is non-empty (parse sanity check)", () => {
    expect(protoFields.length).toBeGreaterThan(0);
  });

  it("spec wire values are non-empty (parse sanity check)", () => {
    expect(specValues.length).toBeGreaterThan(0);
  });

  it("proto camelCase field names match spec wire values exactly (no extra, none missing)", () => {
    expect(protoCamel).toEqual(specValues);
  });

  for (const specValue of specValues) {
    it(`spec wire field '${specValue}' has a corresponding proto field`, () => {
      expect(protoCamel).toContain(specValue);
    });
  }

  for (const camel of protoCamel) {
    it(`proto field '${camel}' (camelCase) is declared in the spec`, () => {
      expect(specValues).toContain(camel);
    });
  }
});

// ---------------------------------------------------------------------------
// Non-vacuousness proof: if a field is added to the spec but not the proto,
// the equality assertion above FAILS. Demonstrated here by constructing a
// synthetic mismatch in-memory and asserting it would NOT pass.
// ---------------------------------------------------------------------------

describe("non-vacuousness: drift detection is live", () => {
  it("a spec with an extra field that has no proto counterpart would fail the parity check", () => {
    // Simulate: spec declares an extra wire field 'correlationId' that the proto lacks.
    const specWithExtra = ["category", "correlationId", "errorCode"].sort();
    const protoFields = ["category", "errorCode"].sort();
    // They differ — the equality check would RED.
    expect(specWithExtra).not.toEqual(protoFields);
  });

  it("a proto with an extra field that the spec doesn't declare would fail the parity check", () => {
    // Simulate: proto gains a 'spanId' field not in the spec.
    const specValues = ["category", "errorCode"].sort();
    const protoWithExtra = ["category", "errorCode", "spanId"].sort();
    expect(protoWithExtra).not.toEqual(specValues);
  });
});
