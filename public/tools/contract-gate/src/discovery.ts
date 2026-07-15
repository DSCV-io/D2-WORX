// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Pure file-discovery for the JSON gate arms (spec / i18n / OpenAPI).
//
// Discovery contract (canonical skip-set home — do not re-enumerate elsewhere):
//   - Working-tree walks never follow symlinks (Dirent-based; isDirectory is
//     false for a symlink-to-dir). Zero committed symlinks exist under the
//     walked roots; not-following is the hardened posture.
//   - Directory names in SKIP_DIR_NAMES are pruned at walk time (exact
//     lowercase match). Package/build dirs are pruned without counting;
//     `tests` trees are pruned AND census'd for suffix matches so the gate
//     can announce how many candidate files sit outside the contract surface.
//   - Candidate set for each arm = set-union of (a) working-tree walk results
//     and (b) baseline-tracked paths filtered by the arm's suffix / locale
//     rules + the same skip-set path-segment filter. Whole-file deletion of a
//     published catalog / locale / OpenAPI doc therefore still enumerates and
//     the orchestrator deletion branches fire BREAKING.
//   - Paths are always repo-relative with forward slashes.

import { readdirSync, existsSync } from "node:fs";
import type { Dirent } from "node:fs";
import { basename, join, relative } from "node:path";

import { falsey } from "@d2/utilities";

// ---------------------------------------------------------------------------
// Skip set (canonical name list)
// ---------------------------------------------------------------------------

/**
 * Directory names pruned during discovery (exact lowercase name match).
 * This is the CANONICAL skip-set home — README / VALIDATION refer here.
 */
export const SKIP_DIR_NAMES = [
  "node_modules",
  "obj",
  "bin",
  ".git",
  "tests",
] as const;

/** Set view of {@link SKIP_DIR_NAMES} for O(1) membership checks. */
export const SKIP_DIRS: ReadonlySet<string> = new Set(SKIP_DIR_NAMES);

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Result of a pure discovery pass for one JSON arm. */
export interface DiscoveryResult {
  /** Candidate paths = working-tree ∪ baseline-tracked, after skip/suffix filters. */
  readonly files: readonly string[];
  /**
   * Suffix-matching files under pruned `tests` trees (working tree only).
   * Used for the gate's exclusion-scope announcement; never candidates.
   */
  readonly excludedTestFiles: readonly string[];
}

/**
 * Exclusion-scope data carried on {@link SpecGateResult} and rendered by
 * {@link formatScopeAnnouncement}.
 */
export interface GateScope {
  /** Skip-set directory names (stable order — {@link SKIP_DIR_NAMES}). */
  readonly skipDirs: readonly string[];
  /** Spec files under excluded `tests` trees (repo-relative forward-slash paths). */
  readonly excludedSpecTestFiles: readonly string[];
  /** OpenAPI files under excluded `tests` trees. */
  readonly excludedOpenApiTestFiles: readonly string[];
}

// ---------------------------------------------------------------------------
// Path helpers
// ---------------------------------------------------------------------------

function toRepoRelative(repoRoot: string, absPath: string): string {
  return relative(repoRoot, absPath).replace(/\\/g, "/");
}

/**
 * True when any path segment is a skip-dir name.
 *
 * @param relPath - Repo-relative path (any slash style accepted).
 * @param skipDirs - Skip-set to test against (defaults to {@link SKIP_DIRS}).
 * @returns `true` when a segment is a member of `skipDirs`.
 */
export function pathHasSkippedSegment(
  relPath: string,
  skipDirs: ReadonlySet<string> = SKIP_DIRS,
): boolean {
  const normalized = relPath.replace(/\\/g, "/");
  const segments = normalized.split("/");

  return segments.some((segment) => skipDirs.has(segment));
}

/** Safe Dirent readdir — missing/unreadable roots degrade to empty. */
function safeReaddirDirents(dir: string): Dirent[] {
  try {
    return readdirSync(dir, { withFileTypes: true });
  } catch {
    return [];
  }
}

/** Safe name readdir — missing/unreadable dirs degrade to empty. */
function safeReaddirNames(dir: string): string[] {
  try {
    return readdirSync(dir);
  } catch {
    return [];
  }
}

// ---------------------------------------------------------------------------
// Working-tree walker
// ---------------------------------------------------------------------------

interface WalkResult {
  readonly files: string[];
  readonly excludedTestFiles: string[];
}

/**
 * Recursively collect matching files under `roots` (absolute paths).
 * Prunes `skipDirs` by exact name. When `tests` is in the skip set, census
 * matching files under pruned `tests` trees into `excludedTestFiles`.
 */
function walkWorkingTree(
  roots: readonly string[],
  repoRoot: string,
  match: (fileName: string) => boolean,
  skipDirs: ReadonlySet<string>,
): WalkResult {
  const files: string[] = [];
  const excludedTestFiles: string[] = [];
  const countTests = skipDirs.has("tests");

  function walkCountOnly(dir: string): void {
    const entries = safeReaddirDirents(dir);

    for (const entry of entries) {
      if (entry.isDirectory()) {
        // Package/build dirs inside a tests tree: prune without counting.
        // Nested `tests` dirs are still walked for census completeness.
        if (skipDirs.has(entry.name) && entry.name !== "tests") {
          continue;
        }

        walkCountOnly(join(dir, entry.name));
      } else if (entry.isFile() && match(entry.name)) {
        excludedTestFiles.push(toRepoRelative(repoRoot, join(dir, entry.name)));
      }
    }
  }

  function walk(dir: string): void {
    const entries = safeReaddirDirents(dir);

    for (const entry of entries) {
      // Name-check before type check — matches the shared collector skip contract.
      if (skipDirs.has(entry.name)) {
        if (countTests && entry.name === "tests" && entry.isDirectory()) {
          walkCountOnly(join(dir, entry.name));
        }

        continue;
      }

      const fullPath = join(dir, entry.name);

      if (entry.isDirectory()) {
        walk(fullPath);
      } else if (entry.isFile() && match(entry.name)) {
        files.push(toRepoRelative(repoRoot, fullPath));
      }
    }
  }

  for (const root of roots) {
    walk(root);
  }

  return { files, excludedTestFiles };
}

// ---------------------------------------------------------------------------
// Baseline filter + set-union
// ---------------------------------------------------------------------------

function filterBaselinePaths(
  baselineTrackedPaths: readonly string[] | undefined,
  matchPath: (relPath: string) => boolean,
  skipDirs: ReadonlySet<string>,
): string[] {
  if (baselineTrackedPaths === undefined || falsey(baselineTrackedPaths)) {
    return [];
  }

  const results: string[] = [];

  for (const raw of baselineTrackedPaths) {
    const relPath = raw.replace(/\\/g, "/");

    if (pathHasSkippedSegment(relPath, skipDirs)) continue;
    if (!matchPath(relPath)) continue;

    results.push(relPath);
  }

  return results;
}

function unionSorted(a: readonly string[], b: readonly string[]): string[] {
  return [...new Set([...a, ...b])].sort();
}

// ---------------------------------------------------------------------------
// Public collectors
// ---------------------------------------------------------------------------

function isOpenApiFileName(name: string): boolean {
  return name.endsWith(".openapi.g.json");
}

function isSpecFileName(name: string): boolean {
  return name.endsWith(".spec.json");
}

function isI18nLocaleFileName(name: string): boolean {
  return name.endsWith(".json") && !name.startsWith("$");
}

/**
 * Collect `*.openapi.g.json` under `contracts/` + `server/` (baseline ∪ WT).
 *
 * @param repoRoot - Absolute path to the repository root.
 * @param baselineTrackedPaths - Optional `git ls-tree` path list at baseRef.
 * @returns Sorted candidate paths (WT ∪ baseline) plus WT-only excluded-under-`tests` census.
 */
export function collectOpenApiFiles(
  repoRoot: string,
  baselineTrackedPaths?: readonly string[],
): DiscoveryResult {
  const wt = walkWorkingTree(
    [join(repoRoot, "contracts"), join(repoRoot, "server")],
    repoRoot,
    isOpenApiFileName,
    SKIP_DIRS,
  );

  const baseline = filterBaselinePaths(
    baselineTrackedPaths,
    (relPath) => {
      // Same walk roots as the WT pass: contracts/ + server/ only.
      if (!relPath.startsWith("contracts/") && !relPath.startsWith("server/")) {
        return false;
      }

      return isOpenApiFileName(basename(relPath));
    },
    SKIP_DIRS,
  );

  return {
    files: unionSorted(wt.files, baseline),
    excludedTestFiles: [...wt.excludedTestFiles].sort(),
  };
}

/**
 * Collect `*.spec.json` under `contracts/` (baseline ∪ WT).
 *
 * @param repoRoot - Absolute path to the repository root.
 * @param baselineTrackedPaths - Optional `git ls-tree` path list at baseRef.
 * @returns Sorted candidate paths (WT ∪ baseline) plus WT-only excluded-under-`tests` census.
 */
export function collectSpecFiles(
  repoRoot: string,
  baselineTrackedPaths?: readonly string[],
): DiscoveryResult {
  const wt = walkWorkingTree(
    [join(repoRoot, "contracts")],
    repoRoot,
    isSpecFileName,
    SKIP_DIRS,
  );

  const baseline = filterBaselinePaths(
    baselineTrackedPaths,
    (relPath) => {
      if (!relPath.startsWith("contracts/")) return false;

      return isSpecFileName(basename(relPath));
    },
    SKIP_DIRS,
  );

  return {
    files: unionSorted(wt.files, baseline),
    excludedTestFiles: [...wt.excludedTestFiles].sort(),
  };
}

/**
 * Collect i18n locale files under `contracts/messages/` (baseline ∪ WT).
 *
 * Locale rules match the shared collector contract: top-level `*.json` files
 * that do not start with `$` (schema files). The arm does not require the
 * messages directory to exist on the working tree — baseline-only locales
 * still enumerate so whole-file deletion is BREAKING.
 *
 * @param repoRoot - Absolute path to the repository root.
 * @param baselineTrackedPaths - Optional `git ls-tree` path list at baseRef.
 * @returns Sorted candidate locale paths (WT ∪ baseline); `excludedTestFiles` is
 *   always empty (flat layout).
 */
export function collectI18nFiles(
  repoRoot: string,
  baselineTrackedPaths?: readonly string[],
): DiscoveryResult {
  const messagesDir = join(repoRoot, "contracts", "messages");
  const wtFiles: string[] = [];

  if (existsSync(messagesDir)) {
    // safeReaddirNames covers the unreadable-dir / file-as-dir case.
    for (const name of safeReaddirNames(messagesDir)) {
      if (!isI18nLocaleFileName(name)) continue;

      wtFiles.push(`contracts/messages/${name}`);
    }
  }

  const baseline = filterBaselinePaths(
    baselineTrackedPaths,
    (relPath) => {
      if (!relPath.startsWith("contracts/messages/")) return false;

      // Flat locale layout: exactly one segment under messages/.
      const rest = relPath.slice("contracts/messages/".length);

      if (rest.includes("/")) return false;

      return isI18nLocaleFileName(rest);
    },
    SKIP_DIRS,
  );

  return {
    files: unionSorted(wtFiles, baseline),
    excludedTestFiles: [],
  };
}

// ---------------------------------------------------------------------------
// Scope announcement (branch-trivial formatter)
// ---------------------------------------------------------------------------

/**
 * Render the one-line JSON-arm discovery-scope announcement for CLI stdout.
 * Branch-trivial single template so coverage stays at 100% without branch cases.
 *
 * @param scope - Exclusion-scope data from {@link runSpecGate}.
 * @returns Single announcement line (skip-set names + excluded-under-`tests` counts).
 */
export function formatScopeAnnouncement(scope: GateScope): string {
  return (
    `  Discovery scope: skip [${scope.skipDirs.join(", ")}]; ` +
    `excluded under tests — spec: ${String(scope.excludedSpecTestFiles.length)}, ` +
    `openapi: ${String(scope.excludedOpenApiTestFiles.length)}`
  );
}
