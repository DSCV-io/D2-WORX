// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { DiagnosticIds } from "../src/lib/diagnostics.js";
import {
  emitWireShape,
  validateWireShapeSpec,
  type WireShapeSpec,
} from "../src/wire-shape-emit.js";

const tkMessageSpec: WireShapeSpec = {
  properties: [
    { constName: "KEY", value: "key", doc: "Key property." },
    { constName: "PARAMS", value: "params", doc: "Params property." },
  ],
};

const inputErrorSpec: WireShapeSpec = {
  properties: [
    { constName: "FIELD", value: "field", doc: "Field property." },
    { constName: "ERRORS", value: "errors", doc: "Errors property." },
  ],
};

describe("validateWireShapeSpec", () => {
  it("happy path returns no error diagnostics", () => {
    const v = validateWireShapeSpec(tkMessageSpec);
    expect(v.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
  });

  it("flags invalid constName via D2WS004", () => {
    const v = validateWireShapeSpec({
      properties: [{ constName: "lowerCase", value: "x", doc: "doc" }],
    });
    expect(v.diagnostics[0]?.id).toBe(DiagnosticIds.WS_INVALID_CONST_NAME);
  });

  it("flags duplicate constName via D2WS002", () => {
    const v = validateWireShapeSpec({
      properties: [
        { constName: "KEY", value: "key", doc: "first" },
        { constName: "KEY", value: "keytwo", doc: "second" },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe(
      DiagnosticIds.WS_DUPLICATE_PROPERTY_CONST_NAME,
    );
  });

  it("flags duplicate wire value via D2WS003", () => {
    const v = validateWireShapeSpec({
      properties: [
        { constName: "KEY_A", value: "k", doc: "first" },
        { constName: "KEY_B", value: "k", doc: "second" },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe(
      DiagnosticIds.WS_DUPLICATE_PROPERTY_VALUE,
    );
  });
});

describe("emitWireShape", () => {
  it("emits the tk-message catalog with KEY + PARAMS constants", () => {
    const result = emitWireShape(tkMessageSpec, {
      specRelativePath: "contracts/tk-message/tk-message.spec.json",
      catalogName: "TkMessageWireShape",
      catalogDescription: "TKMessage",
    });

    expect(result.diagnostics.filter((d) => d.severity === "error")).toEqual(
      [],
    );
    expect(result.source).toContain("export const TkMessageWireShape = {");
    expect(result.source).toContain('KEY: "key",');
    expect(result.source).toContain('PARAMS: "params",');
    expect(result.source).toContain("} as const;");
  });

  it("emits the input-error catalog with FIELD + ERRORS constants", () => {
    const result = emitWireShape(inputErrorSpec, {
      specRelativePath: "contracts/input-error/input-error.spec.json",
      catalogName: "InputErrorWireShape",
      catalogDescription: "InputError",
    });

    expect(result.diagnostics.filter((d) => d.severity === "error")).toEqual(
      [],
    );
    expect(result.source).toContain("export const InputErrorWireShape = {");
    expect(result.source).toContain('FIELD: "field",');
    expect(result.source).toContain('ERRORS: "errors",');
  });

  it("preserves spec order of properties", () => {
    // Out-of-order constNames in the spec; emit must preserve that order.
    const spec: WireShapeSpec = {
      properties: [
        { constName: "Z", value: "z", doc: "z doc" },
        { constName: "A", value: "a", doc: "a doc" },
      ],
    };
    const result = emitWireShape(spec, {
      specRelativePath: "contracts/x/x.spec.json",
      catalogName: "X",
      catalogDescription: "X",
    });

    const zPos = result.source.indexOf("Z:");
    const aPos = result.source.indexOf("A:");
    expect(zPos).toBeLessThan(aPos);
  });

  it("emits empty source string when validation errors are present", () => {
    const result = emitWireShape(
      {
        properties: [{ constName: "badName", value: "v", doc: "d" }],
      },
      {
        specRelativePath: "contracts/x/x.spec.json",
        catalogName: "X",
        catalogDescription: "X",
      },
    );

    expect(result.source).toBe("");
    expect(result.diagnostics.some((d) => d.severity === "error")).toBe(true);
  });

  it("running twice with identical input produces identical source", () => {
    const opts = {
      specRelativePath: "contracts/tk-message/tk-message.spec.json",
      catalogName: "TkMessageWireShape",
      catalogDescription: "TKMessage",
    };
    const first = emitWireShape(tkMessageSpec, opts);
    const second = emitWireShape(tkMessageSpec, opts);
    expect(second.source).toBe(first.source);
  });

  it("escapes special characters in doc text via JSDoc escaping", () => {
    const spec: WireShapeSpec = {
      properties: [
        {
          constName: "X",
          value: "x",
          // Embedded comment-end sequence must be escaped so the
          // generated JSDoc remains valid (otherwise the comment
          // terminates early and breaks the .g.ts file).
          doc: "Has */ in the middle.",
        },
      ],
    };
    const result = emitWireShape(spec, {
      specRelativePath: "contracts/x/x.spec.json",
      catalogName: "X",
      catalogDescription: "X",
    });
    expect(result.source).toContain("*\\/");
  });

  it("escapes special characters in string literal values", () => {
    const spec: WireShapeSpec = {
      properties: [{ constName: "X", value: 'a"b\\c', doc: "d" }],
    };
    const result = emitWireShape(spec, {
      specRelativePath: "contracts/x/x.spec.json",
      catalogName: "X",
      catalogDescription: "X",
    });
    expect(result.source).toContain('X: "a\\"b\\\\c",');
  });
});
