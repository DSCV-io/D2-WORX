// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { ICacheSet, ITieredCache } from "@d2/caching-abstractions";
import { describe, expect, it } from "vitest";

import * as barrel from "../src/index.js";
import {
  BACKPLANE_NOT_REGISTERED_MESSAGE,
  DefaultTieredCache,
  type DefaultTieredCacheDeps,
} from "../src/index.js";
import {
  createBackplaneTestDouble,
  createDistributedCacheTestDouble,
  createLocalCacheTestDouble,
  createNoOpTestLogger,
} from "./tiered-double-test-harness.js";

type AssertTrue<T extends true> = T;

type _ImplementsITieredCache = AssertTrue<
  DefaultTieredCache extends ITieredCache ? true : false
>;

/** ITieredCache must not require ICacheSet methods. */
type _DoesNotImplementICacheSet = AssertTrue<
  DefaultTieredCache extends ICacheSet ? false : true
>;

/** Barrel type re-export pin (types erased at runtime; not in Object.keys). */
type _BarrelExportsDefaultTieredCacheDeps = AssertTrue<
  DefaultTieredCacheDeps extends {
    l1: unknown;
    l2: unknown;
    logger: unknown;
  }
    ? true
    : false
>;

const _typeGates: [
  _ImplementsITieredCache,
  _DoesNotImplementICacheSet,
  _BarrelExportsDefaultTieredCacheDeps,
] = [true, true, true];
void _typeGates;

describe("construction + barrel", () => {
  it("barrel_exportsExactPublicSet", () => {
    expect(Object.keys(barrel).sort()).toEqual(
      ["BACKPLANE_NOT_REGISTERED_MESSAGE", "DefaultTieredCache"].sort(),
    );
    expect(barrel.BACKPLANE_NOT_REGISTERED_MESSAGE).toBe(
      BACKPLANE_NOT_REGISTERED_MESSAGE,
    );
  });

  it("barrel_reexportsDefaultTieredCacheDeps_typeAssignable", () => {
    const deps: DefaultTieredCacheDeps = {
      l1: createLocalCacheTestDouble(),
      l2: createDistributedCacheTestDouble(),
      logger: createNoOpTestLogger(),
    };
    expect(new DefaultTieredCache(deps)).toBeInstanceOf(DefaultTieredCache);
  });

  it("composition_publicSurface_satisfiesITieredCache_endToEnd", async () => {
    const l1 = createLocalCacheTestDouble();
    const l2 = createDistributedCacheTestDouble();
    const backplane = createBackplaneTestDouble();
    const asPort: ITieredCache = new DefaultTieredCache({
      l1,
      l2,
      logger: createNoOpTestLogger(),
      backplane,
    });

    expect((await asPort.set("k", "v")).success).toBe(true);
    expect((await asPort.get<string>("k")).data).toBe("v");
    expect((await asPort.exists("k")).data).toBe(true);
    expect((await asPort.getTtl("k")).success).toBe(true);
    expect(
      (await asPort.getMany<string>(["k"])).success ||
        (await asPort.getMany<string>(["k"])).isPartialSuccess,
    ).toBe(true);
    expect((await asPort.setNx("k2", 1)).data).toBe(true);
    expect((await asPort.increment("c")).data).toBe(1);
    expect((await asPort.acquireLock("lk", "id", 1000)).data).toBe(true);
    expect((await asPort.releaseLock("lk", "id")).success).toBe(true);
    expect((await asPort.setAndBroadcast("kb", "vb")).success).toBe(true);
    expect(
      (await asPort.setManyAndBroadcast(new Map([["km", "vm"]]))).success,
    ).toBe(true);
    expect((await asPort.removeAndBroadcast("kb")).success).toBe(true);
    expect((await asPort.removeManyAndBroadcast(["km"])).success).toBe(true);
    expect((await asPort.remove("k")).success).toBe(true);
    expect((await asPort.removeMany(["k2", "c"])).success).toBe(true);
    await (asPort as DefaultTieredCache)[Symbol.asyncDispose]();
  });

  it("ctor_nullishL1_throwsTypeError", () => {
    expect(
      () =>
        new DefaultTieredCache({
          l1: null as unknown as DefaultTieredCacheDeps["l1"],
          l2: createDistributedCacheTestDouble(),
          logger: createNoOpTestLogger(),
        }),
    ).toThrow(TypeError);
  });

  it("ctor_nullishL2_throwsTypeError", () => {
    expect(
      () =>
        new DefaultTieredCache({
          l1: createLocalCacheTestDouble(),
          l2: null as unknown as DefaultTieredCacheDeps["l2"],
          logger: createNoOpTestLogger(),
        }),
    ).toThrow(TypeError);
  });

  it("ctor_nullishLogger_throwsTypeError", () => {
    expect(
      () =>
        new DefaultTieredCache({
          l1: createLocalCacheTestDouble(),
          l2: createDistributedCacheTestDouble(),
          logger: null as unknown as DefaultTieredCacheDeps["logger"],
        }),
    ).toThrow(TypeError);
  });

  it("ctor_throws_isTypeErrorNotD2Result", () => {
    try {
      new DefaultTieredCache({
        l1: undefined as unknown as DefaultTieredCacheDeps["l1"],
        l2: createDistributedCacheTestDouble(),
        logger: createNoOpTestLogger(),
      });
      expect.fail("expected throw");
    } catch (e) {
      expect(e).toBeInstanceOf(TypeError);
      expect(e).not.toHaveProperty("success");
    }
  });

  it("ctor_withBackplane_subscribes", () => {
    const backplane = createBackplaneTestDouble();
    new DefaultTieredCache({
      l1: createLocalCacheTestDouble(),
      l2: createDistributedCacheTestDouble(),
      logger: createNoOpTestLogger(),
      backplane,
    });
    expect(backplane.subscribeCount).toBe(1);
    expect(backplane.handlers.size).toBe(1);
  });

  it("ctor_withoutBackplane_doesNotSubscribe", () => {
    const backplane = createBackplaneTestDouble();
    new DefaultTieredCache({
      l1: createLocalCacheTestDouble(),
      l2: createDistributedCacheTestDouble(),
      logger: createNoOpTestLogger(),
    });
    expect(backplane.subscribeCount).toBe(0);
  });
});
