// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Tests for the .husky/commit-msg hook.
//
// Drives the REAL hook script (../../.husky/commit-msg) against temporary
// commit-message files so the test cannot drift from the live check.
//
// LOCKSTEP: the allowed-type pattern below must be kept in sync with the
// pattern in .husky/commit-msg (see the LOCKSTEP comment in that file).
// If you update the type set in the hook, update the pattern here too.

import { execFile } from "node:child_process";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";
import { afterAll, beforeAll, describe, expect, it } from "vitest";

const execFileAsync = promisify(execFile);

// ---------------------------------------------------------------------------
// Setup: resolve hook path + temp dir
// ---------------------------------------------------------------------------

// __dirname is not available in ESM; use fileURLToPath + dirname instead of
// url.pathname which produces a leading /C: double-drive-letter on Windows.
const HOOK_PATH = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../.husky/commit-msg",
);

let tmpDir: string;

beforeAll(async () => {
  tmpDir = await mkdtemp(join(tmpdir(), "commit-msg-test-"));
});

afterAll(async () => {
  await rm(tmpDir, { recursive: true, force: true });
});

// ---------------------------------------------------------------------------
// Helper: write a temp file and invoke the hook; return exit code + output.
// ---------------------------------------------------------------------------

async function runHook(
  message: string,
): Promise<{ code: number; output: string }> {
  const msgFile = join(
    tmpDir,
    `msg-${Date.now()}-${Math.random().toString(36).slice(2)}.txt`,
  );
  await writeFile(msgFile, message, "utf8");

  try {
    await execFileAsync("sh", [HOOK_PATH, msgFile]);
    return { code: 0, output: "" };
  } catch (err: unknown) {
    const e = err as { code?: number; stderr?: string; stdout?: string };
    return {
      code: e.code ?? 1,
      output: `${e.stdout ?? ""}${e.stderr ?? ""}`,
    };
  }
}

// ---------------------------------------------------------------------------
// ACCEPT: well-formed conventional-commit subjects
// ---------------------------------------------------------------------------

describe("commit-msg hook — ACCEPT (valid subjects)", () => {
  it("accepts: bare type — feat", async () => {
    const { code } = await runHook("feat: add country resolver");
    expect(code).toBe(0);
  });

  it("accepts: bare type — fix", async () => {
    const { code } = await runHook("fix: correct null-ref in auth handler");
    expect(code).toBe(0);
  });

  it("accepts: bare type — perf", async () => {
    const { code } = await runHook("perf: cache resolved geo entries");
    expect(code).toBe(0);
  });

  it("accepts: bare type — chore", async () => {
    const { code } = await runHook("chore: bump vitest to 4.0.18");
    expect(code).toBe(0);
  });

  it("accepts: bare type — refactor", async () => {
    const { code } = await runHook("refactor: extract domain mapper");
    expect(code).toBe(0);
  });

  it("accepts: bare type — docs", async () => {
    const { code } = await runHook(
      "docs: add propagation note to CONTRIBUTING",
    );
    expect(code).toBe(0);
  });

  it("accepts: bare type — test", async () => {
    const { code } = await runHook(
      "test: pin dependency-propagation regression",
    );
    expect(code).toBe(0);
  });

  it("accepts: bare type — ci", async () => {
    const { code } = await runHook("ci: add matrix for node 22");
    expect(code).toBe(0);
  });

  it("accepts: bare type — build", async () => {
    const { code } = await runHook("build: switch to tsc project references");
    expect(code).toBe(0);
  });

  it("accepts: bare type — style", async () => {
    const { code } = await runHook("style: run prettier on hook scripts");
    expect(code).toBe(0);
  });

  it("accepts: type with scope — feat(geo): x", async () => {
    const { code } = await runHook("feat(geo): add country resolver");
    expect(code).toBe(0);
  });

  it("accepts: type with scope — fix(api): y", async () => {
    const { code } = await runHook("fix(api): correct response mapping");
    expect(code).toBe(0);
  });

  it("accepts: breaking shorthand — feat(api)!: z", async () => {
    const { code } = await runHook("feat(api)!: remove legacy endpoint");
    expect(code).toBe(0);
  });

  it("accepts: breaking shorthand without scope — fix!: bump", async () => {
    const { code } = await runHook("fix!: remove deprecated error shape");
    expect(code).toBe(0);
  });

  it("accepts: commit with WIRE-BREAKING footer", async () => {
    const { code } = await runHook(
      "feat(contracts): restructure proto package\n\nBody.\n\nWIRE-BREAKING: renamed RPC method",
    );
    expect(code).toBe(0);
  });

  it("accepts: commit with BREAKING CHANGE footer", async () => {
    const { code } = await runHook(
      "refactor(result): collapse error hierarchy\n\nBody.\n\nBREAKING CHANGE: D2Result.fail() signature changed",
    );
    expect(code).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// REJECT: invalid conventional-commit subjects
// ---------------------------------------------------------------------------

describe("commit-msg hook — REJECT (invalid subjects)", () => {
  it("rejects: capitalized type — Fix: thing", async () => {
    const { code, output } = await runHook("Fix: correct null-ref");
    expect(code).not.toBe(0);
    expect(output).toContain("Conventional Commits");
  });

  it("rejects: unknown type — update: x", async () => {
    const { code } = await runHook("update: refresh readme");
    expect(code).not.toBe(0);
  });

  it("rejects: unknown type — wip: x", async () => {
    const { code } = await runHook("wip: exploratory auth work");
    expect(code).not.toBe(0);
  });

  it("rejects: misspelled type — feature: x", async () => {
    const { code } = await runHook("feature: add resolver");
    expect(code).not.toBe(0);
  });

  it("rejects: missing colon — feat x", async () => {
    const { code } = await runHook("feat add resolver");
    expect(code).not.toBe(0);
  });

  it("rejects: colon but no non-whitespace subject — feat:  ", async () => {
    // The pattern requires the first char after ': ' to be non-whitespace.
    const { code } = await runHook("feat:  ");
    expect(code).not.toBe(0);
  });

  it("rejects: plain sentence (no type prefix)", async () => {
    const { code } = await runHook("Update the readme");
    expect(code).not.toBe(0);
  });

  it("rejects: AI Co-Authored-By trailer (rule 1 fires before rule 2)", async () => {
    const { code, output } = await runHook(
      "feat: add thing\n\nCo-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>",
    );
    expect(code).not.toBe(0);
    expect(output).toContain("AI-tool Co-Authored-By");
  });

  it("rejects: subject exceeding 100 characters", async () => {
    // 6 chars for "feat: " + 96 × "a" = 102 chars total.
    const longSubject = "feat: " + "a".repeat(96);
    const { code, output } = await runHook(longSubject);
    expect(code).not.toBe(0);
    expect(output).toContain("100 characters");
  });
});

// ---------------------------------------------------------------------------
// LENGTH BOUNDARY: exact 100-char (passes) and 101-char (rejects) boundary
// ---------------------------------------------------------------------------
//
// The hook uses `wc -c` which counts bytes including the newline that
// writeFile appends (UTF-8 ASCII-only subjects: byte count = char count + 1).
// The guard is `[ "$_SUBJ_LEN" -gt 101 ]`, so:
//   100-char subject → wc -c = 101 → 101 -gt 101 is false → PASSES
//   101-char subject → wc -c = 102 → 102 -gt 101 is true  → REJECTS

describe("commit-msg hook — LENGTH BOUNDARY (100/101-char subject)", () => {
  it("accepts: subject of exactly 100 characters", async () => {
    // "feat: " (6 chars) + 94 × "a" = 100 chars total.
    const subject = "feat: " + "a".repeat(94);
    expect(subject.length).toBe(100);
    const { code } = await runHook(subject);
    expect(code).toBe(0);
  });

  it("rejects: subject of exactly 101 characters", async () => {
    // "feat: " (6 chars) + 95 × "a" = 101 chars total.
    const subject = "feat: " + "a".repeat(95);
    expect(subject.length).toBe(101);
    const { code, output } = await runHook(subject);
    expect(code).not.toBe(0);
    expect(output).toContain("100 characters");
  });
});

// ---------------------------------------------------------------------------
// SKIP: auto-generated subjects that bypass type enforcement
// ---------------------------------------------------------------------------

describe("commit-msg hook — SKIP (bypass type enforcement)", () => {
  it("skips: merge commit", async () => {
    const { code } = await runHook("Merge branch 'feat/geo' into nova");
    expect(code).toBe(0);
  });

  it("skips: revert commit", async () => {
    const { code } = await runHook('Revert "feat(geo): add country resolver"');
    expect(code).toBe(0);
  });

  it("skips: fixup commit", async () => {
    const { code } = await runHook("fixup! feat(geo): add country resolver");
    expect(code).toBe(0);
  });

  it("skips: squash commit", async () => {
    const { code } = await runHook("squash! docs: update CONTRIBUTING");
    expect(code).toBe(0);
  });

  it("skips: amend commit", async () => {
    const { code } = await runHook("amend! chore: bump deps");
    expect(code).toBe(0);
  });
});
