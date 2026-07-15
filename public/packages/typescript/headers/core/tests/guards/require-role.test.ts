// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { Role } from "@d2/auth-context-abstractions";
import { requireRole } from "../../src/guards/require-role.js";
import { authenticatedCtx, makeEvent, makeThrowers } from "./helpers.js";

describe("requireRole — happy path", () => {
  it("does not throw when any role is present and no roles specified", () => {
    const event = makeEvent(authenticatedCtx({ orgRole: Role.Agent }));
    const { throwers } = makeThrowers();
    expect(() => requireRole(event, throwers)).not.toThrow();
  });

  it("does not throw when orgRole matches one of the requested", () => {
    const event = makeEvent(authenticatedCtx({ orgRole: Role.Owner }));
    const { throwers } = makeThrowers();
    expect(() =>
      requireRole(event, throwers, Role.Owner, Role.Officer),
    ).not.toThrow();
  });
});

describe("requireRole — rejection branches", () => {
  it("throws 401 when not authenticated (auth check fires first)", () => {
    const event = makeEvent(authenticatedCtx({ isAuthenticated: false }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireRole(event, throwers)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(401);
    }
  });

  it("throws 403 when orgRole is absent and no roles arg", () => {
    const event = makeEvent(authenticatedCtx({ orgRole: undefined }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireRole(event, throwers)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });

  it("throws 403 when orgRole does not match any requested role", () => {
    const event = makeEvent(authenticatedCtx({ orgRole: Role.Agent }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireRole(event, throwers, Role.Owner)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });

  it("throws 403 when orgRole absent and roles arg present", () => {
    const event = makeEvent(authenticatedCtx({ orgRole: undefined }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireRole(event, throwers, Role.Owner)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });
});

describe("requireRole — RFC 7807 §3.1 wire compliance", () => {
  // Regression pin: HTTP throw status MUST equal ProblemDetails body.status.
  // Earlier code used `AuthFailures.scopeInsufficient` (which surfaces as
  // 401) but threw HTTP 403 — the two values disagreed and violated RFC
  // 7807 §3.1.
  it("body.status equals HTTP throw status (403) on missing role", () => {
    const event = makeEvent(authenticatedCtx({ orgRole: undefined }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireRole(event, throwers)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].body.status).toBe(thrown[0].status);
      expect(thrown[0].body.status).toBe(403);
    }
  });

  it("body.status equals HTTP throw status (403) on role mismatch", () => {
    const event = makeEvent(authenticatedCtx({ orgRole: Role.Agent }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireRole(event, throwers, Role.Owner)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].body.status).toBe(thrown[0].status);
      expect(thrown[0].body.status).toBe(403);
    }
  });
});
