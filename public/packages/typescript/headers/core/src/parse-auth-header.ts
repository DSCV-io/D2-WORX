// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  AuthFailures,
  JwtClaimTypes,
  type JwtPayload,
} from "@dcsv-io/d2-auth-abstractions";
import type { D2Result } from "@dcsv-io/d2-result";
import { ok } from "@dcsv-io/d2-result";
import { falsey } from "@dcsv-io/d2-utilities";

/**
 * Maximum total Authorization header length accepted by the parser.
 *
 * 8 KB is the de facto industry-standard cap for Authorization headers
 * (matches common reverse-proxy defaults — nginx `client_header_buffer_size`,
 * AWS ALB `request_header_size`, etc.). Typical D² JWTs are well under
 * 4 KB; a header > 8 KB is either malformed or an oversized-token DoS
 * probe. Anything longer is rejected before any decode work happens.
 */
const _MAX_AUTHORIZATION_LENGTH = 8 * 1024;

/**
 * Maximum byte length of a single base64url-decoded JWT segment. Standard
 * JWTs in this platform fit comfortably under 4 KB; oversized payloads are
 * rejected as malformed before any JSON parse.
 */
const _MAX_SEGMENT_BYTES = 4 * 1024;

/**
 * Options for `parseAuthHeader`.
 */
export interface ParseAuthHeaderOptions {
  /** Optional trace id for the failure envelope returned on rejection. */
  readonly traceId?: string;
}

/**
 * Decodes an `Authorization: Bearer <jwt>` header into a structured payload.
 *
 * SHAPE-ONLY validation: this BFF-side helper trusts that Edge already
 * validated the JWT signature + expiry + audience. The BFF runs on the
 * `internal` overlay reachable only from Edge; signature verification is
 * Edge's job, not ours. Re-validating here would require fetching JWKS
 * from Edge — a layer-of-trust regression for no security gain.
 *
 * Returns `D2Result<JwtPayload>` — no throwing on bad input. Callers do not
 * need a try/catch.
 *
 * Wire-boundary carve-out per rules.md §6.15: the `authHeader` parameter
 * is typed `string | null | undefined` because the primary caller passes
 * `Headers.get(...)` directly, and the Web `Headers` API contract returns
 * `string | null` (`null` for absent headers). The parser normalizes to
 * the `bearerMissing` failure envelope at this boundary — no `null`
 * propagates inward.
 */
export function parseAuthHeader(
  authHeader: string | null | undefined,
  opts: ParseAuthHeaderOptions = {},
): D2Result<JwtPayload> {
  if (falsey(authHeader)) {
    return AuthFailures.bearerMissing({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }
  const trimmed = (authHeader as string).trim();
  if (trimmed.length > _MAX_AUTHORIZATION_LENGTH) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }
  // Reject obvious header-injection attempts before any further work.
  if (/[\r\n]/.test(trimmed)) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }
  // Case-insensitive Bearer prefix per RFC 6750 §2.1. The regex requires
  // at least one whitespace character separating the scheme from the
  // token; "Bearer" (no token at all) fails this check.
  const SCHEME_RE = /^Bearer\s+/i;
  if (!SCHEME_RE.test(trimmed)) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }
  const token = trimmed.replace(SCHEME_RE, "").trim();

  // JWT compact serialization: header.payload.signature (RFC 7519 §3).
  const segments = token.split(".");
  if (segments.length !== 3) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }

  // segments.length === 3 guarantees segments[1] is defined; the bang is
  // safe because of the gate two lines up.
  const payloadSegment = segments[1]!;
  if (falsey(payloadSegment)) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }

  // Buffer.from with "base64url" never throws on garbage input — it
  // silently returns whatever bytes it could decode (often a much shorter
  // buffer). The size cap below is the real adversarial gate.
  const decoded = Buffer.from(payloadSegment, "base64url");
  if (decoded.length > _MAX_SEGMENT_BYTES) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(decoded.toString("utf8"));
  } catch {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }

  const claims = parsed as Record<string, unknown>;

  // Required claim: sub. RFC 7519 §4.1.2; D2 platform requires it on all
  // BFF-bound tokens.
  const subClaim = claims[JwtClaimTypes.SUB];
  if (
    subClaim !== undefined &&
    subClaim !== null &&
    typeof subClaim !== "string"
  ) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }

  const aud = _normalizeAudience(claims[JwtClaimTypes.AUD]);
  if (aud === undefined) {
    return AuthFailures.bearerMalformed({
      traceId: opts.traceId,
    }) as D2Result<JwtPayload>;
  }

  const payload: JwtPayload = {
    sub: _stringOrUndef(subClaim),
    aud,
    iat: _numberOrUndef(claims[JwtClaimTypes.IAT]),
    exp: _numberOrUndef(claims[JwtClaimTypes.EXP]),
    azp: _stringOrUndef(claims[JwtClaimTypes.AZP]),
    scope: _stringOrUndef(claims[JwtClaimTypes.SCOPE]),
    act: _objectOrUndef(claims[JwtClaimTypes.ACT]),
    client_id: _stringOrUndef(claims[JwtClaimTypes.CLIENT_ID]),
    d2_session_id: _stringOrUndef(claims[JwtClaimTypes.SESSION_ID]),
    d2_username: _stringOrUndef(claims[JwtClaimTypes.USERNAME]),
    d2_fp: _stringOrUndef(claims[JwtClaimTypes.FINGERPRINT]),
    d2_org_id: _stringOrUndef(claims[JwtClaimTypes.ORG_ID]),
    d2_org_name: _stringOrUndef(claims[JwtClaimTypes.ORG_NAME]),
    d2_org_type: _stringOrUndef(claims[JwtClaimTypes.ORG_TYPE]),
    d2_org_role: _stringOrUndef(claims[JwtClaimTypes.ORG_ROLE]),
    amr: _stringOrUndef(claims[JwtClaimTypes.AMR]),
    d2_step_up_at: _stringOrUndef(claims[JwtClaimTypes.STEP_UP_AT]),
    raw: claims,
  };

  return ok(payload, opts.traceId);
}

function _stringOrUndef(value: unknown): string | undefined {
  if (typeof value !== "string") return undefined;
  if (value.length === 0) return undefined;
  return value;
}

function _numberOrUndef(value: unknown): number | undefined {
  if (typeof value !== "number" || !Number.isFinite(value)) return undefined;
  return value;
}

function _objectOrUndef(
  value: unknown,
): Readonly<Record<string, unknown>> | undefined {
  if (value === null || typeof value !== "object" || Array.isArray(value))
    return undefined;
  return value as Readonly<Record<string, unknown>>;
}

/**
 * Normalize the `aud` claim per RFC 7519 §4.1.3 — single string OR array
 * of strings. Returns the normalized array; returns `undefined` when the
 * shape is broken (number / bool / mixed-type array).
 */
function _normalizeAudience(value: unknown): readonly string[] | undefined {
  if (value === undefined || value === null) return [];
  if (typeof value === "string") return value.length === 0 ? [] : [value];
  if (Array.isArray(value)) {
    const out: string[] = [];
    for (const entry of value) {
      if (typeof entry !== "string") return undefined;
      if (entry.length === 0) continue;
      out.push(entry);
    }
    return out;
  }
  return undefined;
}
