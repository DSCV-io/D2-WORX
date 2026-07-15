// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Single-source wire-channel derivation and cross-validation.
//
// The canonical channel lives in the `proto-package` tspconfig option
// (e.g. "d2.keycustodian.v2alpha"). This module promotes the test-local
// CHANNEL_GRAMMAR regex to an emitter-owned constant, parses the channel
// triple from the proto-package string, and validates that the
// `proto-csharp-namespace` trailing segment agrees (PascalCase) and —
// when a @versioned active-version channel is present — that it also
// agrees. Mismatch fires D2TSP010 (channel-segment-mismatch, error).
//
// validateChannelAgreement accepts an onError callback (same shape as the
// proto emitter's onError parameter) so it can be unit-tested without a live
// TypeSpec program. The emitter.ts call site wraps $lib.reportDiagnostic into
// the callback so the diagnostic reaches the TypeSpec compiler surface.
//
// Both regexes in this module are linear with no super-linear backtracking
// and operate on bounded-length identifier strings (Bucket 2 per
// regex-redos-discipline — no matchTimeout and no JIT pre-warm are needed;
// the input cannot grow unboundedly and neither pattern has nested
// quantifiers). Matches the rationale comment in name-transforms.ts:8-10.

// ---------------------------------------------------------------------------
// Grammar constant (promoted from proto-emitter.test.ts:1495 test-local regex)
// ---------------------------------------------------------------------------

/**
 * Grammar for a valid proto-package string.
 * Matches: d2.<lowercase-svc>.v<N>(alpha|beta)?
 * Examples: d2.keycustodian.v2alpha, d2.geo.v1, d2.auth.v3beta
 */
export const WIRE_CHANNEL_GRAMMAR = /^d2\.[a-z][a-z0-9]*\.v\d+(alpha|beta)?$/;

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Parsed wire-channel triple derived from a proto-package string. */
export interface WireChannel {
  /** Service name segment, e.g. "keycustodian". */
  readonly svc: string;
  /** Numeric wire generation, e.g. 2. */
  readonly generation: number;
  /** Stability tier: "alpha", "beta", or "stable". */
  readonly stability: "alpha" | "beta" | "stable";
  /** Lowercase channel string, e.g. "v2alpha". */
  readonly lowerChannel: string;
  /** PascalCase channel string for C# namespace segments, e.g. "V2Alpha". */
  readonly pascalChannel: string;
}

// ---------------------------------------------------------------------------
// Parse
// ---------------------------------------------------------------------------

/**
 * Parse a proto-package string into a WireChannel triple.
 * Returns undefined when the input does not match WIRE_CHANNEL_GRAMMAR.
 *
 * @param protoPackage - The full proto-package string, e.g. "d2.keycustodian.v2alpha".
 */
export function parseChannel(protoPackage: string): WireChannel | undefined {
  if (!WIRE_CHANNEL_GRAMMAR.test(protoPackage)) return undefined;

  // Split on "." — grammar guarantees exactly 3 segments.
  const parts = protoPackage.split(".");
  const svc = parts[1]!;
  const channelSegment = parts[2]!; // e.g. "v2alpha"

  // Parse vN(alpha|beta)? from the channel segment.
  // Bucket-2 regex: bounded input, no super-linear backtracking.
  const channelMatch = /^v(\d+)(alpha|beta)?$/.exec(channelSegment);
  /* v8 ignore start — unreachable: WIRE_CHANNEL_GRAMMAR already validated the segment */
  if (channelMatch === null) return undefined;
  /* v8 ignore stop */

  const generation = parseInt(channelMatch[1]!, 10);
  const stabilityRaw = channelMatch[2];
  const stability: "alpha" | "beta" | "stable" =
    stabilityRaw === "alpha"
      ? "alpha"
      : stabilityRaw === "beta"
        ? "beta"
        : "stable";

  const lowerChannel = channelSegment; // already lowercase
  const pascalChannel = expectedCsharpChannelSegment(lowerChannel);

  return { svc, generation, stability, lowerChannel, pascalChannel };
}

// ---------------------------------------------------------------------------
// PascalCase conversion
// ---------------------------------------------------------------------------

/**
 * Convert a lowercase channel string to the PascalCase form used in C# namespace
 * segments. E.g. "v2alpha" → "V2Alpha", "v2beta" → "V2Beta", "v2" → "V2".
 *
 * @param lowerChannel - Lowercase channel string, e.g. "v2alpha".
 */
export function expectedCsharpChannelSegment(lowerChannel: string): string {
  // Capitalize the "v", then capitalize "alpha" or "beta" suffix if present.
  // Bucket-2 regex: bounded identifier input.
  return lowerChannel.replace(
    /^(v)(\d+)(alpha|beta)?$/,
    (_, v: string, n: string, suf?: string) =>
      `${v.toUpperCase()}${n}${suf !== undefined ? suf.charAt(0).toUpperCase() + suf.slice(1) : ""}`,
  );
}

// ---------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------

/**
 * Cross-validate the proto-package channel against the proto-csharp-namespace
 * trailing segment and — when present — the @versioned active-version channel.
 *
 * On any mismatch, calls the TypeSpec $lib.reportDiagnostic to emit D2TSP010
 * (channel-segment-mismatch, error) and returns undefined.
 * On full agreement, returns the parsed WireChannel.
 *
 * @param protoPackage - The `proto-package` tspconfig value, e.g. "d2.keycustodian.v2alpha".
 * @param protoCsharpNs - The `proto-csharp-namespace` tspconfig value, e.g. "D2.Services.Protos.KeyCustodian.V2Alpha".
 * @param versionedChannel - The active-version VALUE from a @versioned enum, if present (e.g. "v2alpha"). Pass undefined when no @versioned namespace is found.
 * @param onError - Callback invoked on any mismatch: `(code, message) => void`.
 *   The emitter.ts call site wraps `$lib.reportDiagnostic` into this callback.
 */
export function validateChannelAgreement(
  protoPackage: string,
  protoCsharpNs: string,
  versionedChannel: string | undefined,
  onError: (code: string, message: string) => void,
): WireChannel | undefined {
  const parsed = parseChannel(protoPackage);

  if (parsed === undefined) {
    // proto-package did not match grammar — the grammar-guard test (proto-emitter.test.ts:1493)
    // will have already fired a separate diagnostic; skip the mismatch check.
    return undefined;
  }

  const { pascalChannel, lowerChannel } = parsed;

  // Extract the trailing dotted segment of proto-csharp-namespace.
  const nsSegments = protoCsharpNs.split(".");
  /* v8 ignore start — unreachable: split(".") always yields ≥1 element, so the ?? "" fallback never fires */
  const trailingSegment = nsSegments[nsSegments.length - 1] ?? "";
  /* v8 ignore stop */

  if (trailingSegment !== pascalChannel) {
    onError(
      "channel-segment-mismatch",
      `D2TSP010: wire-generation channel mismatch — proto-package channel '${lowerChannel}' (expected C# segment '${pascalChannel}') does not match proto-csharp-namespace trailing segment '${trailingSegment}'; every emitted wire surface must agree on the V<N>(alpha|beta)? generation`,
    );

    return undefined;
  }

  // Cross-validate the @versioned active-version channel when present.
  if (versionedChannel !== undefined && versionedChannel !== lowerChannel) {
    onError(
      "channel-segment-mismatch",
      `D2TSP010: wire-generation channel mismatch — @versioned active channel '${versionedChannel}' does not match proto-package channel '${lowerChannel}'; every version surface must agree on the channel`,
    );

    return undefined;
  }

  return parsed;
}
