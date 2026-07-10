// Copyright (c) DCSV. All rights reserved.
//
// Regression test: d2-policy-guard.mjs block/allow matrix. Runnable with the
// built-in node test runner (zero config, portable):
//   node --test tools/scripts/tests/d2-policy-guard.test.mjs
//
// Mirrors tools/scripts/tests/git-guard.test.mjs for the Codex PreToolUse
// structural backstop (§13.1 / §13.1a / §13.3 + secret-path deny). Invokes the
// hook via spawnSync, feeds a PreToolUse JSON envelope on stdin, and asserts
// exit codes against an isolated temp repo (.git + optional marker).

import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

const REPO_ROOT = join(
  dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
  "..",
);
const HOOK = join(REPO_ROOT, ".codex", "hooks", "d2-policy-guard.mjs");

const TMP_PROJECT = mkdtempSync(join(tmpdir(), "d2-policy-guard-test-"));
const TMP_GIT = join(TMP_PROJECT, ".git");
const TMP_CLAUDE_DIR = join(TMP_PROJECT, ".claude");
mkdirSync(TMP_GIT, { recursive: true });
mkdirSync(TMP_CLAUDE_DIR, { recursive: true });
const MARKER = join(TMP_CLAUDE_DIR, ".commit-authorized");

process.on("exit", () => {
  rmSync(TMP_PROJECT, { recursive: true, force: true });
});

/**
 * @param {{ tool_name?: string, tool_input?: Record<string, unknown> }} event
 * @param {{ withMarker?: boolean }} [opts]
 */
function runHook(event, { withMarker = false } = {}) {
  if (withMarker) {
    writeFileSync(MARKER, "");
  } else {
    try {
      rmSync(MARKER);
    } catch {
      // absent — fine
    }
  }

  const payload = {
    cwd: TMP_PROJECT,
    tool_name: event.tool_name ?? "Bash",
    tool_input: event.tool_input ?? {},
  };

  return spawnSync(process.execPath, [HOOK], {
    input: JSON.stringify(payload),
    encoding: "utf-8",
    cwd: TMP_PROJECT,
  });
}

/** @type {Array<[string, string]>} */
const BLOCKED_CASES = [
  ["git commit -m 'msg'", "git commit"],
  ["git push origin main", "git push"],
  ["git reset --hard HEAD", "git reset"],
  ["git clean -fd", "git clean"],
  ["git restore .", "git restore"],
  ["git rm src/foo.ts", "git rm"],
  ["git branch -D feature/x", "git branch -D"],
  ["git branch -d feature/x", "git branch -d"],
  ["git rebase main", "git rebase"],
  ["git stash", "git stash (bare)"],
  ["git stash push", "git stash push"],
  ["git stash pop", "git stash pop"],
  ["git stash drop", "git stash drop"],
  ["git stash clear", "git stash clear"],
  ["git stash apply", "git stash apply"],
  ["git worktree remove .claude/worktrees/wt1", "git worktree remove"],
  ["git checkout main", "git checkout (branch switch)"],
  ["git checkout -- .", "git checkout (path revert)"],
  ["git checkout -b feature/y", "git checkout -b"],
  ["git add -A && git commit -m 'x'", "chained add + commit"],
  ["git -c user.name=x commit -m 'x'", "git -c … commit"],
];

for (const [cmd, label] of BLOCKED_CASES) {
  test(`BLOCKED without marker: ${label}`, () => {
    const r = runHook({ tool_name: "Bash", tool_input: { command: cmd } });
    assert.strictEqual(
      r.status,
      2,
      `expected exit 2 for "${cmd}"\nstderr: ${r.stderr}`,
    );
  });
}

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
    const r = runHook({ tool_name: "Bash", tool_input: { command: cmd } });
    assert.strictEqual(
      r.status,
      0,
      `expected exit 0 for "${cmd}"\nstderr: ${r.stderr}`,
    );
  });
}

test("ALLOWED with marker: git commit", () => {
  const r = runHook(
    {
      tool_name: "Bash",
      tool_input: { command: "git commit -m 'authorized'" },
    },
    { withMarker: true },
  );
  assert.strictEqual(
    r.status,
    0,
    `expected exit 0 with marker\nstderr: ${r.stderr}`,
  );
});

test("ALLOWED with marker: git push", () => {
  const r = runHook(
    { tool_name: "Bash", tool_input: { command: "git push origin main" } },
    { withMarker: true },
  );
  assert.strictEqual(
    r.status,
    0,
    `expected exit 0 with marker\nstderr: ${r.stderr}`,
  );
});

test("ALLOWED with marker: git reset --hard", () => {
  const r = runHook(
    { tool_name: "Bash", tool_input: { command: "git reset --hard HEAD" } },
    { withMarker: true },
  );
  assert.strictEqual(
    r.status,
    0,
    `expected exit 0 with marker\nstderr: ${r.stderr}`,
  );
});

test("BLOCKED even with marker: Bash references .env.secrets", () => {
  const r = runHook(
    {
      tool_name: "Bash",
      tool_input: { command: "cat .env.secrets" },
    },
    { withMarker: true },
  );
  assert.strictEqual(
    r.status,
    2,
    `expected exit 2 for secret path even with marker\nstderr: ${r.stderr}`,
  );
});

test("BLOCKED even with marker: Bash references secrets/", () => {
  const r = runHook(
    {
      tool_name: "Bash",
      tool_input: { command: "cat secrets/root.key" },
    },
    { withMarker: true },
  );
  assert.strictEqual(
    r.status,
    2,
    `expected exit 2 for secrets/ even with marker\nstderr: ${r.stderr}`,
  );
});

test("BLOCKED even with marker: Edit path .env.secrets", () => {
  const r = runHook(
    {
      tool_name: "Edit",
      tool_input: { path: ".env.secrets", old_string: "x", new_string: "y" },
    },
    { withMarker: true },
  );
  assert.strictEqual(
    r.status,
    2,
    `expected exit 2 for Edit .env.secrets\nstderr: ${r.stderr}`,
  );
});

test("BLOCKED even with marker: Read path secrets/foo.pem", () => {
  const r = runHook(
    {
      tool_name: "Read",
      tool_input: { path: "secrets/dev.pem" },
    },
    { withMarker: true },
  );
  assert.strictEqual(
    r.status,
    2,
    `expected exit 2 for Read secrets/*.pem\nstderr: ${r.stderr}`,
  );
});

test("ALLOWED: .env.secrets.example is not blocked", () => {
  const r = runHook({
    tool_name: "Read",
    tool_input: { path: ".env.secrets.example" },
  });
  assert.strictEqual(
    r.status,
    0,
    `expected exit 0 for .env.secrets.example\nstderr: ${r.stderr}`,
  );
});

test("ALLOWED: Bash cat .env.secrets.example", () => {
  const r = runHook({
    tool_name: "Bash",
    tool_input: { command: "cat .env.secrets.example" },
  });
  assert.strictEqual(
    r.status,
    0,
    `expected exit 0 for .env.secrets.example via Bash\nstderr: ${r.stderr}`,
  );
});

test("BLOCKED: ambiguous shell escape", () => {
  const r = runHook({
    tool_name: "Bash",
    tool_input: { command: "git commit -m `whoami`" },
  });
  assert.strictEqual(
    r.status,
    2,
    `expected exit 2 for ambiguous escape\nstderr: ${r.stderr}`,
  );
});
