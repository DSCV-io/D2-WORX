// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Production DiffProvider — wires the real per-ecosystem extraction adapters
// (nuget-extractor.ts / ts-api-adapter.ts) into the `DiffProvider` seam the
// artifact-diff engine (diff-runner.ts) consumes.
//
// This is the single home of the per-package FINGERPRINT COMPOSITION:
//
//   .NET fingerprint = SHA-256( PublicAPI.Shipped.txt
//                             + PublicAPI.Unshipped.txt
//                             + normalized IL dump
//                             + manifest metadata { packageId, version, deps } )
//
//   TS fingerprint   = computeDistFingerprint( dist/**, package.json metadata
//                             with @d2/* dep versions substituted from
//                             resolvedVersions )
//
// PROPAGATION-VIA-FINGERPRINT (no BFS): the engine processes packages in
// topological (leaf-first) order and forwards the in-memory resolved-version map
// to each DiffProvider call. This provider folds those resolved versions into
// the manifest-metadata input (the `deps` map), so when a dependency bumps, the
// dependent's manifest input changes → its fingerprint changes → it floors at
// PATCH. The fingerprint is the SINGLE mechanism that drives both the
// internal-change floor and the dependency-update floor — there is no separate
// dependency-graph BFS pass.
//
// Injectable design: the per-ecosystem extractors are injectable so the
// provider's dispatch + mapping + fingerprint-composition logic is unit-testable
// with synthetic extractor results (no real build, no real api-extractor).

import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { isAbsolute, join, resolve } from "node:path";
import { falsey } from "@d2/utilities";
import {
  extractNugetDiff,
  fingerprintBaselinePath,
  makeRealDotnetShell,
  type DotnetShell,
  type NugetExtractionResult,
} from "./nuget-extractor.js";
import {
  computeDistFingerprint,
  diffApiMembers,
  makeGitBaselineReader,
  makeRealApiExtractorRunner,
  makeRealDistReader,
  parseApiMembers,
  readCommittedFingerprint,
  resolveApiMdPath,
  type ApiExtractorRunner,
  type BaselineReader,
  type DistReader,
} from "./ts-api-adapter.js";
import type { ApiDiff, FingerprintDiff } from "./diff-bump.js";
import type {
  DiffProvider,
  DiffProviderInput,
  PackageDiff,
} from "./diff-runner.js";
import type { PackageDescriptor } from "./types.js";

// ---------------------------------------------------------------------------
// Injectable extractor seams
// ---------------------------------------------------------------------------

/**
 * The NuGet extractor function shape — defaults to the real `extractNugetDiff`.
 * Injectable so the provider's composition logic is unit-testable.
 */
export type NugetExtractor = (
  pkg: PackageDescriptor,
  shell: DotnetShell,
) => NugetExtractionResult;

/**
 * The TS extractor pieces the provider drives. The provider runs api-extractor +
 * reads dist/ + composes the fingerprint itself (so it can substitute resolved
 * dep versions before hashing), rather than delegating to `extractTsPackageDiff`
 * whole — that function reads package.json verbatim and cannot fold in resolved
 * versions. The provider therefore reuses the lower-level api-extractor /
 * dist-reader / fingerprint helpers directly.
 */
export interface TsExtractorSeams {
  readonly baselineReader: BaselineReader;
  readonly apiExtractorRunner: ApiExtractorRunner;
  readonly distReader: DistReader;
  /**
   * Read + parse a package.json into the metadata subset used for fingerprinting.
   * Injectable so tests do not need a real package.json on disk.
   */
  readonly readPackageJson: (packageDir: string) => {
    name?: string;
    version?: string;
    dependencies?: Record<string, string>;
  };
}

/**
 * Options for `makeRealDiffProvider`.
 */
export interface RealDiffProviderOptions {
  /** Inject a synthetic NuGet extractor (default: real `extractNugetDiff`). */
  readonly nugetExtractor?: NugetExtractor;
  /** Inject a synthetic DotnetShell (default: real shell over `repoRoot`). */
  readonly dotnetShell?: DotnetShell;
  /** Inject synthetic TS extractor seams (default: real git + api-extractor + fs). */
  readonly tsSeams?: TsExtractorSeams;
  /**
   * When true (default), run api-extractor in localBuild mode (updates
   * etc/*.api.md in-place). CI drift checks pass false to fail on report drift.
   */
  readonly localBuild?: boolean;
}

// ---------------------------------------------------------------------------
// .NET fingerprint composition
// ---------------------------------------------------------------------------

/**
 * Build the deterministic manifest-metadata JSON for a NuGet package, folding in
 * the resolved dependency versions so a dependency bump moves the fingerprint.
 *
 * Only this package's consumable dependencies are included (sorted), each mapped
 * to its resolved version (from `resolvedVersions`, falling back to the dep's own
 * current version). The package's own version is included too — but the IL dump
 * deliberately EXCLUDES the assembly version, so the manifest is the sole home of
 * version-driven fingerprint movement (no double-counting).
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
 * Compose the .NET output fingerprint from the extraction result + the manifest
 * metadata. SHA-256 over the ordered tuple
 *   ( PublicAPI.Shipped.txt + PublicAPI.Unshipped.txt + IL dump + manifestMeta ).
 *
 * Each component is prefixed + LF-terminated so a boundary shift between two
 * components cannot collide with a content change.
 */
export function composeNugetFingerprint(
  extraction: Pick<
    NugetExtractionResult,
    "shippedTxt" | "unshippedTxt" | "ilDump"
  >,
  manifestMeta: string,
): string {
  const hash = createHash("sha256");
  hash.update(`SHIPPED:\n${normalizeLf(extraction.shippedTxt)}\n`);
  hash.update(`UNSHIPPED:\n${normalizeLf(extraction.unshippedTxt)}\n`);
  hash.update(`IL:\n${normalizeLf(extraction.ilDump)}\n`);
  hash.update(`MANIFEST:\n${manifestMeta}\n`);

  return hash.digest("hex");
}

/** LF-normalize so a CRLF/LF checkout difference cannot perturb the hash. */
function normalizeLf(text: string): string {
  return text.replace(/\r\n/g, "\n");
}

// ---------------------------------------------------------------------------
// Per-ecosystem getDiff implementations
// ---------------------------------------------------------------------------

function getNugetDiff(
  input: DiffProviderInput,
  extractor: NugetExtractor,
  shell: DotnetShell,
): PackageDiff {
  const { pkg, resolvedVersions } = input;

  const extraction = extractor(pkg, shell);

  const manifestMeta = buildNugetManifestMeta(pkg, resolvedVersions);
  const freshFingerprint = composeNugetFingerprint(extraction, manifestMeta);

  const committed = shell
    .readFile(fingerprintBaselinePath(pkg.manifestPath))
    ?.trim();

  const baselineMissing = committed === undefined;
  const fingerprintDiff: FingerprintDiff = {
    changed: baselineMissing || committed !== freshFingerprint,
  };

  return {
    apiDiff: extraction.apiDiff,
    fingerprintDiff,
    baselineMissing,
  };
}

function getTsDiff(
  input: DiffProviderInput,
  seams: TsExtractorSeams,
  repoRoot: string,
): PackageDiff {
  const { pkg, resolvedVersions } = input;

  // pkg.dir is repo-root-relative (e.g. server/shared/typescript/result); the
  // extractor seams need an absolute path. An already-absolute dir (injected in
  // tests) is passed through unchanged.
  const packageDir = isAbsolute(pkg.dir) ? pkg.dir : resolve(repoRoot, pkg.dir);

  // --- API surface diff via api-extractor ---------------------------------

  const configPath = join(packageDir, "api-extractor.json");
  const freshApiMd = seams.apiExtractorRunner.run(packageDir, configPath);

  // Derive the report path from the api-extractor.json config so that packages
  // whose directory basename differs from their reportFileName are resolved
  // correctly (e.g. headers/amqp → etc/headers-amqp.api.md, not etc/amqp.api.md).
  const reportPath = resolveApiMdPath(packageDir, configPath);
  const committedApiMd = seams.baselineReader.read(reportPath);

  let apiDiff: ApiDiff;

  if (committedApiMd === undefined) {
    const freshMembers = parseApiMembers(freshApiMd);
    apiDiff = { added: freshMembers.size > 0, removed: false, changed: false };
  } else {
    apiDiff = diffApiMembers(
      parseApiMembers(committedApiMd),
      parseApiMembers(freshApiMd),
    );
  }

  // --- Dist fingerprint, with resolved @d2/* dep versions substituted ------

  const packageJson = seams.readPackageJson(packageDir);
  const substituted = substituteResolvedDeps(packageJson, resolvedVersions);

  const freshFingerprint = computeDistFingerprint(
    packageDir,
    substituted,
    seams.distReader,
  );

  const baselineFingerprint = readCommittedFingerprint(
    packageDir,
    seams.baselineReader,
  );

  const baselineMissing =
    committedApiMd === undefined || baselineFingerprint === undefined;

  const fingerprintDiff: FingerprintDiff = {
    changed:
      baselineFingerprint === undefined ||
      freshFingerprint !== baselineFingerprint,
  };

  return { apiDiff, fingerprintDiff, baselineMissing };
}

/**
 * Substitute each `@d2/*` dependency's `workspace:*` (or any) version literal
 * with its resolved version from `resolvedVersions`. A non-consumable or
 * unresolved dependency keeps its original literal. This is what makes a
 * dependency bump move a TS dependent's dist fingerprint (propagation).
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
 * Read + parse a package.json into the metadata subset used for fingerprinting.
 * The default `readPackageJson` seam. Exported so the found / not-found branches
 * are unit-testable directly against a real temp file.
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
// Public API — makeRealDiffProvider
// ---------------------------------------------------------------------------

/**
 * Construct the production `DiffProvider` the CLI wires into `runDiffRelease`.
 *
 * Dispatches per `pkg.ecosystem`:
 *   - "nuget" → build + IL dump + compose SHA over PublicAPI.* + IL + manifest.
 *   - "npm"   → api-extractor + dist fingerprint (with resolved dep versions).
 *
 * Both branches fold `input.resolvedVersions` into the fingerprint so dependency
 * propagation falls out of the fingerprint with no separate BFS pass.
 *
 * @param repoRoot - Absolute path to the repository root (locates il-fingerprint
 *                   + the git baseline reader).
 * @param options  - Optional injected seams (for unit-testing the composition).
 * @returns A DiffProvider whose `getDiff` dispatches per ecosystem.
 */
export function makeRealDiffProvider(
  repoRoot: string,
  options: RealDiffProviderOptions = {},
): DiffProvider {
  const nugetExtractor = options.nugetExtractor ?? extractNugetDiff;
  const dotnetShell = options.dotnetShell ?? makeRealDotnetShell(repoRoot);
  const localBuild = options.localBuild ?? true;

  const tsSeams: TsExtractorSeams = options.tsSeams ?? {
    baselineReader: makeGitBaselineReader(repoRoot),
    apiExtractorRunner: makeRealApiExtractorRunner(localBuild),
    distReader: makeRealDistReader(),
    readPackageJson: readPackageJsonFile,
  };

  return {
    getDiff(input: DiffProviderInput): PackageDiff {
      if (falsey(input.pkg.ecosystem)) {
        throw new Error(
          `package ${input.pkg.name} has no ecosystem discriminator`,
        );
      }

      if (input.pkg.ecosystem === "nuget") {
        return getNugetDiff(input, nugetExtractor, dotnetShell);
      }

      return getTsDiff(input, tsSeams, repoRoot);
    },
  };
}
