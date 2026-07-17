// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Spec/i18n/OpenAPI arm orchestrator.
//
// Resolves baseline content for each discovered `*.spec.json`,
// `…/contracts/messages/*.json`, and `*.openapi.g.json` file via
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
//   - Pre-reorg baseline paths under monorepo-root `contracts/**` are remapped
//     to `public/contracts/**` for identity join; content is read via modern
//     then legacy git path candidates.
//   - Dual-tree i18n: monorepo-combined mode unions public + private locale
//     keys for the same basename when diffing against a pre-split baseline
//     (keys moved to private/ must not look "removed" from public/).

import { readFileSync, existsSync } from "node:fs";
import { basename, join } from "node:path";

import type { BreakingFinding } from "./breaking-finding.js";
import { diffCatalog } from "./spec-diff.js";
import { diffMessageKeys } from "./i18n-diff.js";
import { diffOpenApi } from "./openapi-diff.js";
import { fileAtRef, listTrackedPathsAtRef } from "./git-show.js";
import { getCatalogIdentity } from "./catalog-identity.js";
import {
  PRIVATE_CONTRACTS_ROOT,
  PUBLIC_CONTRACTS_ROOT,
  SKIP_DIR_NAMES,
  baselineGitPathCandidates,
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
  /**
   * When true, discover only under `public/contracts` (export / d2-public mode).
   * Default false = dual roots (`public/contracts` + `private/contracts`).
   */
  readonly publicOnly?: boolean;
}

/**
 * Read baseline file content trying modern then legacy git paths (X11).
 */
function fileAtRefWithBaselineRemap(
  baseRef: string,
  relPath: string,
  repoRoot: string,
): string | undefined {
  for (const candidate of baselineGitPathCandidates(relPath)) {
    const content = fileAtRef(baseRef, candidate, repoRoot);

    if (content !== undefined) {
      return content;
    }
  }

  return undefined;
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

/**
 * Load proposed i18n locale JSON, unioning private sibling keys in dual-root
 * mode so a split of product keys out of public/ is not a false removal vs
 * pre-reorg monorepo-root `contracts/messages/*` baselines.
 */
function loadProposedI18nLocale(
  repoRoot: string,
  relPath: string,
  publicOnly: boolean,
): unknown {
  const absPath = join(repoRoot, relPath);
  const proposed = safeParseJson(
    readFileSync(absPath, "utf-8"),
    relPath,
  ) as Record<string, unknown>;

  if (publicOnly) {
    return proposed;
  }

  const normalized = relPath.replace(/\\/g, "/");
  const publicPrefix = `${PUBLIC_CONTRACTS_ROOT}/messages/`;
  const privatePrefix = `${PRIVATE_CONTRACTS_ROOT}/messages/`;

  if (!normalized.startsWith(publicPrefix)) {
    return proposed;
  }

  const localeFile = basename(normalized);
  const privateRel = `${privatePrefix}${localeFile}`;
  const privateAbs = join(repoRoot, privateRel);

  if (!existsSync(privateAbs)) {
    return proposed;
  }

  const privateDoc = safeParseJson(
    readFileSync(privateAbs, "utf-8"),
    privateRel,
  ) as Record<string, unknown>;

  return { ...proposed, ...privateDoc };
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
  const discoveryOpts = { publicOnly: opts.publicOnly === true };

  const findings: BreakingFinding[] = [];

  // One ls-tree serves all three pure collectors (baseline ∪ WT).
  const tracked = listTrackedPathsAtRef(baseRef, repoRoot);

  const specDiscovery = collectSpecFiles(repoRoot, tracked, discoveryOpts);
  const i18nDiscovery = collectI18nFiles(repoRoot, tracked, discoveryOpts);
  const openApiDiscovery = collectOpenApiFiles(
    repoRoot,
    tracked,
    discoveryOpts,
  );

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

    const baselineContent = fileAtRefWithBaselineRemap(
      baseRef,
      relPath,
      repoRoot,
    );
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

    const baselineContent = fileAtRefWithBaselineRemap(
      baseRef,
      relPath,
      repoRoot,
    );
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

    const proposed = loadProposedI18nLocale(
      repoRoot,
      relPath,
      opts.publicOnly === true,
    );
    const fileFindings = diffMessageKeys(baseline, proposed, relPath);
    findings.push(...fileFindings);
  }

  // ── OpenAPI docs ─────────────────────────────────────────────────
  for (const relPath of openApiDiscovery.files) {
    const absPath = join(repoRoot, relPath);

    const baselineContent = fileAtRefWithBaselineRemap(
      baseRef,
      relPath,
      repoRoot,
    );
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
