// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Sentinel-based repo-root resolver for contract-gate tests.
//
// Walks up from the test file's directory until it finds the repo root
// sentinel (`.git/` directory). This is robust to test directory layout
// changes — no hardcoded `../../../` relative walk-up.
//
// Usage:
//   import { repoRoot } from "./repo-root.js";
//   const REPO_ROOT = repoRoot;

import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Walk up from `startDir` until a directory containing `.git/` is found.
 * Returns the absolute path of the repo root, or throws if the sentinel
 * is not found before reaching the filesystem root.
 */
export function findRepoRoot(startDir: string): string {
  let current = startDir;

  while (true) {
    if (existsSync(join(current, ".git"))) return current;

    const parent = dirname(current);

    if (parent === current) {
      throw new Error(
        `repo-root sentinel: no .git directory found walking up from ${startDir}`,
      );
    }

    current = parent;
  }
}

const __dirname = fileURLToPath(new URL(".", import.meta.url));

/**
 * The absolute path to the repository root, resolved via sentinel walk-up.
 * Use this instead of hardcoded `resolve(__dirname, "../../../")` to remain
 * robust if the test directory structure changes.
 */
export const repoRoot: string = findRepoRoot(resolve(__dirname));
