// -----------------------------------------------------------------------
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// -----------------------------------------------------------------------

// Sentinel-based monorepo-root resolver for release-runner CLIs.
// Walks up until D2.slnx or .git is found (public/tools is four levels deep).

import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Walk up from `startDir` until a monorepo root sentinel is found.
 *
 * @param startDir - Absolute directory to start walking from.
 * @returns Absolute monorepo root path.
 */
export function findRepoRoot(startDir: string): string {
  let current = startDir;

  while (true) {
    if (
      existsSync(join(current, "D2.slnx")) ||
      existsSync(join(current, ".git"))
    ) {
      return current;
    }

    const parent = dirname(current);

    if (parent === current) {
      throw new Error(
        `repo-root sentinel: no D2.slnx/.git found walking up from ${startDir}`,
      );
    }

    current = parent;
  }
}

const __dirname = fileURLToPath(new URL(".", import.meta.url));

/** Monorepo root resolved from this module's location. */
export const repoRoot: string = findRepoRoot(resolve(__dirname));
