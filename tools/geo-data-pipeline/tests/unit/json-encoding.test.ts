// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, it, expect } from "vitest";
import { escapeInvisibles } from "../../src/util/json-encoding.js";

describe("escapeInvisibles", () => {
  it("escapes NBSP (U+00A0) to \\u00a0", () => {
    expect(escapeInvisibles('"a b"')).toBe('"a\\u00a0b"');
  });

  it("escapes NARROW NBSP (U+202F) to \\u202f", () => {
    expect(escapeInvisibles('"x y"')).toBe('"x\\u202fy"');
  });

  it("escapes LRM (U+200E) and RLM (U+200F)", () => {
    expect(escapeInvisibles('"a‎b‏c"')).toBe('"a\\u200eb\\u200fc"');
  });

  it("escapes ZERO WIDTH SPACE (U+200B)", () => {
    expect(escapeInvisibles('"foo​bar"')).toBe('"foo\\u200bbar"');
  });

  it("escapes BOM (U+FEFF)", () => {
    expect(escapeInvisibles('"﻿text"')).toBe('"\\ufefftext"');
  });

  it("escapes all listed invisibles in one pass", () => {
    const input = '"  ​‎"';
    const expected = '"\\u00a0\\u202f\\u200b\\u200e"';
    expect(escapeInvisibles(input)).toBe(expected);
  });

  it("leaves ASCII space and visible characters untouched", () => {
    expect(escapeInvisibles('"hello world"')).toBe('"hello world"');
    expect(escapeInvisibles('"abc 123"')).toBe('"abc 123"');
  });

  it("returns empty string unchanged", () => {
    expect(escapeInvisibles("")).toBe("");
  });

  it("round-trips through JSON.parse (preserves semantic content)", () => {
    const value = "a b c";
    const json = JSON.stringify({ s: value });
    const escaped = escapeInvisibles(json);
    expect(escaped).not.toBe(json); // escape changes the text
    const reparsed = JSON.parse(escaped) as { s: string };
    expect(reparsed.s).toBe(value); // but the parsed value is identical
  });

  it("idempotent on already-escaped output", () => {
    // After one escape pass, the output is plain ASCII; second pass is a no-op.
    const once = escapeInvisibles('"a b"');
    const twice = escapeInvisibles(once);
    expect(twice).toBe(once);
  });

  it("handles multi-line JSON-like input", () => {
    const input = '{\n  "key": "value here"\n}';
    const expected = '{\n  "key": "value\\u00a0here"\n}';
    expect(escapeInvisibles(input)).toBe(expected);
  });
});
