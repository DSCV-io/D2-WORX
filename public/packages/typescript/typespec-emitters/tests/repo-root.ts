// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Sentinel-based repo-root resolver. Walks up from a given URL until it
// finds `pnpm-workspace.yaml`, returning the absolute directory path.
//
// Usage (in a test file):
//   import { findRepoRoot } from "./repo-root.js";
//   const REPO = findRepoRoot(import.meta.url);
//   const fixture = join(REPO, "contracts/resilience/some.fixture.json");
//
// This is more robust than a hardcoded `..`-count walk: it tolerates any
// future folder-depth change between the tests directory and the repo root.

import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

/** The sentinel file that marks the monorepo root. */
const _SENTINEL = "pnpm-workspace.yaml";

/**
 * Walk up from the directory of `importMetaUrl` until `pnpm-workspace.yaml`
 * is found, then return that directory's absolute path.
 *
 * @param importMetaUrl - Pass `import.meta.url` from the calling module.
 * @throws {Error} When the sentinel is not found (runaway walk).
 */
export function findRepoRoot(importMetaUrl: string): string {
  let dir = dirname(fileURLToPath(importMetaUrl));

  // Safety: stop after 20 levels to avoid infinite loop on bad input.
  for (let i = 0; i < 20; i++) {
    if (existsSync(join(dir, _SENTINEL))) return dir;

    const parent = dirname(dir);

    if (parent === dir)
      throw new Error(`findRepoRoot: sentinel '${_SENTINEL}' not found`);

    dir = parent;
  }

  throw new Error(
    `findRepoRoot: sentinel '${_SENTINEL}' not found within 20 levels`,
  );
}
