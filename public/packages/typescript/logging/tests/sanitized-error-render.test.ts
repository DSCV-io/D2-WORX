// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { sanitizedErrorRender } from "../src/sanitized-error-render.js";

describe("sanitizedErrorRender", () => {
  it("extracts name + firstFrame from a real Error", () => {
    function inner() {
      throw new TypeError("password=secret leaked");
    }
    try {
      inner();
    } catch (e) {
      const r = sanitizedErrorRender(e);
      expect(r.name).toBe("TypeError");
      expect(r.firstFrame).toMatch(/^at /);
      // message contents must NEVER appear in the render shape.
      expect(JSON.stringify(r)).not.toContain("password=secret");
    }
  });

  it("uses default 'Error' name when name is empty", () => {
    const e = new Error("x");
    e.name = "";
    expect(sanitizedErrorRender(e).name).toBe("Error");
  });

  it("returns undefined firstFrame when no stack available", () => {
    const e = new Error("x");
    e.stack = undefined;
    expect(sanitizedErrorRender(e).firstFrame).toBeUndefined();
  });

  it("returns undefined firstFrame when stack has no 'at' lines", () => {
    const e = new Error("x");
    e.stack = "Error: x\n  not-a-frame\n  another-non-frame";
    expect(sanitizedErrorRender(e).firstFrame).toBeUndefined();
  });

  it.each([
    ["string", "boom", "string"],
    ["number", 42, "number"],
    ["null", null, "object"],
    ["undefined", undefined, "undefined"],
    ["object", {}, "object"],
  ])("non-Error %s → typeof name", (_label, val, expectedName) => {
    const r = sanitizedErrorRender(val);
    expect(r.name).toBe(expectedName);
    expect(r.firstFrame).toBeUndefined();
  });
});
