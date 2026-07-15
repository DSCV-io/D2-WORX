// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { StringBuilder } from "../src/lib/string-builder.js";

describe("StringBuilder", () => {
  it("emits indented lines", () => {
    const sb = new StringBuilder();
    sb.appendLine("class X {");
    sb.increaseIndent();
    sb.appendLine('field: "a",');
    sb.appendLine();
    sb.appendLine("nested: {");
    sb.increaseIndent();
    sb.appendLine("inner: 1,");
    sb.decreaseIndent();
    sb.appendLine("},");
    sb.decreaseIndent();
    sb.appendLine("}");
    expect(sb.toString()).toBe(
      [
        "class X {",
        '  field: "a",',
        "",
        "  nested: {",
        "    inner: 1,",
        "  },",
        "}",
      ].join("\n"),
    );
  });

  it("rejects decreaseIndent below zero", () => {
    const sb = new StringBuilder();
    expect(() => sb.decreaseIndent()).toThrow(RangeError);
  });

  it("appendLine() with no arg adds blank line", () => {
    const sb = new StringBuilder();
    sb.appendLine("a");
    sb.appendLine();
    sb.appendLine("b");
    expect(sb.toString()).toBe("a\n\nb");
  });

  it("custom indent size respected", () => {
    const sb = new StringBuilder(4);
    sb.appendLine("a");
    sb.increaseIndent();
    sb.appendLine("b");
    expect(sb.toString()).toBe("a\n    b");
  });
});
