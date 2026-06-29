// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Fingerprint-currency checker — pre-commit gate that blocks commits when the
// on-disk committed baselines are STALE (source changed without re-seeding).
//
// Polarity (this is different from drift-check.ts):
//   drift-check.ts    — compares recomputed fingerprint vs git HEAD (CI/PR gate;
//                       fires when a source change lands without a version bump).
//   THIS MODULE        — compares recomputed fingerprint vs the ON-DISK
//                       .release-fingerprint file (pre-commit gate; fires when
//                       source changes without re-running the seed).
//
// Two checks run per package:
//   A. Fingerprint currency — recompute composeSourceFingerprint over the
//      working-tree source and compare to the on-disk .release-fingerprint.
//      A mismatch means the contributor changed source without running the seed.
//   B. Unshipped-empty check (.NET consumables only) — every .NET consumable's
//      PublicAPI.Unshipped.txt must be header-only (only the `#nullable enable`
//      line, no API lines). A non-empty Unshipped means the author added public
//      API without promoting it to Shipped.txt (or forgetting to run the seed
//      which does the promotion step).
//
// This module is PURE over injected seams — no real fs or git calls in the
// core functions — so it is exhaustively unit-testable with synthetic inputs.

import { readFileSync } from "node:fs";
import { join, isAbsolute, resolve } from "node:path";
import { falsey } from "@d2/utilities";
import {
  fingerprintBaselinePath,
  unshippedTxtPath,
} from "./nuget-extractor.js";
import { tsFingerprintBaselinePath } from "./ts-api-adapter.js";
import {
  buildSourceDump,
  composeSourceFingerprint,
  listSourceFiles,
  makeRepoFileReader,
  readToolchainPin,
  type RepoFileReader,
  type SourceFileReader,
} from "./source-fingerprint.js";
import {
  buildNugetManifestMeta,
  buildNpmManifestMeta,
  substituteResolvedDeps,
  type SourceLister,
} from "./real-diff-provider.js";
import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Result types
// ---------------------------------------------------------------------------

/** Reasons a single package failed the currency gate. */
export type StaleReason =
  | "fingerprint-mismatch"
  | "unshipped-not-empty"
  | "baseline-missing";

/** The currency verdict for a single package. */
export interface PackageCurrencyResult {
  readonly name: string;
  readonly ecosystem: string;
  /** True when the on-disk baseline exists and matches the recompute. */
  readonly current: boolean;
  /** The reasons the package is stale (empty when current). */
  readonly reasons: readonly StaleReason[];
  /** Human-readable detail for the report table. */
  readonly detail: string;
}

/** The aggregate currency-check result across all packages. */
export interface FingerprintCurrencyResult {
  readonly results: readonly PackageCurrencyResult[];
  /** The subset of results whose baseline is stale. */
  readonly stale: readonly PackageCurrencyResult[];
  /** True when every package is current. */
  readonly allCurrent: boolean;
}

// ---------------------------------------------------------------------------
// Injectable seams for testability
// ---------------------------------------------------------------------------

/**
 * Reads a file's content given its absolute path, or returns undefined when
 * the file does not exist. Injectable so the currency checker is testable
 * without real package trees.
 */
export type CurrencyFileReader = (absolutePath: string) => string | undefined;

/** Default CurrencyFileReader — reads from disk. */
export function makeRealCurrencyFileReader(): CurrencyFileReader {
  return (absolutePath: string): string | undefined => {
    try {
      return readFileSync(absolutePath, "utf-8");
    } catch {
      return undefined;
    }
  };
}

/** Options for checkFingerprintCurrency. */
export interface FingerprintCurrencyOptions {
  /** Inject a synthetic file reader (default: real fs reader). */
  readonly fileReader?: CurrencyFileReader;
  /**
   * Inject a synthetic source-file lister (default: real fs walk via
   * listSourceFiles with git-tracked lister). Tests inject a fixed file set.
   */
  readonly sourceLister?: SourceLister;
  /**
   * Inject a synthetic repo-file reader for toolchain pin
   * (default: real fs reader rooted at repoRoot).
   */
  readonly toolchainReader?: RepoFileReader;
}

// ---------------------------------------------------------------------------
// Per-ecosystem currency helpers
// ---------------------------------------------------------------------------

/** Recompute the NuGet source-based fingerprint for the given package. */
function recomputeNugetFingerprint(
  pkg: PackageDescriptor,
  packageDir: string,
  fileReader: CurrencyFileReader,
  sourceLister: SourceLister,
  toolchainReader: RepoFileReader,
  allResolved: ReadonlyMap<string, string>,
): string {
  const sourceRead: SourceFileReader = (relPosix) =>
    fileReader(join(packageDir, relPosix)) ?? "";
  const sourceDump = buildSourceDump(
    sourceLister(packageDir, "nuget"),
    sourceRead,
  );

  const shippedAbs = join(packageDir, "PublicAPI.Shipped.txt");
  const unshippedAbs = unshippedTxtPath(pkg.manifestPath);
  const headShipped = fileReader(shippedAbs) ?? "";
  const headUnshipped = fileReader(unshippedAbs) ?? "";

  return composeSourceFingerprint({
    sourceDump,
    apiReport: headShipped + headUnshipped,
    depsJson: buildNugetManifestMeta(pkg, allResolved),
    toolchainJson: readToolchainPin("nuget", toolchainReader),
  });
}

/**
 * Resolve the `etc/<name>.api.md` path for a TS package using the injected
 * fileReader — mirrors the logic of `resolveApiMdPath` but reads
 * `api-extractor.json` via the fileReader seam rather than `existsSync`, so
 * the npm fingerprint recompute is testable with synthetic inputs.
 */
function resolveApiMdPathFromReader(
  packageDir: string,
  fileReader: CurrencyFileReader,
): string {
  const configPath = join(packageDir, "api-extractor.json");
  const configText = fileReader(configPath);
  let reportFileName: string | undefined;

  if (configText !== undefined) {
    try {
      const raw = JSON.parse(configText) as {
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

/** Recompute the npm source-based fingerprint for the given package. */
function recomputeNpmFingerprint(
  _pkg: PackageDescriptor,
  packageDir: string,
  fileReader: CurrencyFileReader,
  sourceLister: SourceLister,
  toolchainReader: RepoFileReader,
  allResolved: ReadonlyMap<string, string>,
): string {
  const sourceRead: SourceFileReader = (relPosix) =>
    fileReader(join(packageDir, relPosix)) ?? "";
  const sourceDump = buildSourceDump(
    sourceLister(packageDir, "npm"),
    sourceRead,
  );

  const reportPath = resolveApiMdPathFromReader(packageDir, fileReader);
  const headApiMd = fileReader(reportPath) ?? "";

  const packageJsonText = fileReader(join(packageDir, "package.json"));
  const packageJson =
    packageJsonText === undefined
      ? {}
      : (JSON.parse(packageJsonText) as {
          name?: string;
          version?: string;
          dependencies?: Record<string, string>;
        });

  // Use the full cross-package resolved-version map (all consumables at their
  // committed versions) so that @d2/* dep substitution matches the seed exactly.
  // A narrow map (own name only) left every @d2/* dep unresolved → kept its
  // workspace:* literal → DEPS input differed from the seed → false positive.
  const substituted = substituteResolvedDeps(packageJson, allResolved);

  return composeSourceFingerprint({
    sourceDump,
    apiReport: headApiMd,
    depsJson: buildNpmManifestMeta(substituted),
    toolchainJson: readToolchainPin("npm", toolchainReader),
  });
}

// ---------------------------------------------------------------------------
// Unshipped-empty check (.NET only)
// ---------------------------------------------------------------------------

/**
 * Return true when the Unshipped.txt content is header-only — only the
 * `#nullable enable` pragma line (possibly with surrounding whitespace/blanks),
 * no actual API lines.
 *
 * @param content - Raw text of PublicAPI.Unshipped.txt.
 */
export function isUnshippedHeaderOnly(content: string): boolean {
  for (const rawLine of content.split("\n")) {
    const line = rawLine.trimEnd();

    if (truthy(line) && !line.startsWith("#")) return false;
  }

  return true;
}

// Inline truthy for non-empty strings (avoids importing D2 Truthy which is
// for D2Result/collections; here we just need "non-blank line" check).
function truthy(s: string): boolean {
  return s.length > 0;
}

// ---------------------------------------------------------------------------
// Core — checkFingerprintCurrency
// ---------------------------------------------------------------------------

/**
 * For each consumable package, recompute the source-based fingerprint over
 * the WORKING TREE (reading on-disk files) and compare to the ON-DISK
 * `.release-fingerprint`. Also assert every .NET consumable's
 * PublicAPI.Unshipped.txt is header-only.
 *
 * A mismatch (recomputed ≠ on-disk) means the source changed without
 * re-running the seed — that package is STALE.
 *
 * @param packages  - The consumable package inventory.
 * @param repoRoot  - Absolute path to the repository root.
 * @param options   - Injectable seams (default: real fs / git readers).
 */
export function checkFingerprintCurrency(
  packages: readonly PackageDescriptor[],
  repoRoot: string,
  options: FingerprintCurrencyOptions = {},
): FingerprintCurrencyResult {
  if (falsey(packages)) return { results: [], stale: [], allCurrent: true };

  const fileReader = options.fileReader ?? makeRealCurrencyFileReader();
  const sourceLister = options.sourceLister ?? listSourceFiles;
  const toolchainReader =
    options.toolchainReader ?? makeRepoFileReader(repoRoot);

  // Build the full cross-package resolved-version map (every consumable at its
  // committed version) so that @d2/* dep substitution in the npm composition
  // matches what the seed script writes. Mirrors drift-check's map exactly:
  // packages.map(p => [p.name, p.currentVersion]). A narrow per-package map
  // (own name only) left @d2/* deps unresolved, producing false positives.
  const allResolved = new Map<string, string>(
    packages.map((p) => [p.name, p.currentVersion]),
  );

  const results: PackageCurrencyResult[] = [];

  for (const pkg of packages) {
    const packageDir = isAbsolute(pkg.dir)
      ? pkg.dir
      : resolve(repoRoot, pkg.dir);

    const reasons: StaleReason[] = [];

    // ----- Fingerprint currency check -----

    const fingerprintPath =
      pkg.ecosystem === "nuget"
        ? fingerprintBaselinePath(pkg.manifestPath)
        : tsFingerprintBaselinePath(packageDir);

    const onDisk = fileReader(fingerprintPath)?.trim();

    if (onDisk === undefined) {
      reasons.push("baseline-missing");
    } else {
      const fresh =
        pkg.ecosystem === "nuget"
          ? recomputeNugetFingerprint(
              pkg,
              packageDir,
              fileReader,
              sourceLister,
              toolchainReader,
              allResolved,
            )
          : recomputeNpmFingerprint(
              pkg,
              packageDir,
              fileReader,
              sourceLister,
              toolchainReader,
              allResolved,
            );

      if (fresh !== onDisk) reasons.push("fingerprint-mismatch");
    }

    // ----- Unshipped-empty check (.NET only) -----

    if (pkg.ecosystem === "nuget") {
      const unshippedPath = unshippedTxtPath(pkg.manifestPath);
      const unshippedContent = fileReader(unshippedPath) ?? "";

      if (!isUnshippedHeaderOnly(unshippedContent)) {
        reasons.push("unshipped-not-empty");
      }
    }

    const current = falsey(reasons);
    const detailParts: string[] = [];

    for (const r of reasons) {
      if (r === "baseline-missing") detailParts.push("baseline missing");
      else if (r === "fingerprint-mismatch")
        detailParts.push("fingerprint mismatch");
      else if (r === "unshipped-not-empty")
        detailParts.push("Unshipped.txt not empty");
    }

    results.push({
      name: pkg.name,
      ecosystem: pkg.ecosystem,
      current,
      reasons,
      detail: current ? "current" : detailParts.join("; "),
    });
  }

  const stale = results.filter((r) => !r.current);

  return { results, stale, allCurrent: falsey(stale) };
}

// ---------------------------------------------------------------------------
// Reporting
// ---------------------------------------------------------------------------

/**
 * Render a currency-check result as a human-readable table.
 *
 * @param result - The aggregate currency-check result.
 * @returns A multi-line report string (no trailing process control).
 */
export function formatCurrencyReport(
  result: FingerprintCurrencyResult,
): string {
  const lines: string[] = [];

  lines.push("Baseline currency check");
  lines.push("");

  if (result.allCurrent) {
    lines.push(
      `All ${result.results.length.toString()} package baselines are current — working tree matches on-disk fingerprints.`,
    );

    return lines.join("\n") + "\n";
  }

  lines.push(
    `STALE BASELINES detected in ${result.stale.length.toString()} package(s):`,
  );
  lines.push("");
  lines.push("  package | ecosystem | detail");
  lines.push("  ------- | --------- | ------");

  for (const r of result.stale) {
    lines.push(`  ${r.name} | ${r.ecosystem} | ${r.detail}`);
  }

  lines.push("");
  lines.push(
    "Baselines are stale — the source changed without re-seeding. Remediation:",
  );
  lines.push("  .NET :  node tools/scripts/seed-publicapi-baselines.mjs");
  lines.push(
    "  npm  :  pnpm --filter './server/shared/typescript/**' -r build",
  );
  lines.push("          node tools/scripts/seed-apiextractor-baselines.mjs");
  lines.push("Re-stage the updated baseline files and commit again.");

  return lines.join("\n") + "\n";
}
