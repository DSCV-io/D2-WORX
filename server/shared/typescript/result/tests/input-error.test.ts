// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { inputError } from "../src/input-error.js";

describe("inputError()", () => {
  it("constructs the wire-shape", () => {
    const e = inputError("email", [{ key: "TK.X" }]);
    expect(e).toEqual({ field: "email", errors: [{ key: "TK.X" }] });
  });
});
