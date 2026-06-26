// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Unit tests for the resolveBaseline precedence contract.
//
// resolveBaseline lives in src/baseline.ts — a side-effect-free module
// extracted from cli.ts so it can be imported and tested directly.

import { describe, expect, it } from "vitest";
import { resolveBaseline } from "../src/baseline.js";

// ---------------------------------------------------------------------------
// resolveBaseline — resolution precedence
// ---------------------------------------------------------------------------

describe("resolveBaseline — arg > env > undefined", () => {
  it("returns the arg when both arg and env are provided", () => {
    expect(resolveBaseline("feature-branch", "env-branch")).toBe(
      "feature-branch",
    );
  });

  it("returns the arg when only arg is provided", () => {
    expect(resolveBaseline("feature-branch", undefined)).toBe("feature-branch");
  });

  it("returns the env var when arg is absent and env is provided", () => {
    expect(resolveBaseline(undefined, "env-branch")).toBe("env-branch");
  });

  it("returns the env var when arg is empty string and env is provided", () => {
    expect(resolveBaseline("", "env-branch")).toBe("env-branch");
  });

  it("returns undefined when both arg and env are undefined", () => {
    expect(resolveBaseline(undefined, undefined)).toBeUndefined();
  });

  it("returns undefined when arg is empty string and env is undefined", () => {
    expect(resolveBaseline("", undefined)).toBeUndefined();
  });

  it("returns undefined when both arg and env are empty strings", () => {
    expect(resolveBaseline("", "")).toBeUndefined();
  });

  it("arg value takes precedence over env even when env is non-empty", () => {
    expect(resolveBaseline("my-ref", "other-ref")).toBe("my-ref");
  });
});
