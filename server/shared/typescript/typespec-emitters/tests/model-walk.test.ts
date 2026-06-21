// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for walkModel.
// Every public code path exercised: scalar fields, optional, collections,
// nested models, @d2Redact, empty models, unmapped scalars (D2TSP001),
// unsupported types (D2TSP002), and nested-model deduplication.

import { describe, it, expect } from "vitest";
import type {
  Model,
  ModelProperty,
  Program,
  Scalar,
  Type,
} from "@typespec/compiler";
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
        [
          "kid",
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
      indexer: { value: jwkModel },
      properties: new Map(),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(arrayModel as unknown as Type);
    const model = makeModel([["keys", prop]]);
    const { fields, nestedModels } = walkModel(
      makeProgram(redactMap),
      model,
      () => {},
    );

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
        [
          "kid",
          {
            type: makeScalar("string"),
            optional: false,
          } as unknown as ModelProperty,
        ],
        [
          "n",
          {
            type: makeScalar("string"),
            optional: false,
          } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(inner as unknown as Type);
    const model = makeModel([["jwk", prop]]);
    const { fields, nestedModels } = walkModel(
      makeProgram(redactMap),
      model,
      () => {},
    );

    expect(fields[0]!.csType).toBe("Jwk");
    expect(nestedModels).toHaveLength(1);
    expect(nestedModels[0]!.fields).toHaveLength(2);
  });

  it("same nested model referenced twice → collected once (dedup)", () => {
    const inner: Model = {
      kind: "Model",
      name: "Jwk",
      properties: new Map([
        [
          "kid",
          {
            type: makeScalar("string"),
            optional: false,
          } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;

    const prop1 = { type: inner, optional: false } as unknown as ModelProperty;
    const prop2 = { type: inner, optional: false } as unknown as ModelProperty;
    const model = makeModel([
      ["first", prop1],
      ["second", prop2],
    ]);
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
    const { prop, redactMap } = makeProp(makeScalar("notARealScalar"));
    const model = makeModel([["createdAt", prop]]);
    const errors: Array<{ code: string; message: string }> = [];

    const { fields } = walkModel(
      makeProgram(redactMap),
      model,
      (code, message) => {
        errors.push({ code, message });
      },
    );

    expect(errors).toHaveLength(1);
    expect(errors[0]!.code).toBe("unmapped-scalar");
    expect(errors[0]!.message).toContain("D2TSP001");
    expect(errors[0]!.message).toContain("notARealScalar");
    // Field is omitted on error.
    expect(fields).toHaveLength(0);
  });
});

describe("walkModel_TemporalScalars_ResolveCorrectly", () => {
  it("utcDateTime / offsetDateTime → DateTimeOffset; plain-local + duration → string", () => {
    const cases: Array<[string, string]> = [
      ["utcDateTime", "DateTimeOffset"],
      ["offsetDateTime", "DateTimeOffset"],
      ["plainDate", "string"],
      ["plainTime", "string"],
      ["plainDateTime", "string"],
      ["duration", "string"],
    ];
    for (const [scalar, expectedCs] of cases) {
      const { prop, redactMap } = makeProp(makeScalar(scalar));
      const model = makeModel([["v", prop]]);
      const errors: string[] = [];
      const { fields } = walkModel(makeProgram(redactMap), model, (_, m) =>
        errors.push(m),
      );

      expect(
        errors,
        `scalar '${scalar}' must resolve (no D2TSP001)`,
      ).toHaveLength(0);
      expect(fields[0]!.csType).toBe(expectedCs);
      expect(fields[0]!.tsType).toBe("string");
      expect(fields[0]!.protoType).toBe("string");
    }
  });

  it("optional utcDateTime → DateTimeOffset? (C#) + optional flag", () => {
    const { prop, redactMap } = makeProp(makeScalar("utcDateTime"), true);
    const model = makeModel([["optionalInstant", prop]]);
    const { fields } = walkModel(makeProgram(redactMap), model, () => {});

    expect(fields[0]!.csType).toBe("DateTimeOffset?");
    expect(fields[0]!.optional).toBe(true);
  });

  it("composite (ZonedInstantWire / LocalAnchoredEventWire) → walked as nested via the EXISTING nested branch (no temporal special-case)", () => {
    // The composites are ordinary nested models whose fields are temporal scalars;
    // they flow through the SAME nested-model walk as Jwk — no walker change.
    const zonedInstantWire: Model = {
      kind: "Model",
      name: "ZonedInstantWire",
      properties: new Map<string, ModelProperty>([
        [
          "instant",
          {
            type: makeScalar("utcDateTime"),
            optional: false,
          } as unknown as ModelProperty,
        ],
        [
          "zoneId",
          {
            type: makeScalar("string"),
            optional: false,
          } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;

    const localAnchoredEventWire: Model = {
      kind: "Model",
      name: "LocalAnchoredEventWire",
      properties: new Map<string, ModelProperty>([
        [
          "scheduledLocal",
          {
            type: makeScalar("plainDateTime"),
            optional: false,
          } as unknown as ModelProperty,
        ],
        [
          "ianaZone",
          {
            type: makeScalar("string"),
            optional: false,
          } as unknown as ModelProperty,
        ],
        [
          "nextFireUtc",
          {
            type: makeScalar("utcDateTime"),
            optional: true,
          } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;

    const model = makeModel([
      [
        "zoned",
        { type: zonedInstantWire, optional: false } as unknown as ModelProperty,
      ],
      [
        "schedule",
        {
          type: localAnchoredEventWire,
          optional: false,
        } as unknown as ModelProperty,
      ],
    ]);

    const { fields, nestedModels } = walkModel(makeProgram(), model, () => {});

    // Both composites collected as nested siblings; field types are the model names.
    expect(fields.map((f) => f.csType)).toEqual([
      "ZonedInstantWire",
      "LocalAnchoredEventWire",
    ]);
    expect(nestedModels.map((n) => n.name)).toEqual([
      "ZonedInstantWire",
      "LocalAnchoredEventWire",
    ]);

    const zonedNested = nestedModels.find(
      (n) => n.name === "ZonedInstantWire",
    )!;
    expect(zonedNested.fields.find((f) => f.name === "instant")!.csType).toBe(
      "DateTimeOffset",
    );
    expect(zonedNested.fields.find((f) => f.name === "zoneId")!.csType).toBe(
      "string",
    );

    const laeNested = nestedModels.find(
      (n) => n.name === "LocalAnchoredEventWire",
    )!;
    expect(
      laeNested.fields.find((f) => f.name === "scheduledLocal")!.csType,
    ).toBe("string");
    const nextFire = laeNested.fields.find((f) => f.name === "nextFireUtc")!;
    expect(nextFire.csType).toBe("DateTimeOffset?");
    expect(nextFire.optional).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Enum / string-literal-union recognition (the C-2 SUPPORTED shapes)
// ---------------------------------------------------------------------------

/** Build a synthetic TypeSpec Enum stub with the given members. */
function makeEnum(
  name: string,
  members: Array<{ name: string; value?: string | number }>,
): Type {
  return {
    kind: "Enum",
    name,
    members: new Map(members.map((m) => [m.name, { name: m.name, value: m.value }])),
  } as unknown as Type;
}

/** Build a synthetic TypeSpec Union stub with the given variant types. */
function makeUnion(name: string | undefined, variantTypes: Type[]): Type {
  return {
    kind: "Union",
    name,
    variants: new Map(
      variantTypes.map((t, i) => [Symbol(`v${i}`), { type: t }]),
    ),
  } as unknown as Type;
}

function strLit(value: string): Type {
  return { kind: "String", value } as unknown as Type;
}

function numLit(value: number): Type {
  return { kind: "Number", value } as unknown as Type;
}

const NULL_INTRINSIC = { kind: "Intrinsic", name: "null" } as unknown as Type;

describe("walkModel_NamedEnum_Supported", () => {
  it("S-1 bare-member named enum → enum collected, field type = enum name, proto string", () => {
    const e = makeEnum("KeyKind", [
      { name: "Rsa" },
      { name: "Aes" },
      { name: "Secret" },
    ]);
    const prop = { type: e, optional: false } as unknown as ModelProperty;
    const model = makeModel([["keyKind", prop]]);
    const errors: string[] = [];

    const { fields, nestedEnums } = walkModel(makeProgram(), model, (_, m) =>
      errors.push(m),
    );

    expect(errors).toHaveLength(0);
    expect(fields[0]!.csType).toBe("KeyKind");
    expect(fields[0]!.tsType).toBe("KeyKind");
    expect(fields[0]!.protoType).toBe("string");
    expect(fields[0]!.enumRef!.name).toBe("KeyKind");
    expect(nestedEnums).toHaveLength(1);
    expect(nestedEnums[0]!.members.map((m) => m.csName)).toEqual([
      "Rsa",
      "Aes",
      "Secret",
    ]);
    expect(nestedEnums[0]!.members.every((m) => !m.needsEnumMember)).toBe(true);
  });

  it("S-2 explicit-int named enum → intValue preserved, wireValue is the NAME", () => {
    const e = makeEnum("Level", [
      { name: "Low", value: 0 },
      { name: "Medium", value: 5 },
      { name: "High", value: 10 },
    ]);
    const prop = { type: e, optional: false } as unknown as ModelProperty;
    const { nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["level", prop]]),
      () => {},
    );

    const m = nestedEnums[0]!.members;
    expect(m.map((x) => x.intValue)).toEqual([0, 5, 10]);
    // The wire value is the member NAME (string wire), NOT the int.
    expect(m.map((x) => x.wireValue)).toEqual(["Low", "Medium", "High"]);
    expect(m.every((x) => !x.needsEnumMember)).toBe(true);
  });

  it("optional named enum (S-5) → csType KeyKind?, optional true, enum collected once", () => {
    const e = makeEnum("KeyKind", [{ name: "Rsa" }, { name: "Aes" }]);
    const prop = { type: e, optional: true } as unknown as ModelProperty;
    const { fields, nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["optionalKind", prop]]),
      () => {},
    );

    expect(fields[0]!.csType).toBe("KeyKind?");
    expect(fields[0]!.optional).toBe(true);
    expect(nestedEnums).toHaveLength(1);
  });

  it("array of a supported enum (L-6 positive) → IReadOnlyList<KeyKind>, repeated, proto string", () => {
    const e = makeEnum("KeyKind", [{ name: "Rsa" }]);
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: e },
      properties: new Map(),
    } as unknown as Model;
    const prop = {
      type: arrayModel,
      optional: false,
    } as unknown as ModelProperty;

    const { fields, nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["kinds", prop]]),
      () => {},
    );

    expect(fields[0]!.csType).toBe("IReadOnlyList<KeyKind>");
    expect(fields[0]!.tsType).toBe("readonly KeyKind[]");
    expect(fields[0]!.protoType).toBe("string");
    expect(fields[0]!.repeated).toBe(true);
    expect(nestedEnums).toHaveLength(1);
  });

  it("enum dedup across two fields referencing the SAME enum → collected once", () => {
    const e = makeEnum("KeyKind", [{ name: "Rsa" }]);
    const model = makeModel([
      ["a", { type: e, optional: false } as unknown as ModelProperty],
      ["b", { type: e, optional: false } as unknown as ModelProperty],
    ]);
    const { nestedEnums } = walkModel(makeProgram(), model, () => {});

    expect(nestedEnums).toHaveLength(1);
  });

  it("empty named enum (no members) → loud D2TSP002", () => {
    const e = makeEnum("Empty", []);
    const errors: Array<{ code: string; message: string }> = [];
    const { fields } = walkModel(
      makeProgram(),
      makeModel([["x", { type: e, optional: false } as unknown as ModelProperty]]),
      (code, message) => errors.push({ code, message }),
    );

    expect(errors[0]!.code).toBe("unsupported-property-type");
    expect(errors[0]!.message).toContain("no members");
    expect(fields).toHaveLength(0);
  });
});

describe("walkModel_StringLiteralUnion_Supported", () => {
  it("S-3 named string-literal union → enum, lowercase literal needs no rename", () => {
    const u = makeUnion("Status", [
      strLit("active"),
      strLit("inactive"),
      strLit("pending"),
    ]);
    const { fields, nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["status", { type: u, optional: false } as unknown as ModelProperty]]),
      () => {},
    );

    expect(fields[0]!.csType).toBe("Status");
    expect(fields[0]!.protoType).toBe("string");
    const m = nestedEnums[0]!.members;
    expect(m.map((x) => x.csName)).toEqual(["Active", "Inactive", "Pending"]);
    expect(m.map((x) => x.wireValue)).toEqual(["active", "inactive", "pending"]);
    // PascalCase member differs from lowercase literal → needs the attribute.
    expect(m.every((x) => x.needsEnumMember)).toBe(true);
  });

  it("S-3 non-identifier literal → sanitized member + needsEnumMember true", () => {
    const u = makeUnion("AccountKind", [
      strLit("internal"),
      strLit("third-party"),
    ]);
    const { nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["accountKind", { type: u, optional: false } as unknown as ModelProperty]]),
      () => {},
    );

    const tp = nestedEnums[0]!.members.find((x) => x.wireValue === "third-party")!;
    expect(tp.csName).toBe("ThirdParty");
    expect(tp.needsEnumMember).toBe(true);
  });

  it("S-4 inline anonymous string-literal union → synthetic <Model><Field> enum", () => {
    const u = makeUnion(undefined, [
      strLit("draft"),
      strLit("published"),
      strLit("archived"),
    ]);
    const { fields, nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["inlineState", { type: u, optional: false } as unknown as ModelProperty]]),
      () => {},
    );

    // Model name in the helper is "TestModel" → synthetic "TestModelInlineState".
    expect(fields[0]!.csType).toBe("TestModelInlineState");
    expect(nestedEnums[0]!.name).toBe("TestModelInlineState");
  });

  it("S-6 <literals> | null union → non-null set + optional true", () => {
    const u = makeUnion("Status", [
      strLit("active"),
      strLit("inactive"),
      NULL_INTRINSIC,
    ]);
    const { fields, nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["status", { type: u, optional: false } as unknown as ModelProperty]]),
      () => {},
    );

    // The null variant ⇒ optional; the member set is the non-null literals.
    expect(fields[0]!.optional).toBe(true);
    expect(fields[0]!.csType).toBe("Status?");
    expect(nestedEnums[0]!.members.map((m) => m.wireValue)).toEqual([
      "active",
      "inactive",
    ]);
  });

  it("enum name collision with a DIFFERENT member set → loud D2TSP007", () => {
    const u1 = makeUnion("Status", [strLit("a")]);
    const u2 = makeUnion("Status", [strLit("b")]); // same name, different members
    const errors: Array<{ code: string }> = [];
    walkModel(
      makeProgram(),
      makeModel([
        ["x", { type: u1, optional: false } as unknown as ModelProperty],
        ["y", { type: u2, optional: false } as unknown as ModelProperty],
      ]),
      (code) => errors.push({ code }),
    );

    expect(errors.some((e) => e.code === "unsupported-union-shape")).toBe(true);
  });

  it("enum name collision with a DIFFERENT-LENGTH member set → loud (sameMembers length guard)", () => {
    const u1 = makeUnion("Status", [strLit("a"), strLit("b")]);
    const u2 = makeUnion("Status", [strLit("a")]); // same name, fewer members
    const errors: Array<{ code: string }> = [];
    walkModel(
      makeProgram(),
      makeModel([
        ["x", { type: u1, optional: false } as unknown as ModelProperty],
        ["y", { type: u2, optional: false } as unknown as ModelProperty],
      ]),
      (code) => errors.push({ code }),
    );

    expect(errors.some((e) => e.code === "unsupported-union-shape")).toBe(true);
  });

  it("identical enum reused (SAME member set) across two fields → no collision (dedup)", () => {
    const u1 = makeUnion("Status", [strLit("a"), strLit("b")]);
    const u2 = makeUnion("Status", [strLit("a"), strLit("b")]); // identical
    const errors: string[] = [];
    const { nestedEnums } = walkModel(
      makeProgram(),
      makeModel([
        ["x", { type: u1, optional: false } as unknown as ModelProperty],
        ["y", { type: u2, optional: false } as unknown as ModelProperty],
      ]),
      (_, m) => errors.push(m),
    );

    expect(errors).toHaveLength(0);
    expect(nestedEnums).toHaveLength(1);
  });

  it("leading-digit + all-separator literals → sanitized to valid identifiers", () => {
    // "3d" is one segment → first char uppercased (a digit stays a digit) → "3d"
    // → leading digit → "_3d". "--" is all separators → "_".
    const u = makeUnion("Weird", [strLit("3d"), strLit("--")]);
    const { nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["v", { type: u, optional: false } as unknown as ModelProperty]]),
      () => {},
    );

    const names = nestedEnums[0]!.members.map((m) => m.csName);
    expect(names[0]).toBe("_3d");
    expect(names[1]).toBe("_");
    // The wire values are preserved verbatim (the literal).
    expect(nestedEnums[0]!.members.map((m) => m.wireValue)).toEqual(["3d", "--"]);
    expect(nestedEnums[0]!.members.every((m) => m.needsEnumMember)).toBe(true);
  });
});

describe("walkModel_UnsupportedUnionShape_D2TSP007Loud", () => {
  it("NV-1 mixed-primitive union (string | int32) → D2TSP007", () => {
    const u = makeUnion(undefined, [
      { kind: "Scalar", name: "string" } as unknown as Type,
      { kind: "Scalar", name: "int32" } as unknown as Type,
    ]);
    const errors: Array<{ code: string; message: string }> = [];
    const { fields } = walkModel(
      makeProgram(),
      makeModel([["v", { type: u, optional: false } as unknown as ModelProperty]]),
      (code, message) => errors.push({ code, message }),
    );

    expect(errors[0]!.code).toBe("unsupported-union-shape");
    expect(errors[0]!.message).toContain("D2TSP007");
    expect(fields).toHaveLength(0);
  });

  it("NV-2 numeric-literal-only union (1 | 2 | 3) → D2TSP007", () => {
    const u = makeUnion(undefined, [numLit(1), numLit(2), numLit(3)]);
    const errors: Array<{ code: string }> = [];
    walkModel(
      makeProgram(),
      makeModel([["v", { type: u, optional: false } as unknown as ModelProperty]]),
      (code) => errors.push({ code }),
    );

    expect(errors[0]!.code).toBe("unsupported-union-shape");
  });

  it("NV mixed-literal-kind union (\"a\" | 1) → D2TSP007", () => {
    const u = makeUnion(undefined, [strLit("a"), numLit(1)]);
    const errors: Array<{ code: string }> = [];
    walkModel(
      makeProgram(),
      makeModel([["v", { type: u, optional: false } as unknown as ModelProperty]]),
      (code) => errors.push({ code }),
    );

    expect(errors[0]!.code).toBe("unsupported-union-shape");
  });

  it("NV-3 model-variant union (Circle | Square) → loud", () => {
    const circle = { kind: "Model", name: "Circle", properties: new Map() } as unknown as Type;
    const square = { kind: "Model", name: "Square", properties: new Map() } as unknown as Type;
    const u = makeUnion(undefined, [circle, square]);
    const errors: Array<{ code: string }> = [];
    walkModel(
      makeProgram(),
      makeModel([["shape", { type: u, optional: false } as unknown as ModelProperty]]),
      (code) => errors.push({ code }),
    );

    expect(errors).toHaveLength(1);
    expect(errors[0]!.code).toBe("unsupported-union-shape");
  });

  it("null-only union → D2TSP007 (no string literals)", () => {
    const u = makeUnion(undefined, [NULL_INTRINSIC]);
    const errors: Array<{ code: string; message: string }> = [];
    walkModel(
      makeProgram(),
      makeModel([["v", { type: u, optional: false } as unknown as ModelProperty]]),
      (code, message) => errors.push({ code, message }),
    );

    expect(errors[0]!.code).toBe("unsupported-union-shape");
    expect(errors[0]!.message).toContain("no string-literal variants");
  });

  it("malformed union (no variants map) → loud, no crash", () => {
    const u = { kind: "Union" } as unknown as Type;
    const errors: Array<{ code: string }> = [];
    const { fields } = walkModel(
      makeProgram(),
      makeModel([["v", { type: u, optional: false } as unknown as ModelProperty]]),
      (code) => errors.push({ code }),
    );

    expect(errors[0]!.code).toBe("unsupported-union-shape");
    expect(fields).toHaveLength(0);
  });

  it("malformed enum (no members map) → loud D2TSP002, no crash", () => {
    const e = { kind: "Enum", name: "Bad" } as unknown as Type;
    const errors: Array<{ code: string }> = [];
    const { fields } = walkModel(
      makeProgram(),
      makeModel([["v", { type: e, optional: false } as unknown as ModelProperty]]),
      (code) => errors.push({ code }),
    );

    expect(errors[0]!.code).toBe("unsupported-property-type");
    expect(fields).toHaveLength(0);
  });

  it("a bare intrinsic property (not scalar/model/enum/union) → loud D2TSP002 fallthrough", () => {
    const intrinsic = { kind: "Intrinsic", name: "unknown" } as unknown as Type;
    const errors: Array<{ code: string; message: string }> = [];
    const { fields } = walkModel(
      makeProgram(),
      makeModel([
        ["v", { type: intrinsic, optional: false } as unknown as ModelProperty],
      ]),
      (code, message) => errors.push({ code, message }),
    );

    expect(errors[0]!.code).toBe("unsupported-property-type");
    expect(errors[0]!.message).toContain("D2TSP002");
    expect(errors[0]!.message).toContain("Intrinsic");
    expect(fields).toHaveLength(0);
  });

  it("NV-5 array of a mixed union element → loud", () => {
    const u = makeUnion(undefined, [
      { kind: "Scalar", name: "string" } as unknown as Type,
      { kind: "Scalar", name: "int32" } as unknown as Type,
    ]);
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: u },
      properties: new Map(),
    } as unknown as Model;
    const errors: Array<{ code: string }> = [];
    const { fields } = walkModel(
      makeProgram(),
      makeModel([["vs", { type: arrayModel, optional: false } as unknown as ModelProperty]]),
      (code) => errors.push({ code }),
    );

    expect(errors[0]!.code).toBe("unsupported-union-shape");
    expect(fields).toHaveLength(0);
  });

  it("array of a string-literal union element → supported", () => {
    const u = makeUnion("Tag", [strLit("a"), strLit("b")]);
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: u },
      properties: new Map(),
    } as unknown as Model;
    const { fields, nestedEnums } = walkModel(
      makeProgram(),
      makeModel([["tags", { type: arrayModel, optional: false } as unknown as ModelProperty]]),
      () => {},
    );

    expect(fields[0]!.csType).toBe("IReadOnlyList<Tag>");
    expect(fields[0]!.repeated).toBe(true);
    expect(nestedEnums[0]!.name).toBe("Tag");
  });

  it("array of a string-literal union with a null variant → loud (no nullable array element)", () => {
    const u = makeUnion("Tag", [strLit("a"), NULL_INTRINSIC]);
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: u },
      properties: new Map(),
    } as unknown as Model;
    const errors: Array<{ code: string; message: string }> = [];
    walkModel(
      makeProgram(),
      makeModel([["tags", { type: arrayModel, optional: false } as unknown as ModelProperty]]),
      (code, message) => errors.push({ code, message }),
    );

    expect(errors[0]!.code).toBe("unsupported-union-shape");
    expect(errors[0]!.message).toContain("array-element position");
  });
});

describe("walkModel_ArrayElement_UnmappedScalar_D2TSP001", () => {
  it("array of unmapped scalar (notARealScalar[]) → D2TSP001 fired for array element", () => {
    // Build an Array model whose indexer.value is an unmapped scalar.
    const badElementScalar = makeScalar("notARealScalar");
    const arrayModel: Model = {
      kind: "Model",
      name: "Array",
      indexer: { value: badElementScalar },
      properties: new Map(),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(arrayModel as unknown as Type);
    const model = makeModel([["timestamps", prop]]);
    const errors: Array<{ code: string; message: string }> = [];

    const { fields } = walkModel(
      makeProgram(redactMap),
      model,
      (code, message) => {
        errors.push({ code, message });
      },
    );

    expect(errors).toHaveLength(1);
    expect(errors[0]!.code).toBe("unmapped-scalar");
    expect(errors[0]!.message).toContain("D2TSP001");
    expect(errors[0]!.message).toContain("notARealScalar");
    // Field omitted on error.
    expect(fields).toHaveLength(0);
  });
});

describe("walkModel_ArrayElement_EmptyEnum_D2TSP002", () => {
  it("array of an empty enum (no members) → D2TSP002 (an array of a VALID enum is supported elsewhere)", () => {
    // A members-less enum element is malformed → loud (the no-members guard).
    // A well-formed enum array element is covered by the S-1 array-of-enum test.
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

    const { fields } = walkModel(
      makeProgram(redactMap),
      model,
      (code, message) => {
        errors.push({ code, message });
      },
    );

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
        [
          "id",
          {
            type: { kind: "Scalar", name: "string" } as unknown as Scalar,
            optional: false,
          } as unknown as ModelProperty,
        ],
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
        [
          "required",
          {
            type: { kind: "Scalar", name: "string" } as unknown as Scalar,
            optional: false,
          } as unknown as ModelProperty,
        ],
        [
          "hint",
          {
            type: { kind: "Scalar", name: "string" } as unknown as Scalar,
            optional: true,
          } as unknown as ModelProperty,
        ],
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

    const { fields } = walkModel(
      makeProgram(redactMap),
      model,
      (code, message) => {
        errors.push({ code, message });
      },
    );

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
        [
          "id",
          {
            type: { kind: "Scalar", name: "string" } as unknown as Scalar,
            optional: false,
          } as unknown as ModelProperty,
        ],
        // Non-scalar (Model) field — will be skipped inside collectNested.
        [
          "deep",
          { type: anotherModel, optional: false } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;

    const prop = {
      type: innerModel,
      optional: false,
    } as unknown as ModelProperty;
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
        [
          "goodField",
          {
            type: makeScalar("string"),
            optional: false,
          } as unknown as ModelProperty,
        ],
        [
          "badField",
          {
            type: makeScalar("notARealScalar"),
            optional: false,
          } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;

    const { prop, redactMap } = makeProp(innerModel as unknown as Type);
    const model = makeModel([["inner", prop]]);
    const errors: string[] = [];

    const { fields, nestedModels } = walkModel(
      makeProgram(redactMap),
      model,
      (_, m) => errors.push(m),
    );

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
