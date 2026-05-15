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

interface ThrownError {
  status: number;
  body: ProblemDetailsBody;
}

function makeThrowers(): { throwers: GuardThrowers; thrown: ThrownError[] } {
  const thrown: ThrownError[] = [];
  const throwers: GuardThrowers = {
    throwError(status, body) {
      thrown.push({ status, body });
      throw new Error(`HTTP ${status}: ${body[`d2_error_code`]}`);
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

  it("throws 401 when isAuthenticated is null (pre-auth)", () => {
    const event = makeEvent(authenticatedCtx({ isAuthenticated: null }));
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
    expect(thrown[0]?.body["traceId"]).toBe("trace-77");
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
