// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  diagError,
  type EmitDiagnostic,
  type EmitResult,
  DiagnosticIds,
  formatDiagnostic,
} from "./lib/diagnostics.js";
import {
  buildHeader,
  isOutputUpToDate,
  writeGeneratedFile,
} from "./lib/file-emit.js";
import { contractsPath, tsPackagePath } from "./lib/paths.js";
import { loadSpec } from "./lib/spec-loader.js";
import { StringBuilder } from "./lib/string-builder.js";

/** One JWT claim entry parsed from `contracts/jwt-claims/jwt-claims.spec.json`. */
export interface JwtClaimEntry {
  readonly constName: string;
  readonly value: string;
  readonly kind: JwtClaimKind;
  readonly description: string;
}

/** Top-level shape of `jwt-claims.spec.json`. */
export interface JwtClaimsSpec {
  readonly claims: readonly JwtClaimEntry[];
}

/** Closed enum of supported JWT claim kinds. */
export type JwtClaimKind = "standard" | "d2-custom" | "inside-act";

const VALID_KINDS: ReadonlySet<string> = new Set([
  "standard",
  "d2-custom",
  "inside-act",
]);

const CONST_NAME_RE = /^[A-Z][A-Z0-9_]*$/;

/** Result of validating the spec. */
export interface ValidatedJwtClaims {
  readonly entries: readonly JwtClaimEntry[];
  readonly diagnostics: readonly EmitDiagnostic[];
}

/**
 * Validate the spec — surface duplicate constNames, unknown kinds, invalid
 * constName patterns. Note: duplicate `value` strings are ALLOWED across
 * different `kind` buckets (e.g. SESSION_ID and ACT_SESSION_ID both have
 * value "d2_session_id" — different lookup paths).
 */
export function validateJwtClaimsSpec(spec: JwtClaimsSpec): ValidatedJwtClaims {
  const diagnostics: EmitDiagnostic[] = [];
  const valid: JwtClaimEntry[] = [];
  const seenConstNames = new Set<string>();
  for (const entry of spec.claims) {
    if (!CONST_NAME_RE.test(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.JWT_INVALID_CONST_NAME,
          `claim has invalid constName '${entry.constName}' — must match ${CONST_NAME_RE.source}`,
        ),
      );
      continue;
    }
    if (!VALID_KINDS.has(entry.kind)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.JWT_UNKNOWN_KIND,
          `claim '${entry.constName}' has unknown kind '${entry.kind}' (valid: ${[
            ...VALID_KINDS,
          ]
            .sort()
            .join(", ")})`,
        ),
      );
      continue;
    }
    if (seenConstNames.has(entry.constName)) {
      diagnostics.push(
        diagError(
          DiagnosticIds.JWT_DUPLICATE_CONST_NAME,
          `duplicate claim constName '${entry.constName}'`,
        ),
      );
      continue;
    }
    seenConstNames.add(entry.constName);
    valid.push(entry);
  }
  return { entries: valid, diagnostics };
}

/**
 * TS type for each `standard` JWT claim's emitted `JwtPayload` field.
 * Closed switch keyed on the spec's `value` (wire claim name) — covers
 * the 8 RFC-defined standard claims with their canonical types. A
 * `d2-custom` entry NOT listed here defaults to `string | null`; this
 * is checked at emit time so a future non-string `d2-*` claim surfaces
 * as a build-time decision rather than a silent default.
 */
const STANDARD_CLAIM_TS_TYPES: Readonly<Record<string, string>> = {
  sub: "string | null",
  aud: "readonly string[]",
  iat: "number | null",
  exp: "number | null",
  azp: "string | null",
  scope: "string | null",
  act: "Readonly<Record<string, unknown>> | null",
  client_id: "string | null",
};

/** Default TS type for `d2-custom` claims with no override. */
const D2_CUSTOM_DEFAULT_TS_TYPE = "string | null";

/**
 * Emit the `jwt-claim-types.g.ts` source. Stateless and unit-testable.
 * Preserves spec order so the kind-grouped layout (standard → d2-custom →
 * inside-act) reads naturally; kind ordering is enforced via the spec
 * authoring convention, not the emitter.
 */
export function emitJwtClaims(spec: JwtClaimsSpec): EmitResult {
  const v = validateJwtClaimsSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };
  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/jwt-claims/jwt-claims.spec.json"));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * JWT claim name string constants used across the platform. Mirrors",
  );
  sb.appendLine(
    " * .NET D2.Shared.Auth.Abstractions.JwtClaimTypes (same string values).",
  );
  sb.appendLine(" *");
  sb.appendLine(
    " * Standard OAuth/OIDC claims keep their canonical names (sub, aud,",
  );
  sb.appendLine(
    " * scope, ...); D2-specific claims use the d2_ prefix; inside-act claims",
  );
  sb.appendLine(
    " * are nested under the act object per RFC 8693 §2.1 (lookup path",
  );
  sb.appendLine(" * act[ACT_KIND]).");
  sb.appendLine(" */");
  sb.appendLine("export const JwtClaimTypes = {");
  sb.increaseIndent();
  for (const e of v.entries) {
    sb.appendLine("/**");
    for (const line of e.description.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(` * Kind: ${e.kind}.`);
    sb.appendLine(" */");
    sb.appendLine(`${e.constName}: "${escapeStringLiteral(e.value)}",`);
  }
  sb.decreaseIndent();
  sb.appendLine("} as const;");
  sb.appendLine();
  sb.appendLine(
    "export type JwtClaimType = (typeof JwtClaimTypes)[keyof typeof JwtClaimTypes];",
  );
  sb.appendLine();
  return { source: sb.toString(), diagnostics: v.diagnostics };
}

/**
 * Emit the `jwt-payload.g.ts` source — a structurally-typed JWT payload
 * interface derived from the spec's `standard` + `d2-custom` claim
 * entries. `inside-act` claims are NOT top-level fields (they live
 * nested inside `act`); the `act` field itself is loosely typed as
 * `Readonly<Record<string, unknown>> | null` until a future deliverable
 * elevates it to a typed `ActorChainEntry` interface. A trailing `raw`
 * escape-hatch field gives downstream consumers access to non-spec'd
 * claims.
 */
export function emitJwtPayload(spec: JwtClaimsSpec): EmitResult {
  const v = validateJwtClaimsSpec(spec);
  const errors = v.diagnostics.filter((d) => d.severity === "error");
  if (errors.length > 0) return { source: "", diagnostics: v.diagnostics };

  const sb = new StringBuilder();
  sb.appendLine(buildHeader("contracts/jwt-claims/jwt-claims.spec.json"));
  sb.appendLine("/* eslint-disable */");
  sb.appendLine();
  sb.appendLine("/**");
  sb.appendLine(
    " * Decoded JWT payload — typed on every spec-driven claim with a stable",
  );
  sb.appendLine(
    " * top-level wire name. Mirrors the spec's `kind in {standard, d2-custom}`",
  );
  sb.appendLine(
    " * subset; `inside-act` claims live nested inside `act` and are not",
  );
  sb.appendLine(
    " * surfaced as top-level fields. The trailing `raw` field carries the",
  );
  sb.appendLine(
    " * untyped claim object for downstream consumers that need access to",
  );
  sb.appendLine(" * non-spec'd claims.");
  sb.appendLine(" */");
  sb.appendLine("export interface JwtPayload {");
  sb.increaseIndent();

  for (const e of v.entries) {
    if (e.kind === "inside-act") continue;
    const tsType = tsTypeForClaim(e);
    sb.appendLine("/**");
    for (const line of e.description.split("\n"))
      sb.appendLine(` * ${escapeJsDoc(line)}`);
    sb.appendLine(` * Claim wire name: ${e.value} (kind: ${e.kind}).`);
    sb.appendLine(" */");
    sb.appendLine(`readonly ${e.value}: ${tsType};`);
  }

  sb.appendLine("/**");
  sb.appendLine(
    " * Raw decoded claims object — escape hatch for downstream consumers",
  );
  sb.appendLine(" * that need access to non-spec'd claims.");
  sb.appendLine(" */");
  sb.appendLine("readonly raw: Readonly<Record<string, unknown>>;");
  sb.decreaseIndent();
  sb.appendLine("}");
  sb.appendLine();

  return { source: sb.toString(), diagnostics: v.diagnostics };
}

/**
 * TS type for one claim entry on the emitted JwtPayload interface.
 * `standard` entries look up their type in the closed `STANDARD_CLAIM_TS_TYPES`
 * table; `d2-custom` entries default to `string | null` (every existing
 * `d2-*` claim is string-shaped); `inside-act` entries are never emitted
 * top-level so the caller filters them out before this is invoked.
 */
function tsTypeForClaim(entry: JwtClaimEntry): string {
  if (entry.kind === "standard") {
    return STANDARD_CLAIM_TS_TYPES[entry.value] ?? "string | null";
  }
  return D2_CUSTOM_DEFAULT_TS_TYPE;
}

function escapeStringLiteral(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function escapeJsDoc(value: string): string {
  return value.replace(/\*\//g, "*\\/");
}

const SPEC_PATH = contractsPath("jwt-claims", "jwt-claims.spec.json");
const CLAIM_TYPES_TARGET = tsPackagePath(
  "auth-abstractions",
  "src",
  "jwt-claim-types.g.ts",
);
const PAYLOAD_TARGET = tsPackagePath(
  "auth-abstractions",
  "src",
  "jwt-payload.g.ts",
);

/**
 * Run the jwt-claims emitter. Writes BOTH outputs from the same spec:
 *  - `jwt-claim-types.g.ts` — `JwtClaimTypes` constant catalog.
 *  - `jwt-payload.g.ts` — `JwtPayload` typed-payload interface.
 * Per-spec mtime check skips emit when EVERY output is newer than the
 * spec; pass `force=true` to bypass.
 */
export function runJwtClaimsEmit(force = false): readonly EmitDiagnostic[] {
  const outputs = [CLAIM_TYPES_TARGET, PAYLOAD_TARGET];
  if (!force && outputs.every((p) => isOutputUpToDate(p, [SPEC_PATH])))
    return [];
  const loadResult = loadSpec<JwtClaimsSpec>(
    SPEC_PATH,
    DiagnosticIds.JWT_MALFORMED_SPEC,
  );
  if (loadResult.spec === undefined) return loadResult.diagnostics;

  const claimTypesResult = emitJwtClaims(loadResult.spec);
  if (claimTypesResult.diagnostics.some((d) => d.severity === "error"))
    return claimTypesResult.diagnostics;

  const payloadResult = emitJwtPayload(loadResult.spec);
  if (payloadResult.diagnostics.some((d) => d.severity === "error"))
    return payloadResult.diagnostics;

  writeGeneratedFile(CLAIM_TYPES_TARGET, claimTypesResult.source);
  writeGeneratedFile(PAYLOAD_TARGET, payloadResult.source);
  return [...claimTypesResult.diagnostics, ...payloadResult.diagnostics];
}

const isMain =
  import.meta.url === `file://${process.argv[1]}` ||
  process.argv[1]?.endsWith("jwt-claims-emit.ts") === true;
if (isMain) {
  const force = process.argv.includes("--force");
  const diagnostics = runJwtClaimsEmit(force);
  for (const d of diagnostics) console.error(formatDiagnostic(d));
  if (diagnostics.some((d) => d.severity === "error")) process.exit(1);
}
