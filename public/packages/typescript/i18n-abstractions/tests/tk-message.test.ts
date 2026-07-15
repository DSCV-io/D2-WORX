// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { TkMessageWireShape } from "../src/generated/tk-message.g.js";
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

  it("emits wire-shape property names from the spec-derived catalog", () => {
    // Pin the property names that ship on the wire. If the spec changes
    // these constants, the .NET side will drift to match, but this assertion
    // pins the TS-side property names to the spec-derived constants so the
    // tk() builder never accidentally emits ad-hoc property names.
    const m = tk("TK.X", { p: 1 });
    expect(Object.keys(m).sort()).toEqual(
      [TkMessageWireShape.KEY, TkMessageWireShape.PARAMS].sort(),
    );
  });

  it("wire-shape catalog pins KEY = 'key'", () => {
    expect(TkMessageWireShape.KEY).toBe("key");
  });

  it("wire-shape catalog pins PARAMS = 'params'", () => {
    expect(TkMessageWireShape.PARAMS).toBe("params");
  });
});
