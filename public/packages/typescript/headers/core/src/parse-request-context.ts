// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { falsey, UUID_RE } from "@d2/utilities";
import type {
  IRequestContext,
  ActorEntry,
} from "@d2/request-context-abstractions";
import {
  PropagatedContextSerializer,
  OrgType,
  Role,
  ActorKind,
  ImpersonationKind,
  RequestOrigin,
} from "@d2/request-context-abstractions";
import type { D2Result } from "@d2/result";
import { ok } from "@d2/result";
import { CommonHeaders } from "@d2/headers-common";
import type { JwtPayload } from "@d2/auth-abstractions";
import { parseAuthHeader } from "./parse-auth-header.js";

/**
 * Options for `parseRequestContextFromHeaders`.
 */
export interface ParseRequestContextOptions {
  /**
   * Trace id surfaced on the failure envelope when the Authorization header
   * is malformed.
   */
  readonly traceId?: string;
  /**
   * When true (default false), return a failure envelope for missing or
   * malformed Authorization. When false, an absent or unparseable
   * Authorization yields an unauthenticated context
   * (`isAuthenticated: false`).
   */
  readonly requireAuth?: boolean;
}

/**
 * High-level header → IRequestContext composer used by the SvelteKit BFF.
 *
 * Reads:
 *  - `Authorization: Bearer <jwt>` — decoded into identity fields.
 *  - `x-d2-context` — base64url-of-JSON envelope decoded into propagated
 *    fields (request id, fingerprints, WhoIs hash, risk score).
 *
 * Returns `D2Result<IRequestContext>`. On rejected Authorization with
 * `requireAuth=true`, returns the underlying `AuthFailures.bearer*`
 * envelope unchanged. On rejected `x-d2-context` envelope, the
 * propagated fields silently fall through to `undefined` (return-empty
 * over wrong-data per fail-soft posture); the JWT-derived identity fields
 * are still surfaced.
 */
export function parseRequestContextFromHeaders(
  headers: Headers,
  opts: ParseRequestContextOptions = {},
): D2Result<IRequestContext> {
  // `Headers.get(...)` returns `string | null` (Web API contract). The
  // parser absorbs the wire `null` immediately — no `null` propagates inward.
  const authHeader = headers.get(CommonHeaders.AUTHORIZATION);
  const requireAuth = opts.requireAuth ?? false;

  let payload: JwtPayload | undefined;
  if (authHeader !== null) {
    const decoded = parseAuthHeader(authHeader, { traceId: opts.traceId });
    if (decoded.success) {
      payload = decoded.data!;
    } else if (requireAuth) {
      // Cast preserves the failure shape across type parameter change.
      return decoded as unknown as D2Result<IRequestContext>;
    }
  } else if (requireAuth) {
    // Same shape as parseAuthHeader's "missing" branch; lazy-import via
    // the parser so we keep the AuthFailures coupling in one place. Pass
    // `undefined` (not `null`) inward to honor the §6.15 "no `null` past
    // the wire boundary" rule.
    return parseAuthHeader(undefined, {
      traceId: opts.traceId,
    }) as unknown as D2Result<IRequestContext>;
  }

  const propagatedRaw = headers.get(CommonHeaders.PROPAGATED_CONTEXT);
  // Base64url decode; tryDecode itself enforces shape + per-field caps.
  // Buffer.from never throws on garbage base64url input — it silently
  // returns whatever bytes it could decode; tryDecode then rejects any
  // non-JSON / shape-violating payload by returning undefined.
  let propagatedDecoded: ReturnType<
    typeof PropagatedContextSerializer.tryDecode
  > = undefined;
  if (
    propagatedRaw !== null &&
    propagatedRaw.length > 0 &&
    !/[\r\n]/.test(propagatedRaw)
  ) {
    const json = Buffer.from(propagatedRaw, "base64url").toString("utf8");
    propagatedDecoded = PropagatedContextSerializer.tryDecode(json);
  }

  const ctx: IRequestContext = _composeRequestContext(
    payload,
    propagatedDecoded,
  );
  return ok(ctx, opts.traceId);
}

function _composeRequestContext(
  payload: JwtPayload | undefined,
  propagated: ReturnType<typeof PropagatedContextSerializer.tryDecode>,
): IRequestContext {
  const isAuthenticated = payload !== undefined ? true : false;
  const orgType = _toOrgType(payload?.d2_org_type);
  const orgRole = _toRole(payload?.d2_org_role);
  const actorChain = _flattenActorChain(payload?.act);
  const scopes = _parseScopes(payload?.scope);

  const ctx: IRequestContext = {
    // Tracing — sourced from propagated envelope; traceId itself comes from
    // OTel's own propagator on the request, not from this header.
    traceId: undefined,
    requestId: propagated?.requestId ?? undefined,
    requestPath: propagated?.requestPath ?? undefined,
    httpMethod: undefined,
    requestStartedAt: propagated?.requestStartedAt ?? undefined,
    idempotencyKey: propagated?.idempotencyKey ?? undefined,
    // Network — populated by Edge upstream, not via headers parsed here.
    clientIp: undefined,
    // Fingerprints — propagated subset only.
    sessionFingerprint: propagated?.sessionFingerprint ?? undefined,
    currentFingerprint: propagated?.currentFingerprint ?? undefined,
    riskScore: propagated?.riskScore ?? undefined,
    // Infrastructure — propagated.
    edgeNodeId: propagated?.edgeNodeId ?? undefined,
    // User Preferences — propagated.
    localeIetfBcp47Tag: propagated?.localeIetfBcp47Tag ?? undefined,
    timezoneIanaName: propagated?.timezoneIanaName ?? undefined,
    currencyIso4217Code: propagated?.currencyIso4217Code ?? undefined,
    // Entitlements — propagated.
    orgPlanTier: propagated?.orgPlanTier ?? undefined,
    featureFlagsCsv: propagated?.featureFlagsCsv ?? undefined,
    // WhoIs — only the hash propagates; full record is recomputed downstream.
    whoIsHashId: propagated?.whoIsHashId ?? undefined,
    adminLocationHashId: undefined,
    city: undefined,
    subdivisionIso31662Code: undefined,
    countryIso31661Alpha2Code: undefined,
    postalCode: undefined,
    latitude: undefined,
    longitude: undefined,
    geohash: undefined,
    isVpn: undefined,
    isProxy: undefined,
    isTor: undefined,
    isHosting: undefined,
    asn: undefined,
    asnName: undefined,
    asnType: undefined,
    // Establishment — Origin is recomputed locally by each trust boundary and
    // is never propagated, so a header-parse reconstruction leaves it
    // fail-closed Unestablished; ImmediateCaller is the (non-propagated) mTLS
    // peer id, absent on this path; CallPath is propagated telemetry inherited
    // from the envelope (empty when absent).
    origin: RequestOrigin.Unestablished,
    immediateCaller: undefined,
    callPath: propagated?.callPath ?? [],
    // Auth identity — JWT-sourced.
    isAuthenticated,
    audience: payload?.aud ?? [],
    sessionId: payload?.d2_session_id,
    tokenIssuedAt: payload?.iat !== undefined ? String(payload.iat) : undefined,
    tokenExpiresAt:
      payload?.exp !== undefined ? String(payload.exp) : undefined,
    actorChain,
    authMethod: payload?.amr,
    lastStepUpAt: payload?.d2_step_up_at,
    subject: payload?.sub,
    userId: _guidOrUndef(payload?.sub),
    username: payload?.d2_username,
    requestedByClientId: payload?.client_id,
    immediateCallerClientId: _immediateCallerClientId(actorChain),
    originatingClientId: _originatingClientId(actorChain, payload?.sub),
    isServiceIdentity:
      payload === undefined ? undefined : _isServiceIdentity(payload),
    orgId: payload?.d2_org_id,
    orgName: payload?.d2_org_name,
    orgType,
    orgRole,
    isImpersonating:
      payload === undefined
        ? undefined
        : actorChain.some((a) => a.kind === ActorKind.Impersonation),
    impersonationKind: undefined,
    impersonatedBy: undefined,
    impersonationSessionId: undefined,
    impersonatorOrgId: undefined,
    impersonatorOrgName: undefined,
    impersonatorOrgType: undefined,
    impersonatorOrgRole: undefined,
    scopes,
  };
  return ctx;
}

function _toOrgType(value: string | undefined): OrgType | undefined {
  if (falsey(value)) return undefined;
  // Compile-time check that the runtime values match OrgType.
  for (const candidate of Object.values(OrgType)) {
    if (candidate === value) return candidate;
  }
  return undefined;
}

function _toRole(value: string | undefined): Role | undefined {
  if (falsey(value)) return undefined;
  for (const candidate of Object.values(Role)) {
    if (candidate === value) return candidate;
  }
  return undefined;
}

function _guidOrUndef(value: string | undefined): string | undefined {
  if (value === undefined) return undefined;
  return UUID_RE.test(value) ? value : undefined;
}

function _flattenActorChain(
  act: Readonly<Record<string, unknown>> | undefined,
): readonly ActorEntry[] {
  if (act === undefined) return [];
  // Flatten outermost-first per RFC 8693 §4.1.
  const out: ActorEntry[] = [];
  let current: Readonly<Record<string, unknown>> | undefined = act;
  let guard = 0;
  while (current !== undefined && guard < 32) {
    const sub = current["sub"];
    if (typeof sub !== "string" || sub.length === 0) break;
    const kindRaw = current["d2_kind"];
    const isImpersonation = kindRaw === "consent" || kindRaw === "force";
    const entry: ActorEntry = {
      kind: isImpersonation ? ActorKind.Impersonation : ActorKind.Service,
      subject: sub,
      impersonationKind: isImpersonation
        ? kindRaw === "consent"
          ? ImpersonationKind.Consent
          : ImpersonationKind.Force
        : undefined,
    };
    out.push(entry);
    const nextAct: unknown = current["act"];
    current =
      nextAct !== null && typeof nextAct === "object" && !Array.isArray(nextAct)
        ? (nextAct as Readonly<Record<string, unknown>>)
        : undefined;
    guard++;
  }
  return out;
}

function _immediateCallerClientId(
  chain: readonly ActorEntry[],
): string | undefined {
  for (const entry of chain) {
    if (entry.kind === ActorKind.Service) return entry.subject;
  }
  return undefined;
}

function _originatingClientId(
  chain: readonly ActorEntry[],
  subject: string | undefined,
): string | undefined {
  let last: string | undefined;
  for (const entry of chain) {
    if (entry.kind === ActorKind.Service) last = entry.subject;
  }
  return last ?? subject;
}

function _isServiceIdentity(payload: JwtPayload): boolean {
  // Pure service-identity = subject is non-Guid AND no Impersonation in chain.
  const subject = payload.sub;
  if (subject === undefined) return false;
  if (UUID_RE.test(subject)) return false;
  if (payload.act !== undefined) {
    // Walk; any Impersonation flips it.
    let current: Readonly<Record<string, unknown>> | undefined = payload.act;
    let guard = 0;
    while (current !== undefined && guard < 32) {
      const kind = current["d2_kind"];
      if (kind === "consent" || kind === "force") return false;
      const nextAct: unknown = current["act"];
      current =
        nextAct !== null &&
        typeof nextAct === "object" &&
        !Array.isArray(nextAct)
          ? (nextAct as Readonly<Record<string, unknown>>)
          : undefined;
      guard++;
    }
  }
  return true;
}

function _parseScopes(value: string | undefined): ReadonlySet<string> {
  if (falsey(value)) return new Set<string>();
  const out = new Set<string>();
  for (const entry of value!.split(/\s+/)) {
    if (entry.length === 0) continue;
    out.add(entry);
  }
  return out;
}
