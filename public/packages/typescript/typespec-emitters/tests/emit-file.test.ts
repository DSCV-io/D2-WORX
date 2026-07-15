// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for the emit-file wrapper — direct-unit with mock program/context.
//
// Verifies:
//   - emitGeneratedFile delegates to the compiler's emitFile with the
//     correct { program, path, content } shape.
//   - resolveOutputPath joins segments onto context.emitterOutputDir.

import { describe, it, expect, vi } from "vitest";
import type { Program, EmitContext } from "@typespec/compiler";
import type * as CompilerNs from "@typespec/compiler";

// ---------------------------------------------------------------------------
// Module-level spy storage for vi.mock (hoisted above the it() body).
// vi.mock factories are hoisted by Vitest and CANNOT capture variables
// defined inside test bodies. Use module-level state instead.
// ---------------------------------------------------------------------------

const emitFileCalls: Array<[Program, string, string]> = [];

vi.mock("@typespec/compiler", async (importOriginal) => {
  const original = await importOriginal<typeof CompilerNs>();
  return {
    ...original,
    emitFile: async (
      prog: Program,
      opts: { path: string; content: string },
    ) => {
      emitFileCalls.push([prog, opts.path, opts.content]);
    },
    // resolvePath remains the real implementation for resolveOutputPath tests.
  };
});

// Import AFTER the mock so the module under test gets the mocked emitFile.
const { emitGeneratedFile, resolveOutputPath } =
  await import("../src/lib/emit-file.js");

// ---------------------------------------------------------------------------
// emitGeneratedFile
// ---------------------------------------------------------------------------

describe("emitGeneratedFile", () => {
  it("calls the compiler emitFile with { program, path, content }", async () => {
    emitFileCalls.length = 0; // clear before the test

    const mockProgram = {} as Program;
    const path = "/out/contracts/operations-manifest.json";
    const content = '{"emitter":"@dcsv-io/d2-typespec-emitters"}';

    await emitGeneratedFile(mockProgram, path, content);

    expect(emitFileCalls).toHaveLength(1);
    expect(emitFileCalls[0]![0]).toBe(mockProgram);
    expect(emitFileCalls[0]![1]).toBe(path);
    expect(emitFileCalls[0]![2]).toBe(content);
  });
});

// ---------------------------------------------------------------------------
// resolveOutputPath
// ---------------------------------------------------------------------------

describe("resolveOutputPath", () => {
  it("resolves a single segment relative to emitterOutputDir", () => {
    const mockContext = {
      emitterOutputDir: "/workspace/generated/@dcsv-io/d2-typespec-emitters",
    } as unknown as EmitContext;

    const result = resolveOutputPath(mockContext, "operations-manifest.json");
    expect(result).toContain("operations-manifest.json");
    expect(result).toContain("typespec-emitters");
  });

  it("resolves nested segments relative to emitterOutputDir", () => {
    const mockContext = {
      emitterOutputDir: "/workspace/out",
    } as unknown as EmitContext;

    const result = resolveOutputPath(mockContext, "contracts", "auth.proto");
    expect(result).toContain("contracts");
    expect(result).toContain("auth.proto");
  });

  it("handles an empty segments list (returns emitterOutputDir)", () => {
    const mockContext = {
      emitterOutputDir: "/workspace/out",
    } as unknown as EmitContext;

    const result = resolveOutputPath(mockContext);
    expect(result).toContain("out");
  });
});
