// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Real git IO adapter — thin seam over `git log` and `git diff-tree`.
//
// Isolated here so the bump engine, manifest editor, and changelog editor
// all stay pure and unit-testable against synthetic commit arrays.
//
// Excluded from the unit-coverage threshold (see vitest.config.ts).
// Integration-tested via the dry-run CLI path against the real repository.

import { spawnSync } from "node:child_process";
import { falsey, truthy } from "@d2/utilities";
import { validateGitRef } from "contract-gate";
import type { CommitRecord } from "./types.js";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Return one CommitRecord per commit in the range `baseRef..headRef`.
 *
 * Uses `git log --format=%H%x00%B%x01` to collect SHA + full message per
 * commit (NUL-delimited SHAs, SOH-delimited records), then runs
 * `git diff-tree --no-commit-id -r --name-only <sha>` for each commit to
 * get the touched file paths.
 *
 * @throws {Error} When any git subprocess exits non-zero.
 */
export function commitsInRange(
  baseRef: string,
  headRef: string,
): CommitRecord[] {
  validateGitRef(baseRef);
  validateGitRef(headRef);

  // Step 1: get SHA + message body for each commit (NUL = record separator).
  const logResult = spawnSync(
    "git",
    ["log", "--format=%H%x00%B%x01", `${baseRef}..${headRef}`],
    { encoding: "utf-8", maxBuffer: 10 * 1024 * 1024 },
  );

  if (logResult.status !== 0) {
    const stderr = (logResult.stderr ?? "").trim();
    throw new Error(
      `git log failed (exit ${logResult.status?.toString() ?? "unknown"}): ${stderr}`,
    );
  }

  const raw = logResult.stdout ?? "";
  // Records are split on SOH (\x01). Each record: "<sha>\x00<body>".
  const records = raw.split("\x01").filter((r) => truthy(r));

  const commits: CommitRecord[] = [];

  for (const record of records) {
    const nulIdx = record.indexOf("\x00");

    if (nulIdx === -1) continue;

    const sha = record.slice(0, nulIdx).trim();
    const message = record.slice(nulIdx + 1);

    if (falsey(sha)) continue;

    // Step 2: get touched file paths for this commit.
    const diffResult = spawnSync(
      "git",
      ["diff-tree", "--no-commit-id", "-r", "--name-only", sha],
      { encoding: "utf-8", maxBuffer: 10 * 1024 * 1024 },
    );

    if (diffResult.status !== 0) {
      const stderr = (diffResult.stderr ?? "").trim();
      throw new Error(
        `git diff-tree failed for commit ${sha}` +
          ` (exit ${diffResult.status?.toString() ?? "unknown"}): ${stderr}`,
      );
    }

    const files = (diffResult.stdout ?? "")
      .split("\n")
      .map((f) => f.trim())
      .filter((f) => truthy(f));

    commits.push({ message: message.trim(), files });
  }

  return commits;
}
