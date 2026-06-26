// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Shared type definitions for the release runner.

// ---------------------------------------------------------------------------
// Commit record — injectable via GitProvider
// ---------------------------------------------------------------------------

/**
 * A single commit entry as supplied to the runner engine.
 *
 * The real git adapter constructs these from `git log`. Test suites inject
 * synthetic arrays so no real git process is required.
 */
export interface CommitRecord {
  /** Full raw commit message (subject + body + footer block). */
  readonly message: string;
  /**
   * File paths touched by this commit, relative to the repository root.
   * Provided by `git diff-tree --no-commit-id -r --name-only <sha>` or
   * `git diff --name-only <base>..<head>` per-commit.
   */
  readonly files: readonly string[];
}

// ---------------------------------------------------------------------------
// Package manifest descriptors
// ---------------------------------------------------------------------------

/** Ecosystem discriminator for the package manifest. */
export type PackageEcosystem = "npm" | "nuget";

/**
 * Descriptor for a single consumable package known to the runner.
 *
 * The manifest loader populates these from `package.json` (npm) and
 * `*.csproj` (nuget) files found under the repo tree.
 */
export interface PackageDescriptor {
  /** Unique key used to refer to this package (e.g. "@d2/result", "D2.Shared.Result"). */
  readonly name: string;
  readonly ecosystem: PackageEcosystem;
  /** Directory that owns the manifest file, repo-root-relative (no trailing slash). */
  readonly dir: string;
  /** Absolute path to the manifest file. */
  readonly manifestPath: string;
  /** Absolute path to the CHANGELOG.md. */
  readonly changelogPath: string;
  /** Current version string parsed from the manifest (e.g. "0.1.0"). */
  readonly currentVersion: string;
  /**
   * Names of consumable packages this package directly depends on.
   *
   * npm: `@d2/*` entries from `dependencies` + `devDependencies` that are in
   * the consumable set. nuget: `<ProjectReference>` targets resolved to their
   * descriptor name (basename minus `.csproj`), filtered to the consumable set.
   * Non-consumable edges (SourceGen shells, external packages) are excluded.
   */
  readonly dependencies: readonly string[];
}

// ---------------------------------------------------------------------------
// Bump-severity enum
// ---------------------------------------------------------------------------

/**
 * The computed bump level for a package.
 *
 * `none` means no qualifying commits touched this package.
 */
export type BumpKind = "none" | "patch" | "minor" | "major";

// ---------------------------------------------------------------------------
// Per-package bump plan
// ---------------------------------------------------------------------------

/**
 * Everything the runner needs to apply (or dry-report) a bump for one package.
 */
export interface BumpPlan {
  readonly pkg: PackageDescriptor;
  readonly bump: BumpKind;
  /** New version string that will be written if bump !== "none". */
  readonly newVersion: string;
  /** Descriptions collected for the Wire-breaking changelog section. */
  readonly wireBreakingEntries: readonly string[];
  /** Descriptions collected for the API-breaking changelog section. */
  readonly apiBreakingEntries: readonly string[];
  /** feat commit subjects for the Added section. */
  readonly addedEntries: readonly string[];
  /** fix/perf commit subjects for the Fixed section. */
  readonly fixedEntries: readonly string[];
  /**
   * Names of upstream packages that triggered a propagated dependency-update
   * bump for this plan. Populated ONLY on plans produced by propagation (not
   * directly bumped); empty on direct-bump plans so the ### Changed section
   * is omitted for packages with their own Added/Fixed/Breaking entries.
   */
  readonly dependencyEntries: readonly string[];
}

// ---------------------------------------------------------------------------
// Runner options
// ---------------------------------------------------------------------------

/**
 * Options for a single runner invocation.
 *
 * `today` is injectable for deterministic tests. The real default is the
 * current date formatted as `YYYY-MM-DD`.
 */
export interface RunnerOptions {
  /**
   * ISO date string `YYYY-MM-DD` stamped into CHANGELOG version headers.
   * Inject a fixed value in tests for deterministic output.
   */
  readonly today: string;
  /**
   * When true, compute and report planned bumps without writing any files.
   * Default: false (apply mode).
   */
  readonly dryRun: boolean;
  /**
   * When set, restrict the run to this single package name.
   * When undefined, all packages with qualifying commits are processed.
   *
   * The filter is applied AFTER propagation so the dependency graph can
   * resolve all edges; only the final plan set is restricted to this name.
   */
  readonly packageFilter?: string;
  /**
   * When true (default), propagate a PATCH dependency-update bump to every
   * consumable that transitively depends on a directly-bumped package.
   * Pass `--no-propagate` on the CLI to set this to false and reproduce the
   * direct-only bumping behavior.
   */
  readonly propagate: boolean;
}
