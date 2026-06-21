// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Cross-language parity test: one walkModel → C# field set ≡ TS field set.
//
// The shared walkModel guarantees that both the C# and TS DTO emitters see
// the same field list. This test pins that the field names and optionality are
// identical across the two emitters for the same walk result.

import { describe, it, expect } from "vitest";
import type { Model, ModelProperty, Program, Scalar } from "@typespec/compiler";
import { D2_REDACT_KEY } from "@d2/typespec-decorators";
import { walkModel } from "../src/lib/model-walk.js";
import { emitCsharpDtos } from "../src/lib/csharp-dto-emitter.js";
import { emitTsDtos } from "../src/lib/ts-dto-emitter.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeScalar(name: string): Scalar {
  return { kind: "Scalar", name } as unknown as Scalar;
}

function makeModel(
  entries: Array<[string, { type: Scalar; optional: boolean }]>,
): Model {
  const properties = new Map<string, ModelProperty>();
  for (const [name, { type, optional }] of entries)
    properties.set(name, { type, optional } as unknown as ModelProperty);
  return {
    kind: "Model",
    name: "TestModel",
    properties,
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
// Parity test: one walkModel → both emitters produce same field names + optionality
// ---------------------------------------------------------------------------

describe("dtoParity_OneWalkModel_CsAndTsFieldSetIdentical", () => {
  it("string + optional boolean fields → same name/optional in C# and TS", () => {
    const model = makeModel([
      ["kid", { type: makeScalar("string"), optional: false }],
      ["hint", { type: makeScalar("string"), optional: true }],
      ["active", { type: makeScalar("boolean"), optional: false }],
    ]);

    const program = makeProgram();
    const errors: string[] = [];
    const { fields } = walkModel(program, model, (_, m) => errors.push(m));

    expect(errors).toHaveLength(0);

    // Emit C# + TS from the SAME walk result.
    const csFiles = emitCsharpDtos(
      "testOp",
      "D2.Test",
      "test.tsp",
      fields,
      [],
      [],
    );
    const tsFile = emitTsDtos("testOp", "test.tsp", fields, [], []);

    // Extract field names from C# (the positional record params).
    const csContent = csFiles[0]!.content;
    const tsContent = tsFile.content;

    // All three field names present in both.
    expect(csContent).toContain("string Kid");
    expect(csContent).toContain("string? Hint"); // optional → T?
    expect(csContent).toContain("bool Active");

    expect(tsContent).toContain("readonly kid: string;");
    expect(tsContent).toContain("readonly hint?: string;"); // optional → ?:
    expect(tsContent).toContain("readonly active: boolean;");

    // Field count matches: walk produced 3 fields, both emitters consumed them.
    expect(fields).toHaveLength(3);
    expect(fields.map((f) => f.name)).toEqual(["kid", "hint", "active"]);
    expect(fields.map((f) => f.optional)).toEqual([false, true, false]);
  });

  it("temporal scalars + optional → same name/type/optionality in C# and TS", () => {
    const model = makeModel([
      ["pastInstant", { type: makeScalar("utcDateTime"), optional: false }],
      ["withOffset", { type: makeScalar("offsetDateTime"), optional: false }],
      ["birthday", { type: makeScalar("plainDate"), optional: false }],
      ["alarmTime", { type: makeScalar("plainTime"), optional: false }],
      ["wallClock", { type: makeScalar("plainDateTime"), optional: false }],
      ["elapsed", { type: makeScalar("duration"), optional: false }],
      ["optionalInstant", { type: makeScalar("utcDateTime"), optional: true }],
    ]);

    const errors: string[] = [];
    const { fields } = walkModel(makeProgram(), model, (_, m) =>
      errors.push(m),
    );
    expect(errors).toHaveLength(0);

    const csFiles = emitCsharpDtos("t", "D2.Test", "t.tsp", fields, [], []);
    const tsFile = emitTsDtos("t", "t.tsp", fields, [], []);
    const cs = csFiles[0]!.content;
    const ts = tsFile.content;

    // Instant-bearing → DateTimeOffset (C#) / string (TS).
    expect(cs).toContain("DateTimeOffset PastInstant");
    expect(cs).toContain("DateTimeOffset WithOffset");
    expect(cs).toContain("DateTimeOffset? OptionalInstant"); // optional → T?
    // Offset-free → string (C#) / string (TS).
    expect(cs).toContain("string Birthday");
    expect(cs).toContain("string WallClock");
    expect(cs).toContain("string Elapsed");

    expect(ts).toContain("readonly pastInstant: string;");
    expect(ts).toContain("readonly optionalInstant?: string;"); // optional → ?:
    expect(ts).toContain("readonly wallClock: string;");

    // Field set + optionality identical across both emitters.
    expect(fields.map((f) => f.name)).toEqual([
      "pastInstant",
      "withOffset",
      "birthday",
      "alarmTime",
      "wallClock",
      "elapsed",
      "optionalInstant",
    ]);
    expect(fields.map((f) => f.optional)).toEqual([
      false,
      false,
      false,
      false,
      false,
      false,
      true,
    ]);
  });

  it("empty model → both emitters produce empty field body", () => {
    const model = makeModel([]);
    const program = makeProgram();
    const { fields } = walkModel(program, model, () => {});

    const csFiles = emitCsharpDtos(
      "empty",
      "D2.Test",
      "test.tsp",
      fields,
      [],
      [],
    );
    const tsFile = emitTsDtos("empty", "test.tsp", fields, [], []);

    // C#: parameterless record.
    expect(csFiles[0]!.content).toContain("public sealed record EmptyInput;");
    // TS: empty interface.
    expect(tsFile.content).toContain("export interface EmptyInput {");
    // No field declarations.
    expect(tsFile.content).not.toContain("readonly ");
  });

  it("enum field → C# enum member-set ≡ TS enum member-set (same wire strings); field type parity", () => {
    // One walkModel feeds both emitters; the SAME walked NestedEnum drives the C#
    // sibling enum + the TS const-object, so the member set + wire strings are
    // identical by construction. This pins that cross-language identity for the
    // [JsonStringEnumMemberName]-vs-const-value case (the load-bearing P-4 claim).
    const e = {
      kind: "Enum",
      name: "AccountKind",
      members: new Map([
        ["internal", { name: "internal", value: "internal" }],
        ["thirdParty", { name: "thirdParty", value: "third-party" }],
      ]),
    } as unknown as Scalar;

    const model = {
      kind: "Model",
      name: "TestModel",
      properties: new Map<string, ModelProperty>([
        [
          "accountKind",
          { type: e, optional: false } as unknown as ModelProperty,
        ],
      ]),
    } as unknown as Model;

    const errors: string[] = [];
    const { fields, nestedEnums } = walkModel(makeProgram(), model, (_, m) =>
      errors.push(m),
    );
    expect(errors).toHaveLength(0);

    const csFiles = emitCsharpDtos(
      "op",
      "D2.Test",
      "test.tsp",
      [],
      fields,
      [],
      [],
      nestedEnums,
    );
    const tsFile = emitTsDtos(
      "op",
      "test.tsp",
      [],
      fields,
      [],
      [],
      nestedEnums,
    );

    const cs = csFiles[1]!.content;
    const ts = tsFile.content;

    // C# member names + the wire attribute carry the SAME wire strings the TS
    // const-object values carry.
    expect(cs).toContain('[JsonStringEnumMemberName("internal")]');
    expect(cs).toContain('[JsonStringEnumMemberName("third-party")]');
    expect(cs).toContain("    Internal,");
    expect(cs).toContain("    ThirdParty,");
    expect(ts).toContain('  Internal: "internal",');
    expect(ts).toContain('  ThirdParty: "third-party",');

    // Field-type parity: C# uses the enum name, TS uses the enum name.
    expect(cs).toContain("AccountKind AccountKind");
    expect(ts).toContain("readonly accountKind: AccountKind;");

    // The member identity (csName + wireValue) is shared — one source.
    expect(nestedEnums[0]!.members.map((m) => [m.csName, m.wireValue])).toEqual(
      [
        ["Internal", "internal"],
        ["ThirdParty", "third-party"],
      ],
    );
  });
});
