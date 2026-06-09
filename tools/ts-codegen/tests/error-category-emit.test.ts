// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { DiagnosticIds } from "../src/lib/diagnostics.js";
import {
  emitErrorCategory,
  validateErrorCategorySpec,
  wireToMemberName,
  type ErrorCategorySpec,
} from "../src/error-category-emit.js";

const validSpec: ErrorCategorySpec = {
  categories: [
    { wire: "not_found", doc: "Not found." },
    { wire: "conflict", doc: "Conflict." },
    { wire: "validation_failure", doc: "Validation failed." },
  ],
};

describe("wireToMemberName", () => {
  it.each([
    ["validation_failure", "ValidationFailure"],
    ["not_found", "NotFound"],
    ["conflict", "Conflict"],
    ["policy_denied", "PolicyDenied"],
    ["infrastructure_unavailable", "InfrastructureUnavailable"],
    ["single", "Single"],
    ["a_b_c", "ABC"],
  ])("%s -> %s", (wire, expected) => {
    expect(wireToMemberName(wire)).toBe(expected);
  });
});

describe("validateErrorCategorySpec", () => {
  it("happy path returns no error diagnostics", () => {
    const v = validateErrorCategorySpec(validSpec);
    expect(v.diagnostics.filter((d) => d.severity === "error")).toEqual([]);
    expect(v.categories).toHaveLength(3);
  });

  it("flags invalid wire via D2ECAT003", () => {
    const v = validateErrorCategorySpec({
      categories: [{ wire: "NotSnake", doc: "doc" }],
    });
    expect(v.diagnostics[0]?.id).toBe(DiagnosticIds.ECAT_INVALID_WIRE);
  });

  it("flags empty doc via D2ECAT004", () => {
    const v = validateErrorCategorySpec({
      categories: [{ wire: "not_found", doc: "   " }],
    });
    expect(v.diagnostics[0]?.id).toBe(DiagnosticIds.ECAT_EMPTY_DOC);
  });

  it("flags duplicate wire via D2ECAT002", () => {
    const v = validateErrorCategorySpec({
      categories: [
        { wire: "not_found", doc: "first" },
        { wire: "not_found", doc: "second" },
      ],
    });
    expect(
      v.diagnostics.some((d) => d.id === DiagnosticIds.ECAT_DUPLICATE_WIRE),
    ).toBe(true);
  });
});

describe("emitErrorCategory", () => {
  it("emits the union, ErrorCategoryWire map, and ALL_ERROR_CATEGORIES", () => {
    const result = emitErrorCategory(validSpec);
    expect(result.diagnostics.filter((d) => d.severity === "error")).toEqual(
      [],
    );
    expect(result.source).toContain("export type ErrorCategory =");
    expect(result.source).toContain('| "conflict"');
    expect(result.source).toContain('| "not_found"');
    expect(result.source).toContain('| "validation_failure";');
    expect(result.source).toContain("export const ErrorCategoryWire = {");
    expect(result.source).toContain('NotFound: "not_found",');
    expect(result.source).toContain('ValidationFailure: "validation_failure",');
    expect(result.source).toContain(
      "} as const satisfies Record<string, ErrorCategory>;",
    );
    expect(result.source).toContain(
      "export const ALL_ERROR_CATEGORIES: readonly ErrorCategory[] = [",
    );
  });

  it("sorts members by wire string (ordinal) — deterministic output", () => {
    const result = emitErrorCategory(validSpec);
    const conflictIdx = result.source.indexOf('Conflict: "conflict"');
    const notFoundIdx = result.source.indexOf('NotFound: "not_found"');
    const validationIdx = result.source.indexOf(
      'ValidationFailure: "validation_failure"',
    );
    expect(conflictIdx).toBeLessThan(notFoundIdx);
    expect(notFoundIdx).toBeLessThan(validationIdx);
  });

  it("returns empty source on validation error (no partial emit)", () => {
    const result = emitErrorCategory({
      categories: [{ wire: "BAD", doc: "doc" }],
    });
    expect(result.source).toBe("");
    expect(result.diagnostics.some((d) => d.severity === "error")).toBe(true);
  });

  it("escapes embedded comment-close sequences in the doc", () => {
    const result = emitErrorCategory({
      categories: [{ wire: "not_found", doc: "ends with */ sequence" }],
    });
    expect(result.source).toContain("*\\/");
    expect(result.source).not.toContain("*/ sequence");
  });
});

// ---------------------------------------------------------------------------
// Byte-parity golden test: regenerate error-category.g.ts IN-MEMORY from the
// real spec and assert it equals the committed file byte-for-byte
// (LF-normalized). Turns the byte-parity invariant into a CI test.
// ---------------------------------------------------------------------------

const _here = dirname(fileURLToPath(import.meta.url));
const _repoRoot = resolve(_here, "..", "..", "..");

function _readJson<T>(...parts: string[]): T {
  return JSON.parse(readFileSync(resolve(_repoRoot, ...parts), "utf8")) as T;
}

function _readGenerated(...parts: string[]): string {
  return readFileSync(resolve(_repoRoot, ...parts), "utf8").replace(/\r\n/g, "\n");
}

describe("error-category byte-parity (in-memory regen == committed .g.ts)", () => {
  it("error-category.g.ts is byte-identical to committed", () => {
    const spec = _readJson<ErrorCategorySpec>(
      "contracts",
      "error-category",
      "error-category.spec.json",
    );
    const r = emitErrorCategory(spec);
    expect(r.diagnostics).toEqual([]);
    const committed = _readGenerated(
      "server",
      "shared",
      "typescript",
      "error-category",
      "src",
      "generated",
      "error-category.g.ts",
    );
    expect(r.source).toBe(committed);
  });
});
