// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { requireScope } from "../../src/guards/require-scope.js";
import { authenticatedCtx, makeEvent, makeThrowers } from "./helpers.js";

describe("requireScope — happy path", () => {
  it("does not throw when scope set contains the requested scope", () => {
    const event = makeEvent(
      authenticatedCtx({ scopes: new Set(["scope.a", "scope.b"]) }),
    );
    const { throwers } = makeThrowers();
    expect(() => requireScope(event, throwers, "scope.a")).not.toThrow();
  });

  it("any-of semantics: passes when set has any one of the requested", () => {
    const event = makeEvent(authenticatedCtx({ scopes: new Set(["scope.b"]) }));
    const { throwers } = makeThrowers();
    expect(() =>
      requireScope(event, throwers, "scope.a", "scope.b", "scope.c"),
    ).not.toThrow();
  });
});

describe("requireScope — rejection branches", () => {
  it("throws 500 when called with no scopes (programmer error)", () => {
    const event = makeEvent(authenticatedCtx({ scopes: new Set(["scope.a"]) }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireScope(event, throwers)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(500);
      expect(thrown[0].body[`d2_error_code`]).toBe("REQUIRE_SCOPE_NO_ARGS");
    }
  });

  it("throws 401 when not authenticated", () => {
    const event = makeEvent(
      authenticatedCtx({
        isAuthenticated: false,
        scopes: new Set(["scope.a"]),
      }),
    );
    const { throwers, thrown } = makeThrowers();
    expect(() => requireScope(event, throwers, "scope.a")).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(401);
    }
  });

  it("throws 403 when none of the requested scopes are present", () => {
    const event = makeEvent(authenticatedCtx({ scopes: new Set(["scope.x"]) }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireScope(event, throwers, "scope.a", "scope.b")).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });

  it("throws 403 when scopes is empty Set", () => {
    const event = makeEvent(authenticatedCtx({ scopes: new Set() }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireScope(event, throwers, "scope.a")).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });

  it("throws 403 when scopes is not a Set (malformed context)", () => {
    const ctx = authenticatedCtx() as unknown as { scopes: unknown };
    ctx.scopes = ["scope.a"]; // wrong shape — Array, not Set
    const event = makeEvent(
      ctx as unknown as ReturnType<typeof authenticatedCtx>,
    );
    const { throwers, thrown } = makeThrowers();
    expect(() => requireScope(event, throwers, "scope.a")).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(403);
    }
  });
});

describe("requireScope — RFC 7807 §3.1 wire compliance", () => {
  // Regression pin: HTTP throw status MUST equal ProblemDetails body.status.
  // Earlier code used `AuthFailures.scopeInsufficient` (which surfaces as
  // 401) but threw HTTP 403 — the two values disagreed and violated RFC
  // 7807 §3.1.
  it("body.status equals HTTP throw status (403) on scope mismatch", () => {
    const event = makeEvent(authenticatedCtx({ scopes: new Set(["scope.x"]) }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireScope(event, throwers, "scope.a")).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].body.status).toBe(thrown[0].status);
      expect(thrown[0].body.status).toBe(403);
    }
  });

  it("body.status equals HTTP throw status (500) on programmer-error misuse", () => {
    const event = makeEvent(authenticatedCtx({ scopes: new Set(["scope.a"]) }));
    const { throwers, thrown } = makeThrowers();
    expect(() => requireScope(event, throwers)).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].body.status).toBe(thrown[0].status);
      expect(thrown[0].body.status).toBe(500);
    }
  });
});
