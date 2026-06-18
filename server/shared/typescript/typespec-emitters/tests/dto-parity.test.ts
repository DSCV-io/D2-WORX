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
});
