// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Git tree/blob read seams — `git show` + `git ls-tree`.
//
// `fileAtRef` returns the file contents at a given ref, or undefined when the
// file did not exist on that ref (a new file is always additive, never a break).
// `listTrackedPathsAtRef` lists every path tracked at a ref so discovery can
// union baseline-tracked paths with the working tree (whole-file deletion).
// Kept separate from git.ts (commit-log reader) so each IO seam has a
// single responsibility and can be independently tested.

import { spawnSync } from "node:child_process";

import { truthy } from "@d2/utilities";

import { validateGitPath, validateGitRef } from "./safe-args.js";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Return the UTF-8 content of `filePath` at git ref `ref`, or undefined
 * when the path did not exist at that ref.
 *
 * @param ref      - The git ref to read from, e.g. a branch name or commit SHA.
 * @param filePath - Path to the file, relative to the repo root (forward slashes).
 * @param cwd      - The repo root directory (where `.git/` lives).
 * @returns The file content as a string, or undefined when not found.
 * @throws {Error} When `git show` fails for a reason other than "path not found".
 */
export function fileAtRef(
  ref: string,
  filePath: string,
  cwd: string,
): string | undefined {
  validateGitRef(ref);
  validateGitPath(filePath);

  const normalizedPath = filePath.replace(/\\/g, "/");
  const gitObject = `${ref}:${normalizedPath}`;

  const result = spawnSync("git", ["show", gitObject], {
    encoding: "utf-8",
    maxBuffer: 8 * 1024 * 1024,
    cwd,
  });

  if (result.status === 0) {
    return result.stdout ?? "";
  }

  const stderr = (result.stderr ?? "").toLowerCase();

  // Common "path not in tree at that ref" messages from git.
  if (
    stderr.includes("does not exist") ||
    stderr.includes("exists on disk") ||
    stderr.includes("path not in the working tree") ||
    stderr.includes("not a valid object name") ||
    stderr.includes("no such path")
  ) {
    return undefined;
  }

  const exit = result.status?.toString() ?? "unknown";
  const detail = (result.stderr ?? "").trim();

  throw new Error(`git show ${gitObject} failed (exit ${exit}): ${detail}`);
}

/**
 * List every path tracked at git ref `ref` (recursive tree walk).
 *
 * Used by the JSON-arm orchestrator to union baseline-tracked paths with the
 * working tree so whole-file deletion of a published contract is BREAKING.
 *
 * @param ref - The git ref to list, e.g. a branch name or commit SHA.
 * @param cwd - The repo root directory (where `.git/` lives).
 * @returns Repo-relative paths with forward slashes.
 * @throws {Error} When `git ls-tree` fails for any reason.
 */
export function listTrackedPathsAtRef(ref: string, cwd: string): string[] {
  validateGitRef(ref);

  const result = spawnSync("git", ["ls-tree", "-r", "--name-only", ref], {
    encoding: "utf-8",
    maxBuffer: 32 * 1024 * 1024,
    cwd,
  });

  if (result.status !== 0) {
    throw new Error(
      `git ls-tree -r --name-only ${ref} failed` +
        ` (exit ${result.status?.toString() ?? "unknown"}):` +
        ` ${(result.stderr ?? "").trim()}`,
    );
  }

  const stdout = result.stdout ?? "";

  return stdout
    .split("\n")
    .map((line) => line.trim().replace(/\\/g, "/"))
    .filter((line) => truthy(line));
}
