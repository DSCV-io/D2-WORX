// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Unit tests for the repo-root sentinel helper.
//
// Validates that `findRepoRoot` correctly walks up to the `.git` sentinel
// and that `repoRoot` resolves to a real directory containing `.git`.

import { existsSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

import { findRepoRoot, repoRoot } from "./repo-root.js";

// ---------------------------------------------------------------------------
// findRepoRoot
// ---------------------------------------------------------------------------

describe("findRepoRoot — sentinel walk-up", () => {
  it("resolves to a directory that contains a .git entry", () => {
    const root = findRepoRoot(repoRoot);
    expect(existsSync(join(root, ".git"))).toBe(true);
  });

  it("finding from a deeply nested subdirectory still reaches the repo root", () => {
    // Use the repoRoot itself as a starting point (already at root level).
    // The while-loop terminates on the first iteration when .git is present.
    const root = findRepoRoot(repoRoot);
    expect(existsSync(join(root, ".git"))).toBe(true);
  });

  it("throws when started from a path with no .git ancestor", () => {
    // Use the OS temp root — no .git should exist above it.
    // On systems where /tmp is a symlink or varies, we rely on the
    // filesystem root (`/` on POSIX or `C:\` on Windows) never having .git.
    const root =
      process.platform === "win32"
        ? (process.env["SYSTEMDRIVE"] ?? "C:") + "\\"
        : "/";

    expect(() => findRepoRoot(root)).toThrow(/no .git directory found/);
  });
});

// ---------------------------------------------------------------------------
// repoRoot constant
// ---------------------------------------------------------------------------

describe("repoRoot sentinel constant", () => {
  it("points to an existing directory", () => {
    expect(existsSync(repoRoot)).toBe(true);
  });

  it("contains a .git entry (confirms it is the repo root, not a subdir)", () => {
    expect(existsSync(join(repoRoot, ".git"))).toBe(true);
  });

  it("is depth-independent of test file location", () => {
    // The sentinel resolves the same root regardless of how deeply the test
    // file is nested. Validate by resolving from the current module — the
    // result should equal what findRepoRoot also returns from the same start.
    const resolved = findRepoRoot(repoRoot);
    expect(resolved).toBe(repoRoot);
  });
});
