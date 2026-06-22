// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Direct-unit tests for the native-TypeSpec model-graph resolver
// (src/predicate-model-walk.ts). These build lightweight mock Model / Type
// graphs and call walkPredicateModel directly so V8 sees the source file —
// the compile-host tests in result-predicate-validate.test.ts exercise the
// built dist/ module and prove the end-to-end $onValidate wiring; THESE tests
// pin every branch of the resolver.
//
// Rejection assertions are non-vacuous: each asserts the returned error array
// contains the expected code (an empty array would fail the .some()).

import { describe, it, expect } from "vitest";
import { walkPredicateModel } from "../src/predicate-model-walk.js";
import { parseResultPredicate } from "../src/result-predicate-dsl.js";
import type { Model, Program, Type } from "@typespec/compiler";
import type { PredicateNode } from "../src/result-predicate-dsl.js";

// ----------------------------------------------------------------
// Mock model-graph builders (native @typespec/compiler Type shapes)
// ----------------------------------------------------------------

function scalar(name: string): Type {
  return { kind: "Scalar", name } as unknown as Type;
}

function arrayOf(element: Type): Type {
  return {
    kind: "Model",
    name: "Array",
    indexer: { value: element },
  } as unknown as Type;
}

/** A model with the given { propName: { type, optional } } properties. */
function model(
  name: string,
  props: Record<string, { type: Type; optional?: boolean }>,
): Model {
  const properties = new Map<string, unknown>();
  for (const [propName, spec] of Object.entries(props))
    properties.set(propName, {
      type: spec.type,
      optional: spec.optional ?? false,
    });

  return { kind: "Model", name, properties } as unknown as Model;
}

const fakeProgram = {} as unknown as Program;

function root(expr: string): PredicateNode {
  const r = parseResultPredicate(expr);
  if (!r.ok) throw new Error(`fixture predicate failed to parse: ${expr}`);
  return r.root;
}

function walk(outputModel: Model | undefined, expr: string): readonly string[] {
  return walkPredicateModel(fakeProgram, outputModel, root(expr)).map(
    (e) => e.code,
  );
}

// A reusable PlaceOrder-shaped graph.
const OrderItem = model("OrderItem", {
  itemId: { type: scalar("string") },
  status: { type: scalar("string") },
  quantity: { type: scalar("int32") },
});
const VendorInfo = model("VendorInfo", { tier: { type: scalar("string") } });
const PlaceOrderOutput = model("PlaceOrderOutput", {
  orderId: { type: scalar("string") },
  items: { type: arrayOf(OrderItem) },
  retryCodes: { type: arrayOf(scalar("string")) },
  flags: { type: arrayOf(scalar("boolean")) },
  vendor: { type: VendorInfo, optional: true },
});

// ----------------------------------------------------------------
// Valid — no errors
// ----------------------------------------------------------------

describe("walk_EnvelopeOnly_NoWork", () => {
  it("returns no errors for an envelope-only predicate (no data path)", () => {
    expect(
      walk(PlaceOrderOutput, 'result.category == "infrastructure_unavailable"'),
    ).toEqual([]);
  });

  it("returns no errors for an envelope-only boolean tree", () => {
    expect(
      walk(
        PlaceOrderOutput,
        "result.success == false && result.statusCode == 503",
      ),
    ).toEqual([]);
  });
});

describe("walk_FlatScalarPath_Ok", () => {
  it('accepts result.data.orderId == "X" (scalar string terminal)', () => {
    expect(walk(PlaceOrderOutput, 'result.data.orderId == "X"')).toEqual([]);
  });
});

describe("walk_NestedNullablePath_Ok", () => {
  it("accepts result.data.vendor.tier (optional vendor → nullable boundary, no error)", () => {
    expect(
      walk(PlaceOrderOutput, 'result.data.vendor.tier == "TRIAL"'),
    ).toEqual([]);
  });
});

describe("walk_Count_Ok", () => {
  it("accepts result.data.items.count == 0", () => {
    expect(walk(PlaceOrderOutput, "result.data.items.count == 0")).toEqual([]);
  });
});

describe("walk_AnyQuantifier_Ok", () => {
  it('accepts result.data.items.any(i => i.status == "PENDING")', () => {
    expect(
      walk(
        PlaceOrderOutput,
        'result.data.items.any(i => i.status == "PENDING")',
      ),
    ).toEqual([]);
  });

  it("accepts an int-typed element field comparison", () => {
    expect(
      walk(PlaceOrderOutput, "result.data.items.any(i => i.quantity == 5)"),
    ).toEqual([]);
  });

  it("accepts a boolean tree inside the sub-predicate", () => {
    expect(
      walk(
        PlaceOrderOutput,
        'result.data.items.any(i => i.status == "A" || i.quantity == 1)',
      ),
    ).toEqual([]);
  });
});

describe("walk_ContainsScalarElement_Ok", () => {
  it('accepts result.data.retryCodes.contains("X") (string element)', () => {
    expect(
      walk(PlaceOrderOutput, 'result.data.retryCodes.contains("X")'),
    ).toEqual([]);
  });

  it("accepts result.data.flags.contains(true) (bool element)", () => {
    expect(walk(PlaceOrderOutput, "result.data.flags.contains(true)")).toEqual(
      [],
    );
  });
});

describe("walk_NonModelOutput_EnvelopeStillOk", () => {
  it("returns no errors for an envelope-only predicate even with no output model", () => {
    expect(walk(undefined, "result.success == false")).toEqual([]);
  });
});

// ----------------------------------------------------------------
// Invalid — unknown output field
// ----------------------------------------------------------------

describe("walk_UnknownOutputField_Reject", () => {
  it("flags result.data.bogus as unknown-output-field", () => {
    expect(walk(PlaceOrderOutput, "result.data.bogus == 1")).toContain(
      "resilience-predicate-unknown-output-field",
    );
  });

  it("flags a bad mid-path segment result.data.vendor.bogus", () => {
    expect(walk(PlaceOrderOutput, 'result.data.vendor.bogus == "x"')).toContain(
      "resilience-predicate-unknown-output-field",
    );
  });

  it("flags a field access THROUGH a scalar (result.data.orderId.bogus)", () => {
    // orderId resolves to a scalar; a further field segment cannot exist.
    expect(
      walk(PlaceOrderOutput, 'result.data.orderId.bogus == "x"'),
    ).toContain("resilience-predicate-unknown-output-field");
  });

  it("flags any result.data.* path when the output model is undefined", () => {
    expect(walk(undefined, "result.data.x == 1")).toContain(
      "resilience-predicate-unknown-output-field",
    );
  });
});

// ----------------------------------------------------------------
// Invalid — array accessor on a non-collection
// ----------------------------------------------------------------

describe("walk_NotACollection_Reject", () => {
  it("flags .count on a scalar (result.data.orderId.count)", () => {
    expect(walk(PlaceOrderOutput, "result.data.orderId.count == 0")).toContain(
      "resilience-predicate-not-a-collection",
    );
  });

  it("flags .any on a nested model (result.data.vendor.any(...))", () => {
    expect(
      walk(PlaceOrderOutput, 'result.data.vendor.any(v => v.tier == "X")'),
    ).toContain("resilience-predicate-not-a-collection");
  });
});

// ----------------------------------------------------------------
// Invalid — unknown element field
// ----------------------------------------------------------------

describe("walk_UnknownElementField_Reject", () => {
  it('flags result.data.items.any(i => i.bogus == "x")', () => {
    expect(
      walk(PlaceOrderOutput, 'result.data.items.any(i => i.bogus == "x")'),
    ).toContain("resilience-predicate-unknown-element-field");
  });
});

// ----------------------------------------------------------------
// Invalid — type mismatch (model-dependent)
// ----------------------------------------------------------------

describe("walk_TerminalTypeMismatch_Reject", () => {
  it("flags a scalar string terminal compared with an int (result.data.orderId == 5)", () => {
    expect(walk(PlaceOrderOutput, "result.data.orderId == 5")).toContain(
      "resilience-predicate-type-mismatch",
    );
  });

  it("flags an int element field compared with a string in a sub-predicate", () => {
    expect(
      walk(PlaceOrderOutput, 'result.data.items.any(i => i.quantity == "x")'),
    ).toContain("resilience-predicate-type-mismatch");
  });

  it("flags contains(5) on a string-element collection", () => {
    expect(
      walk(PlaceOrderOutput, "result.data.retryCodes.contains(5)"),
    ).toContain("resilience-predicate-type-mismatch");
  });
});

// ----------------------------------------------------------------
// Non-comparable scalar terminals — no type-mismatch raised
// ----------------------------------------------------------------

describe("walk_NonComparableScalarTerminal_NoMismatch", () => {
  it("does not flag a float terminal (no literal kind maps) as a mismatch", () => {
    const m = model("Out", { ratio: { type: scalar("float64") } });
    // float64 has no literal kind → the walk records nothing (model-free parser
    // already constrained envelope types; data floats are simply not checked).
    expect(walk(m, "result.data.ratio == 5")).toEqual([]);
  });

  it("does not flag contains on a non-scalar element collection", () => {
    const m = model("Out", { rows: { type: arrayOf(OrderItem) } });
    // contains on a model-element collection: element is not a Scalar → no
    // literal-type check (the parser permitted it; emission handles the rest).
    expect(walk(m, 'result.data.rows.contains("x")')).toEqual([]);
  });
});

// ----------------------------------------------------------------
// Bool-tree + standalone-boolean-access recursion at the top level
// ----------------------------------------------------------------

describe("walk_DataPathInList_Ok", () => {
  it('accepts result.data.orderId in ("A", "B") — an in-list on a data path', () => {
    // Exercises the Array.isArray(rhs) branch for a data-path comparison.
    expect(walk(PlaceOrderOutput, 'result.data.orderId in ("A", "B")')).toEqual(
      [],
    );
  });
});

describe("walk_AnyOnScalarCollection_ElementNotModel", () => {
  it("flags an element-field path inside .any over a scalar collection (no element model)", () => {
    // retryCodes is string[]; .any(c => c.foo == 1) — the element is a Scalar, so
    // the element model is undefined → the sub-predicate field is unknown.
    expect(
      walk(PlaceOrderOutput, 'result.data.retryCodes.any(c => c.foo == "x")'),
    ).toContain("resilience-predicate-unknown-element-field");
  });
});

describe("walk_NestedModelTerminal_NoMismatch", () => {
  it("does not flag a nested-model terminal comparison (result.data.vendor == ...)", () => {
    // vendor resolves to a Model (not a Scalar) → checkTerminalScalar returns
    // without a type-mismatch (the comparison literal is not checked against a model).
    expect(walk(PlaceOrderOutput, 'result.data.vendor == "x"')).toEqual([]);
  });
});

describe("walk_TopLevelBoolAndBooleanAccess", () => {
  it("walks both sides of a top-level && (one good, one bad)", () => {
    expect(
      walk(
        PlaceOrderOutput,
        'result.data.orderId == "X" && result.data.bogus == 1',
      ),
    ).toContain("resilience-predicate-unknown-output-field");
  });

  it("walks a standalone boolean-access path at the top level (any over items)", () => {
    expect(
      walk(PlaceOrderOutput, 'result.data.items.any(i => i.bogus == "x")'),
    ).toContain("resilience-predicate-unknown-element-field");
  });
});
