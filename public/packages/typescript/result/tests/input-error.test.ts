// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { InputErrorWireShape } from "../src/input-error.g.js";
import { inputError } from "../src/input-error.js";

describe("inputError()", () => {
  it("constructs the wire-shape", () => {
    const e = inputError("email", [{ key: "TK.X" }]);
    expect(e).toEqual({ field: "email", errors: [{ key: "TK.X" }] });
  });

  it("emits wire-shape property names from the spec-derived catalog", () => {
    // Pin the property names that ship on the wire — the inputError()
    // builder emits `field` and `errors` byte-for-byte against the spec
    // catalog (single object shape).
    const e = inputError("email", [{ key: "TK.X" }]);
    expect(Object.keys(e).sort()).toEqual(
      [InputErrorWireShape.FIELD, InputErrorWireShape.ERRORS].sort(),
    );
  });

  it("wire-shape catalog pins FIELD = 'field'", () => {
    expect(InputErrorWireShape.FIELD).toBe("field");
  });

  it("wire-shape catalog pins ERRORS = 'errors'", () => {
    expect(InputErrorWireShape.ERRORS).toBe("errors");
  });
});
