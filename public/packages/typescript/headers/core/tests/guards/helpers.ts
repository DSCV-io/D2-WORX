// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { IRequestContext } from "@dcsv-io/d2-request-context-abstractions";
import { RequestOrigin } from "@dcsv-io/d2-request-context-abstractions";
import type {
  GuardRequestEvent,
  GuardThrowers,
} from "../../src/guards/guard-types.js";
import type { ProblemDetailsBody } from "../../src/problem-details.js";

export interface ThrownErrorRecord {
  kind: "error";
  status: number;
  body: ProblemDetailsBody;
  contentType: string;
}

export interface ThrownRedirectRecord {
  kind: "redirect";
  status: number;
  location: string;
}

export type ThrownRecord = ThrownErrorRecord | ThrownRedirectRecord;

export function makeThrowers(): {
  throwers: GuardThrowers;
  thrown: ThrownRecord[];
} {
  const thrown: ThrownRecord[] = [];
  const throwers: GuardThrowers = {
    throwError(status, body, contentType) {
      thrown.push({ kind: "error", status, body, contentType });
      throw new Error(`HTTP ${status}: ${String(body[`d2_error_code`])}`);
    },
    throwRedirect(status, location) {
      thrown.push({ kind: "redirect", status, location });
      throw new Error(`Redirect ${status} → ${location}`);
    },
  };
  return { throwers, thrown };
}

export function authenticatedCtx(
  overrides: Partial<IRequestContext> = {},
): IRequestContext {
  return {
    isAuthenticated: true,
    audience: ["d2.edge"],
    sessionId: "00000000-0000-0000-0000-000000000002",
    tokenIssuedAt: undefined,
    tokenExpiresAt: undefined,
    actorChain: [],
    authMethod: undefined,
    lastStepUpAt: undefined,
    subject: "00000000-0000-0000-0000-000000000001",
    userId: "00000000-0000-0000-0000-000000000001",
    username: "alice",
    requestedByClientId: "d2.web",
    immediateCallerClientId: undefined,
    originatingClientId: "00000000-0000-0000-0000-000000000001",
    isServiceIdentity: false,
    orgId: "00000000-0000-0000-0000-000000000003",
    orgName: "Acme",
    orgType: undefined,
    orgRole: undefined,
    isImpersonating: false,
    impersonationKind: undefined,
    impersonatedBy: undefined,
    impersonationSessionId: undefined,
    impersonatorOrgId: undefined,
    impersonatorOrgName: undefined,
    impersonatorOrgType: undefined,
    impersonatorOrgRole: undefined,
    scopes: new Set<string>(),
    traceId: undefined,
    requestId: undefined,
    requestPath: undefined,
    httpMethod: undefined,
    requestStartedAt: undefined,
    idempotencyKey: undefined,
    clientIp: undefined,
    sessionFingerprint: undefined,
    currentFingerprint: undefined,
    riskScore: undefined,
    edgeNodeId: undefined,
    localeIetfBcp47Tag: undefined,
    timezoneIanaName: undefined,
    currencyIso4217Code: undefined,
    orgPlanTier: undefined,
    featureFlagsCsv: undefined,
    whoIsHashId: undefined,
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
    immediateCaller: undefined,
    ...overrides,
    origin: overrides.origin ?? RequestOrigin.Unestablished,
    callPath: overrides.callPath ?? [],
  };
}

export function makeEvent(
  ctx: IRequestContext | undefined,
  pathname = "/test",
): GuardRequestEvent {
  return {
    url: { pathname },
    locals: ctx === undefined ? {} : { requestContext: ctx },
  };
}
