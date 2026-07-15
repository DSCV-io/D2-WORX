// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Spec/i18n/OpenAPI arm orchestrator.
//
// Resolves baseline content for each discovered `*.spec.json`,
// `contracts/messages/*.json`, and `*.openapi.g.json` file via
// `git show <baseRef>:<path>`, then diffs against the working-tree version
// using the appropriate diff engine (spec-diff, i18n-diff, or openapi-diff).
//
// Discovery is delegated to pure collectors in `discovery.ts` that union
// working-tree paths with baseline-tracked paths at `baseRef`, so:
//   - A file that is NEW at HEAD (no baseline version) is fully additive —
//     no findings.
//   - A file that existed on baseline but is missing at HEAD is a complete
//     removal — every entry in the baseline is flagged as removed (BREAKING).
//   - Paths under excluded directory names (tests / package / build) never
//     enter the candidate set on either side of the union.

import { readFileSync, existsSync } from "node:fs";
import { join } from "node:path";

import type { BreakingFinding } from "./breaking-finding.js";
import { diffCatalog } from "./spec-diff.js";
import { diffMessageKeys } from "./i18n-diff.js";
import { diffOpenApi } from "./openapi-diff.js";
import { fileAtRef, listTrackedPathsAtRef } from "./git-show.js";
import { getCatalogIdentity } from "./catalog-identity.js";
import {
  SKIP_DIR_NAMES,
  collectOpenApiFiles,
  collectSpecFiles,
  collectI18nFiles,
  type GateScope,
} from "./discovery.js";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Options for the spec/i18n/OpenAPI gate. */
export interface SpecGateOptions {
  /** Absolute path to the repo root. */
  readonly repoRoot: string;
  /** The integration baseline git ref (e.g. a branch name or commit SHA). */
  readonly baseRef: string;
  /** True when the force valve has been pulled. */
  readonly valveOpen: boolean;
}

/** Result of the spec gate. */
export interface SpecGateResult {
  readonly passed: boolean;
  readonly findings: readonly BreakingFinding[];
  /** Exclusion-scope data for the CLI announcement. */
  readonly scope: GateScope;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function safeParseJson(content: string, label: string): unknown {
  try {
    return JSON.parse(content) as unknown;
  } catch {
    throw new Error(
      `[run-spec-gate] failed to parse JSON for '${label}': content is not valid JSON`,
    );
  }
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Run the spec/i18n/OpenAPI gate.
 *
 * @param opts - Gate options.
 * @returns A {@link SpecGateResult} describing the outcome.
 */
export async function runSpecGate(
  opts: SpecGateOptions,
): Promise<SpecGateResult> {
  const { repoRoot, baseRef, valveOpen } = opts;

  const findings: BreakingFinding[] = [];

  // One ls-tree serves all three pure collectors (baseline ∪ WT).
  const tracked = listTrackedPathsAtRef(baseRef, repoRoot);

  const specDiscovery = collectSpecFiles(repoRoot, tracked);
  const i18nDiscovery = collectI18nFiles(repoRoot, tracked);
  const openApiDiscovery = collectOpenApiFiles(repoRoot, tracked);

  const scope: GateScope = {
    skipDirs: SKIP_DIR_NAMES,
    excludedSpecTestFiles: specDiscovery.excludedTestFiles,
    excludedOpenApiTestFiles: openApiDiscovery.excludedTestFiles,
  };

  // ── Spec catalogs ────────────────────────────────────────────────
  for (const relPath of specDiscovery.files) {
    const absPath = join(repoRoot, relPath);

    let identity;

    try {
      identity = getCatalogIdentity(relPath);
    } catch (err) {
      findings.push({
        arm: "spec",
        severity: "ERROR",
        file: relPath,
        message:
          `✗ BREAKING (gate error): ${relPath}\n` +
          `  ${String(err)}\n` +
          `  Gate FAILED — unregistered spec catalog.`,
      });

      continue;
    }

    if (identity.kind === "exempt") continue;

    const baselineContent = fileAtRef(baseRef, relPath, repoRoot);
    const baseline =
      baselineContent !== undefined
        ? safeParseJson(baselineContent, `${relPath}@${baseRef}`)
        : undefined;

    if (!existsSync(absPath)) {
      // File existed on baseline but deleted at HEAD — treat as all-entries-removed.
      if (baseline !== undefined) {
        findings.push({
          arm: "spec",
          severity: "ERROR",
          file: relPath,
          message:
            `✗ BREAKING: ${relPath}\n` +
            `  File was deleted. All published entries in this catalog are now removed.\n` +
            `  Gate FAILED — spec file deleted without force valve.`,
        });
      }

      continue;
    }

    const proposed = safeParseJson(readFileSync(absPath, "utf-8"), relPath);
    const fileFindings = diffCatalog(baseline, proposed, identity, relPath);
    findings.push(...fileFindings);
  }

  // ── i18n message keys ────────────────────────────────────────────
  // No whole-arm short-circuit when messages/ is absent from the WT —
  // baseline-only locale paths must still enumerate (whole-file deletion).
  for (const relPath of i18nDiscovery.files) {
    const absPath = join(repoRoot, relPath);

    const baselineContent = fileAtRef(baseRef, relPath, repoRoot);
    const baseline =
      baselineContent !== undefined
        ? safeParseJson(baselineContent, `${relPath}@${baseRef}`)
        : undefined;

    if (!existsSync(absPath)) {
      if (baseline !== undefined) {
        findings.push({
          arm: "i18n",
          severity: "ERROR",
          file: relPath,
          message:
            `✗ BREAKING: ${relPath}\n` +
            `  Locale file deleted — all translation keys removed.\n` +
            `  Gate FAILED — locale file deleted without force valve.`,
        });
      }

      continue;
    }

    const proposed = safeParseJson(readFileSync(absPath, "utf-8"), relPath);
    const fileFindings = diffMessageKeys(baseline, proposed, relPath);
    findings.push(...fileFindings);
  }

  // ── OpenAPI docs ─────────────────────────────────────────────────
  for (const relPath of openApiDiscovery.files) {
    const absPath = join(repoRoot, relPath);

    const baselineContent = fileAtRef(baseRef, relPath, repoRoot);
    const baseline =
      baselineContent !== undefined
        ? safeParseJson(baselineContent, `${relPath}@${baseRef}`)
        : undefined;

    if (!existsSync(absPath)) {
      if (baseline !== undefined) {
        findings.push({
          arm: "openapi",
          severity: "ERROR",
          file: relPath,
          message:
            `✗ BREAKING: ${relPath}\n` +
            `  OpenAPI document deleted.\n` +
            `  Gate FAILED — OpenAPI doc deleted without force valve.`,
        });
      }

      continue;
    }

    const proposed = safeParseJson(readFileSync(absPath, "utf-8"), relPath);
    const fileFindings = diffOpenApi(baseline, proposed, relPath);
    findings.push(...fileFindings);
  }

  const passed = findings.length === 0 || valveOpen;

  return { passed, findings, scope };
}
