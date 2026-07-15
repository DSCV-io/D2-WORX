// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Integration smoke-test: validates that release-runner correctly imports
// validateGitRef from contract-gate (one home — no copy-paste).
//
// The full adversarial suite lives in contract-gate/tests/safe-args.test.ts.
// This file only asserts the import seam and a representative accept/reject
// pair to prove the contract-gate dependency is wired correctly.

import { describe, expect, it } from "vitest";
import { validateGitRef } from "contract-gate";

describe("release-runner — validateGitRef imported from contract-gate", () => {
  it("accepts a legitimate baseline ref: nova", () => {
    expect(() => validateGitRef("nova")).not.toThrow();
  });

  it("accepts origin/main (remote-tracking ref)", () => {
    expect(() => validateGitRef("origin/main")).not.toThrow();
  });

  it("accepts a 40-hex SHA", () => {
    expect(() =>
      validateGitRef("a3f4b1c2d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0"),
    ).not.toThrow();
  });

  it("rejects a leading-dash injection: --upload-pack=x", () => {
    expect(() => validateGitRef("--upload-pack=x")).toThrow(
      /must not start with '-'/,
    );
  });

  it("rejects shell command substitution: $(whoami)", () => {
    expect(() => validateGitRef("$(whoami)")).toThrow(/disallowed characters/);
  });

  it("rejects dot-dot traversal: ..", () => {
    expect(() => validateGitRef("..")).toThrow(/must not contain '\.\.'/);
  });

  it("rejects semicolon injection: ; rm -rf /", () => {
    expect(() => validateGitRef("; rm -rf /")).toThrow(
      /disallowed characters|must not start with '-'|must not be empty/,
    );
  });

  it("rejects pipe metacharacter: a|b", () => {
    expect(() => validateGitRef("a|b")).toThrow(/disallowed characters/);
  });
});
