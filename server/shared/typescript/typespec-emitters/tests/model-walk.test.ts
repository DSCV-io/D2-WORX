// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for walkModel.
// Every public code path exercised: scalar fields, optional, collections,
// nested models, @d2Redact, empty models, unmapped scalars (D2TSP001),
// unsupported types (D2TSP002), and nested-model deduplication.

import { describe, it, expect } from "vitest";
import type { Model, ModelProperty, Program, Scalar, Type } from "@typespec/compiler";
import { D2_REDACT_KEY } from "@d2/typespec-decorators";
import { walkModel } from "../src/lib/model-walk.js";

// ---------------------------------------------------------------------------
// Test helpers — build minimal TypeSpec type stubs
// ---------------------------------------------------------------------------

function makeScalar(name: string): Scalar {
  return { kind: "Scalar", name } as unknown as Scalar;
}

function makeProp(
  type: Type,
  optional = false,
  redact = false,
): { prop: ModelProperty; redactMap: Map<object, unknown> } {
  const prop = { type, optional } as unknown as ModelProperty;
  const redactMap = new Map<object, unknown>();
  if (redact) redactMap.set(prop, true);
  return { prop, redactMap };
}

function makeModel(entries: Array<[string, ModelProperty]>): Model {
  return {
    kind: "Model",
    name: "TestModel",
    properties: new Map(entries),
  } as unknown as Model;
}

function makeProgram(redactMap: Map<object, unknown> = new Map()): Program {
  return {
    stateMap(key: symbol): Map<object, unknown> {
      if (key === D2_REDACT_KEY) return redactMap;
      return new Map();
    },
  } as unknown as Program;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("walkModel_ScalarField_ResolvesCorrectly", () => {
  it("string scalar → correct csType and tsType", () => {
    const { prop, redactMap } = makeProp(makeScalar("string"));
    const model = makeModel([["name", prop]]);
    const program = makeProgram(redactMap);

    const errors: string[] = [];
    const { fields } = walkModel(program, model, (_, m) => errors.push(m));

    expect(errors).toHaveLength(0);
    expect(fields).toHaveLength(1);
    expect(fields[0]!.csType).toBe("string");
    expect(fields[0]!.tsType).toBe("string");
    expect(fields[0]!.csName).toBe("Name");
    expect(fields[0]!.optional).toBe(false);
    expect(fields[0]!.redact).toBe(false);
  });

  it("boolean scalar → bool / boolean", () => {
    const { prop, redactMap } = makeProp(makeScalar("boolean"));
    const model = makeModel([["active", prop]]);
    const { fields } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.csType).toBe("bool");
    expect(fields[0]!.tsType).toBe("boolean");
  });

  it("bytes scalar → byte[] / Uint8Array", () => {
    const { prop, redactMap } = makeProp(makeScalar("bytes"));
    const model = makeModel([["payload", prop]]);
    const { fields } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.csType).toBe("byte[]");
    expect(fields[0]!.tsType).toBe("Uint8Array");
  });

  it("int32 scalar → int / number", () => {
    const { prop, redactMap } = makeProp(makeScalar("int32"));
    const model = makeModel([["count", prop]]);
    const { fields } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.csType).toBe("int");
    expect(fields[0]!.tsType).toBe("number");
  });
});

describe("walkModel_OptionalField_NullableTypes", () => {
  it("optional string → string? (C#) and optional flag true", () => {
    const { prop, redactMap } = makeProp(makeScalar("string"), true);
    const model = makeModel([["hint", prop]]);
    const { fields } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.optional).toBe(true);
    expect(fields[0]!.csType).toBe("string?");
    expect(fields[0]!.tsType).toBe("string");
  });
});

describe("walkModel_CollectionField_IReadOnlyList", () => {
  it("string[] → IReadOnlyList<string> / readonly string[]", () => {
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: makeScalar("string") },
      properties: new Map(),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(arrayModel as unknown as Type);
    const model = makeModel([["tags", prop]]);
    const { fields } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.csType).toBe("IReadOnlyList<string>");
    expect(fields[0]!.tsType).toBe("readonly string[]");
  });

  it("nested-model[] → IReadOnlyList<Jwk> / readonly Jwk[]", () => {
    const jwkModel: Model = {
      kind: "Model",
      name: "Jwk",
      properties: new Map([
        ["kid", { type: makeScalar("string"), optional: false } as unknown as ModelProperty],
      ]),
    } as unknown as Model;

    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: jwkModel },
      properties: new Map(),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(arrayModel as unknown as Type);
    const model = makeModel([["keys", prop]]);
    const { fields, nestedModels } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.csType).toBe("IReadOnlyList<Jwk>");
    expect(fields[0]!.tsType).toBe("readonly Jwk[]");
    // Nested model collected.
    expect(nestedModels).toHaveLength(1);
    expect(nestedModels[0]!.name).toBe("Jwk");
  });
});

describe("walkModel_NestedModel_RecursionAndDedup", () => {
  it("nested model → nested FieldInfo collected once", () => {
    const inner: Model = {
      kind: "Model",
      name: "Jwk",
      properties: new Map([
        ["kid", { type: makeScalar("string"), optional: false } as unknown as ModelProperty],
        ["n", { type: makeScalar("string"), optional: false } as unknown as ModelProperty],
      ]),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(inner as unknown as Type);
    const model = makeModel([["jwk", prop]]);
    const { fields, nestedModels } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.csType).toBe("Jwk");
    expect(nestedModels).toHaveLength(1);
    expect(nestedModels[0]!.fields).toHaveLength(2);
  });

  it("same nested model referenced twice → collected once (dedup)", () => {
    const inner: Model = {
      kind: "Model",
      name: "Jwk",
      properties: new Map([
        ["kid", { type: makeScalar("string"), optional: false } as unknown as ModelProperty],
      ]),
    } as unknown as Model;

    const prop1 = { type: inner, optional: false } as unknown as ModelProperty;
    const prop2 = { type: inner, optional: false } as unknown as ModelProperty;
    const model = makeModel([["first", prop1], ["second", prop2]]);
    const { nestedModels } = walkModel(makeProgram(), model, () => {});

    expect(nestedModels).toHaveLength(1);
  });
});

describe("walkModel_RedactField_RedactFlagTrue", () => {
  it("@d2Redact property → redact: true", () => {
    const { prop, redactMap } = makeProp(makeScalar("bytes"), false, true);
    const model = makeModel([["payload", prop]]);
    const { fields } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.redact).toBe(true);
    expect(fields[0]!.name).toBe("payload");
  });
});

describe("walkModel_EmptyModel_EmptyFieldList", () => {
  it("model with no properties → empty fields list", () => {
    const model = makeModel([]);
    const { fields, nestedModels } = walkModel(makeProgram(), model, () => {});

    expect(fields).toHaveLength(0);
    expect(nestedModels).toHaveLength(0);
  });
});

describe("walkModel_UnmappedScalar_D2TSP001Loud", () => {
  it("unknown scalar → onError called with D2TSP001 code, field omitted", () => {
    const { prop, redactMap } = makeProp(makeScalar("utcDateTime"));
    const model = makeModel([["createdAt", prop]]);
    const errors: Array<{ code: string; message: string }> = [];

    const { fields } = walkModel(makeProgram(redactMap), model, (code, message) => {
      errors.push({ code, message });
    });

    expect(errors).toHaveLength(1);
    expect(errors[0]!.code).toBe("unmapped-scalar");
    expect(errors[0]!.message).toContain("D2TSP001");
    expect(errors[0]!.message).toContain("utcDateTime");
    // Field is omitted on error.
    expect(fields).toHaveLength(0);
  });
});

describe("walkModel_UnsupportedPropertyType_D2TSP002Loud", () => {
  it("union type → onError called with D2TSP002 code", () => {
    const unionType = { kind: "Union" } as unknown as Type;
    const prop = { type: unionType, optional: false } as unknown as ModelProperty;
    const model = makeModel([["status", prop]]);
    const errors: Array<{ code: string; message: string }> = [];

    const { fields } = walkModel(makeProgram(), model, (code, message) => {
      errors.push({ code, message });
    });

    expect(errors).toHaveLength(1);
    expect(errors[0]!.code).toBe("unsupported-property-type");
    expect(errors[0]!.message).toContain("D2TSP002");
    expect(fields).toHaveLength(0);
  });

  it("enum type → onError called with D2TSP002 code", () => {
    const enumType = { kind: "Enum" } as unknown as Type;
    const prop = { type: enumType, optional: false } as unknown as ModelProperty;
    const model = makeModel([["kind", prop]]);
    const errors: Array<{ code: string }> = [];

    walkModel(makeProgram(), model, (code) => { errors.push({ code }); });

    expect(errors[0]!.code).toBe("unsupported-property-type");
  });
});

describe("walkModel_ArrayElement_UnmappedScalar_D2TSP001", () => {
  it("array of unmapped scalar (utcDateTime[]) → D2TSP001 fired for array element", () => {
    // Build an Array model whose indexer.value is an unmapped scalar.
    const badElementScalar = makeScalar("utcDateTime");
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: badElementScalar },
      properties: new Map(),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(arrayModel as unknown as Type);
    const model = makeModel([["timestamps", prop]]);
    const errors: Array<{ code: string; message: string }> = [];

    const { fields } = walkModel(makeProgram(redactMap), model, (code, message) => {
      errors.push({ code, message });
    });

    expect(errors).toHaveLength(1);
    expect(errors[0]!.code).toBe("unmapped-scalar");
    expect(errors[0]!.message).toContain("D2TSP001");
    expect(errors[0]!.message).toContain("utcDateTime");
    // Field omitted on error.
    expect(fields).toHaveLength(0);
  });
});

describe("walkModel_ArrayElement_UnsupportedKind_D2TSP002", () => {
  it("array of enum (Enum[]) → D2TSP002 for unsupported array element kind", () => {
    const enumType = { kind: "Enum", name: "Status" } as unknown as Type;
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: enumType },
      properties: new Map(),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(arrayModel as unknown as Type);
    const model = makeModel([["statuses", prop]]);
    const errors: Array<{ code: string; message: string }> = [];

    const { fields } = walkModel(makeProgram(redactMap), model, (code, message) => {
      errors.push({ code, message });
    });

    expect(errors).toHaveLength(1);
    expect(errors[0]!.code).toBe("unsupported-property-type");
    expect(errors[0]!.message).toContain("D2TSP002");
    expect(fields).toHaveLength(0);
  });
});

describe("walkModel_NestedModel_OptionalField_NullableType", () => {
  it("optional nested model → csType with ? suffix", () => {
    const inner: Model = {
      kind: "Model",
      name: "Inner",
      properties: new Map<string, ModelProperty>([
        ["id", { type: { kind: "Scalar", name: "string" } as unknown as Scalar, optional: false } as unknown as ModelProperty],
      ]),
    } as unknown as Model;

    // optional=true on the nested model property — exercises optional branch in walkProp.
    const prop = { type: inner, optional: true } as unknown as ModelProperty;
    const model = makeModel([["inner", prop]]);

    const { fields } = walkModel(makeProgram(), model, () => {});

    expect(fields[0]!.csType).toBe("Inner?");
    expect(fields[0]!.optional).toBe(true);
  });
});

describe("walkModel_CollectedNested_OptionalScalarField", () => {
  it("optional scalar inside a nested model → csType with ? suffix inside collectNested", () => {
    // This exercises the `optional ? ${mapping.cs}? : mapping.cs` branch in collectNested.
    const inner: Model = {
      kind: "Model",
      name: "OptionalFieldNested",
      properties: new Map<string, ModelProperty>([
        ["required", { type: { kind: "Scalar", name: "string" } as unknown as Scalar, optional: false } as unknown as ModelProperty],
        ["hint", { type: { kind: "Scalar", name: "string" } as unknown as Scalar, optional: true } as unknown as ModelProperty],
      ]),
    } as unknown as Model;

    const prop = { type: inner, optional: false } as unknown as ModelProperty;
    const model = makeModel([["nested", prop]]);

    const { nestedModels } = walkModel(makeProgram(), model, () => {});

    expect(nestedModels).toHaveLength(1);
    const nestedFields = nestedModels[0]!.fields;
    const requiredField = nestedFields.find((f) => f.name === "required");
    const hintField = nestedFields.find((f) => f.name === "hint");
    expect(requiredField!.csType).toBe("string");
    expect(hintField!.csType).toBe("string?");
    expect(hintField!.optional).toBe(true);
  });
});

describe("walkModel_ArrayElement_NullIndexerKind_UnknownFallback", () => {
  it("array with null indexer value kind → fires D2TSP002 with 'unknown' kind fallback", () => {
    // Exercises `elementType?.kind ?? "unknown"` where elementType exists but kind is undefined.
    const noKindElement = { kind: undefined } as unknown as Scalar;
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: noKindElement },
      properties: new Map(),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(arrayModel as unknown as Type);
    const model = makeModel([["items", prop]]);
    const errors: Array<{ code: string; message: string }> = [];

    const { fields } = walkModel(makeProgram(redactMap), model, (code, message) => {
      errors.push({ code, message });
    });

    expect(errors).toHaveLength(1);
    expect(errors[0]!.code).toBe("unsupported-property-type");
    // The "unknown" fallback appears in the error message when kind is undefined.
    expect(errors[0]!.message).toContain("unknown");
    expect(fields).toHaveLength(0);
  });
});

describe("walkModel_CollectedNested_NonScalarFieldSkipped", () => {
  it("non-scalar field inside a nested model is intentionally skipped (no deep recursion)", () => {
    // Exercises the `false` branch of `if (t.kind === "Scalar")` in collectNested:
    // a nested model field that is another Model (not a scalar) is silently skipped.
    const anotherModel: Model = {
      kind: "Model",
      name: "Deep",
      properties: new Map<string, ModelProperty>(),
    } as unknown as Model;

    const innerModel: Model = {
      kind: "Model",
      name: "Shallow",
      properties: new Map<string, ModelProperty>([
        ["id", { type: { kind: "Scalar", name: "string" } as unknown as Scalar, optional: false } as unknown as ModelProperty],
        // Non-scalar (Model) field — will be skipped inside collectNested.
        ["deep", { type: anotherModel, optional: false } as unknown as ModelProperty],
      ]),
    } as unknown as Model;

    const prop = { type: innerModel, optional: false } as unknown as ModelProperty;
    const model = makeModel([["nested", prop]]);

    const { nestedModels } = walkModel(makeProgram(), model, () => {});

    // The Shallow nested model is registered.
    expect(nestedModels).toHaveLength(1);
    expect(nestedModels[0]!.name).toBe("Shallow");
    // Only the scalar 'id' field was collected; 'deep' (non-scalar) was skipped.
    expect(nestedModels[0]!.fields).toHaveLength(1);
    expect(nestedModels[0]!.fields[0]!.name).toBe("id");
  });
});

describe("walkModel_NestedModel_UnmappedScalarSkippedSilently", () => {
  it("unmapped scalar inside a nested model → field silently skipped, no top-level error", () => {
    // A nested model whose field uses an unmapped scalar.
    // The walker skips it silently in the nested context.
    const innerModel: Model = {
      kind: "Model",
      name: "Inner",
      properties: new Map<string, ModelProperty>([
        ["goodField", { type: makeScalar("string"), optional: false } as unknown as ModelProperty],
        ["badField", { type: makeScalar("utcDateTime"), optional: false } as unknown as ModelProperty],
      ]),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(innerModel as unknown as Type);
    const model = makeModel([["inner", prop]]);
    const errors: string[] = [];

    const { fields, nestedModels } = walkModel(makeProgram(redactMap), model, (_, m) => errors.push(m));

    // No errors propagated to top level.
    expect(errors).toHaveLength(0);
    // The nested model is registered; only the good field passes through.
    expect(nestedModels).toHaveLength(1);
    expect(nestedModels[0]!.fields).toHaveLength(1);
    expect(nestedModels[0]!.fields[0]!.name).toBe("goodField");
    // The outer field for 'inner' uses the nested model name.
    expect(fields[0]!.csType).toBe("Inner");
  });
});
