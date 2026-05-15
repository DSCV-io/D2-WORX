// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { redirectIfAuthenticated } from "../../src/guards/redirect-if-authenticated.js";
import { authenticatedCtx, makeEvent, makeThrowers } from "./helpers.js";

describe("redirectIfAuthenticated — redirect branch", () => {
  it("redirects 303 when authenticated", () => {
    const event = makeEvent(authenticatedCtx());
    const { throwers, thrown } = makeThrowers();
    expect(() =>
      redirectIfAuthenticated(event, throwers, "/dashboard"),
    ).toThrow();
    expect(thrown[0]?.kind).toBe("redirect");
    if (thrown[0]?.kind === "redirect") {
      expect(thrown[0].status).toBe(303);
      expect(thrown[0].location).toBe("/dashboard");
    }
  });
});

describe("redirectIfAuthenticated — pass-through branches", () => {
  it("returns void (no throw) when not authenticated", () => {
    const event = makeEvent(authenticatedCtx({ isAuthenticated: false }));
    const { throwers, thrown } = makeThrowers();
    expect(() =>
      redirectIfAuthenticated(event, throwers, "/dashboard"),
    ).not.toThrow();
    expect(thrown).toHaveLength(0);
  });

  it("returns void when isAuthenticated is null (pre-auth)", () => {
    const event = makeEvent(authenticatedCtx({ isAuthenticated: null }));
    const { throwers, thrown } = makeThrowers();
    expect(() =>
      redirectIfAuthenticated(event, throwers, "/dashboard"),
    ).not.toThrow();
    expect(thrown).toHaveLength(0);
  });

  it("returns void when no requestContext is present", () => {
    const event = makeEvent(undefined);
    const { throwers, thrown } = makeThrowers();
    expect(() =>
      redirectIfAuthenticated(event, throwers, "/dashboard"),
    ).not.toThrow();
    expect(thrown).toHaveLength(0);
  });
});

describe("redirectIfAuthenticated — programmer-error branch", () => {
  it("throws 500 on empty `to`", () => {
    const event = makeEvent(authenticatedCtx());
    const { throwers, thrown } = makeThrowers();
    expect(() => redirectIfAuthenticated(event, throwers, "")).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(500);
    }
  });

  it("throws 500 on whitespace-only `to`", () => {
    const event = makeEvent(authenticatedCtx());
    const { throwers, thrown } = makeThrowers();
    expect(() => redirectIfAuthenticated(event, throwers, "   ")).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(500);
    }
  });

  it("throws 500 on non-string `to`", () => {
    const event = makeEvent(authenticatedCtx());
    const { throwers, thrown } = makeThrowers();
    expect(() =>
      redirectIfAuthenticated(event, throwers, 42 as unknown as string),
    ).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(500);
    }
  });

  it("throws 500 on `to` containing CR (header injection)", () => {
    const event = makeEvent(authenticatedCtx());
    const { throwers, thrown } = makeThrowers();
    expect(() =>
      redirectIfAuthenticated(event, throwers, "/x\rhack"),
    ).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(500);
    }
  });

  it("throws 500 on `to` containing LF (header injection)", () => {
    const event = makeEvent(authenticatedCtx());
    const { throwers, thrown } = makeThrowers();
    expect(() =>
      redirectIfAuthenticated(event, throwers, "/x\nhack"),
    ).toThrow();
    if (thrown[0]?.kind === "error") {
      expect(thrown[0].status).toBe(500);
    }
  });
});
