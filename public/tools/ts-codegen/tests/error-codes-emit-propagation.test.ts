// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// This test file isolates malformed-spec error-propagation for the three
// error-codes runners and the empty-spec behavior of the emit helpers.
// Vitest's vi.mock is hoisted to the module scope, so it lives in a dedicated
// file to avoid interfering with the main error-codes-emit.test.ts suite.

import type { loadSpec as LoadSpecFn } from "../src/lib/spec-loader.js";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import {
  AUTH_CONFIG,
  AUTH_FAILURES_CONFIG,
  emitErrorCodesCatalog,
  emitFailuresCatalog,
  GENERIC_CONFIG,
  type ErrorCodesSpec,
} from "../src/error-codes-emit.js";

type SpecLoaderModule = { loadSpec: typeof LoadSpecFn };

vi.mock("../src/lib/spec-loader.js", async (importActual) => {
  const actual = await importActual<SpecLoaderModule>();
  return {
    ...actual,
    loadSpec: vi.fn(actual.loadSpec),
  };
});

// ---------------------------------------------------------------------------
// Malformed-spec propagation — mirrors the field-constraints-emit-propagation
// pattern: mock loadSpec to return a diagnostic, assert the runner propagates it.
// ---------------------------------------------------------------------------

describe("runErrorCodesEmit — loadSpec error propagation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("propagates D2EC001 when loadSpec returns a malformed-spec diagnostic", async () => {
    const specLoader =
      (await import("../src/lib/spec-loader.js")) as SpecLoaderModule;
    vi.mocked(specLoader.loadSpec).mockReturnValueOnce({
      spec: undefined,
      diagnostics: [
        { id: "D2EC001", severity: "error", message: "malformed spec" },
      ],
    });

    const { runErrorCodesEmit } = await import("../src/error-codes-emit.js");
    const diagnostics = runErrorCodesEmit(true);

    expect(diagnostics).not.toEqual([]);
    expect(diagnostics.some((d) => d.id === "D2EC001")).toBe(true);
  });
});

describe("runAuthErrorCodesEmit — loadSpec error propagation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("propagates D2AEC001 when loadSpec returns a malformed-spec diagnostic", async () => {
    const specLoader =
      (await import("../src/lib/spec-loader.js")) as SpecLoaderModule;
    vi.mocked(specLoader.loadSpec).mockReturnValueOnce({
      spec: undefined,
      diagnostics: [
        { id: "D2AEC001", severity: "error", message: "malformed spec" },
      ],
    });

    const { runAuthErrorCodesEmit } =
      await import("../src/error-codes-emit.js");
    const diagnostics = runAuthErrorCodesEmit(true);

    expect(diagnostics).not.toEqual([]);
    expect(diagnostics.some((d) => d.id === "D2AEC001")).toBe(true);
  });
});

describe("runAuthFailuresEmit — loadSpec error propagation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("propagates D2AEC001 when loadSpec returns a malformed-spec diagnostic", async () => {
    const specLoader =
      (await import("../src/lib/spec-loader.js")) as SpecLoaderModule;
    vi.mocked(specLoader.loadSpec).mockReturnValueOnce({
      spec: undefined,
      diagnostics: [
        { id: "D2AEC001", severity: "error", message: "malformed spec" },
      ],
    });

    const { runAuthFailuresEmit } = await import("../src/error-codes-emit.js");
    const diagnostics = runAuthFailuresEmit(true);

    expect(diagnostics).not.toEqual([]);
    expect(diagnostics.some((d) => d.id === "D2AEC001")).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Empty-spec behavior — a spec with errorCodes: [] must emit a valid (empty)
// constants object with no diagnostics, not a crash or missing structure.
// ---------------------------------------------------------------------------

const emptySpec: ErrorCodesSpec = { errorCodes: [] };

describe("emitErrorCodesCatalog — empty spec", () => {
  it("emits a valid empty constants object with no diagnostics", () => {
    const r = emitErrorCodesCatalog(emptySpec, GENERIC_CONFIG);
    expect(r.diagnostics).toEqual([]);
    // The constants object must be present and closed
    expect(r.source).toContain("export const ErrorCodes = {");
    expect(r.source).toContain("} as const;");
    // The ALL_ array must be present and empty
    expect(r.source).toContain(
      "export const ALL_ERROR_CODES: readonly string[] = [",
    );
    // The switch must have a default branch only
    expect(r.source).toContain("default: return 500;");
  });

  it("auth catalog: emits a valid empty constants object with no diagnostics", () => {
    const r = emitErrorCodesCatalog(emptySpec, AUTH_CONFIG);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("export const AuthErrorCodes = {");
    expect(r.source).toContain("} as const;");
  });
});

describe("emitFailuresCatalog — empty spec", () => {
  it("emits a valid empty failures object with no diagnostics", () => {
    const r = emitFailuresCatalog(
      emptySpec,
      AUTH_CONFIG,
      AUTH_FAILURES_CONFIG,
      new Set<string>(),
    );
    expect(r.diagnostics).toEqual([]);
    // The failures object must be present and closed
    expect(r.source).toContain("export const AuthFailures = {");
    expect(r.source).toContain("} as const;");
    // No factory entries — the object body should have no method calls
    expect(r.source).not.toContain("return unauthorized");
    expect(r.source).not.toContain("return serviceUnavailable");
  });
});
