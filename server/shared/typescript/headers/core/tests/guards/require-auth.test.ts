// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { AuthErrorCodes } from "@d2/auth-abstractions";
import type { IRequestContext } from "@d2/request-context-abstractions";
import { requireAuth } from "../../src/guards/require-auth.js";
import type {
  GuardRequestEvent,
  GuardThrowers,
} from "../../src/guards/guard-types.js";
import type { ProblemDetailsBody } from "../../src/problem-details.js";
import { ProblemDetailsExtensionKeys } from "@d2/problem-details-abstractions";

interface ThrownError {
  status: number;
  body: ProblemDetailsBody;
  contentType: string;
}

function makeThrowers(): { throwers: GuardThrowers; thrown: ThrownError[] } {
  const thrown: ThrownError[] = [];
  const throwers: GuardThrowers = {
    throwError(status, body, contentType) {
      thrown.push({ status, body, contentType });
      throw new Error(
        `HTTP ${status}: ${body[ProblemDetailsExtensionKeys.ERROR_CODE]}`,
      );
    },
    throwRedirect() {
      throw new Error("unexpected redirect");
    },
  };
  return { throwers, thrown };
}

function authenticatedCtx(
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
    ...overrides,
  };
}

function makeEvent(ctx: IRequestContext | undefined): GuardRequestEvent {
  return {
    url: { pathname: "/test" },
    locals: ctx === undefined ? {} : { requestContext: ctx },
  };
}

describe("requireAuth — happy path", () => {
  it("does not throw when isAuthenticated is true", () => {
    const event = makeEvent(authenticatedCtx());
    const { throwers } = makeThrowers();
    expect(() => requireAuth(event, throwers)).not.toThrow();
  });
});

describe("requireAuth — rejection branches", () => {
  it("throws 401 when requestContext is undefined", () => {
    const event = makeEvent(undefined);
    const { throwers, thrown } = makeThrowers();
    expect(() => requireAuth(event, throwers)).toThrow();
    expect(thrown).toHaveLength(1);
    expect(thrown[0]?.status).toBe(401);
    expect(thrown[0]?.body[`d2_error_code`]).toBe(
      AuthErrorCodes.AUTH_BEARER_MISSING,
    );
  });

  it("throws 401 when isAuthenticated is false", () => {
    const event = makeEvent(authenticatedCtx({ isAuthenticated: false }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireAuth(event, throwers)).toThrow();
    expect(thrown[0]?.status).toBe(401);
  });

  it("throws 401 when isAuthenticated is undefined (pre-auth)", () => {
    const event = makeEvent(authenticatedCtx({ isAuthenticated: undefined }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireAuth(event, throwers)).toThrow();
    expect(thrown[0]?.status).toBe(401);
  });

  it("throws 401 when isAuthenticated is missing (malformed context)", () => {
    const ctx = authenticatedCtx() as unknown as Record<string, unknown>;
    delete ctx["isAuthenticated"];
    const event = makeEvent(ctx as unknown as IRequestContext);
    const { throwers, thrown } = makeThrowers();
    expect(() => requireAuth(event, throwers)).toThrow();
    expect(thrown[0]?.status).toBe(401);
  });

  it("propagates traceId from context to ProblemDetails body", () => {
    const event = makeEvent(
      authenticatedCtx({ isAuthenticated: false, traceId: "trace-77" }),
    );
    const { throwers, thrown } = makeThrowers();
    expect(() => requireAuth(event, throwers)).toThrow();
    expect(thrown[0]?.body[ProblemDetailsExtensionKeys.TRACE_ID]).toBe(
      "trace-77",
    );
  });

  it("emits ProblemDetails with the request URL pathname as instance", () => {
    const event: GuardRequestEvent = {
      url: { pathname: "/my/sub/route" },
      locals: {},
    };
    const { throwers, thrown } = makeThrowers();
    expect(() => requireAuth(event, throwers)).toThrow();
    expect(thrown[0]?.body["instance"]).toBe("/my/sub/route");
  });
});
