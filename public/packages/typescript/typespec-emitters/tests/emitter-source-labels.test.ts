// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Regression pin: emitter SOURCE and TEST files, the C# TypeSpec test tree,
// and contracts/typespec/ must contain no step/phase sequencing labels or
// conversation/decision-scoped IDs.
//
// Context: step/phase identifiers (e.g. "Step 6a", "Step 7 scope",
// "Phase 3") and conversation-decision IDs (e.g. "R-O3a", "D-S1", "D-O2",
// "C-5", "CB7", "D-R4", standalone "D3") in comments are project-internal
// sequencing references that have recurred multiple times in this package.
// They belong in git history, not in source or tests. This test reads every
// relevant source file and asserts that none contain the leak patterns, so
// any future introduction is caught at build time rather than in a post-hoc
// audit.
//
// Scopes guarded:
//   - TS emitter src/  (all *.ts)
//   - TS emitter tests/ (all *.ts, excluding this file — see carve-out below)
//   - TS decorator src/ + tests/ (all *.ts)
//   - C# TypeSpec test tree (Unit/KeyCustodian/TypeSpec*/**/*.cs)
//   - contracts/typespec/ (*.tsp, *.yaml, *.json)
//
// Carve-out: this file (emitter-source-labels.test.ts) is EXCLUDED from its
// own scan. The pattern constants and fail-without-fix reasoning blocks in
// this file necessarily contain example ID strings — scanning them would
// produce guaranteed false positives and defeat the purpose of the guard.
//
// Fail-without-fix reasoning (Step/Phase): before the F-2 fix, src/emitter.ts
// contained:
//   "// New in Step 6a: Clients namespace for exposed-op DTOs + façade interface."
//   "// New in Step 6a: App handler-namespace base; per-op CQRS path ="
//   "// For Step 6a the only exposure decorators used are @d2InProcess ..."
//   "// @route is Step 7 scope."
// All four match /Step\s+\d/ — this test would have reported 4 violations.
// After the fix, all four lines describe current behavior without sequencing
// labels and this test passes.
//
// Fail-without-fix reasoning (R-O*/D-S*/D-O*): before the FINDING-M-1 fix,
// src/emitter.ts:1013 contained "via R-O3a" and tests/ contained multiple
// "R-O3" / "R-O3a" references in comments and one it() test-name string.
// All match /\bR-O\d+[a-z]*\b/ — the tests/ scan would have reported those
// violations. After the fix, all locations use behavior-descriptive text and
// this test passes.
//
// Fail-without-fix reasoning (C-N/CB-N/D-R-N/bare-DN/F-[A-Z]+/Step-Na):
// before the AGG-F1 fix, multiple src/ and tests/ files and C# fixtures
// contained references such as "C-5 result-predicate twin", "(CB7)",
// "(C-3 validator should have rejected)", "D-R4", "D3", "F-HOME",
// "Step-7a". These are all caught by the extended CONVERSATION_ID pattern.
// After the fix, all locations use behavior-descriptive text and the guard
// passes cleanly.
//
// Fail-without-fix reasoning (D-[a-z] lowercase shorthands):
// before the R2-F10 fix, facade-emitter.ts:26 contained "D-b — transport-neutral"
// and emitter.ts:867 contained "per D-c" in a JSDoc comment. These lowercase
// decision shorthands are the same kind of conversation-scoped ID as the
// uppercase forms; they are caught by the \bD-[a-z]\b extension added to
// CONVERSATION_ID. After the fix, both locations use behavior-descriptive text
// and this test passes cleanly.

import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync, existsSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { dirname } from "node:path";
import { findRepoRoot } from "./repo-root.js";

// ---------------------------------------------------------------------------
// Directory resolution
// ---------------------------------------------------------------------------

const thisFile = fileURLToPath(import.meta.url);
const testsDir = dirname(thisFile);
const srcDir = join(testsDir, "..", "src");

// Repo root — resolved via sentinel (pnpm-workspace.yaml) walk-up; tolerates
// any future folder-depth change between tests/ and the repository root.
const repoRoot = findRepoRoot(import.meta.url);
const decoratorSrcDir = join(
  repoRoot,
  "public/packages/typescript/typespec-decorators/src",
);
const decoratorTestsDir = join(
  repoRoot,
  "public/packages/typescript/typespec-decorators/tests",
);
const csTypeSpecDir = join(
  repoRoot,
  "private/services/edge/tests/Unit/KeyCustodian",
);
const contractsTypespecDir = join(repoRoot, "public/contracts/typespec");

// ---------------------------------------------------------------------------
// File collection helpers
// ---------------------------------------------------------------------------

/** Collect all files under `dir` recursively with the given extensions. */
function collectByExt(dir: string, exts: readonly string[]): string[] {
  if (!existsSync(dir)) return [];
  const results: string[] = [];

  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);

    if (statSync(full).isDirectory()) {
      results.push(...collectByExt(full, exts));
    } else if (exts.some((e) => entry.endsWith(e))) {
      results.push(full);
    }
  }

  return results;
}

/** Collect all *.ts files under a directory recursively. */
function collectTs(dir: string): string[] {
  return collectByExt(dir, [".ts"]);
}

/** Collect *.cs and *.json files under `dir` that live inside a TypeSpec* subfolder. */
function collectCsTypeSpec(dir: string): string[] {
  if (!existsSync(dir)) return [];
  const results: string[] = [];

  for (const entry of readdirSync(dir)) {
    if (!entry.startsWith("TypeSpec")) continue;
    const full = join(dir, entry);

    if (statSync(full).isDirectory())
      results.push(...collectByExt(full, [".cs", ".json"]));
  }

  return results;
}

/** Collect TypeSpec contract files (*.tsp + *.yaml + *.json, not node_modules). */
function collectContracts(dir: string): string[] {
  return collectByExt(dir, [".tsp", ".yaml", ".json"]).filter(
    (f) => !f.includes("node_modules"),
  );
}

// ---------------------------------------------------------------------------
// Leak patterns
// ---------------------------------------------------------------------------

// "Step N" or "Step Na" (e.g. "Step 6", "Step 6a") — space-separated form.
// Kept tight: "next step" or "step1" do NOT match.
const STEP_SPACE_LABEL = /\bStep\s+\d+[ab]?\b/;

// "Step-Na" — hyphenated form (e.g. "Step-7a", "Step-6b").
const STEP_HYPHEN_LABEL = /\bStep-\d+[a-z]?\b/;

// "Phase N" (e.g. "Phase 3", "Phase 0").
const PHASE_LABEL = /\bPhase\s+\d+\b/;

// Conversation/decision-scoped IDs — the full class:
//   R-O3, R-O3a       (audit/orchestrator decision references)
//   D-S1, D-O2        (earlier ID classes, kept for history)
//   C-3, C-5, C-6     (conversation-turn IDs)
//   CB7, CB8           (conversation-branch IDs)
//   D-R4, D-R8, D-R9  (decision-revision IDs)
//   D3, D4, D7, D18   (bare decision IDs — known range: D3–D9, D10+)
//   F-HOME, F-XYZ, F-3 (finding-label IDs — 2+ uppercase letters, or digits)
//   D-b, D-c           (lowercase decision shorthands — single lowercase letter)
//   SC1, SC2, SC3      (session-scoped shorthand tokens)
//
// Excluded from the pattern (false-positive suppressions):
//   "D2" — the product name ("DcsvIo.D2", "using D2", "real D2 auth"). The
//          pattern uses \bD[3-9]\d*\b|\bD[12]\d+\b to match D3–D9 and D10+
//          without ever matching the two-character token "D2".
//   Proto field numbers (e.g. "field 1") — no letter prefix.
//   net10 / netstandard2.0 — different prefix form.
//   Version strings like "v2", "v1" — lowercase v, not uppercase D.
//   TypeSpec "using D2" / "autoUsings: ['D2']" / namespace "D2.*" — all "D2".
//   §-refs (§14.1, §24.0) — start with § not a letter.
//   gRPC "StatusCode.Cancelled" — SDK identifier, not a decision-ID pattern.
//   "D2TSP001..D2TSP008" diagnostic codes — start with "D2", excluded by above.
//   "AD2" test names (like AD2_CaseInsensitivity) — "A" precedes "D", so the
//          word-boundary rule means \bD2\b would start at "D"; but AD2 has no
//          \b before D (preceded by A which is \w), so \bD... doesn't match.
//
// The pattern matches:
//   \bR-O\d+[a-z]*\b           R-O3, R-O3a
//   \bD-[SO]\d+\b              D-S1, D-O2
//   \bC-\d+\b                  C-3, C-5, C-6
//   \bCB\d+\b                  CB7, CB8
//   \bD-R\d+\b                 D-R4, D-R8, D-R9
//   \bD[3-9]\d*\b              D3, D4, D7, D9 (single-digit range 3–9)
//   \bD[12]\d+\b               D10..D19, D20+ (two+ digits starting with 1 or 2)
//   \bF-(?:[A-Z]{2,}|\d+)\b    F-HOME, F-XYZ, F-3 (2+ upper letters OR digits)
//   \bD-[a-z]\b                D-b, D-c (lowercase decision shorthands)
//   \bSC\d+\b                  SC1, SC2, SC3 (session-scoped shorthand tokens)
const CONVERSATION_ID =
  /\bR-O\d+[a-z]*\b|\bD-[SO]\d+\b|\bC-\d+\b|\bCB\d+\b|\bD-R\d+\b|\bD[3-9]\d*\b|\bD[12]\d+\b|\bF-(?:[A-Z]{2,}|\d+)\b|\bD-[a-z]\b|\bSC\d+\b/;

// Combined check for any of the above patterns.
function hasLeak(line: string): boolean {
  return (
    STEP_SPACE_LABEL.test(line) ||
    STEP_HYPHEN_LABEL.test(line) ||
    PHASE_LABEL.test(line) ||
    CONVERSATION_ID.test(line)
  );
}

// ---------------------------------------------------------------------------
// CONVERSATION_ID pattern unit checks
//
// This file is excluded from the tests/ scan (see `thisFile` filter below), so
// the example ID tokens embedded in these assertions never self-trip the guard.
// ---------------------------------------------------------------------------

describe("conversation_id_pattern_matches_finding_labels", () => {
  it("matches digit-suffixed finding-label IDs (e.g. F-3, F-12)", () => {
    expect(CONVERSATION_ID.test("carry the F-3 response-enum parse")).toBe(
      true,
    );
    expect(CONVERSATION_ID.test("see F-12 for context")).toBe(true);
  });

  it("matches letter-suffixed finding-label IDs (e.g. F-HOME, F-XYZ)", () => {
    expect(CONVERSATION_ID.test("the F-HOME finding")).toBe(true);
    expect(CONVERSATION_ID.test("the F-XYZ finding")).toBe(true);
  });

  it("does not false-positive on a hyphenated word with an embedded F-digit (UTF-8)", () => {
    expect(CONVERSATION_ID.test("encoded as UTF-8 bytes")).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Shared scan helper
// ---------------------------------------------------------------------------

/** Scan a file list and collect violations matching any leak pattern. */
function scanFiles(
  files: string[],
  label: string,
): { path: string; lineNo: number; text: string }[] {
  const violations: { path: string; lineNo: number; text: string }[] = [];

  for (const filePath of files) {
    const content = readFileSync(filePath, "utf8");
    const lines = content.split("\n");

    lines.forEach((line, idx) => {
      if (hasLeak(line))
        violations.push({
          path: `${label}${filePath}`,
          lineNo: idx + 1,
          text: line.trim(),
        });
    });
  }

  return violations;
}

// ---------------------------------------------------------------------------
// TS emitter src/ scans
// ---------------------------------------------------------------------------

describe("emitter_source_contains_no_sequencing_labels", () => {
  const sourceFiles = collectTs(srcDir);

  it("src/ directory contains at least one TypeScript file (sanity check)", () => {
    expect(sourceFiles.length).toBeGreaterThan(0);
  });

  it("no src/**/*.ts file contains step-sequencing labels (Step N / Step Na / Step-Na)", () => {
    const violations: string[] = [];

    for (const filePath of sourceFiles) {
      const content = readFileSync(filePath, "utf8");
      const relPath = relative(srcDir, filePath).replaceAll("\\", "/");
      const lines = content.split("\n");

      lines.forEach((line, idx) => {
        if (STEP_SPACE_LABEL.test(line) || STEP_HYPHEN_LABEL.test(line))
          violations.push(`src/${relPath}:${idx + 1}: ${line.trim()}`);
      });
    }

    if (violations.length > 0)
      expect.fail(
        `Found ${violations.length} step-sequencing label(s) in emitter source:\n` +
          violations.map((v) => `  ${v}`).join("\n"),
      );
  });

  it("no src/**/*.ts file contains phase-sequencing labels (Phase N)", () => {
    const violations: string[] = [];

    for (const filePath of sourceFiles) {
      const content = readFileSync(filePath, "utf8");
      const relPath = relative(srcDir, filePath).replaceAll("\\", "/");
      const lines = content.split("\n");

      lines.forEach((line, idx) => {
        if (PHASE_LABEL.test(line))
          violations.push(`src/${relPath}:${idx + 1}: ${line.trim()}`);
      });
    }

    if (violations.length > 0)
      expect.fail(
        `Found ${violations.length} phase-sequencing label(s) in emitter source:\n` +
          violations.map((v) => `  ${v}`).join("\n"),
      );
  });

  it("no src/**/*.ts file contains conversation/decision-scoped IDs (C-N, CB-N, D-R-N, bare D-N, F-XYZ, R-O*, D-S*, D-O*)", () => {
    const violations: string[] = [];

    for (const filePath of sourceFiles) {
      const content = readFileSync(filePath, "utf8");
      const relPath = relative(srcDir, filePath).replaceAll("\\", "/");
      const lines = content.split("\n");

      lines.forEach((line, idx) => {
        if (CONVERSATION_ID.test(line))
          violations.push(`src/${relPath}:${idx + 1}: ${line.trim()}`);
      });
    }

    if (violations.length > 0)
      expect.fail(
        `Found ${violations.length} conversation/decision-scoped ID(s) in emitter source:\n` +
          violations.map((v) => `  ${v}`).join("\n"),
      );
  });
});

// ---------------------------------------------------------------------------
// TS emitter tests/ scans
// ---------------------------------------------------------------------------

describe("emitter_tests_contain_no_sequencing_labels", () => {
  // Exclude THIS file — its pattern constants + reasoning blocks intentionally
  // contain example ID strings that would trigger false positives.
  const testFiles = collectTs(testsDir).filter((f) => f !== thisFile);

  it("tests/ directory contains at least one TypeScript file (sanity check)", () => {
    expect(testFiles.length).toBeGreaterThan(0);
  });

  it("no tests/**/*.ts file contains step-sequencing labels (Step N / Step Na / Step-Na)", () => {
    const violations: string[] = [];

    for (const filePath of testFiles) {
      const content = readFileSync(filePath, "utf8");
      const relPath = relative(testsDir, filePath).replaceAll("\\", "/");
      const lines = content.split("\n");

      lines.forEach((line, idx) => {
        if (STEP_SPACE_LABEL.test(line) || STEP_HYPHEN_LABEL.test(line))
          violations.push(`tests/${relPath}:${idx + 1}: ${line.trim()}`);
      });
    }

    if (violations.length > 0)
      expect.fail(
        `Found ${violations.length} step-sequencing label(s) in emitter tests:\n` +
          violations.map((v) => `  ${v}`).join("\n"),
      );
  });

  it("no tests/**/*.ts file contains phase-sequencing labels (Phase N)", () => {
    const violations: string[] = [];

    for (const filePath of testFiles) {
      const content = readFileSync(filePath, "utf8");
      const relPath = relative(testsDir, filePath).replaceAll("\\", "/");
      const lines = content.split("\n");

      lines.forEach((line, idx) => {
        if (PHASE_LABEL.test(line))
          violations.push(`tests/${relPath}:${idx + 1}: ${line.trim()}`);
      });
    }

    if (violations.length > 0)
      expect.fail(
        `Found ${violations.length} phase-sequencing label(s) in emitter tests:\n` +
          violations.map((v) => `  ${v}`).join("\n"),
      );
  });

  it("no tests/**/*.ts file contains conversation/decision-scoped IDs (C-N, CB-N, D-R-N, bare D-N, F-XYZ, R-O*, D-S*, D-O*)", () => {
    const violations: string[] = [];

    for (const filePath of testFiles) {
      const content = readFileSync(filePath, "utf8");
      const relPath = relative(testsDir, filePath).replaceAll("\\", "/");
      const lines = content.split("\n");

      lines.forEach((line, idx) => {
        if (CONVERSATION_ID.test(line))
          violations.push(`tests/${relPath}:${idx + 1}: ${line.trim()}`);
      });
    }

    if (violations.length > 0)
      expect.fail(
        `Found ${violations.length} conversation/decision-scoped ID(s) in emitter tests:\n` +
          violations.map((v) => `  ${v}`).join("\n"),
      );
  });
});

// ---------------------------------------------------------------------------
// TS decorator src/ + tests/ scans
// ---------------------------------------------------------------------------

describe("decorator_source_contains_no_sequencing_labels", () => {
  const decoratorSrcFiles = collectTs(decoratorSrcDir);
  const decoratorTestFiles = collectTs(decoratorTestsDir);
  const allDecoratorFiles = [...decoratorSrcFiles, ...decoratorTestFiles];

  it("decorator src/ + tests/ contain at least one TypeScript file (sanity check)", () => {
    expect(allDecoratorFiles.length).toBeGreaterThan(0);
  });

  it("no decorator *.ts file contains any sequencing label or conversation/decision ID", () => {
    const base = join(
      repoRoot,
      "public/packages/typescript/typespec-decorators",
    );
    const v = scanFiles(allDecoratorFiles, "");

    if (v.length > 0)
      expect.fail(
        `Found ${v.length} leaked ID(s) in decorator source/tests:\n` +
          v
            .map(
              (vv) =>
                `  ${relative(base, vv.path).replaceAll("\\", "/")}:${vv.lineNo}: ${vv.text}`,
            )
            .join("\n"),
      );
  });
});

// ---------------------------------------------------------------------------
// C# TypeSpec test tree scan
// ---------------------------------------------------------------------------

describe("csharp_typespec_tests_contain_no_sequencing_labels", () => {
  const csFiles = collectCsTypeSpec(csTypeSpecDir);

  it("C# TypeSpec test tree contains at least one .cs file (sanity check)", () => {
    expect(csFiles.length).toBeGreaterThan(0);
  });

  it("no C# TypeSpec test file contains any sequencing label or conversation/decision ID", () => {
    const v = scanFiles(csFiles, "");

    if (v.length > 0)
      expect.fail(
        `Found ${v.length} leaked ID(s) in C# TypeSpec tests:\n` +
          v
            .map(
              (vv) =>
                `  ${relative(csTypeSpecDir, vv.path).replaceAll("\\", "/")}:${vv.lineNo}: ${vv.text}`,
            )
            .join("\n"),
      );
  });
});

// ---------------------------------------------------------------------------
// contracts/typespec/ scan
// ---------------------------------------------------------------------------

describe("contracts_typespec_contains_no_sequencing_labels", () => {
  const contractFiles = collectContracts(contractsTypespecDir);

  it("contracts/typespec/ contains at least one file (sanity check)", () => {
    expect(contractFiles.length).toBeGreaterThan(0);
  });

  it("no contracts/typespec/ file contains any sequencing label or conversation/decision ID", () => {
    const v = scanFiles(contractFiles, "");

    if (v.length > 0)
      expect.fail(
        `Found ${v.length} leaked ID(s) in contracts/typespec/:\n` +
          v
            .map(
              (vv) =>
                `  ${relative(contractsTypespecDir, vv.path).replaceAll("\\", "/")}:${vv.lineNo}: ${vv.text}`,
            )
            .join("\n"),
      );
  });
});
