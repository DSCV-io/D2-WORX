// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  LOCAL_CACHE_DEFAULTS,
  type ILocalCache,
  type LocalCacheOptions,
} from "@dcsv-io/d2-caching-abstractions";
import { describe, expect, it } from "vitest";

import { DefaultLocalCache, LOCAL_CACHE_METER_NAME } from "../src/index.js";
import * as barrel from "../src/index.js";

// ---------------------------------------------------------------------------
// Type-level composition gates (exercised by `type-check:test`)
// ---------------------------------------------------------------------------

type AssertTrue<T extends true> = T;

type _ImplementsILocalCache = AssertTrue<
  DefaultLocalCache extends ILocalCache ? true : false
>;

type _CtorOptionsParamOptional = AssertTrue<
  ConstructorParameters<typeof DefaultLocalCache> extends
    | []
    | [Partial<LocalCacheOptions>?]
    | [Partial<LocalCacheOptions>?, (() => number)?]
    ? true
    : false
>;

// Force the aliases into the type graph so type-check:test fails if they
// become `never` / non-true.
const _typeGates: [_ImplementsILocalCache, _CtorOptionsParamOptional] = [
  true,
  true,
];

void _typeGates;

function newCache(
  partial?: Partial<LocalCacheOptions>,
  clock?: () => number,
): DefaultLocalCache {
  return new DefaultLocalCache(partial, clock);
}

describe("DefaultLocalCache construction", () => {
  it("ctor_noArgs_usesLocalCacheDefaults", async () => {
    const cache = newCache();
    const now = 1_000;
    const clocked = newCache(undefined, () => now);

    await clocked.set("k", "v");
    const ttl = await clocked.getTtl("k");

    expect(ttl.success).toBe(true);
    expect(ttl.data).toBe(LOCAL_CACHE_DEFAULTS.defaultExpirationMs);
    cache.dispose();
    clocked.dispose();
  });

  it("ctor_partialOptions_mergesOverDefaults", async () => {
    const now = 0;
    const cache = newCache({ maxEntries: 7, keyPrefix: "p:" }, () => now);

    await cache.set("k", 1);
    const hit = await cache.get<number>("k");

    expect(hit.success).toBe(true);
    expect(hit.data).toBe(1);

    // Prefix applies; maxEntries 7 is accepted without throw.
    cache.dispose();
  });

  it("ctor_maxEntriesNegative_throwsRangeError", () => {
    expect(() => newCache({ maxEntries: -1 })).toThrow(RangeError);
    expect(() => newCache({ maxEntries: -1 })).toThrow(/maxEntries/);
  });

  it("ctor_maxEntriesNonInteger_throwsRangeError", () => {
    expect(() => newCache({ maxEntries: 1.5 })).toThrow(RangeError);
    expect(() => newCache({ maxEntries: 1.5 })).toThrow(/maxEntries/);
  });

  it("ctor_maxEntriesNaN_throwsRangeError", () => {
    expect(() => newCache({ maxEntries: Number.NaN })).toThrow(RangeError);
    expect(() => newCache({ maxEntries: Number.NaN })).toThrow(/maxEntries/);
  });

  it.each([
    Number.POSITIVE_INFINITY,
    Number.NEGATIVE_INFINITY,
    Number.MAX_SAFE_INTEGER + 1,
  ] as const)(
    "ctor_maxEntriesNonSafeInteger_throwsRangeError_theory (%s)",
    (value) => {
      expect(() => newCache({ maxEntries: value })).toThrow(RangeError);
      expect(() => newCache({ maxEntries: value })).toThrow(/maxEntries/);
    },
  );

  it.each([Number.NaN, Number.POSITIVE_INFINITY, Number.NEGATIVE_INFINITY])(
    "ctor_defaultExpirationMsNonFinite_throwsRangeError_theory (%s)",
    (value) => {
      expect(() => newCache({ defaultExpirationMs: value })).toThrow(
        RangeError,
      );
      expect(() => newCache({ defaultExpirationMs: value })).toThrow(
        /defaultExpirationMs/,
      );
    },
  );

  it("ctor_defaultExpirationMsMaxValue_finiteAccepted", async () => {
    const cache = newCache({
      maxEntries: 1,
      defaultExpirationMs: Number.MAX_VALUE,
    });

    await cache.set("k", 1);
    const ttl = await cache.getTtl("k");

    expect(ttl.success).toBe(true);
    expect(ttl.data).toBe(Number.MAX_VALUE);
    cache.dispose();
  });

  it("ctor_maxEntriesZero_allowsCapacityZeroCache", async () => {
    const cache = newCache({ maxEntries: 0, defaultExpirationMs: 0 });

    await cache.set("k", "v");
    const hit = await cache.get<string>("k");

    expect(hit.success).toBe(false);
    expect(hit.errorCode).toBeDefined();
    cache.dispose();
  });

  it("ctor_defaultExpirationZeroOrNegative_meansNoDefaultTtl", async () => {
    const now = 0;
    const zero = newCache({ defaultExpirationMs: 0 }, () => now);
    const neg = newCache({ defaultExpirationMs: -5 }, () => now);

    await zero.set("a", 1);
    await neg.set("b", 2);

    const ttlA = await zero.getTtl("a");
    const ttlB = await neg.getTtl("b");

    expect(ttlA.success).toBe(true);
    expect(ttlA.data).toBeUndefined();
    expect(ttlB.success).toBe(true);
    expect(ttlB.data).toBeUndefined();
    zero.dispose();
    neg.dispose();
  });

  it("ctor_throws_isRangeErrorNotD2Result", () => {
    try {
      newCache({ maxEntries: -3 });
      expect.fail("expected throw");
    } catch (err) {
      expect(err).toBeInstanceOf(RangeError);
      expect(err).not.toHaveProperty("success");
    }
  });

  it("ctor_defaultClock_isDateNow_ttlObservableWithoutSleep", async () => {
    const cache = newCache({ defaultExpirationMs: 60_000 });

    await cache.set("k", "v");
    const ttl = await cache.getTtl("k");

    expect(ttl.success).toBe(true);
    expect(typeof ttl.data).toBe("number");
    expect(ttl.data!).toBeGreaterThan(0);
    expect(ttl.data!).toBeLessThanOrEqual(60_000);
    cache.dispose();
  });

  it("composition_publicSurface_satisfiesILocalCache_endToEnd", async () => {
    const asPort: ILocalCache = new barrel.DefaultLocalCache({
      maxEntries: 10,
      defaultExpirationMs: 0,
    });

    const setR = await asPort.set("k", "v");
    const getR = await asPort.get<string>("k");
    const nxR = await asPort.setNx("k2", 1);
    const incR = await asPort.increment("counter");
    const acqR = await asPort.acquireLock("lk", "id1", 1_000);
    const relR = await asPort.releaseLock("lk", "id1");
    const remR = await asPort.remove("k");

    expect(setR.success).toBe(true);
    expect(getR.success).toBe(true);
    expect(getR.data).toBe("v");
    expect(nxR.success).toBe(true);
    expect(nxR.data).toBe(true);
    expect(incR.success).toBe(true);
    expect(incR.data).toBe(1);
    expect(acqR.success).toBe(true);
    expect(acqR.data).toBe(true);
    expect(relR.success).toBe(true);
    expect(remR.success).toBe(true);

    if (asPort instanceof DefaultLocalCache) {
      asPort.dispose();
    }
  });

  it("packageIndex_exportsExactlyDefaultLocalCacheAndMeterSurface", () => {
    expect(barrel.DefaultLocalCache).toBe(DefaultLocalCache);
    expect(barrel.LOCAL_CACHE_METER_NAME).toBe(LOCAL_CACHE_METER_NAME);
    expect(Object.keys(barrel).sort()).toEqual([
      "DefaultLocalCache",
      "LOCAL_CACHE_INSTRUMENTS",
      "LOCAL_CACHE_METER_NAME",
      "LOCAL_CACHE_METER_VERSION",
    ]);
  });
});
