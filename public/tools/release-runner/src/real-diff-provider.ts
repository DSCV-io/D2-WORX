// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Production DiffProvider — wires the real per-ecosystem extraction into the
// `DiffProvider` seam the artifact-diff engine (diff-runner.ts) consumes.
//
// This is the single home of the per-package FINGERPRINT COMPOSITION, which is
// SOURCE-BASED + PORTABLE — a SHA-256 over committed inputs only, byte-identical
// on every OS/machine, with NO build required:
//
//   fingerprint = SHA-256( committed source dump
//                        + the committed API report (PublicAPI.* / .api.md)
//                        + resolved dependency versions
//                        + the declared toolchain pin )
//
// (composed via composeSourceFingerprint in source-fingerprint.ts).
//
// The apiDiff is likewise build-free: a git-ref TEXT DIFF of the committed API
// report at the baseline ref (`git show <ref>:<path>`) against the HEAD report
// on disk, diffed by the existing pure parsers. Neither the bump nor the
// fingerprint shells `dotnet build` or api-extractor.
//
// PROPAGATION-VIA-FINGERPRINT (no BFS): the engine processes packages in
// topological (leaf-first) order and forwards the in-memory resolved-version map
// to each DiffProvider call. This provider folds those resolved versions into
// the DEPS input (the manifest-metadata `deps` map), so when a dependency bumps,
// the dependent's DEPS input changes → its fingerprint changes → it floors at
// PATCH. The fingerprint is the SINGLE mechanism that drives both the
// source-change floor and the dependency-update floor — there is no separate
// dependency-graph BFS pass.
//
// Injectable design: the readers (committed-source, git-baseline, package.json)
// are injectable so the provider's dispatch + mapping + fingerprint-composition
// logic is unit-testable with synthetic inputs (no real build / no api-extractor
// / no real git).

import { existsSync, readFileSync } from "node:fs";
import { isAbsolute, join, resolve } from "node:path";
import { falsey } from "@dcsv-io/d2-utilities";
import {
  diffShippedLines,
  fingerprintBaselinePath,
  parseShippedTxt,
  shippedTxtPath,
  unshippedTxtPath,
} from "./nuget-extractor.js";
import {
  diffApiMembers,
  makeGitBaselineReader,
  parseApiMembers,
  resolveApiMdPath,
  tsFingerprintBaselinePath,
  type BaselineReader,
} from "./ts-api-adapter.js";
import {
  buildSourceDump,
  composeSourceFingerprint,
  listSourceFiles,
  makeRepoFileReader,
  readToolchainPin,
  type RepoFileReader,
  type SourceEcosystem,
  type SourceFileReader,
} from "./source-fingerprint.js";
import type { ApiDiff, FingerprintDiff } from "./diff-bump.js";
import type {
  DiffProvider,
  DiffProviderInput,
  PackageDiff,
} from "./diff-runner.js";
import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Injectable readers
// ---------------------------------------------------------------------------

/**
 * Reads a file's content given its ABSOLUTE path, or returns undefined when the
 * file does not exist. Used for the committed source files + the committed API
 * report on disk (the HEAD side of the apiDiff). Injectable so the provider's
 * composition logic is unit-testable without a real package tree.
 */
export type FileReader = (absolutePath: string) => string | undefined;

/** Default FileReader — reads from disk via node:fs. */
export function makeRealFileReader(): FileReader {
  return (absolutePath: string): string | undefined => {
    if (!existsSync(absolutePath)) return undefined;

    return readFileSync(absolutePath, "utf-8");
  };
}

/**
 * Lists the repo-relative committed-source paths under a package dir for an
 * ecosystem (default: `listSourceFiles`, a real fs walk). Injectable so the
 * provider's composition is testable against a synthetic file set without a real
 * package tree on disk.
 */
export type SourceLister = (
  packageDir: string,
  ecosystem: SourceEcosystem,
) => string[];

/**
 * Options for `makeRealDiffProvider`.
 */
export interface RealDiffProviderOptions {
  /** Inject a synthetic committed-file reader (default: real fs reader). */
  readonly fileReader?: FileReader;
  /**
   * Inject a synthetic source-file lister (default: real fs walk via
   * `listSourceFiles`). Tests inject a fixed file set so the composition runs
   * without a real package tree.
   */
  readonly sourceLister?: SourceLister;
  /** Inject a synthetic git-baseline reader (default: real `git show HEAD:`). */
  readonly baselineReader?: BaselineReader;
  /**
   * Inject a synthetic repo-file reader for the toolchain pin
   * (default: real fs reader rooted at `repoRoot`).
   */
  readonly toolchainReader?: RepoFileReader;
  /**
   * The baseline git ref for the apiDiff's `git show <ref>:<path>` read.
   * Defaults to "HEAD" (the drift / no-op comparison). The release run passes
   * its release baseline ref.
   */
  readonly baselineRef?: string;
}

// ---------------------------------------------------------------------------
// .NET dependency metadata (DEPS input)
// ---------------------------------------------------------------------------

/**
 * Build the deterministic DEPS (manifest-metadata) JSON for a NuGet package,
 * folding in the resolved dependency versions so a dependency bump moves the
 * fingerprint.
 *
 * Only this package's consumable dependencies are included (sorted), each mapped
 * to its resolved version (from `resolvedVersions`, falling back to ""). The
 * package's own resolved version is included too.
 */
export function buildNugetManifestMeta(
  pkg: PackageDescriptor,
  resolvedVersions: ReadonlyMap<string, string>,
): string {
  const deps: Record<string, string> = {};

  for (const depName of [...pkg.dependencies].sort()) {
    deps[depName] = resolvedVersions.get(depName) ?? "";
  }

  return JSON.stringify({
    packageId: pkg.name,
    version: resolvedVersions.get(pkg.name) ?? pkg.currentVersion,
    deps,
  });
}

/**
 * Substitute each `@dcsv-io/d2-*` dependency's `workspace:*` (or any) version literal
 * with its resolved version from `resolvedVersions`, then serialize the
 * deterministic DEPS JSON for a TS package. A non-consumable or unresolved
 * dependency keeps its original literal. This is what makes a dependency bump
 * move a TS dependent's fingerprint (propagation).
 */
export function substituteResolvedDeps(
  packageJson: {
    name?: string;
    version?: string;
    dependencies?: Record<string, string>;
  },
  resolvedVersions: ReadonlyMap<string, string>,
): { name?: string; version?: string; dependencies?: Record<string, string> } {
  const deps = packageJson.dependencies ?? {};
  const substituted: Record<string, string> = {};

  for (const [name, literal] of Object.entries(deps)) {
    const resolved = resolvedVersions.get(name);
    substituted[name] = resolved ?? literal;
  }

  return {
    name: packageJson.name,
    // The package's own version also moves the fingerprint when it is bumped.
    version:
      (packageJson.name !== undefined
        ? resolvedVersions.get(packageJson.name)
        : undefined) ?? packageJson.version,
    dependencies: substituted,
  };
}

/**
 * Serialize a TS package's DEPS (manifest-metadata) JSON from its substituted
 * `{name, version, dependencies}` subset. Mirrors the shape the seed writes.
 */
export function buildNpmManifestMeta(substituted: {
  name?: string;
  version?: string;
  dependencies?: Record<string, string>;
}): string {
  return JSON.stringify({
    name: substituted.name ?? "",
    version: substituted.version ?? "",
    dependencies: substituted.dependencies ?? {},
  });
}

/**
 * Read + parse a package.json into the metadata subset used for fingerprinting.
 * Exported so the found / not-found branches are unit-testable directly.
 *
 * @param packageDir - Absolute path to the package root.
 * @returns The `{ name, version, dependencies }` subset.
 * @throws {Error} When no package.json exists at `packageDir`.
 */
export function readPackageJsonFile(packageDir: string): {
  name?: string;
  version?: string;
  dependencies?: Record<string, string>;
} {
  const packageJsonPath = join(packageDir, "package.json");

  if (!existsSync(packageJsonPath)) {
    throw new Error(`package.json not found at ${packageJsonPath}`);
  }

  return JSON.parse(readFileSync(packageJsonPath, "utf-8")) as {
    name?: string;
    version?: string;
    dependencies?: Record<string, string>;
  };
}

// ---------------------------------------------------------------------------
// Per-ecosystem getDiff implementations
// ---------------------------------------------------------------------------

/** The resolved seam set threaded into the per-ecosystem getDiff functions. */
interface ResolvedSeams {
  readonly fileReader: FileReader;
  readonly sourceLister: SourceLister;
  readonly baselineReader: BaselineReader;
  readonly toolchainReader: RepoFileReader;
  readonly baselineRef: string;
}

function getNugetDiff(
  input: DiffProviderInput,
  packageDir: string,
  seams: ResolvedSeams,
): PackageDiff {
  const { pkg, resolvedVersions } = input;
  const {
    fileReader,
    sourceLister,
    baselineReader,
    toolchainReader,
    baselineRef,
  } = seams;

  // --- API surface diff: git-ref text diff of PublicAPI.Shipped.txt --------

  const shippedAbs = shippedTxtPath(pkg.manifestPath);
  const unshippedAbs = unshippedTxtPath(pkg.manifestPath);

  const headShipped = fileReader(shippedAbs) ?? "";
  const headUnshipped = fileReader(unshippedAbs) ?? "";

  const baselineShipped = baselineReader.read(shippedAbs, baselineRef);

  let apiDiff: ApiDiff;

  if (baselineShipped === undefined) {
    const headMembers = parseShippedTxt(headShipped);
    apiDiff = { added: headMembers.size > 0, removed: false, changed: false };
  } else {
    apiDiff = diffShippedLines(
      parseShippedTxt(baselineShipped),
      parseShippedTxt(headShipped),
    );
  }

  // --- Source-based fingerprint --------------------------------------------

  const sourceRead: SourceFileReader = (relPosix) =>
    fileReader(join(packageDir, relPosix)) ?? "";
  const sourceDump = buildSourceDump(
    sourceLister(packageDir, "nuget"),
    sourceRead,
  );

  const freshFingerprint = composeSourceFingerprint({
    sourceDump,
    apiReport: headShipped + headUnshipped,
    depsJson: buildNugetManifestMeta(pkg, resolvedVersions),
    toolchainJson: readToolchainPin("nuget", toolchainReader),
  });

  const committed = fileReader(
    fingerprintBaselinePath(pkg.manifestPath),
  )?.trim();
  const baselineMissing =
    committed === undefined || baselineShipped === undefined;

  const fingerprintDiff: FingerprintDiff = {
    changed: committed === undefined || committed !== freshFingerprint,
  };

  return { apiDiff, fingerprintDiff, baselineMissing };
}

function getTsDiff(
  input: DiffProviderInput,
  packageDir: string,
  seams: ResolvedSeams,
): PackageDiff {
  const { resolvedVersions } = input;
  const {
    fileReader,
    sourceLister,
    baselineReader,
    toolchainReader,
    baselineRef,
  } = seams;

  // --- API surface diff: git-ref text diff of etc/<pkg>.api.md -------------

  const configPath = join(packageDir, "api-extractor.json");
  const reportPath = resolveApiMdPath(packageDir, configPath);

  const headApiMd = fileReader(reportPath) ?? "";
  const baselineApiMd = baselineReader.read(reportPath, baselineRef);

  let apiDiff: ApiDiff;

  if (baselineApiMd === undefined) {
    const headMembers = parseApiMembers(headApiMd);
    apiDiff = { added: headMembers.size > 0, removed: false, changed: false };
  } else {
    apiDiff = diffApiMembers(
      parseApiMembers(baselineApiMd),
      parseApiMembers(headApiMd),
    );
  }

  // --- Source-based fingerprint --------------------------------------------

  const sourceRead: SourceFileReader = (relPosix) =>
    fileReader(join(packageDir, relPosix)) ?? "";
  const sourceDump = buildSourceDump(
    sourceLister(packageDir, "npm"),
    sourceRead,
  );

  const packageJsonText = fileReader(join(packageDir, "package.json"));
  const packageJson =
    packageJsonText === undefined
      ? {}
      : (JSON.parse(packageJsonText) as {
          name?: string;
          version?: string;
          dependencies?: Record<string, string>;
        });
  const substituted = substituteResolvedDeps(packageJson, resolvedVersions);

  const freshFingerprint = composeSourceFingerprint({
    sourceDump,
    apiReport: headApiMd,
    depsJson: buildNpmManifestMeta(substituted),
    toolchainJson: readToolchainPin("npm", toolchainReader),
  });

  const committed = fileReader(tsFingerprintBaselinePath(packageDir))?.trim();
  const baselineMissing =
    committed === undefined || baselineApiMd === undefined;

  const fingerprintDiff: FingerprintDiff = {
    changed: committed === undefined || committed !== freshFingerprint,
  };

  return { apiDiff, fingerprintDiff, baselineMissing };
}

// ---------------------------------------------------------------------------
// Public API — makeRealDiffProvider
// ---------------------------------------------------------------------------

/**
 * Construct the production `DiffProvider` the CLI wires into `runDiffRelease`.
 *
 * Dispatches per `pkg.ecosystem`:
 *   - "nuget" → git-ref PublicAPI diff + source-based fingerprint.
 *   - "npm"   → git-ref .api.md diff + source-based fingerprint.
 *
 * Both branches fold `input.resolvedVersions` into the DEPS fingerprint input so
 * dependency propagation falls out of the fingerprint with no separate BFS pass.
 * Nothing here builds — the whole engine is build-free.
 *
 * @param repoRoot - Absolute path to the repository root (roots the git baseline
 *                   reader + the toolchain-pin reader).
 * @param options  - Optional injected readers (for unit-testing the composition).
 * @returns A DiffProvider whose `getDiff` dispatches per ecosystem.
 */
export function makeRealDiffProvider(
  repoRoot: string,
  options: RealDiffProviderOptions = {},
): DiffProvider {
  const seams: ResolvedSeams = {
    fileReader: options.fileReader ?? makeRealFileReader(),
    sourceLister: options.sourceLister ?? listSourceFiles,
    baselineReader: options.baselineReader ?? makeGitBaselineReader(repoRoot),
    toolchainReader: options.toolchainReader ?? makeRepoFileReader(repoRoot),
    baselineRef: options.baselineRef ?? "HEAD",
  };

  return {
    getDiff(input: DiffProviderInput): PackageDiff {
      if (falsey(input.pkg.ecosystem)) {
        throw new Error(
          `package ${input.pkg.name} has no ecosystem discriminator`,
        );
      }

      // pkg.dir is repo-root-relative (e.g. public/packages/typescript/result);
      // resolve to an absolute path. An already-absolute dir (injected in tests)
      // is passed through unchanged.
      const packageDir = isAbsolute(input.pkg.dir)
        ? input.pkg.dir
        : resolve(repoRoot, input.pkg.dir);

      if (input.pkg.ecosystem === "nuget") {
        return getNugetDiff(input, packageDir, seams);
      }

      return getTsDiff(input, packageDir, seams);
    },
  };
}
