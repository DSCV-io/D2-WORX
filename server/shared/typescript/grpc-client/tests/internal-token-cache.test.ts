// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { InternalTokenCache } from "../src/internal-token-cache.js";
import type { InternalTokenSnapshot } from "../src/types.js";

const SNAP: InternalTokenSnapshot = {
  accessToken: "fake-jwt",
  expiresAtMs: Number.MAX_SAFE_INTEGER,
  audience: "d2.edge",
};

describe("InternalTokenCache", () => {
  it("returns null when empty", () => {
    const cache = new InternalTokenCache();
    expect(cache.tryGet()).toBeNull();
  });

  it("returns the snapshot after set when fresh", () => {
    const cache = new InternalTokenCache();
    cache.set(SNAP);
    expect(cache.tryGet()).toEqual(SNAP);
  });

  it("returns null when snapshot is expired (with skew applied)", () => {
    let now = 1_000_000;
    const cache = new InternalTokenCache({ clock: () => now });
    cache.set({
      accessToken: "old",
      expiresAtMs: 1_010_000,
      audience: "d2.edge",
    });
    expect(cache.tryGet()).not.toBeNull();
    now = 1_005_001; // skew 5_000 — token effectively expired now
    expect(cache.tryGet()).toBeNull();
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
    expect(cache.tryGet()).not.toBeNull();
    now = 901; // expiresAt - skew = 900 — past now
    expect(cache.tryGet()).toBeNull();
  });

  it("clear() drops the cache", () => {
    const cache = new InternalTokenCache();
    cache.set(SNAP);
    expect(cache.tryGet()).not.toBeNull();
    cache.clear();
    expect(cache.tryGet()).toBeNull();
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
    expect(cache.tryGet()?.accessToken).toBe("next-jwt");
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
    expect(cache.tryGet()).not.toBeNull();
    now = 5_001;
    expect(cache.tryGet()).toBeNull();
  });
});
