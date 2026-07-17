// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Supplemental model-walk tests for the proto projection:
// verifies that walkModel populates `protoType` and `repeated` on FieldInfo.
//
// These complement model-walk.test.ts which covers cs/ts types and all
// error paths; this file specifically gates the new proto projection.

import { describe, it, expect } from "vitest";
import type { Model, ModelProperty, Program, Scalar } from "@typespec/compiler";
import { D2_REDACT_KEY } from "@dcsv-io/d2-typespec-decorators";
import { walkModel } from "../src/lib/model-walk.js";
import { resolveScalar } from "../src/lib/scalar-registry.js";

// ---------------------------------------------------------------------------
// Helpers (mirrors model-walk.test.ts conventions)
// ---------------------------------------------------------------------------

function makeScalar(name: string): Scalar {
  return { kind: "Scalar", name } as unknown as Scalar;
}

function makeProp(type: Scalar, optional = false): ModelProperty {
  return { type, optional } as unknown as ModelProperty;
}

function makeModel(entries: Array<[string, ModelProperty]>): Model {
  return {
    kind: "Model",
    name: "TestModel",
    properties: new Map(entries),
  } as unknown as Model;
}

function makeProgram(): Program {
  return {
    stateMap(key: symbol): Map<object, unknown> {
      if (key === D2_REDACT_KEY) return new Map();
      return new Map();
    },
  } as unknown as Program;
}

// ---------------------------------------------------------------------------
// Tests: protoType populated for scalar fields
// ---------------------------------------------------------------------------

describe("walkModel_ProtoType_PopulatedForScalars", () => {
  it("string scalar → protoType 'string'", () => {
    const model = makeModel([["name", makeProp(makeScalar("string"))]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    expect(fields[0]!.protoType).toBe("string");
    expect(fields[0]!.repeated).toBe(false);
  });

  it("bytes scalar → protoType 'bytes'", () => {
    const model = makeModel([["payload", makeProp(makeScalar("bytes"))]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    expect(fields[0]!.protoType).toBe("bytes");
  });

  it("int32 scalar → protoType 'int32'", () => {
    const model = makeModel([["count", makeProp(makeScalar("int32"))]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    expect(fields[0]!.protoType).toBe("int32");
  });

  it("decimal scalar → protoType 'string' (lossless wire via registry proto column)", () => {
    const model = makeModel([["amount", makeProp(makeScalar("decimal"))]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    // Verifies the proto column is used, not the cs column (cs = "decimal", proto = "string").
    expect(fields[0]!.protoType).toBe("string");
    expect(fields[0]!.csType).toBe("decimal"); // cs column unchanged
  });
});

// ---------------------------------------------------------------------------
// Tests: repeated populated for array fields
// ---------------------------------------------------------------------------

describe("walkModel_Repeated_PopulatedForArrayFields", () => {
  it("string[] → repeated true + protoType 'string'", () => {
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: makeScalar("string") },
      properties: new Map(),
    } as unknown as Model;
    const prop = {
      type: arrayModel,
      optional: false,
    } as unknown as ModelProperty;
    const model = makeModel([["tags", prop]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    expect(fields[0]!.repeated).toBe(true);
    expect(fields[0]!.protoType).toBe("string");
  });

  it("bytes[] array → repeated true + protoType 'bytes'", () => {
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: makeScalar("bytes") },
      properties: new Map(),
    } as unknown as Model;
    const prop = {
      type: arrayModel,
      optional: false,
    } as unknown as ModelProperty;
    const model = makeModel([["chunks", prop]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    expect(fields[0]!.repeated).toBe(true);
    expect(fields[0]!.protoType).toBe("bytes");
  });

  it("non-array scalar → repeated false", () => {
    const model = makeModel([["id", makeProp(makeScalar("string"))]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    expect(fields[0]!.repeated).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Tests: nested model fields → protoType undefined
// ---------------------------------------------------------------------------

describe("walkModel_NestedModel_ProtoTypeUndefined", () => {
  it("nested model field → protoType undefined (model name is proto type; not a scalar)", () => {
    const inner: Model = {
      kind: "Model",
      name: "Inner",
      properties: new Map([
        [
          "id",
          {
            type: makeScalar("string"),
            optional: false,
          } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;
    const prop = { type: inner, optional: false } as unknown as ModelProperty;
    const model = makeModel([["inner", prop]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    expect(fields[0]!.protoType).toBeUndefined();
    expect(fields[0]!.repeated).toBe(false);
  });

  it("nested model array field → repeated true + protoType undefined", () => {
    const inner: Model = {
      kind: "Model",
      name: "Item",
      properties: new Map([
        [
          "id",
          {
            type: makeScalar("string"),
            optional: false,
          } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: inner },
      properties: new Map(),
    } as unknown as Model;
    const prop = {
      type: arrayModel,
      optional: false,
    } as unknown as ModelProperty;
    const model = makeModel([["items", prop]]);
    const { fields } = walkModel(makeProgram(), model, () => {});
    expect(fields[0]!.repeated).toBe(true);
    expect(fields[0]!.protoType).toBeUndefined(); // model array: model name is the proto type
  });
});

// ---------------------------------------------------------------------------
// Tests: parity across all registry scalars
// ---------------------------------------------------------------------------

describe("walkModel_ProtoColumn_MatchesRegistry_ForEveryScalar", () => {
  const REGISTRY_SCALARS = [
    "string",
    "boolean",
    "bytes",
    "integer",
    "int8",
    "int16",
    "int32",
    "int64",
    "uint8",
    "uint16",
    "uint32",
    "uint64",
    "safeint",
    "float",
    "float32",
    "float64",
    "numeric",
    "decimal",
    "decimal128",
    "url",
  ];

  for (const scalarName of REGISTRY_SCALARS) {
    it(`scalar '${scalarName}' → walkModel protoType matches registry entry`, () => {
      const expected = resolveScalar(scalarName).proto;
      const model = makeModel([["field", makeProp(makeScalar(scalarName))]]);
      const errors: string[] = [];
      const { fields } = walkModel(makeProgram(), model, (_, m) =>
        errors.push(m),
      );
      expect(errors).toHaveLength(0);
      expect(fields[0]!.protoType).toBe(expected);
    });
  }
});
