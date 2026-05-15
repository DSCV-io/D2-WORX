// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

/** Repo root, computed relative to this file's known location. */
export const REPO_ROOT = resolve(here, "..", "..", "..", "..");

/** Absolute path to a `contracts/<topic>/<file>` spec. */
export function contractsPath(...parts: string[]): string {
  return resolve(REPO_ROOT, "contracts", ...parts);
}

/** Absolute path to a `server/shared/typescript/<pkg>/...` file. */
export function tsPackagePath(pkg: string, ...parts: string[]): string {
  return resolve(REPO_ROOT, "server", "shared", "typescript", pkg, ...parts);
}
