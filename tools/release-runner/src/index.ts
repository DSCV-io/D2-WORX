// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Public surface of release-runner.
//
// Re-exports the core engine functions and types consumed by tests and by
// any future programmatic callers. The CLI entry point (cli.ts) is not
// re-exported — it is an executable, not a library surface.

export {
  computeBumpPlans,
  type BreakingSignalProvider,
  type BreakingSignal,
} from "./bump-engine.js";
export { buildPromotedText, promoteChangelog } from "./changelog-editor.js";
export {
  readManifestVersion,
  writeManifestVersion,
  readNpmVersion,
  writeNpmVersion,
  readNugetVersion,
  writeNugetVersion,
} from "./manifest-editor.js";
export {
  loadAllPackages,
  loadNpmPackages,
  loadNugetPackages,
} from "./manifest-loader.js";
export { runRelease, type RunnerResult } from "./runner.js";
export {
  runDiffRelease,
  type DiffProvider,
  type DiffProviderInput,
  type PackageDiff,
  type DiffRunnerResult,
} from "./diff-runner.js";
export {
  buildDependentIndex,
  propagateBumps,
  topoSort,
} from "./dependency-graph.js";
export {
  graduatePackage,
  buildGraduatedChangelogText,
  type GraduateResult,
} from "./graduate.js";
export { resolveBaseline } from "./baseline.js";
export { formatPackageList, type ListEntry } from "./list-formatter.js";
export {
  parseVersion,
  applyBump,
  renderVersion,
  type ParsedVersion,
} from "./semver.js";
export {
  deriveBump,
  type ApiDiff,
  type FingerprintDiff,
  type BreakingFooter,
} from "./diff-bump.js";
export type {
  BumpKind,
  BumpPlan,
  CommitRecord,
  PackageDescriptor,
  PackageEcosystem,
  RunnerOptions,
} from "./types.js";
