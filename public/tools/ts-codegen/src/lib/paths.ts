// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

/**
 * Monorepo root (directory that owns `public/` + `private/` + root `D2.slnx`).
 * This file lives at `public/tools/ts-codegen/src/lib/paths.ts` → five levels up.
 */
export const REPO_ROOT = resolve(here, "..", "..", "..", "..", "..");

/** Absolute path to a `public/contracts/<topic>/<file>` spec. */
export function contractsPath(...parts: string[]): string {
  return resolve(REPO_ROOT, "public", "contracts", ...parts);
}

/** Absolute path to a `public/packages/typescript/<pkg>/...` file. */
export function tsPackagePath(pkg: string, ...parts: string[]): string {
  return resolve(REPO_ROOT, "public", "packages", "typescript", pkg, ...parts);
}
