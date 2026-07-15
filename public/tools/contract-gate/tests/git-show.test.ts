// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Adversarial error-propagation tests for git-show IO seams.
//
// Happy-path `git show` / `git ls-tree` are exercised by the synthetic e2e
// (`run-spec-gate.test.ts`). This file pins the hard-fail throw bodies for
// non-zero git exits that are NOT "path missing at ref" (fileAtRef → undefined).
//
// Isolation: uses a temp directory that is NOT a git repo (no ambient git
// config needed). Ref/path shapes pass validateGitRef / validateGitPath so the
// throw originates in the spawn-result branch, not the allowlist guards.

import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";

import { fileAtRef, listTrackedPathsAtRef } from "../src/git-show.js";

const tempDirectories: string[] = [];

afterEach(() => {
  while (tempDirectories.length > 0) {
    const dir = tempDirectories.pop();

    if (dir !== undefined) {
      rmSync(dir, { recursive: true, force: true, maxRetries: 5 });
    }
  }
});

function nonGitCwd(): string {
  const root = mkdtempSync(join(tmpdir(), "cg-git-show-err-"));
  tempDirectories.push(root);

  return root;
}

describe("listTrackedPathsAtRef — error propagation", () => {
  it("throws a fail-loud Error when git ls-tree exits non-zero (non-git cwd)", () => {
    const cwd = nonGitCwd();

    expect(() => listTrackedPathsAtRef("HEAD", cwd)).toThrow(
      /git ls-tree -r --name-only HEAD failed \(exit /,
    );
  });

  it("includes stderr detail from git in the thrown message", () => {
    const cwd = nonGitCwd();

    expect(() => listTrackedPathsAtRef("HEAD", cwd)).toThrow(
      /not a git repository/i,
    );
  });
});

describe("fileAtRef — error propagation (hard-fail, not path-missing)", () => {
  it("throws a fail-loud Error when git show exits non-zero for a non-missing-path reason", () => {
    const cwd = nonGitCwd();

    expect(() =>
      fileAtRef("HEAD", "contracts/domain/error-codes.spec.json", cwd),
    ).toThrow(
      /git show HEAD:contracts\/domain\/error-codes\.spec\.json failed \(exit /,
    );
  });

  it("includes stderr detail from git in the thrown message", () => {
    const cwd = nonGitCwd();

    expect(() =>
      fileAtRef("HEAD", "contracts/domain/error-codes.spec.json", cwd),
    ).toThrow(/not a git repository/i);
  });
});
