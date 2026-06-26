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
  makeRealDiffProvider,
  makeRealFileReader,
  buildNugetManifestMeta,
  buildNpmManifestMeta,
  substituteResolvedDeps,
  readPackageJsonFile,
  type FileReader,
  type SourceLister,
  type RealDiffProviderOptions,
} from "./real-diff-provider.js";
export {
  parseShippedTxt,
  diffShippedLines,
  fingerprintBaselinePath,
  shippedTxtPath,
  unshippedTxtPath,
} from "./nuget-extractor.js";
export {
  parseApiMembers,
  diffApiMembers,
  extractMemberName,
  resolveApiMdPath,
  tsFingerprintBaselinePath,
  makeGitBaselineReader,
  makeRealApiExtractorRunner,
  type BaselineReader,
  type ApiExtractorRunner,
} from "./ts-api-adapter.js";
export {
  buildSourceDump,
  composeSourceFingerprint,
  listSourceFiles,
  makeGitTrackedLister,
  makeRepoFileReader,
  normalizeLf,
  readToolchainPin,
  stableJson,
  type RepoFileReader,
  type SourceEcosystem,
  type SourceFileReader,
  type SourceFingerprintInput,
  type TrackedFileLister,
} from "./source-fingerprint.js";
export {
  checkBaselineDrift,
  formatDriftReport,
  type PackageDriftResult,
  type DriftCheckResult,
} from "./drift-check.js";
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
  parseVersionLoose,
  applyBump,
  renderVersion,
  type ParsedVersion,
} from "./semver.js";
export {
  deriveBump,
  isPreStable,
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
