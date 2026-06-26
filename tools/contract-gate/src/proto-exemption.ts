// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Proto-arm pre-stable exemption helper.
//
// A proto package whose stability tier is NOT "stable" (i.e. alpha or beta)
// breaks freely — the gate does not enforce FILE-level breaking rules on it.
// Only stable `vN` packages (no alpha/beta suffix) are gate-enforced.
//
// Stability derivation mirrors `parseChannel` from @d2/typespec-emitters
// wire-channel.ts. The grammar and stability logic are re-implemented as a
// 6-line local mirror (rather than a cross-package import from
// server/shared/typescript/) to keep the tools/ package self-contained and
// avoid an awkward workspace reference across tool boundaries. A parity
// assertion in proto-exemption.test.ts verifies both grammars agree on the
// same set of test vectors.
//
// Regex discipline (Bucket 2 per regex-redos-discipline):
//   Both regexes operate on a single bounded-length proto package-declaration
//   line. No nested quantifiers, no super-linear backtracking, no matchTimeout
//   or JIT pre-warm needed. Matches the rationale comment in wire-channel.ts:20-24.

// ---------------------------------------------------------------------------
// Grammar (local mirror of WIRE_CHANNEL_GRAMMAR from wire-channel.ts)
// ---------------------------------------------------------------------------

/** Local mirror of WIRE_CHANNEL_GRAMMAR. Used for cross-version parity tests. */
export const PROTO_PACKAGE_GRAMMAR = /^d2\.[a-z][a-z0-9]*\.v\d+(alpha|beta)?$/;

// Bucket-2 regex: matches `package <identifier>;` in a proto file line.
const PACKAGE_LINE_RE =
  /^package\s+(d2\.[a-z][a-z0-9]*\.v\d+(?:alpha|beta)?)\s*;/;

// Bucket-2 regex: extracts the stability suffix from the channel segment.
const CHANNEL_SUFFIX_RE = /^v\d+(alpha|beta)?$/;

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Determine whether a proto package name is exempt from breaking-change
 * enforcement. Packages whose stability tier is "alpha" or "beta" break freely;
 * only stable `vN` (no suffix) packages are gate-enforced.
 *
 * A proto whose package does not match the D2 grammar is treated as
 * non-exempt (stable-assumed) so a malformed package cannot bypass the gate.
 * A loud warning message is returned alongside the exemption flag.
 *
 * @param protoPackage - The full proto-package name, e.g. "d2.keycustodian.v2alpha".
 * @returns `{ exempt, warning? }` — exempt=true → skip enforcement; warning
 *   is set when the package did not match the grammar.
 */
export function isProtoGateExempt(protoPackage: string): {
  readonly exempt: boolean;
  readonly warning?: string;
} {
  if (!PROTO_PACKAGE_GRAMMAR.test(protoPackage)) {
    return {
      exempt: false,
      warning: `proto package '${protoPackage}' does not match the D2 wire-channel grammar (d2.<svc>.v<N>(alpha|beta)?); treating as stable (gate-enforced) so a malformed package cannot bypass the breaking-change gate`,
    };
  }

  const parts = protoPackage.split(".");
  const channelSegment = parts[2] ?? "";
  const suffixMatch = CHANNEL_SUFFIX_RE.exec(channelSegment);
  const stabilitySuffix = suffixMatch?.[1]; // "alpha" | "beta" | undefined

  const exempt = stabilitySuffix === "alpha" || stabilitySuffix === "beta";

  return { exempt };
}

/**
 * Extract the proto package name from a `package …;` declaration line.
 * Returns undefined when the line does not match the expected pattern.
 *
 * @param line - A single line from a proto file, e.g. `package d2.svc.v2alpha;`
 */
export function extractProtoPackage(line: string): string | undefined {
  const m = PACKAGE_LINE_RE.exec(line.trimEnd());
  return m?.[1];
}
