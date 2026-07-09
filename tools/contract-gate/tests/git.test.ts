// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Synthetic-repo integration tests for commitMessagesInRange (git log seam).
//
// Pins the force-valve IO contract: when `cwd` / repo root is supplied, the
// message range is read from THAT repository — not ambient process.cwd().
// Two independent temp repos with unique commit subjects prove the cwd
// option is honored; without the option the same call would hit process.cwd()
// (this monorepo) and never surface either unique subject.

import { spawnSync } from "node:child_process";
import { mkdtempSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";

import { commitMessagesInRange } from "../src/git.js";

// ---------------------------------------------------------------------------
// Synthetic-repo helper (ambient-git-config isolation — same pattern as e2e)
// ---------------------------------------------------------------------------

const BASE_BRANCH = "baseline";
const tempDirectories: string[] = [];

afterEach(() => {
  while (tempDirectories.length > 0) {
    const dir = tempDirectories.pop();

    if (dir !== undefined) {
      rmSync(dir, { recursive: true, force: true, maxRetries: 5 });
      rmSync(`${dir}.empty-gitconfig`, { force: true, maxRetries: 5 });
    }
  }
});

function emptyGitConfigPath(repoRoot: string): string {
  const path = `${repoRoot}.empty-gitconfig`;
  writeFileSync(path, "", "utf-8");

  return path;
}

function git(cwd: string, args: string[], emptyConfig: string): void {
  const result = spawnSync("git", args, {
    cwd,
    encoding: "utf-8",
    env: {
      ...process.env,
      GIT_CONFIG_GLOBAL: emptyConfig,
      GIT_CONFIG_SYSTEM: emptyConfig,
      GIT_CONFIG_NOSYSTEM: "1",
    },
  });

  if (result.status !== 0) {
    throw new Error(
      `git ${args.join(" ")} failed (exit ${String(result.status)}):` +
        ` ${(result.stderr ?? result.stdout ?? "").trim()}`,
    );
  }
}

function gitStdout(cwd: string, args: string[], emptyConfig: string): string {
  const result = spawnSync("git", args, {
    cwd,
    encoding: "utf-8",
    env: {
      ...process.env,
      GIT_CONFIG_GLOBAL: emptyConfig,
      GIT_CONFIG_SYSTEM: emptyConfig,
      GIT_CONFIG_NOSYSTEM: "1",
    },
  });

  if (result.status !== 0) {
    throw new Error(
      `git ${args.join(" ")} failed (exit ${String(result.status)}):` +
        ` ${(result.stderr ?? result.stdout ?? "").trim()}`,
    );
  }

  return (result.stdout ?? "").trim();
}

/**
 * Build a temp git repo with a baseline commit + one HEAD commit whose
 * full message body is `headMessage`. `baseRef` is the immutable SHA of
 * the first commit (a branch tip moves with HEAD and would make
 * `baseRef..HEAD` empty). Registers for afterEach cleanup before git init
 * so a mid-construction throw cannot leak the directory.
 */
function makeRepoWithHeadMessage(headMessage: string): {
  readonly root: string;
  readonly baseRef: string;
} {
  const root = mkdtempSync(join(tmpdir(), "cg-git-msg-"));
  tempDirectories.push(root);

  const emptyConfig = emptyGitConfigPath(root);

  git(root, ["init", "-b", BASE_BRANCH], emptyConfig);
  git(root, ["config", "user.name", "contract-gate-fixture"], emptyConfig);
  git(root, ["config", "user.email", "fixture@example.invalid"], emptyConfig);

  writeFileSync(join(root, "marker.txt"), "baseline\n", "utf-8");
  git(root, ["add", "--", "marker.txt"], emptyConfig);
  git(
    root,
    [
      "-c",
      "commit.gpgsign=false",
      "-c",
      "core.hooksPath=",
      "commit",
      "-m",
      "baseline",
    ],
    emptyConfig,
  );

  // Pin baseline SHA before the second commit — branch name alone moves with HEAD.
  const baseRef = gitStdout(root, ["rev-parse", "HEAD"], emptyConfig);

  writeFileSync(join(root, "marker.txt"), "head\n", "utf-8");
  git(root, ["add", "--", "marker.txt"], emptyConfig);
  git(
    root,
    [
      "-c",
      "commit.gpgsign=false",
      "-c",
      "core.hooksPath=",
      "commit",
      "-m",
      headMessage,
    ],
    emptyConfig,
  );

  return { root, baseRef };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("commitMessagesInRange — cwd / repo-root honor", () => {
  it("returns messages from the supplied cwd, not process.cwd() (two-repo)", () => {
    // Unique subjects cannot appear in ambient process.cwd() monorepo history
    // under normal operation; matching both proves each call hit its own root.
    const msgA =
      "UNIQUE_CG_REPO_A_VALVE_SUBJECT_DR6M1_aaaaaaaaaaaaaaaaaaaaaaaa";
    const msgB =
      "UNIQUE_CG_REPO_B_VALVE_SUBJECT_DR6M1_bbbbbbbbbbbbbbbbbbbbbbbb";

    const repoA = makeRepoWithHeadMessage(msgA);
    const repoB = makeRepoWithHeadMessage(msgB);

    const fromA = commitMessagesInRange(repoA.baseRef, "HEAD", repoA.root);
    const fromB = commitMessagesInRange(repoB.baseRef, "HEAD", repoB.root);

    expect(fromA.some((m) => m.includes(msgA))).toBe(true);
    expect(fromA.some((m) => m.includes(msgB))).toBe(false);

    expect(fromB.some((m) => m.includes(msgB))).toBe(true);
    expect(fromB.some((m) => m.includes(msgA))).toBe(false);

    // Cross-repo isolation is the RED-without-fix proof: if `cwd` were ignored,
    // both calls would log ambient process.cwd() and return the same set — so
    // neither unique subject (present only in its own temp repo) would appear.
  });

  it("returns an empty array when the range has no commits", () => {
    const repo = makeRepoWithHeadMessage("head-only");
    // baseRef..baseRef is empty (exclusive lower, inclusive upper of same tip).
    // Use HEAD..HEAD which is always empty.
    const messages = commitMessagesInRange("HEAD", "HEAD", repo.root);

    expect(messages).toEqual([]);
  });

  it("throws a fail-loud Error when git log exits non-zero (non-git cwd)", () => {
    const cwd = mkdtempSync(join(tmpdir(), "cg-git-err-"));
    tempDirectories.push(cwd);

    expect(() => commitMessagesInRange("HEAD", "HEAD", cwd)).toThrow(
      /git log failed \(exit /,
    );
  });
});
