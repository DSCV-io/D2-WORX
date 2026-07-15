// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Dual-suite gate for product-home byte-parity tests.
//
// Product TypeSpec homes live under monorepo-private `private/**` and are not
// present on a public-only (`d2-public`) clone. Public package tests that
// hard-read those homes must skip when:
//   - PUBLIC_ONLY=1 (or true) is set (public-lane / export-parity CI), OR
//   - the private product tree is absent on disk.
//
// Fixture-only / in-package golden tests always run.

import { existsSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";

/** True when the suite must not touch monorepo-private product homes. */
export function isPublicOnlyMode(): boolean {
  const v = process.env.PUBLIC_ONLY;

  return v === "1" || v === "true";
}

/**
 * True when private product TypeSpec homes exist (combined monorepo checkout).
 *
 * @param importMetaUrl - Pass `import.meta.url` from the calling test module.
 */
export function hasPrivateProductHomes(importMetaUrl: string): boolean {
  const root = findRepoRoot(importMetaUrl);

  return existsSync(
    join(root, "private", "contracts", "typespec", "key-custodian"),
  );
}

/**
 * Run product-home parity only in the combined monorepo and not under PUBLIC_ONLY.
 *
 * @param importMetaUrl - Pass `import.meta.url` from the calling test module.
 */
export function shouldRunPrivateProductParity(importMetaUrl: string): boolean {
  if (isPublicOnlyMode()) return false;

  return hasPrivateProductHomes(importMetaUrl);
}
