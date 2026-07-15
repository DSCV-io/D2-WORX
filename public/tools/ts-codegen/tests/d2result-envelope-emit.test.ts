// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { DiagnosticIds } from "../src/lib/diagnostics.js";
import {
  emitD2ResultEnvelope,
  validateD2ResultEnvelopeSpec,
  type D2ResultEnvelopeSpec,
} from "../src/d2result-envelope-emit.js";

const validSpec: D2ResultEnvelopeSpec = {
  fields: [
    { constName: "SUCCESS", value: "success", doc: "Success flag." },
    { constName: "DATA", value: "data", doc: "Data payload." },
    { constName: "MESSAGES", value: "messages", doc: "Translation messages." },
    {
      constName: "INPUT_ERRORS",
      value: "inputErrors",
      doc: "Per-field validation errors.",
    },
    {
      constName: "ERROR_CODE",
      value: "errorCode",
      doc: "Standardized error code.",
    },
    { constName: "TRACE_ID", value: "traceId", doc: "W3C trace id." },
    { constName: "STATUS_CODE", value: "statusCode", doc: "HTTP status code." },
  ],
};

describe("validateD2ResultEnvelopeSpec", () => {
  it("happy path returns no error diagnostics", () => {
    const v = validateD2ResultEnvelopeSpec(validSpec);
    expect(v.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
  });

  it("flags invalid constName via D2DRE004", () => {
    const v = validateD2ResultEnvelopeSpec({
      fields: [{ constName: "lowerCase", value: "x", doc: "doc" }],
    });
    expect(v.diagnostics[0]?.id).toBe(DiagnosticIds.DRE_INVALID_CONST_NAME);
  });

  it("flags duplicate constName via D2DRE002", () => {
    const v = validateD2ResultEnvelopeSpec({
      fields: [
        { constName: "SUCCESS", value: "success", doc: "first" },
        { constName: "SUCCESS", value: "ok", doc: "second" },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe(
      DiagnosticIds.DRE_DUPLICATE_FIELD_CONST_NAME,
    );
  });

  it("flags duplicate wire value via D2DRE003", () => {
    const v = validateD2ResultEnvelopeSpec({
      fields: [
        { constName: "A", value: "x", doc: "first" },
        { constName: "B", value: "x", doc: "second" },
      ],
    });
    expect(v.diagnostics[0]?.id).toBe(DiagnosticIds.DRE_DUPLICATE_FIELD_VALUE);
  });

  it("flags empty value via D2DRE005", () => {
    const v = validateD2ResultEnvelopeSpec({
      fields: [{ constName: "A", value: "", doc: "doc" }],
    });
    expect(v.diagnostics[0]?.id).toBe(DiagnosticIds.DRE_EMPTY_VALUE);
  });

  it("flags whitespace-only value via D2DRE005", () => {
    const v = validateD2ResultEnvelopeSpec({
      fields: [{ constName: "A", value: "   ", doc: "doc" }],
    });
    expect(v.diagnostics[0]?.id).toBe(DiagnosticIds.DRE_EMPTY_VALUE);
  });
});

describe("emitD2ResultEnvelope", () => {
  it("emits the envelope catalog with all 7 constants", () => {
    const result = emitD2ResultEnvelope(validSpec);

    expect(result.diagnostics.filter((d) => d.severity === "error")).toEqual(
      [],
    );
    expect(result.source).toContain(
      "export const D2ResultEnvelopeFieldNames = {",
    );
    expect(result.source).toContain('SUCCESS: "success",');
    expect(result.source).toContain('DATA: "data",');
    expect(result.source).toContain('MESSAGES: "messages",');
    expect(result.source).toContain('INPUT_ERRORS: "inputErrors",');
    expect(result.source).toContain('ERROR_CODE: "errorCode",');
    expect(result.source).toContain('TRACE_ID: "traceId",');
    expect(result.source).toContain('STATUS_CODE: "statusCode",');
    expect(result.source).toContain("} as const;");
    expect(result.source).toContain("export type D2ResultEnvelopeFieldName");
    expect(result.source).toContain(
      "export const ALL_D2RESULT_ENVELOPE_FIELD_NAMES",
    );
  });

  it("preserves spec order of fields", () => {
    // Out-of-order constNames in the spec; emit must preserve that order.
    const spec: D2ResultEnvelopeSpec = {
      fields: [
        { constName: "Z", value: "z", doc: "z doc" },
        { constName: "A", value: "a", doc: "a doc" },
      ],
    };
    const result = emitD2ResultEnvelope(spec);

    const zPos = result.source.indexOf("Z:");
    const aPos = result.source.indexOf("A:");
    expect(zPos).toBeLessThan(aPos);
  });

  it("emits empty source string when validation errors are present", () => {
    const result = emitD2ResultEnvelope({
      fields: [{ constName: "badName", value: "v", doc: "d" }],
    });

    expect(result.source).toBe("");
    expect(result.diagnostics.some((d) => d.severity === "error")).toBe(true);
  });

  it("running twice with identical input produces identical source", () => {
    const first = emitD2ResultEnvelope(validSpec);
    const second = emitD2ResultEnvelope(validSpec);
    expect(second.source).toBe(first.source);
  });

  it("escapes special characters in doc text via JSDoc escaping", () => {
    const spec: D2ResultEnvelopeSpec = {
      fields: [
        {
          constName: "X",
          value: "x",
          // Embedded comment-end sequence must be escaped so the
          // generated JSDoc remains valid.
          doc: "Has */ in the middle.",
        },
      ],
    };
    const result = emitD2ResultEnvelope(spec);
    expect(result.source).toContain("*\\/");
  });

  it("escapes special characters in string literal values", () => {
    const spec: D2ResultEnvelopeSpec = {
      fields: [{ constName: "X", value: 'a"b\\c', doc: "d" }],
    };
    const result = emitD2ResultEnvelope(spec);
    expect(result.source).toContain('X: "a\\"b\\\\c",');
  });

  it("ALL_D2RESULT_ENVELOPE_FIELD_NAMES contains every wire value in spec order", () => {
    const result = emitD2ResultEnvelope(validSpec);
    const arrayStart = result.source.indexOf(
      "export const ALL_D2RESULT_ENVELOPE_FIELD_NAMES",
    );
    const arrayEnd = result.source.indexOf("];", arrayStart);
    const arraySection = result.source.slice(arrayStart, arrayEnd);
    const successPos = arraySection.indexOf('"success"');
    const dataPos = arraySection.indexOf('"data"');
    const statusPos = arraySection.indexOf('"statusCode"');
    expect(successPos).toBeGreaterThan(0);
    expect(dataPos).toBeGreaterThan(successPos);
    expect(statusPos).toBeGreaterThan(dataPos);
  });
});

// ---------------------------------------------------------------------------
// Byte-parity golden test: regenerate d2result-envelope.g.ts IN-MEMORY from
// the real spec and assert it equals the committed file byte-for-byte
// (LF-normalized). Turns the byte-parity invariant into a CI test.
// ---------------------------------------------------------------------------

const _here = dirname(fileURLToPath(import.meta.url));
const _repoRoot = resolve(_here, "..", "..", "..");

function _readJson<T>(...parts: string[]): T {
  return JSON.parse(readFileSync(resolve(_repoRoot, ...parts), "utf8")) as T;
}

function _readGenerated(...parts: string[]): string {
  return readFileSync(resolve(_repoRoot, ...parts), "utf8").replace(
    /\r\n/g,
    "\n",
  );
}

describe("d2result-envelope byte-parity (in-memory regen == committed .g.ts)", () => {
  it("d2result-envelope.g.ts is byte-identical to committed", () => {
    const spec = _readJson<D2ResultEnvelopeSpec>(
      "contracts",
      "d2result-envelope",
      "d2result-envelope.spec.json",
    );
    const r = emitD2ResultEnvelope(spec);
    expect(r.diagnostics).toEqual([]);
    const committed = _readGenerated(
      "packages",
      "typescript",
      "result",
      "src",
      "d2result-envelope.g.ts",
    );
    expect(r.source).toBe(committed);
  });
});
