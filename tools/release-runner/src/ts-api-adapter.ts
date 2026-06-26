// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// TS extraction adapter — api surface + dist fingerprint for npm packages.
//
// Produces ApiDiff and FingerprintDiff (as defined in diff-bump.ts) for a
// single @d2/* package by:
//
//   A. Running @microsoft/api-extractor against the package's committed
//      etc/<name>.api.md baseline to detect public-API changes.
//
//   B. Hashing the package's built dist/ output (comment-stripped .js +
//      .d.ts) combined with package.json runtime metadata to detect internal
//      changes not visible in the API surface.
//
// Both IO operations are behind injectable seams so tests can supply
// pre-built fixtures without spawning real tooling.
//
// Baseline files:
//   etc/<pkgShortName>.api.md   — committed API report (managed by api-extractor)
//   etc/dist-fingerprint.txt    — committed dist hash (managed by this module)
//
// --- Comment-handling decision ---
//
// tsc with the base tsconfig does NOT strip JS comments (removeComments is
// absent / false in tsconfig.base.json). A comment-only source edit would
// therefore change the dist .js output and WOULD change the fingerprint.
//
// Rather than claim the limitation silently, this adapter normalises the
// .js content before hashing: it strips single-line (//) comments, block
// (/* */) comments, and sourcemap URL tail comments, then collapses runs
// of blank lines. .d.ts files are hashed verbatim (they are part of the
// public type surface and comment changes there intentionally trigger a
// bump — they affect generated documentation).
//
// Stability evidence: see tests/ts-api-adapter.test.ts which proves that
// a comment-only .js edit produces the SAME fingerprint as the baseline,
// while an internal logic edit produces a DIFFERENT fingerprint.

import { createHash } from "node:crypto";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { createRequire } from "node:module";
import { join, relative } from "node:path";
import { spawnSync } from "node:child_process";
import type { ApiDiff, FingerprintDiff } from "./diff-bump.js";

// api-extractor is a CommonJS package; this module is ESM. `require` is not a
// global in ESM scope, so build one bound to this module's URL. (A bare
// `require(...)` works under vitest's shim but throws under a plain tsx/node ESM
// run such as the drift-check CLI lane.)
const r_require = createRequire(import.meta.url);

// ---------------------------------------------------------------------------
// Injectable IO seams
// ---------------------------------------------------------------------------

/**
 * Seam for reading the committed baseline content of a file (typically via
 * `git show HEAD:<path>` for the real adapter). Returns `undefined` when
 * the file has no committed version.
 */
export interface BaselineReader {
  /**
   * Read the committed content of `filePath` (repo-root-relative or
   * absolute). Returns `undefined` when not committed.
   */
  read(filePath: string): string | undefined;
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

/**
 * Seam for reading and walking the dist/ directory of a package.
 */
export interface DistReader {
  /**
   * Return the sorted list of absolute file paths in `distDir` matching the
   * given extensions (e.g. [".js", ".d.ts"]).  Does not recurse into
   * sub-directories of sub-directories beyond the dist root.
   */
  listFiles(distDir: string, extensions: string[]): string[];

  /**
   * Read the content of a file (utf-8).
   */
  readFile(filePath: string): string;
}

// ---------------------------------------------------------------------------
// Real implementations of the seams
// ---------------------------------------------------------------------------

/**
 * Real BaselineReader — reads from `git show HEAD:<repo-root-relative-path>`.
 */
export function makeGitBaselineReader(repoRoot: string): BaselineReader {
  return {
    read(filePath: string): string | undefined {
      const rel = relative(repoRoot, filePath).replace(/\\/g, "/");
      const result = spawnSync("git", ["show", `HEAD:${rel}`], {
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
 * CI should use localBuild: false to get a failure on drift instead (the
 * adapter always reads the freshly-generated file, so either mode works for
 * deriving the diff).
 *
 * This function returns the fresh .api.md string so the adapter can diff it
 * against the committed baseline without the caller needing to read from disk.
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

/**
 * Real DistReader — walks `dist/` recursively and reads files from disk.
 */
export function makeRealDistReader(): DistReader {
  return {
    listFiles(distDir: string, extensions: string[]): string[] {
      const results: string[] = [];
      walkDir(distDir, extensions, results);

      return results.sort();
    },

    readFile(filePath: string): string {
      return readFileSync(filePath, "utf-8");
    },
  };
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

/**
 * Recursively walk `dir`, collecting files whose extension is in `extensions`.
 */
function walkDir(dir: string, extensions: string[], out: string[]): void {
  if (!existsSync(dir)) return;

  const entries = readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const full = join(dir, entry.name);

    if (entry.isDirectory()) {
      walkDir(full, extensions, out);
    } else if (entry.isFile()) {
      const ext = entry.name.slice(entry.name.lastIndexOf("."));

      if (extensions.includes(ext)) out.push(full);
    }
  }
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
// B — Dist fingerprint
// ---------------------------------------------------------------------------

/**
 * Strip single-line and block JS comments from a .js file's content, then
 * collapse consecutive blank lines to a single blank line.
 *
 * This ensures that comment-only edits do not change the fingerprint.
 *
 * Note: This is a best-effort normalisation adequate for clean TypeScript
 * compiler output (no embedded comment-like strings that would be
 * mis-stripped). It intentionally does NOT use a full JS parser — the
 * goal is fingerprint stability, not source transformation.
 */
export function normaliseJsForFingerprint(js: string): string {
  // Remove block comments first (non-greedy, DOTALL).
  let s = js.replace(/\/\*[\s\S]*?\*\//g, "");

  // Remove single-line comments (including sourcemap URLs: //# sourceMappingURL=...).
  s = s.replace(/\/\/[^\n]*/g, "");

  // Collapse runs of blank / whitespace-only lines.
  s = s.replace(/(\r?\n\s*){2,}/g, "\n\n");

  return s.trim();
}

/**
 * Compute the dist fingerprint for a package.
 *
 * Hashes (SHA-256):
 *   - All .js files under dist/ (comment-normalised content)
 *   - All .d.ts files under dist/ (verbatim — part of public type surface)
 *   - The package.json `name`, `version`, and `dependencies` object
 *     (workspace:* resolved to concrete if a resolver is provided; this
 *     module stores the raw strings — the runtime engine resolves before
 *     calling computeDistFingerprint if needed)
 *
 * Files are processed in sorted path order for determinism.
 *
 * @param packageDir  - Absolute path to the package root.
 * @param packageJson - Parsed package.json object (or its relevant subset).
 * @param distReader  - Injectable DistReader seam.
 */
export function computeDistFingerprint(
  packageDir: string,
  packageJson: {
    name?: string;
    version?: string;
    dependencies?: Record<string, string>;
  },
  distReader: DistReader,
): string {
  const distDir = join(packageDir, "dist");
  const hash = createHash("sha256");

  // Hash .js files (comment-stripped).
  const jsFiles = distReader.listFiles(distDir, [".js"]);

  for (const filePath of jsFiles) {
    const relPath = relative(packageDir, filePath).replace(/\\/g, "/");
    const content = distReader.readFile(filePath);
    const normalised = normaliseJsForFingerprint(content);

    hash.update(`JS:${relPath}\n${normalised}\n`);
  }

  // Hash .d.ts files (verbatim).
  const dtsFiles = distReader.listFiles(distDir, [".d.ts"]);

  for (const filePath of dtsFiles) {
    const relPath = relative(packageDir, filePath).replace(/\\/g, "/");
    const content = distReader.readFile(filePath);

    hash.update(`DTS:${relPath}\n${content}\n`);
  }

  // Hash package.json runtime metadata.
  const meta = JSON.stringify({
    name: packageJson.name ?? "",
    version: packageJson.version ?? "",
    dependencies: packageJson.dependencies ?? {},
  });

  hash.update(`PKG:${meta}\n`);

  return hash.digest("hex");
}

/**
 * Read the committed dist-fingerprint baseline from
 * `<packageDir>/etc/dist-fingerprint.txt`.
 *
 * Returns `undefined` when no committed baseline exists yet (first run).
 */
export function readCommittedFingerprint(
  packageDir: string,
  baselineReader: BaselineReader,
): string | undefined {
  const fingerprintPath = join(packageDir, "etc", "dist-fingerprint.txt");

  return baselineReader.read(fingerprintPath)?.trim();
}

// ---------------------------------------------------------------------------
// Shared path helper
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

// ---------------------------------------------------------------------------
// C — The combined adapter entry point
// ---------------------------------------------------------------------------

/**
 * Options for extractTsPackageDiff.
 */
export interface TsPackageDiffOptions {
  /**
   * Absolute path to the package root (directory containing package.json).
   */
  readonly packageDir: string;

  /**
   * Absolute path to the api-extractor.json for this package.
   * Defaults to `<packageDir>/api-extractor.json`.
   */
  readonly apiExtractorConfigPath?: string;

  /** Injectable seam for reading committed baselines (default: git). */
  readonly baselineReader?: BaselineReader;

  /** Injectable seam for running api-extractor (default: real runner). */
  readonly apiExtractorRunner?: ApiExtractorRunner;

  /** Injectable seam for reading dist/ files (default: fs). */
  readonly distReader?: DistReader;

  /**
   * When true, api-extractor is run in localBuild mode (updates etc/*.api.md
   * in-place, does not fail on drift). Default: true.
   *
   * CI should set this to false to get build failures on uncommitted API
   * surface changes.
   */
  readonly localBuild?: boolean;
}

/**
 * Result of a TS package diff extraction.
 */
export interface TsPackageDiffResult {
  readonly apiDiff: ApiDiff;
  readonly fingerprintDiff: FingerprintDiff;
  /**
   * The freshly-computed fingerprint hex string. The caller may persist this
   * to `etc/dist-fingerprint.txt` to update the baseline.
   */
  readonly freshFingerprint: string;
  /**
   * The committed baseline fingerprint, or `undefined` if none exists yet.
   */
  readonly baselineFingerprint: string | undefined;
}

/**
 * Extract the ApiDiff and FingerprintDiff for a single TypeScript package.
 *
 * Requires:
 *   - The package to have a built `dist/` directory.
 *   - An `api-extractor.json` at `<packageDir>/api-extractor.json` (or
 *     overridden via `options.apiExtractorConfigPath`).
 *   - A committed `etc/<name>.api.md` baseline (api-extractor generates it on
 *     first run with `localBuild: true`).
 *
 * @returns TsPackageDiffResult with both diffs and the fresh fingerprint.
 */
export function extractTsPackageDiff(
  options: TsPackageDiffOptions,
): TsPackageDiffResult {
  const {
    packageDir,
    apiExtractorConfigPath = join(packageDir, "api-extractor.json"),
    localBuild = true,
  } = options;

  const baselineReader =
    options.baselineReader ?? makeGitBaselineReader(packageDir);

  const apiExtractorRunner =
    options.apiExtractorRunner ?? makeRealApiExtractorRunner(localBuild);

  const distReader = options.distReader ?? makeRealDistReader();

  // -------------------------------------------------------------------------
  // A — API surface diff
  // -------------------------------------------------------------------------

  // Run api-extractor — returns the freshly-generated .api.md content.
  const freshApiMd = apiExtractorRunner.run(packageDir, apiExtractorConfigPath);

  // Read the committed baseline for the .api.md (the version BEFORE this run
  // updated it in localBuild mode). In non-local mode the file on disk IS the
  // baseline (api-extractor would have failed if they differ, so we read from
  // git to stay consistent regardless of mode).
  //
  // Derive the report path from api-extractor.json's reportFileName so that
  // packages whose directory basename differs from their report name are
  // resolved correctly (e.g. headers/amqp → etc/headers-amqp.api.md).
  const reportPath = resolveApiMdPath(packageDir, apiExtractorConfigPath);
  const committedApiMd = baselineReader.read(reportPath);

  let apiDiff: ApiDiff;

  if (committedApiMd === undefined) {
    // No baseline exists yet — treat everything in the fresh report as added.
    const freshMembers = parseApiMembers(freshApiMd);
    apiDiff = {
      added: freshMembers.size > 0,
      removed: false,
      changed: false,
    };
  } else {
    const baselineMembers = parseApiMembers(committedApiMd);
    const freshMembers = parseApiMembers(freshApiMd);

    apiDiff = diffApiMembers(baselineMembers, freshMembers);
  }

  // -------------------------------------------------------------------------
  // B — Dist fingerprint diff
  // -------------------------------------------------------------------------

  const packageJsonPath = join(packageDir, "package.json");
  const packageJsonText = readFileSync(packageJsonPath, "utf-8");
  const packageJson = JSON.parse(packageJsonText) as {
    name?: string;
    version?: string;
    dependencies?: Record<string, string>;
  };

  const freshFingerprint = computeDistFingerprint(
    packageDir,
    packageJson,
    distReader,
  );

  const baselineFingerprint = readCommittedFingerprint(
    packageDir,
    baselineReader,
  );

  const fingerprintDiff: FingerprintDiff = {
    changed:
      baselineFingerprint === undefined
        ? true // first run: treat as changed so a baseline is established
        : freshFingerprint !== baselineFingerprint,
  };

  return { apiDiff, fingerprintDiff, freshFingerprint, baselineFingerprint };
}
