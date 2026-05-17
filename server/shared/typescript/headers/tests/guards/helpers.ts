// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { IRequestContext } from "@d2/request-context-abstractions";
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
    tokenIssuedAt: null,
    tokenExpiresAt: null,
    actorChain: [],
    subject: "00000000-0000-0000-0000-000000000001",
    userId: "00000000-0000-0000-0000-000000000001",
    username: "alice",
    requestedByClientId: "d2.web",
    immediateCallerClientId: null,
    originatingClientId: "00000000-0000-0000-0000-000000000001",
    isServiceIdentity: false,
    orgId: "00000000-0000-0000-0000-000000000003",
    orgName: "Acme",
    orgType: null,
    orgRole: null,
    isImpersonating: false,
    impersonationKind: null,
    impersonatedBy: null,
    impersonationSessionId: null,
    impersonatorOrgId: null,
    impersonatorOrgName: null,
    impersonatorOrgType: null,
    impersonatorOrgRole: null,
    scopes: new Set<string>(),
    traceId: null,
    requestId: null,
    requestPath: null,
    clientIp: null,
    sessionFingerprint: null,
    currentFingerprint: null,
    riskScore: null,
    whoIsHashId: null,
    adminLocationHashId: null,
    city: null,
    region: null,
    subdivisionCode: null,
    countryCode: null,
    postalCode: null,
    latitude: null,
    longitude: null,
    geohash: null,
    isVpn: null,
    isProxy: null,
    isTor: null,
    isHosting: null,
    asn: null,
    asnName: null,
    asnType: null,
    ...overrides,
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
