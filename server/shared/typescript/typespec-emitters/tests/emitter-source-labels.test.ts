// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Regression pin: emitter SOURCE files must contain no step/phase sequencing
// labels.
//
// Context: step/phase identifiers (e.g. "Step 6a", "Step 7 scope",
// "Phase 3") in comments are project-internal sequencing references that have
// recurred four times in this package. They belong in git history, not in
// source. This test reads every *.ts file under src/ and asserts that none
// contain the leak patterns, so any future introduction is caught at build
// time rather than in a post-hoc audit.
//
// Fail-without-fix reasoning: before the F-2 fix, src/emitter.ts contained:
//   "// New in Step 6a: Clients namespace for exposed-op DTOs + façade interface."
//   "// New in Step 6a: App handler-namespace base; per-op CQRS path ="
//   "// For Step 6a the only exposure decorators used are @d2InProcess ..."
//   "// @route is Step 7 scope."
// All four match /Step\s+\d/ — this test would have reported 4 violations.
// After the fix, all four lines describe current behavior without sequencing
// labels and this test passes.

import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

// Resolve the src/ directory relative to this test file.
const testsDir = fileURLToPath(new URL(".", import.meta.url));
const srcDir = join(testsDir, "..", "src");

// Collect all *.ts files under src/ recursively.
function collectTs(dir: string): string[] {
  const results: string[] = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) results.push(...collectTs(full));
    else if (entry.endsWith(".ts")) results.push(full);
  }
  return results;
}

// Pattern: "Step N" or "Step Na" (e.g. "Step 6", "Step 6a", "Step 7").
// Matches the actual leak style in comments — kept tight to avoid false
// positives on e.g. "next step" or variable names like "step1".
const STEP_LABEL = /\bStep\s+\d+[ab]?\b/;

// Pattern: "Phase N" (e.g. "Phase 3", "Phase 0").
const PHASE_LABEL = /\bPhase\s+\d+\b/;

describe("emitter_source_contains_no_sequencing_labels", () => {
  const sourceFiles = collectTs(srcDir);

  it("src/ directory contains at least one TypeScript file (sanity check)", () => {
    expect(sourceFiles.length).toBeGreaterThan(0);
  });

  it("no src/**/*.ts file contains step-sequencing labels (Step N / Step Na)", () => {
    const violations: string[] = [];
    for (const filePath of sourceFiles) {
      const content = readFileSync(filePath, "utf8");
      const relPath = relative(srcDir, filePath).replaceAll("\\", "/");
      const lines = content.split("\n");
      lines.forEach((line, idx) => {
        if (STEP_LABEL.test(line))
          violations.push(`src/${relPath}:${idx + 1}: ${line.trim()}`);
      });
    }
    if (violations.length > 0)
      // Report every violation so authors can fix them all in one pass.
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
});
