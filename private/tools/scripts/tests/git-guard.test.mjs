// Copyright (c) DCSV. All rights reserved.
//
// Regression test: git-guard.sh block/allow matrix. Runnable with the
// built-in node test runner (zero config, portable):
//   node --test private/tools/scripts/tests/git-guard.test.mjs
//
// The guard is the structural backstop for §13.1 (commit) / §13.3
// (destructive git). It reads a JSON envelope on stdin (PreToolUse shape),
// extracts the Bash command string, and exits 2 when a destructive/commit
// verb is detected without the one-shot marker file; exits 0 otherwise.
//
// This test pins the block/allow matrix by invoking the hook script directly
// via spawnSync, feeding the JSON envelope on stdin, and asserting the exit
// code. The marker is a real file written/removed per test case so the allow-
// with-marker path is exercised concretely.

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdtempSync,
  mkdirSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

// ---------------------------------------------------------------------------
// Locate the hook script (depth-invariant monorepo root).
// ---------------------------------------------------------------------------

/**
 * Walk up until monorepo root sentinel (`.git` / `D2.slnx` / `pnpm-workspace.yaml`).
 * @param {string} startDir
 * @returns {string}
 */
function findRepoRoot(startDir) {
  let current = resolve(startDir);

  while (true) {
    if (
      existsSync(join(current, ".git")) ||
      existsSync(join(current, "D2.slnx")) ||
      existsSync(join(current, "pnpm-workspace.yaml"))
    ) {
      return current;
    }

    const parent = dirname(current);

    if (parent === current) {
      throw new Error(
        `repo-root sentinel: no .git / D2.slnx / pnpm-workspace.yaml from ${startDir}`,
      );
    }

    current = parent;
  }
}

const REPO_ROOT = findRepoRoot(dirname(fileURLToPath(import.meta.url)));
const HOOK = join(REPO_ROOT, ".claude", "hooks", "git-guard.sh");

/**
 * Resolve a real POSIX shell. Prefer Git for Windows bash over the Windows
 * WSL shim at System32/bash.exe when that shim cannot exec a distro. CI
 * (ubuntu) has plain `sh` on PATH.
 * @returns {string}
 */
function resolveShell() {
  const candidates = [
    "C:\\Program Files\\Git\\bin\\bash.exe",
    "C:\\Program Files\\Git\\usr\\bin\\bash.exe",
    process.env.GIT_BASH,
    "sh",
    "bash",
  ].filter((p) => typeof p === "string" && p.length > 0);

  for (const candidate of candidates) {
    if (candidate.includes("\\") || candidate.includes("/")) {
      if (!existsSync(candidate)) continue;
    }

    const probe = spawnSync(candidate, ["-c", "echo ok"], {
      encoding: "utf-8",
    });

    if (probe.status === 0 && String(probe.stdout).includes("ok")) {
      return candidate;
    }
  }

  throw new Error(
    "git-guard tests require a working sh/bash (Git for Windows bash recommended on Windows)",
  );
}

const SHELL = resolveShell();

// ---------------------------------------------------------------------------
// Temp project dir — isolated from the real repo's marker state.
// ---------------------------------------------------------------------------

const TMP_PROJECT = mkdtempSync(join(tmpdir(), "git-guard-test-"));
const TMP_CLAUDE_DIR = join(TMP_PROJECT, ".claude");
mkdirSync(TMP_CLAUDE_DIR, { recursive: true });
const MARKER = join(TMP_CLAUDE_DIR, ".commit-authorized");

process.on("exit", () => {
  rmSync(TMP_PROJECT, { recursive: true, force: true });
});

// ---------------------------------------------------------------------------
// Helper: invoke the hook with a given command string.
// The hook reads the PreToolUse JSON envelope from stdin; CLAUDE_PROJECT_DIR
// points at the temp project so the marker check is isolated.
// ---------------------------------------------------------------------------

/**
 * @param {string} cmd
 * @param {{ withMarker?: boolean }} [opts]
 */
function runHook(cmd, { withMarker = false } = {}) {
  if (withMarker) {
    writeFileSync(MARKER, "");
  } else {
    try {
      rmSync(MARKER);
    } catch {
      // absent — fine
    }
  }

  const input = JSON.stringify({ tool_input: { command: cmd } });

  return spawnSync(SHELL, [HOOK], {
    input,
    encoding: "utf-8",
    env: { ...process.env, CLAUDE_PROJECT_DIR: TMP_PROJECT },
  });
}

// ---------------------------------------------------------------------------
// BLOCKED verbs — exit 2 WITHOUT the marker (§13.1 / §13.3)
// ---------------------------------------------------------------------------

/** @type {Array<[string, string]>} */
const BLOCKED_CASES = [
  // commit
  ["git commit -m 'msg'", "git commit"],
  // push
  ["git push origin main", "git push"],
  // reset
  ["git reset --hard HEAD", "git reset"],
  // clean
  ["git clean -fd", "git clean"],
  // restore
  ["git restore .", "git restore"],
  // rm
  ["git rm src/foo.ts", "git rm"],
  // branch -D
  ["git branch -D feature/x", "git branch -D"],
  // branch -d
  ["git branch -d feature/x", "git branch -d"],
  // rebase
  ["git rebase main", "git rebase"],
  // stash (non-list/show)
  ["git stash", "git stash (bare push)"],
  ["git stash push", "git stash push"],
  ["git stash pop", "git stash pop"],
  ["git stash drop", "git stash drop"],
  ["git stash clear", "git stash clear"],
  ["git stash apply", "git stash apply"],
  // worktree remove
  ["git worktree remove .claude/worktrees/wt1", "git worktree remove"],
  // checkout — branch switch
  ["git checkout main", "git checkout (branch switch)"],
  // checkout — path revert
  ["git checkout -- .", "git checkout (path revert)"],
  // checkout — create
  ["git checkout -b feature/y", "git checkout -b"],
  // chained: add + commit
  ["git add -A && git commit -m 'x'", "chained add + commit"],
];

for (const [cmd, label] of BLOCKED_CASES) {
  test(`BLOCKED without marker: ${label}`, () => {
    const r = runHook(cmd, { withMarker: false });
    assert.strictEqual(
      r.status,
      2,
      `expected exit 2 for "${cmd}"\nstderr: ${r.stderr}`,
    );
  });
}

// ---------------------------------------------------------------------------
// READ-ONLY verbs — exit 0 regardless of marker (§13.3 safe operations)
// ---------------------------------------------------------------------------

/** @type {Array<[string, string]>} */
const READONLY_CASES = [
  ["git status", "git status"],
  ["git log --oneline", "git log"],
  ["git diff HEAD", "git diff"],
  ["git show HEAD", "git show"],
  ["git stash list", "git stash list"],
  ["git stash show", "git stash show"],
  ["git worktree list", "git worktree list"],
  ["git checkout --help", "git checkout --help"],
  ["git checkout -h", "git checkout -h"],
];

for (const [cmd, label] of READONLY_CASES) {
  test(`ALLOWED (read-only): ${label}`, () => {
    const r = runHook(cmd, { withMarker: false });
    assert.strictEqual(
      r.status,
      0,
      `expected exit 0 for "${cmd}"\nstderr: ${r.stderr}`,
    );
  });
}

// ---------------------------------------------------------------------------
// Marker-present — destructive/commit allowed when the marker exists
// ---------------------------------------------------------------------------

test("ALLOWED with marker: git commit", () => {
  const r = runHook("git commit -m 'authorized'", { withMarker: true });
  assert.strictEqual(
    r.status,
    0,
    `expected exit 0 with marker\nstderr: ${r.stderr}`,
  );
});

test("ALLOWED with marker: git push", () => {
  const r = runHook("git push origin main", { withMarker: true });
  assert.strictEqual(
    r.status,
    0,
    `expected exit 0 with marker\nstderr: ${r.stderr}`,
  );
});

test("ALLOWED with marker: git reset --hard", () => {
  const r = runHook("git reset --hard HEAD", { withMarker: true });
  assert.strictEqual(
    r.status,
    0,
    `expected exit 0 with marker\nstderr: ${r.stderr}`,
  );
});
