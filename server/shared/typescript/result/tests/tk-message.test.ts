// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { tk } from "../src/tk-message.js";

describe("tk()", () => {
  it("returns key-only message when no params", () => {
    expect(tk("TK.X")).toEqual({ key: "TK.X" });
  });

  it("returns key + params when params supplied", () => {
    expect(tk("TK.X", { name: "Alice" })).toEqual({
      key: "TK.X",
      params: { name: "Alice" },
    });
  });

  it("explicit empty params object preserved", () => {
    expect(tk("TK.X", {})).toEqual({ key: "TK.X", params: {} });
  });
});
