// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// This test file isolates the `runFieldConstraintsEmit` malformed-spec
// error-propagation branch. Vitest's vi.mock is hoisted to the module scope,
// so it lives in a dedicated file to avoid interfering with the main
// field-constraints-emit.test.ts suite.

import type { loadSpec as LoadSpecFn } from "../src/lib/spec-loader.js";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

type SpecLoaderModule = { loadSpec: typeof LoadSpecFn };

vi.mock("../src/lib/spec-loader.js", async (importActual) => {
  const actual = await importActual<SpecLoaderModule>();
  return {
    ...actual,
    loadSpec: vi.fn(actual.loadSpec),
  };
});

describe("runFieldConstraintsEmit — loadSpec error propagation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("propagates D2FC001 when loadSpec returns a malformed-spec diagnostic", async () => {
    // Arrange: override loadSpec to simulate a missing / malformed spec file.
    const specLoader =
      (await import("../src/lib/spec-loader.js")) as SpecLoaderModule;
    vi.mocked(specLoader.loadSpec).mockReturnValueOnce({
      spec: undefined,
      diagnostics: [
        { id: "D2FC001", severity: "error", message: "malformed spec" },
      ],
    });

    // Act: import after the mock is installed so the emitter module's
    // loadSpec call picks up the mocked version.
    const { runFieldConstraintsEmit } =
      await import("../src/field-constraints-emit.js");

    const diagnostics = runFieldConstraintsEmit(true);

    // Assert: D2FC001 propagates out of runFieldConstraintsEmit.
    expect(diagnostics).not.toEqual([]);
    expect(diagnostics.some((d) => d.id === "D2FC001")).toBe(true);
  });
});
