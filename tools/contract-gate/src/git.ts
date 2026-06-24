// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Git IO seam — isolated here so footer-parser.ts stays pure and
// unit-testable against synthetic message arrays (no real git required).
//
// Consumers: the breaking-change gate CLI and the release runner both call
// `commitMessagesInRange` to obtain the raw commit log, then pass the
// result to `parseBreakingFooters`.

import { spawnSync } from "node:child_process";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Return the full commit messages for every commit in `baseRef..headRef`
 * (exclusive of `baseRef`, inclusive of `headRef`), in reverse-chronological
 * order (newest first — the default `git log` order).
 *
 * Each element is the complete raw commit message for one commit, including
 * the subject line, body paragraphs, and any Conventional Commits footer block.
 * An empty range (no commits between the two refs) returns an empty array.
 *
 * Implementation note: uses NUL (`\x00`) as the record delimiter so that
 * multi-line message bodies survive the split unmangled. The `--format=%B%x00`
 * git-log format appends a NUL after the full message body (`%B`) of each
 * commit so splitting on `\x00` yields one element per commit.
 *
 * @param baseRef - The exclusive lower bound ref, e.g. `"nova"` or a commit SHA.
 * @param headRef - The inclusive upper bound ref, e.g. `"HEAD"` or a commit SHA.
 * @returns Array of raw commit-message strings, one per commit.
 * @throws {Error} When `git log` exits non-zero.
 */
export function commitMessagesInRange(
  baseRef: string,
  headRef: string,
): string[] {
  const result = spawnSync(
    "git",
    ["log", "--format=%B%x00", `${baseRef}..${headRef}`],
    { encoding: "utf-8", maxBuffer: 10 * 1024 * 1024 },
  );

  if (result.status !== 0) {
    const stderr = (result.stderr ?? "").trim();
    throw new Error(
      `git log failed (exit ${result.status?.toString() ?? "unknown"}): ${stderr}`,
    );
  }

  const raw = result.stdout ?? "";

  // Split on NUL delimiter; filter out empty strings that arise from the
  // trailing NUL after the last commit and any blank separators.
  return raw.split("\x00").filter((msg) => msg.trim().length > 0);
}
