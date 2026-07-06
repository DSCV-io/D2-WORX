// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { Temporal } from "temporal-polyfill";
import { WorkloadLeafCache } from "../src/issuance/leaf-cache.js";
import type { LeafSnapshot } from "../src/issuance/workload-leaf-material.js";

function snapshotExpiring(atMs: number): LeafSnapshot {
  return {
    certChainPem: "chain",
    privateKeyPem: "key",
    notAfter: Temporal.Instant.fromEpochMilliseconds(atMs),
  };
}

describe("WorkloadLeafCache", () => {
  it("returns nothing before anything is set", () => {
    const cache = new WorkloadLeafCache();

    expect(cache.tryGet(0)).toBeUndefined();
    expect(cache.peekRaw()).toBeUndefined();
  });

  it("serves the snapshot while it is unexpired (notAfter > now)", () => {
    const cache = new WorkloadLeafCache();
    const snap = snapshotExpiring(1000);
    cache.set(snap);

    expect(cache.tryGet(999)).toBe(snap);
  });

  it("withholds an expired snapshot from tryGet but still peeks it raw", () => {
    const cache = new WorkloadLeafCache();
    const snap = snapshotExpiring(1000);
    cache.set(snap);

    expect(cache.tryGet(1000)).toBeUndefined();
    expect(cache.tryGet(2000)).toBeUndefined();
    expect(cache.peekRaw()).toBe(snap);
  });

  it("supersedes a prior snapshot on set", () => {
    const cache = new WorkloadLeafCache();
    cache.set(snapshotExpiring(1000));
    const next = snapshotExpiring(5000);
    cache.set(next);

    expect(cache.tryGet(2000)).toBe(next);
  });
});
