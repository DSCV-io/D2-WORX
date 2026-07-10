// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { LocalCacheOptions } from "@d2/caching-abstractions";
import { describe, expect, it } from "vitest";

import { DefaultLocalCache } from "../src/index.js";

function newCache(
  partial?: Partial<LocalCacheOptions>,
  clock?: () => number,
): DefaultLocalCache {
  return new DefaultLocalCache(partial, clock);
}

describe("DefaultLocalCache behavior", () => {
  it("singleClock_drivesValueExpiryAndLockExpiryTogether", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("v", 1, 100);
    await cache.acquireLock("lk", "id1", 100);
    now = 100;

    const valueMiss = await cache.get("v");
    const lockAcq = await cache.acquireLock("lk", "id2", 50);

    expect(valueMiss.success).toBe(false);
    expect(lockAcq.data).toBe(true);
    cache.dispose();
  });

  it("setNx_concurrentContenders_exactlyOneWins", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const results = await Promise.all(
      Array.from({ length: 32 }, (_, i) => cache.setNx("k", i)),
    );
    const winners = results.filter((r) => r.data === true);

    expect(winners).toHaveLength(1);
    cache.dispose();
  });

  it("increment_concurrentBatch_sumExact", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    await Promise.all(Array.from({ length: 1000 }, () => cache.increment("c")));
    const total = await cache.get<number>("c");

    expect(total.data).toBe(1000);
    cache.dispose();
  });

  it("acquireLock_concurrentContenders_exactlyOneAcquires", async () => {
    const cache = newCache();
    const results = await Promise.all(
      Array.from({ length: 16 }, (_, i) =>
        cache.acquireLock("lk", `id${i}`, 1_000),
      ),
    );
    const winners = results.filter((r) => r.data === true);

    expect(winners).toHaveLength(1);
    cache.dispose();
  });

  it("increment_interleavedWithRemove_everyCallReturnsOk", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const ops = await Promise.all([
      cache.increment("k"),
      cache.remove("k"),
      cache.increment("k"),
      cache.remove("k"),
      cache.increment("k", 5),
    ]);

    for (const r of ops) {
      expect(r.success).toBe(true);
    }

    cache.dispose();
  });

  it("mixedOps_interleavedBatch_neverThrows_andStateCoherent", async () => {
    const now = 0;
    const cache = newCache(
      { maxEntries: 50, defaultExpirationMs: 0 },
      () => now,
    );

    const batch = await Promise.all([
      cache.set("s", 1),
      cache.setMany(
        new Map([
          ["m1", 1],
          ["m2", 2],
        ]),
      ),
      cache.get("s"),
      cache.getMany(["m1", "missing"]),
      cache.exists("s"),
      cache.getTtl("s"),
      cache.increment("c"),
      cache.remove("s"),
      cache.set("ttl", "x", 1_000),
      cache.getTtl("ttl"),
    ]);

    for (const r of batch) {
      expect(r).toBeDefined();
    }

    const ttl = await cache.getTtl("ttl");

    expect(ttl.success).toBe(true);
    expect(ttl.data).toBe(1_000);

    const counter = await cache.get<number>("c");

    expect(counter.data).toBe(1);
    cache.dispose();
  });

  it("dispose_isIdempotent_secondCallNoThrow", () => {
    const cache = newCache();

    expect(() => {
      cache.dispose();
      cache.dispose();
    }).not.toThrow();
  });

  it("dispose_viaSymbolDispose_delegatesAndIdempotent", () => {
    const cache = newCache();

    cache[Symbol.dispose]();
    expect(() => cache.dispose()).not.toThrow();
  });

  it("everyOp_afterDispose_throwsDisposedError_theory", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    cache.dispose();

    const ops: Array<() => Promise<unknown>> = [
      () => cache.get("k"),
      () => cache.getMany(["k"]),
      () => cache.exists("k"),
      () => cache.getTtl("k"),
      () => cache.set("k", 1),
      () => cache.setMany(new Map([["k", 1]])),
      () => cache.remove("k"),
      () => cache.removeMany(["k"]),
      () => cache.setNx("k", 1),
      () => cache.increment("k"),
      () => cache.acquireLock("k", "id", 100),
      () => cache.releaseLock("k", "id"),
    ];

    for (const op of ops) {
      await expect(op()).rejects.toThrow("DefaultLocalCache is disposed.");
    }
  });

  it("disposedCheck_precedesValidation_falseyKeyStillThrows", async () => {
    const cache = newCache();

    cache.dispose();
    await expect(cache.get("")).rejects.toThrow(
      "DefaultLocalCache is disposed.",
    );
    await expect(cache.acquireLock("", "", 0)).rejects.toThrow(
      "DefaultLocalCache is disposed.",
    );
  });
});
