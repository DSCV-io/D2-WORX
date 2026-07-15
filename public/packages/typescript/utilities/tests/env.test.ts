// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { parseEnvArray } from "../src/env.js";

describe("parseEnvArray", () => {
  it("reads dense indexed env-var array", () => {
    const env = {
      MY_PREFIX__0: "alpha",
      MY_PREFIX__1: "bravo",
      MY_PREFIX__2: "charlie",
    };
    expect(parseEnvArray("MY_PREFIX", env)).toEqual([
      "alpha",
      "bravo",
      "charlie",
    ]);
  });

  it("returns empty when no entries", () => {
    expect(parseEnvArray("MISSING", {})).toEqual([]);
  });

  it("stops at first gap (matches .NET IConfiguration semantics)", () => {
    const env = {
      P__0: "a",
      P__1: "b",
      P__3: "should-be-ignored",
    };
    expect(parseEnvArray("P", env)).toEqual(["a", "b"]);
  });

  it("ignores non-prefixed keys", () => {
    const env = {
      P__0: "a",
      OTHER__0: "x",
    };
    expect(parseEnvArray("P", env)).toEqual(["a"]);
  });

  it.each(["", "   "])("throws on empty prefix %j", (prefix) => {
    expect(() => parseEnvArray(prefix, {})).toThrow(RangeError);
  });
});
