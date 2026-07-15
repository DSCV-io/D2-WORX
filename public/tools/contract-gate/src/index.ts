// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Public surface of contract-gate.
// Re-exports the shared footer parser (consumed by both the breaking-change gate
// and the release runner) and the git IO seam.

export { resolveBaseline } from "./baseline.js";
export { validateGitRef, validateGitPath } from "./safe-args.js";
export { parseBreakingFooters, type BreakingValve } from "./footer-parser.js";
export { commitMessagesInRange } from "./git.js";
export {
  isProtoGateExempt,
  extractProtoPackage,
  PROTO_PACKAGE_GRAMMAR,
} from "./proto-exemption.js";
export {
  diffFlatCatalog,
  diffNestedCatalog,
  diffCatalog,
} from "./spec-diff.js";
export { diffMessageKeys } from "./i18n-diff.js";
export { diffOpenApi } from "./openapi-diff.js";
export { getCatalogIdentity } from "./catalog-identity.js";
export {
  runProtoArm,
  type ProtoArmOptions,
  type ProtoArmResult,
} from "./proto-arm.js";
export {
  runSpecGate,
  type SpecGateOptions,
  type SpecGateResult,
} from "./run-spec-gate.js";
export {
  type BreakingFinding,
  type GateArm,
  type FindingSeverity,
} from "./breaking-finding.js";
