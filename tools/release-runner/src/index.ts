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
export type {
  BumpKind,
  BumpPlan,
  CommitRecord,
  PackageDescriptor,
  PackageEcosystem,
  RunnerOptions,
} from "./types.js";
