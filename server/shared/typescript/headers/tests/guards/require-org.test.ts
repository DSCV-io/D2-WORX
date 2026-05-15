// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { AuthErrorCodes } from "@d2/auth-abstractions";
import { OrgType } from "@d2/auth-context-abstractions";
import { requireOrg } from "../../src/guards/require-org.js";
import { authenticatedCtx, makeEvent, makeThrowers } from "./helpers.js";

describe("requireOrg — happy path", () => {
  it("does not throw when org context is present and no types specified", () => {
    const event = makeEvent(authenticatedCtx());
    const { throwers } = makeThrowers();
    expect(() => requireOrg(event, throwers)).not.toThrow();
  });

  it("does not throw when orgType matches one of the requested", () => {
    const event = makeEvent(authenticatedCtx({ orgType: OrgType.Customer }));
    const { throwers } = makeThrowers();
    expect(() =>
      requireOrg(event, throwers, OrgType.Admin, OrgType.Customer),
    ).not.toThrow();
  });
});

describe("requireOrg — rejection branches", () => {
  it("throws 401 when not authenticated (auth check fires first)", () => {
    const event = makeEvent(authenticatedCtx({ isAuthenticated: false }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireOrg(event, throwers)).toThrow();
    expect(thrown[0]?.kind).toBe("error");
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(401);
    }
  });

  it("throws 403 when orgId is empty", () => {
    const event = makeEvent(authenticatedCtx({ orgId: "" }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireOrg(event, throwers)).toThrow();
    expect(thrown[0]?.kind).toBe("error");
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
      expect(thrown[0].body[`d2_error_code`]).toBe(
        AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT,
      );
    }
  });

  it("throws 403 when orgId is null", () => {
    const event = makeEvent(authenticatedCtx({ orgId: null }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireOrg(event, throwers)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });

  it("throws 403 when orgType is null and types arg non-empty", () => {
    const event = makeEvent(authenticatedCtx({ orgType: null }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireOrg(event, throwers, OrgType.Admin)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });

  it("throws 403 when orgType does not match any of the requested types", () => {
    const event = makeEvent(authenticatedCtx({ orgType: OrgType.Customer }));
    const { throwers, thrown } = makeThrowers();
    expect(() =>
      requireOrg(event, throwers, OrgType.Admin, OrgType.Support),
    ).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });
});

describe("requireOrg — RFC 7807 §3.1 wire compliance", () => {
  // Regression pin: HTTP throw status MUST equal ProblemDetails body.status.
  // Earlier code used `AuthFailures.scopeInsufficient` (which surfaces as
  // 401) but threw HTTP 403 — the two values disagreed and violated RFC
  // 7807 §3.1.
  it("body.status equals HTTP throw status (403) on missing org", () => {
    const event = makeEvent(authenticatedCtx({ orgId: "" }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireOrg(event, throwers)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].body.status).toBe(thrown[0].status);
      expect(thrown[0].body.status).toBe(403);
    }
  });

  it("body.status equals HTTP throw status (403) on wrong org type", () => {
    const event = makeEvent(authenticatedCtx({ orgType: OrgType.Customer }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireOrg(event, throwers, OrgType.Admin)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].body.status).toBe(thrown[0].status);
      expect(thrown[0].body.status).toBe(403);
    }
  });
});
