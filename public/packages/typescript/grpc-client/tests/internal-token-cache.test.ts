// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { InternalTokenCache } from "../src/internal-token-cache.js";
import type { InternalTokenSnapshot } from "../src/types.js";

const SNAP: InternalTokenSnapshot = {
  accessToken: "fake-jwt",
  expiresAtMs: Number.MAX_SAFE_INTEGER,
  audience: "d2.edge",
};

// ---------------------------------------------------------------------------
// Baseline: empty / clear / set / replace
// ---------------------------------------------------------------------------

describe("InternalTokenCache — empty / clear / replace", () => {
  it("returns undefined snapshot when empty", () => {
    const cache = new InternalTokenCache();
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot).toBeUndefined();
    expect(shouldRefreshAhead).toBe(false);
  });

  it("returns the snapshot after set when fresh", () => {
    const cache = new InternalTokenCache();
    cache.set(SNAP);
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot).toEqual(SNAP);
    expect(shouldRefreshAhead).toBe(false);
  });

  it("clear() drops the cache", () => {
    const cache = new InternalTokenCache();
    cache.set(SNAP);
    expect(cache.tryGet().snapshot).not.toBeUndefined();
    cache.clear();
    expect(cache.tryGet().snapshot).toBeUndefined();
    expect(cache.tryGet().shouldRefreshAhead).toBe(false);
  });

  it("set() replaces a prior entry", () => {
    const cache = new InternalTokenCache();
    cache.set(SNAP);
    const next: InternalTokenSnapshot = {
      accessToken: "next-jwt",
      expiresAtMs: SNAP.expiresAtMs,
      audience: "d2.edge",
    };
    cache.set(next);
    expect(cache.tryGet().snapshot?.accessToken).toBe("next-jwt");
  });
});

// ---------------------------------------------------------------------------
// Hard-expiry (skew boundary) — pre-existing behavior, adapted for new shape
// ---------------------------------------------------------------------------

describe("InternalTokenCache — hard-expiry skew", () => {
  it("returns undefined snapshot when snapshot is expired (with skew applied)", () => {
    let now = 1_000_000;
    const cache = new InternalTokenCache({ clock: () => now });
    cache.set({
      accessToken: "old",
      expiresAtMs: 1_010_000,
      audience: "d2.edge",
    });
    expect(cache.tryGet().snapshot).not.toBeUndefined();
    now = 1_005_001; // skew 5_000 — token effectively expired now
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot).toBeUndefined();
    expect(shouldRefreshAhead).toBe(false);
  });

  it("respects custom skew", () => {
    let now = 0;
    const cache = new InternalTokenCache({ clock: () => now, skewMs: 100 });
    cache.set({
      accessToken: "x",
      expiresAtMs: 1_000,
      audience: "d2.edge",
    });
    now = 800;
    expect(cache.tryGet().snapshot).not.toBeUndefined();
    now = 901; // expiresAt - skew = 900 — past now
    expect(cache.tryGet().snapshot).toBeUndefined();
  });

  it("default skew is 5_000ms", () => {
    let now = 0;
    const cache = new InternalTokenCache({ clock: () => now });
    cache.set({
      accessToken: "x",
      expiresAtMs: 10_000,
      audience: "d2.edge",
    });
    now = 4_999; // expiresAt(10000) - skew(5000) = 5000 > now(4999) → fresh
    expect(cache.tryGet().snapshot).not.toBeUndefined();
    now = 5_001;
    expect(cache.tryGet().snapshot).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Cleanup #2 — refresh-ahead three-state logic
// ---------------------------------------------------------------------------

describe("InternalTokenCache — refresh-ahead: fresh token", () => {
  it("fresh token (now < expiresAtMs − refreshLeadMs) → snapshot served, NO refresh signal", () => {
    // expiresAtMs = 200_000, refreshLeadMs = 60_000 → lead boundary = 140_000
    // skewMs = 5_000 → hard expiry = 195_000
    // now = 100_000 → well before lead boundary → FRESH
    const now = 100_000;
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs: 5_000,
      refreshLeadMs: 60_000,
    });
    cache.set({
      accessToken: "tok",
      expiresAtMs: 200_000,
      audience: "d2.edge",
    });
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot?.accessToken).toBe("tok");
    expect(shouldRefreshAhead).toBe(false);
  });
});

describe("InternalTokenCache — refresh-ahead: aging token", () => {
  it("aging token (expiresAtMs − refreshLeadMs ≤ now < expiresAtMs − skewMs) → snapshot served AND refresh signal", () => {
    // expiresAtMs = 200_000, refreshLeadMs = 60_000, skewMs = 5_000
    // lead boundary = 140_000, hard expiry = 195_000
    // now = 150_000 → inside aging window → AGING
    const now = 150_000;
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs: 5_000,
      refreshLeadMs: 60_000,
    });
    cache.set({
      accessToken: "tok",
      expiresAtMs: 200_000,
      audience: "d2.edge",
    });
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot?.accessToken).toBe("tok");
    expect(shouldRefreshAhead).toBe(true);
  });

  it("now exactly at the lead boundary → aging (shouldRefreshAhead = true)", () => {
    const expiresAtMs = 200_000;
    const refreshLeadMs = 60_000;
    const skewMs = 5_000;
    // Exactly at lead boundary: expiresAtMs − refreshLeadMs = 140_000
    const now = expiresAtMs - refreshLeadMs;
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs,
      refreshLeadMs,
    });
    cache.set({ accessToken: "tok", expiresAtMs, audience: "d2.edge" });
    expect(cache.tryGet().shouldRefreshAhead).toBe(true);
  });

  it("now one ms before lead boundary → fresh (shouldRefreshAhead = false)", () => {
    const expiresAtMs = 200_000;
    const refreshLeadMs = 60_000;
    const skewMs = 5_000;
    const now = expiresAtMs - refreshLeadMs - 1;
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs,
      refreshLeadMs,
    });
    cache.set({ accessToken: "tok", expiresAtMs, audience: "d2.edge" });
    expect(cache.tryGet().shouldRefreshAhead).toBe(false);
  });

  it("now one ms before hard expiry → still aging (token still served)", () => {
    const expiresAtMs = 200_000;
    const refreshLeadMs = 60_000;
    const skewMs = 5_000;
    // now = hardExpiryMs − 1 = 194_999 → inside aging window, not yet expired
    const now = expiresAtMs - skewMs - 1;
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs,
      refreshLeadMs,
    });
    cache.set({ accessToken: "tok", expiresAtMs, audience: "d2.edge" });
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot).not.toBeUndefined();
    expect(shouldRefreshAhead).toBe(true);
  });
});

describe("InternalTokenCache — refresh-ahead: hard-expired in aging zone", () => {
  it("hard-expired (now ≥ expiresAtMs − skewMs) → snapshot undefined even when in refreshLeadMs zone", () => {
    // expiresAtMs = 200_000, skewMs = 5_000 → hardExpiryMs = 195_000
    // refreshLeadMs = 60_000 → would be aging at 150_000, but at 195_001 it's expired
    const now = 195_001;
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs: 5_000,
      refreshLeadMs: 60_000,
    });
    cache.set({
      accessToken: "tok",
      expiresAtMs: 200_000,
      audience: "d2.edge",
    });
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot).toBeUndefined();
    expect(shouldRefreshAhead).toBe(false);
  });
});

describe("InternalTokenCache — refresh-ahead: after successful ahead-refresh", () => {
  it("set() with a new token after an ahead-refresh serves the NEW token and no longer signals refresh", () => {
    const expiresAtMs = 200_000;
    const refreshLeadMs = 60_000;
    const skewMs = 5_000;
    const now = 150_000; // aging window
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs,
      refreshLeadMs,
    });
    cache.set({ accessToken: "old-tok", expiresAtMs, audience: "d2.edge" });

    // confirm aging
    expect(cache.tryGet().shouldRefreshAhead).toBe(true);

    // background refresh completes — store new token with far-future expiry
    const newExpiry = 2_000_000;
    cache.set({
      accessToken: "new-tok",
      expiresAtMs: newExpiry,
      audience: "d2.edge",
    });

    // now still at 150_000, new token's lead boundary = 2_000_000 − 60_000 = 1_940_000
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot?.accessToken).toBe("new-tok");
    expect(shouldRefreshAhead).toBe(false);
  });
});

describe("InternalTokenCache — constructor guard: refreshLeadMs > skewMs", () => {
  it("throws TypeError when refreshLeadMs === skewMs (aging window would be empty)", () => {
    expect(
      () => new InternalTokenCache({ skewMs: 10, refreshLeadMs: 10 }),
    ).toThrow(TypeError);
  });

  it("throws TypeError when refreshLeadMs < skewMs (aging window would invert)", () => {
    expect(
      () => new InternalTokenCache({ skewMs: 5_000, refreshLeadMs: 3_000 }),
    ).toThrow(TypeError);
  });

  it("does NOT throw when refreshLeadMs > skewMs (valid aging window)", () => {
    expect(
      () => new InternalTokenCache({ skewMs: 5_000, refreshLeadMs: 60_000 }),
    ).not.toThrow();
  });

  it("default values (refreshLeadMs=60_000, skewMs=5_000) do NOT trip the guard", () => {
    expect(() => new InternalTokenCache()).not.toThrow();
  });
});

describe("InternalTokenCache — custom refreshLeadMs", () => {
  it("respects custom refreshLeadMs", () => {
    let now = 0;
    // Small refreshLeadMs = 100 ms, skewMs = 5
    const cache = new InternalTokenCache({
      clock: () => now,
      skewMs: 5,
      refreshLeadMs: 100,
    });
    cache.set({ accessToken: "x", expiresAtMs: 1_000, audience: "d2.edge" });

    // now = 800 → lead boundary = 1_000 − 100 = 900 → 800 < 900 → FRESH
    now = 800;
    expect(cache.tryGet().shouldRefreshAhead).toBe(false);

    // now = 900 → exactly at lead boundary → AGING
    now = 900;
    expect(cache.tryGet().shouldRefreshAhead).toBe(true);

    // now = 994 → still aging (hard expiry at 995)
    now = 994;
    const { snapshot, shouldRefreshAhead } = cache.tryGet();
    expect(snapshot).not.toBeUndefined();
    expect(shouldRefreshAhead).toBe(true);

    // now = 995 → hard expired
    now = 995;
    expect(cache.tryGet().snapshot).toBeUndefined();
  });
});
