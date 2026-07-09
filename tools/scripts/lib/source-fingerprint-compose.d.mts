// Copyright (c) DCSV. All rights reserved.
//
// Type declarations for the plain-JS shared fingerprint-composition primitive
// (source-fingerprint-compose.mjs). The seed scripts run under node with no
// build step, so the primitive is authored as .mjs; this declaration lets the
// release-runner's TypeScript byte-identity test consume it with real types.

/** LF-normalize so a CRLF/LF checkout difference cannot perturb the hash. */
export function normalizeLf(text: string): string;

/** The four ordered components of the source-based fingerprint. */
export interface SourceFingerprintParts {
  readonly sourceDump: string;
  readonly apiReport: string;
  readonly depsJson: string;
  readonly toolchainJson: string;
}

/**
 * Compose the source-based fingerprint over the four ordered components.
 * Byte-identical to the release-runner's composeSourceFingerprint.
 */
export function composeSourceFingerprintFromParts(
  input: SourceFingerprintParts,
): string;
