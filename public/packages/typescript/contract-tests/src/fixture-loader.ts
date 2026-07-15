// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Envelope shape for every committed fixture file. Metadata fields
 * (`$schema`, `$comment`, `scenario`) annotate the file; the `data`
 * payload is the only field compared by parity tests.
 */
export interface FixtureEnvelope<T> {
  readonly $schema?: string;
  readonly $comment?: string;
  readonly scenario: string;
  readonly data: T;
}

/**
 * Resolve a fixture file URL relative to the contract-tests package
 * root. The TS-side test code and the .NET emitter both reference the
 * SAME repo path; the URL form keeps the resolution working under
 * Vitest's various worker layouts.
 */
export function fixtureUrl(catalog: string, scenario: string): URL {
  return new URL(`../fixtures/${catalog}/${scenario}.json`, import.meta.url);
}

/**
 * Read and parse a fixture file. Throws if the file does not exist —
 * the failure message names the path so a missing-fixture mistake is
 * obvious.
 */
export function loadFixture<T>(
  catalog: string,
  scenario: string,
): FixtureEnvelope<T> {
  const url = fixtureUrl(catalog, scenario);
  const path = fileURLToPath(url);
  const raw = readFileSync(path, "utf8");
  return JSON.parse(raw) as FixtureEnvelope<T>;
}

/**
 * Walk up from `startDir` until a directory containing `pnpm-workspace.yaml`
 * is found (the repo root). Throws if the filesystem root is reached without
 * finding the marker.
 */
function findRepoRoot(startDir: string): string {
  let dir = startDir;
  for (;;) {
    if (existsSync(join(dir, "pnpm-workspace.yaml"))) return dir;
    const parent = dirname(dir);
    if (parent === dir)
      throw new Error(
        `Could not locate the repo root (no 'pnpm-workspace.yaml' found) ` +
          `walking up from '${startDir}'.`,
      );
    dir = parent;
  }
}

/**
 * Resolve a fixture file path rooted at the repository's top-level
 * `contracts/` directory (NOT the contract-tests package's own
 * `fixtures/` tree). Used for hand-authored cross-language corpora that
 * BOTH runtimes read as the source of truth — e.g.
 * `contracts/validation/fixtures/email.json`.
 *
 * The repo root is located by walking UP from this file's directory until
 * a directory containing `pnpm-workspace.yaml` is found — making the
 * resolution robust against any future folder rearrangement.
 */
export function contractFixtureUrl(area: string, name: string): URL {
  const thisDir = dirname(fileURLToPath(import.meta.url));
  const repoRoot = findRepoRoot(thisDir);
  // Dual-tree: public open corpora live under public/contracts/**.
  const absPath = join(
    repoRoot,
    "public",
    "contracts",
    area,
    "fixtures",
    `${name}.json`,
  );
  return new URL(`file:///${absPath.replaceAll("\\", "/")}`);
}

/**
 * Read and parse a repo-`contracts/`-rooted fixture file. Unlike
 * {@link loadFixture}, the returned value is the RAW parsed JSON — these
 * hand-authored corpora define their own envelope (e.g. `version`,
 * `validator`, `rows[]`) rather than the package's `{ scenario, data }`
 * shape. Throws (naming the path) if the file is absent.
 */
export function loadContractFixture<T>(area: string, name: string): T {
  const url = contractFixtureUrl(area, name);
  const path = fileURLToPath(url);
  const raw = readFileSync(path, "utf8");
  return JSON.parse(raw) as T;
}
