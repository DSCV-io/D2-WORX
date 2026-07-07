// Copyright (c) DCSV. All rights reserved.
//
// The single shared composition primitive for the SOURCE-BASED, PORTABLE
// release fingerprint, used by BOTH seed scripts
// (seed-publicapi-baselines.mjs / seed-apiextractor-baselines.mjs).
//
// It is a byte-for-byte re-implementation of the release-runner provider's
// composeSourceFingerprint (tools/release-runner/src/source-fingerprint.ts):
// SHA-256 over the ordered, prefixed, LF-terminated tuple
//   ( SOURCE, APIREPORT, DEPS, TOOLCHAIN ).
//
// Extracting it here (rather than inlining the hash.update sequence in each
// seed script) gives a SEPARATE, importable implementation that a runner unit
// test pins against the provider's composeSourceFingerprint — so any future
// drift between the seed composition and the provider composition FAILS a test
// instead of silently corrupting a baseline. See the byte-identity tests in
// tools/release-runner/tests/seed-provider-fingerprint-identity.test.ts.

import { createHash } from "node:crypto";

/**
 * LF-normalize so a CRLF/LF checkout difference cannot perturb the hash.
 * @param {string} text
 */
export function normalizeLf(text) {
  return text.replace(/\r\n/g, "\n");
}

/**
 * Compose the source-based fingerprint over the four ordered components.
 * Byte-identical to the release-runner's composeSourceFingerprint: each
 * component is prefixed + LF-terminated so a boundary shift between two
 * components cannot collide, and the APIREPORT component is LF-normalized once
 * here (the caller passes the raw report text).
 *
 * @param {object} input
 * @param {string} input.sourceDump    Ordered, LF-normalized committed source dump.
 * @param {string} input.apiReport     Committed API report text (normalized here).
 * @param {string} input.depsJson      Deterministic resolved-deps JSON.
 * @param {string} input.toolchainJson Deterministic toolchain-pin JSON.
 * @returns {string} SHA-256 hex digest.
 */
export function composeSourceFingerprintFromParts({
  sourceDump,
  apiReport,
  depsJson,
  toolchainJson,
}) {
  const hash = createHash("sha256");

  hash.update(`SOURCE:\n${sourceDump}\n`);
  hash.update(`APIREPORT:\n${normalizeLf(apiReport)}\n`);
  hash.update(`DEPS:\n${depsJson}\n`);
  hash.update(`TOOLCHAIN:\n${toolchainJson}\n`);

  return hash.digest("hex");
}
