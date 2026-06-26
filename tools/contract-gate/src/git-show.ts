// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Git IO seam — `git show <ref>:<path>` wrapper.
//
// Returns the file contents at a given ref, or undefined when the file did
// not exist on that ref (a new file is always additive, never a break).
// Kept separate from git.ts (commit-log reader) so each IO seam has a
// single responsibility and can be independently tested.

import { spawnSync } from "node:child_process";

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

  throw new Error(
    `git show ${gitObject} failed (exit ${result.status?.toString() ?? "unknown"}): ${(result.stderr ?? "").trim()}`,
  );
}
