// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Spec/i18n/OpenAPI arm orchestrator.
//
// Resolves baseline content for each `*.spec.json` and `contracts/messages/*.json`
// file via `git show <baseRef>:<path>`, then diffs against the working-tree
// version using the appropriate diff engine (spec-diff, i18n-diff, or openapi-diff).
//
// A file that is NEW at HEAD (no baseline version) is fully additive — no findings.
// A file that existed on baseline but is missing at HEAD is a complete removal —
// every entry in the baseline is flagged as removed.

import { readFileSync, readdirSync, existsSync, statSync } from "node:fs";
import { join, relative } from "node:path";

import type { BreakingFinding } from "./breaking-finding.js";
import { diffCatalog } from "./spec-diff.js";
import { diffMessageKeys } from "./i18n-diff.js";
import { diffOpenApi } from "./openapi-diff.js";
import { fileAtRef } from "./git-show.js";
import { getCatalogIdentity } from "./catalog-identity.js";

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
 * Collect all `*.spec.json` files under `contractsDir` (recursive).
 * Returns paths relative to `repoRoot` with forward slashes.
 */
function collectSpecFiles(contractsDir: string, repoRoot: string): string[] {
  const results: string[] = [];

  function walk(dir: string): void {
    let names: string[];

    try {
      names = readdirSync(dir);
    } catch {
      return;
    }

    for (const name of names) {
      const fullPath = join(dir, name);

      try {
        const stat = statSync(fullPath);

        if (stat.isDirectory()) {
          walk(fullPath);
        } else if (stat.isFile() && name.endsWith(".spec.json")) {
          results.push(relative(repoRoot, fullPath).replace(/\\/g, "/"));
        }
      } catch {
        // skip unreadable entries
      }
    }
  }

  walk(contractsDir);
  return results;
}

/** Skip-list for directory names when collecting OpenAPI files. */
const SKIP_DIRS = new Set(["node_modules", "obj", "bin", ".git"]);

/**
 * Collect all `*.openapi.g.json` files under `contractsAndServerDir` (recursive).
 * Returns paths relative to `repoRoot` with forward slashes.
 */
function collectOpenApiFiles(repoRoot: string): string[] {
  const results: string[] = [];

  function walk(dir: string): void {
    let names: string[];

    try {
      names = readdirSync(dir);
    } catch {
      return;
    }

    for (const name of names) {
      if (SKIP_DIRS.has(name)) continue;

      const fullPath = join(dir, name);

      try {
        const stat = statSync(fullPath);

        if (stat.isDirectory()) {
          walk(fullPath);
        } else if (stat.isFile() && name.endsWith(".openapi.g.json")) {
          results.push(relative(repoRoot, fullPath).replace(/\\/g, "/"));
        }
      } catch {
        // skip unreadable entries
      }
    }
  }

  walk(join(repoRoot, "contracts"));
  walk(join(repoRoot, "server"));

  return results;
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
  const contractsDir = join(repoRoot, "contracts");
  const messagesDir = join(contractsDir, "messages");

  const findings: BreakingFinding[] = [];

  // ── Spec catalogs ─────────────────────────────────────────────────────────
  const specFiles = collectSpecFiles(contractsDir, repoRoot);

  for (const relPath of specFiles) {
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

    // Read baseline from git.
    const baselineContent = fileAtRef(baseRef, relPath, repoRoot);
    const baseline =
      baselineContent !== undefined
        ? safeParseJson(baselineContent, `${relPath}@${baseRef}`)
        : undefined;

    // Read working-tree version.
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

  // ── i18n message keys ─────────────────────────────────────────────────────
  if (existsSync(messagesDir)) {
    let localeFileNames: string[];

    try {
      localeFileNames = readdirSync(messagesDir);
    } catch {
      localeFileNames = [];
    }

    for (const name of localeFileNames) {
      if (!name.endsWith(".json")) continue;
      if (name.startsWith("$")) continue; // skip schema files

      const relPath = `contracts/messages/${name}`;
      const absPath = join(messagesDir, name);

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
  }

  // ── OpenAPI docs ──────────────────────────────────────────────────────────
  const openApiFiles = collectOpenApiFiles(repoRoot);

  for (const relPath of openApiFiles) {
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

  return { passed, findings };
}
