// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
  emitProblemDetails,
  type ProblemDetailsSpec,
  validateProblemDetailsSpec,
} from "../src/problem-details-emit.js";

const validSpec: ProblemDetailsSpec = {
  typeUriPrefix: "https://problems.d2.dcsv.io/",
  contentType: "application/problem+json",
  extensionKeys: [
    {
      constName: "ERROR_CODE",
      value: "d2_error_code",
      doc: "Machine-readable error code.",
    },
    {
      constName: "MESSAGES",
      value: "d2_messages",
      doc: "TKMessage array.",
    },
    {
      constName: "INPUT_ERRORS",
      value: "d2_input_errors",
      doc: "Per-field input errors.",
    },
    {
      constName: "CATEGORY",
      value: "d2_category",
      doc: "Closed-enum semantic ErrorCategory.",
    },
    {
      constName: "TRACE_ID",
      value: "traceId",
      doc: "W3C trace id.",
    },
    {
      constName: "CORRELATION_ID",
      value: "correlationId",
      doc: "Request correlation id.",
    },
  ],
  titles: [
    {
      constName: "UNAUTHORIZED",
      httpStatus: 401,
      value: "Unauthorized",
      doc: "401 title.",
    },
    {
      constName: "SERVICE_UNAVAILABLE",
      httpStatus: 503,
      value: "Service Unavailable",
      doc: "503 title.",
    },
    {
      constName: "REQUEST_FAILED",
      httpStatus: null,
      value: "Request Failed",
      doc: "Fallback title.",
    },
  ],
};

describe("validateProblemDetailsSpec", () => {
  it("happy path returns no error diagnostics", () => {
    const v = validateProblemDetailsSpec(validSpec);
    expect(v.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
  });

  it("flags typeUriPrefix missing trailing slash", () => {
    const v = validateProblemDetailsSpec({
      ...validSpec,
      typeUriPrefix: "https://problems.d2.dcsv.io",
    });
    expect(v.diagnostics[0]?.id).toBe("D2PRB006");
  });

  it("flags duplicate extension key constName", () => {
    const v = validateProblemDetailsSpec({
      ...validSpec,
      extensionKeys: [
        { constName: "X", value: "v1", doc: "d" },
        { constName: "X", value: "v2", doc: "d" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2PRB002")).toBe(true);
  });

  it("flags duplicate extension key wire value", () => {
    const v = validateProblemDetailsSpec({
      ...validSpec,
      extensionKeys: [
        { constName: "A", value: "v", doc: "d" },
        { constName: "B", value: "v", doc: "d" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2PRB003")).toBe(true);
  });

  it("flags duplicate title constName", () => {
    const v = validateProblemDetailsSpec({
      ...validSpec,
      titles: [
        { constName: "X", httpStatus: 401, value: "v1", doc: "d" },
        { constName: "X", httpStatus: 403, value: "v2", doc: "d" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2PRB004")).toBe(true);
  });

  it("flags duplicate title httpStatus", () => {
    const v = validateProblemDetailsSpec({
      ...validSpec,
      titles: [
        { constName: "A", httpStatus: 401, value: "v1", doc: "d" },
        { constName: "B", httpStatus: 401, value: "v2", doc: "d" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2PRB005")).toBe(true);
  });

  it("flags duplicate null fallback titles", () => {
    const v = validateProblemDetailsSpec({
      ...validSpec,
      titles: [
        { constName: "A", httpStatus: null, value: "v1", doc: "d" },
        { constName: "B", httpStatus: null, value: "v2", doc: "d" },
      ],
    });
    expect(v.diagnostics.some((d) => d.id === "D2PRB005")).toBe(true);
  });

  it("flags invalid extension key constName pattern", () => {
    const v = validateProblemDetailsSpec({
      ...validSpec,
      extensionKeys: [{ constName: "lower-case", value: "v", doc: "d" }],
    });
    expect(v.diagnostics.some((d) => d.id === "D2PRB001")).toBe(true);
  });

  it("flags invalid title constName pattern", () => {
    const v = validateProblemDetailsSpec({
      ...validSpec,
      titles: [{ constName: "lower", httpStatus: 401, value: "v", doc: "d" }],
    });
    expect(v.diagnostics.some((d) => d.id === "D2PRB001")).toBe(true);
  });
});

describe("emitProblemDetails — happy path", () => {
  it("emits PROBLEM_TYPE_URI_PREFIX with the spec value", () => {
    const r = emitProblemDetails(validSpec);
    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain(
      'export const PROBLEM_TYPE_URI_PREFIX = "https://problems.d2.dcsv.io/";',
    );
  });

  it("emits PROBLEM_DETAILS_CONTENT_TYPE with the spec value", () => {
    const r = emitProblemDetails(validSpec);
    expect(r.source).toContain(
      'export const PROBLEM_DETAILS_CONTENT_TYPE = "application/problem+json";',
    );
  });

  it("emits ProblemDetailsExtensionKeys as a const map", () => {
    const r = emitProblemDetails(validSpec);
    expect(r.source).toContain("export const ProblemDetailsExtensionKeys = {");
    expect(r.source).toContain('ERROR_CODE: "d2_error_code",');
    expect(r.source).toContain('MESSAGES: "d2_messages",');
    expect(r.source).toContain('INPUT_ERRORS: "d2_input_errors",');
    expect(r.source).toContain('TRACE_ID: "traceId",');
    expect(r.source).toContain('CORRELATION_ID: "correlationId",');
    expect(r.source).toContain("} as const;");
  });

  it("emits ProblemDetailsTitles as a const map", () => {
    const r = emitProblemDetails(validSpec);
    expect(r.source).toContain("export const ProblemDetailsTitles = {");
    expect(r.source).toContain('UNAUTHORIZED: "Unauthorized",');
    expect(r.source).toContain('SERVICE_UNAVAILABLE: "Service Unavailable",');
    expect(r.source).toContain('REQUEST_FAILED: "Request Failed",');
  });

  it("emits defaultTitleForStatus switch with per-status returns", () => {
    const r = emitProblemDetails(validSpec);
    expect(r.source).toContain(
      "export function defaultTitleForStatus(status: number): string {",
    );
    expect(r.source).toContain("case 401:");
    expect(r.source).toContain("return ProblemDetailsTitles.UNAUTHORIZED;");
    expect(r.source).toContain("case 503:");
    expect(r.source).toContain(
      "return ProblemDetailsTitles.SERVICE_UNAVAILABLE;",
    );
    expect(r.source).toContain("default:");
    expect(r.source).toContain("return ProblemDetailsTitles.REQUEST_FAILED;");
  });

  it("emits empty-string fallback when no null entry is in the spec", () => {
    const r = emitProblemDetails({
      ...validSpec,
      titles: [
        {
          constName: "UNAUTHORIZED",
          httpStatus: 401,
          value: "Unauthorized",
          doc: ".",
        },
      ],
    });
    expect(r.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(r.source).toContain('return "";');
  });

  it("preserves spec order of extension keys + titles", () => {
    const reordered: ProblemDetailsSpec = {
      ...validSpec,
      extensionKeys: [
        { constName: "Z_FIRST", value: "z_value", doc: "z" },
        { constName: "A_SECOND", value: "a_value", doc: "a" },
      ],
    };
    const r = emitProblemDetails(reordered);
    const zPos = r.source.indexOf("Z_FIRST");
    const aPos = r.source.indexOf("A_SECOND");
    expect(zPos).toBeGreaterThan(-1);
    expect(aPos).toBeGreaterThan(zPos);
  });

  it("returns empty source on validation error", () => {
    const r = emitProblemDetails({
      ...validSpec,
      typeUriPrefix: "no-slash",
    });
    expect(r.source).toBe("");
    expect(r.diagnostics.some((d) => d.id === "D2PRB006")).toBe(true);
  });

  it("runs twice with identical input → identical source", () => {
    const first = emitProblemDetails(validSpec);
    const second = emitProblemDetails(validSpec);
    expect(second.source).toBe(first.source);
  });
});

describe("emitProblemDetails — per-VALUE pin", () => {
  // Per-VALUE pins for every extension key the production spec carries.
  it.each([
    ["ERROR_CODE", "d2_error_code"],
    ["MESSAGES", "d2_messages"],
    ["INPUT_ERRORS", "d2_input_errors"],
    ["CATEGORY", "d2_category"],
    ["TRACE_ID", "traceId"],
    ["CORRELATION_ID", "correlationId"],
  ])("extension key %s pins wire value %s", (constName, wireValue) => {
    const r = emitProblemDetails({
      ...validSpec,
      extensionKeys: [{ constName, value: wireValue, doc: "d" }],
    });
    expect(r.source).toContain(`${constName}: "${wireValue}",`);
  });

  // Per-VALUE pins for every title row the production spec carries (matrix
  // superset covers all nine HTTP error titles + the fallback title).
  it.each([
    ["BAD_REQUEST", 400, "Bad Request"],
    ["UNAUTHORIZED", 401, "Unauthorized"],
    ["FORBIDDEN", 403, "Forbidden"],
    ["NOT_FOUND", 404, "Not Found"],
    ["CONFLICT", 409, "Conflict"],
    ["PAYLOAD_TOO_LARGE", 413, "Payload Too Large"],
    ["TOO_MANY_REQUESTS", 429, "Too Many Requests"],
    ["INTERNAL_SERVER_ERROR", 500, "Internal Server Error"],
    ["SERVICE_UNAVAILABLE", 503, "Service Unavailable"],
  ])(
    "title %s (status %i) pins wire value %s",
    (constName, httpStatus, wireValue) => {
      const r = emitProblemDetails({
        ...validSpec,
        titles: [
          {
            constName,
            httpStatus: httpStatus as number,
            value: wireValue,
            doc: "d",
          },
        ],
      });
      expect(r.source).toContain(`${constName}: "${wireValue}",`);
      expect(r.source).toContain(`case ${httpStatus}:`);
    },
  );
});

// ---------------------------------------------------------------------------
// Byte-parity golden test: regenerate problem-details.g.ts IN-MEMORY from
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

describe("problem-details byte-parity (in-memory regen == committed .g.ts)", () => {
  it("problem-details.g.ts is byte-identical to committed", () => {
    const spec = _readJson<ProblemDetailsSpec>(
      "contracts",
      "problem-details",
      "problem-details.spec.json",
    );
    const r = emitProblemDetails(spec);
    expect(r.diagnostics).toEqual([]);
    const committed = _readGenerated(
      "packages",
      "typescript",
      "problem-details-abstractions",
      "src",
      "generated",
      "problem-details.g.ts",
    );
    expect(r.source).toBe(committed);
  });
});
