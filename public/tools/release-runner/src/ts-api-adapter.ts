// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// TS extraction helpers — the @dcsv-io/d2-* half of the build-free artifact-diff engine.
//
// The bump is driven by two signals:
//
//   A. apiDiff — a git-ref TEXT DIFF of the committed `etc/<pkg>.api.md` report:
//      the baseline report at the run's baseline ref (`git show <ref>:<path>`)
//      vs the HEAD report on disk, compared by the pure `.api.md` member parser.
//      No build for the bump.
//
//   B. fingerprint — a SOURCE-BASED hash composed in real-diff-provider.ts over
//      ( committed src + .api.md + resolved deps + toolchain pin ). This module
//      supplies the `.api.md` member parser/differ, the git baseline reader, the
//      report-path resolver, and the TS fingerprint-baseline path; the
//      composition lives in source-fingerprint.ts.
//
// The real api-extractor runner lives here, used by the seed scripts + the CI
// `.api.md` CURRENCY gate (production-mode api-extractor fails on a stale
// committed report). The bump path reads the committed report directly and the
// fingerprint is source-based, so neither needs api-extractor at bump time.

import { existsSync, readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { join, relative } from "node:path";
import { spawnSync } from "node:child_process";
import type { ApiDiff } from "./diff-bump.js";

// api-extractor is a CommonJS package; this module is ESM. `require` is not a
// global in ESM scope, so build one bound to this module's URL. (A bare
// `require(...)` works under vitest's shim but throws under a plain tsx/node ESM
// run such as the drift-check CLI lane.)
const r_require = createRequire(import.meta.url);

// ---------------------------------------------------------------------------
// Injectable IO seams
// ---------------------------------------------------------------------------

/**
 * Seam for reading the committed baseline content of a file at a git ref
 * (typically via `git show <ref>:<path>` for the real adapter). Returns
 * `undefined` when the file has no committed version at that ref.
 */
export interface BaselineReader {
  /**
   * Read the committed content of `filePath` (absolute or repo-relative) at the
   * given git ref (default "HEAD"). Returns `undefined` when not committed.
   */
  read(filePath: string, ref?: string): string | undefined;
}

/**
 * Seam for running @microsoft/api-extractor against a package and returning
 * the fresh .api.md content.
 */
export interface ApiExtractorRunner {
  /**
   * Run api-extractor for the package at `packageDir` (absolute path).
   * The `configPath` is the absolute path to its api-extractor.json.
   *
   * Returns the freshly-generated .api.md content (string), or throws on
   * failure.
   */
  run(packageDir: string, configPath: string): string;
}

// ---------------------------------------------------------------------------
// Real implementations of the seams
// ---------------------------------------------------------------------------

/**
 * Real BaselineReader — reads from `git show <ref>:<repo-root-relative-path>`.
 */
export function makeGitBaselineReader(repoRoot: string): BaselineReader {
  return {
    read(filePath: string, ref = "HEAD"): string | undefined {
      const rel = relative(repoRoot, filePath).replace(/\\/g, "/");
      const result = spawnSync("git", ["show", `${ref}:${rel}`], {
        encoding: "utf-8",
        cwd: repoRoot,
      });

      if (result.status !== 0) return undefined;

      return result.stdout ?? undefined;
    },
  };
}

/**
 * Real ApiExtractorRunner — invokes @microsoft/api-extractor programmatically.
 *
 * Requires api-extractor.json at `configPath` and the package to have a
 * built `dist/` directory with a `dist/index.d.ts` entry point.
 *
 * Uses `localBuild: true` which UPDATES the etc/<name>.api.md file in-place.
 * CI should use localBuild: false to get a failure on drift instead — that
 * production-mode run is the committed-report CURRENCY gate.
 *
 * This function returns the fresh .api.md string.
 */
export function makeRealApiExtractorRunner(
  localBuild = true,
): ApiExtractorRunner {
  return {
    run(_packageDir: string, configPath: string): string {
      // Dynamic require — api-extractor is a CommonJS package. Uses the
      // module-scoped createRequire so it works under a plain ESM run too.
      const { Extractor, ExtractorConfig } = r_require(
        "@microsoft/api-extractor",
      ) as typeof import("@microsoft/api-extractor");

      const config = ExtractorConfig.loadFileAndPrepare(configPath);

      // The report file path is resolved by ExtractorConfig; read it after
      // the run to get the freshly-generated content.
      const reportPath = config.reportFilePath;

      Extractor.invoke(config, {
        localBuild,
        showVerboseMessages: false,
        showDiagnostics: false,
      });

      if (!existsSync(reportPath)) {
        throw new Error(
          `api-extractor ran but the report file was not written: ${reportPath}`,
        );
      }

      return readFileSync(reportPath, "utf-8");
    },
  };
}

// ---------------------------------------------------------------------------
// A — API surface diff via .api.md
// ---------------------------------------------------------------------------

/**
 * Parse the set of member-declaration lines from the TypeScript fence inside
 * an .api.md report file.
 *
 * Each member is a line of the form:
 *   `export <declaration>`
 *
 * Lines beginning with `//` (comments, including `// @public`) and blank
 * lines are excluded; the wrapper header and fence markers are stripped.
 *
 * The returned Set contains the normalised declaration strings. The caller
 * diffs two sets to derive ApiDiff.
 */
export function parseApiMembers(apiMd: string): Set<string> {
  const members = new Set<string>();
  let inFence = false;
  let multiLineBuf = "";

  for (const rawLine of apiMd.split("\n")) {
    const line = rawLine.trimEnd();

    if (!inFence) {
      if (line.startsWith("```ts")) {
        inFence = true;
      }

      continue;
    }

    if (line === "```") {
      inFence = false;

      if (multiLineBuf.length > 0) {
        members.add(multiLineBuf.trimEnd());
        multiLineBuf = "";
      }

      continue;
    }

    // Skip release-tag comment lines and blank lines at top level of the fence.
    if (multiLineBuf.length === 0 && (line.startsWith("//") || line === "")) {
      continue;
    }

    // Accumulate multi-line member declarations (e.g. object-literal exports).
    multiLineBuf += (multiLineBuf.length > 0 ? "\n" : "") + line;

    // A declaration terminates when we see a standalone `;` or `};` at the
    // start of the line, OR the line ends with `;` and the buffer is exactly
    // one logical member.
    const trimmed = line.trim();

    if (trimmed === "}" || trimmed === "};") {
      members.add(multiLineBuf.trimEnd());
      multiLineBuf = "";
    } else if (
      multiLineBuf.split("\n").length === 1 &&
      (trimmed.endsWith(";") || trimmed.endsWith(">;"))
    ) {
      members.add(multiLineBuf.trimEnd());
      multiLineBuf = "";
    }
  }

  return members;
}

/**
 * Derive the member name (identifier) from a declaration line so that a
 * signature change (same name, different type) is classified as `changed`
 * rather than a separate `removed` + `added`.
 *
 * Extracts the identifier after `export const`, `export type`, `export
 * function`, `export class`, `export interface`, `export enum` — the first
 * word after the export keyword (or after `export default`).
 */
export function extractMemberName(declaration: string): string {
  // First line of a potentially multi-line declaration
  const firstLine = declaration.split("\n")[0] ?? declaration;
  const match =
    /^export\s+(?:const|type|function|class|interface|enum|abstract\s+class)\s+(\w+)/.exec(
      firstLine,
    );

  if (match?.[1] !== undefined) return match[1];

  // Fallback: use the whole first line as a unique key
  return firstLine.trim();
}

/**
 * Diff two sets of member-declaration strings and return an ApiDiff.
 *
 * Members present only in `fresh` are `added`.
 * Members present only in `baseline` are `removed`.
 * Members whose NAME matches but whose full declaration differs are `changed`
 * (one `removed` + one `added` entry for the same identifier).
 */
export function diffApiMembers(
  baseline: Set<string>,
  fresh: Set<string>,
): ApiDiff {
  // Build name→declaration maps for change detection.
  const baselineByName = new Map<string, string>();

  for (const decl of baseline) {
    baselineByName.set(extractMemberName(decl), decl);
  }

  const freshByName = new Map<string, string>();

  for (const decl of fresh) {
    freshByName.set(extractMemberName(decl), decl);
  }

  let added = false;
  let removed = false;
  let changed = false;

  // Iterate fresh members: detect added + changed.
  for (const [name, freshDecl] of freshByName) {
    const baseDecl = baselineByName.get(name);

    if (baseDecl === undefined) {
      added = true;
    } else if (baseDecl !== freshDecl) {
      changed = true;
    }
  }

  // Iterate baseline members: detect removed.
  for (const name of baselineByName.keys()) {
    if (!freshByName.has(name)) {
      removed = true;
    }
  }

  return { added, removed, changed };
}

// ---------------------------------------------------------------------------
// Baseline path helpers
// ---------------------------------------------------------------------------

/**
 * Resolve the api.md report path for a TS package by reading the
 * `apiReport.reportFileName` field from its `api-extractor.json`.
 *
 * Using `reportFileName` from the config (rather than `basename(packageDir)`)
 * is required for packages whose directory basename differs from their report
 * name — e.g. `headers/amqp` → `etc/headers-amqp.api.md`, not
 * `etc/amqp.api.md`.
 *
 * Falls back to `<basename(packageDir)>.api.md` when `api-extractor.json` is
 * absent or does not specify `apiReport.reportFileName`, so flat packages
 * (where basename matches) degrade gracefully.
 *
 * @param packageDir - Absolute path to the package root.
 * @param configPath - Absolute path to `api-extractor.json`.
 * @returns Absolute path to the expected `etc/<reportFileName>` file.
 */
export function resolveApiMdPath(
  packageDir: string,
  configPath: string,
): string {
  let reportFileName: string | undefined;

  if (existsSync(configPath)) {
    try {
      const raw = JSON.parse(readFileSync(configPath, "utf-8")) as {
        apiReport?: { reportFileName?: string };
      };
      reportFileName = raw.apiReport?.reportFileName;
    } catch {
      // Malformed JSON — fall through to basename fallback.
    }
  }

  const dirBasename = packageDir.split(/[\\/]/).at(-1) ?? "";
  const name = reportFileName ?? `${dirBasename}.api.md`;

  return join(packageDir, "etc", name);
}

/**
 * Return the path to the committed source-based fingerprint baseline file for a
 * TS package (`etc/.release-fingerprint`, mirroring the .NET filename for a
 * single mental model across both ecosystems).
 *
 * @param packageDir - Absolute path to the package root.
 */
export function tsFingerprintBaselinePath(packageDir: string): string {
  return join(packageDir, "etc", ".release-fingerprint");
}
