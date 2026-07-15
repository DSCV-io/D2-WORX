// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type {
  ICacheInvalidationBackplane,
  ICacheSerializer,
  IDistributedCache,
} from "@d2/caching-abstractions";
import { describe, expect, it } from "vitest";

import * as barrel from "../src/index.js";
import {
  createRedisCacheOptions,
  JsonCacheSerializer,
  REDIS_CACHE_DEFAULTS,
  REDIS_CACHE_METER_NAME,
  RedisCacheInvalidationBackplane,
  RedisDistributedCache,
  type RedisDistributedCacheDeps,
} from "../src/index.js";
import {
  createNoOpTestLogger,
  createRedisTestDouble,
} from "./redis-double-test-harness.js";

type AssertTrue<T extends true> = T;

type _ImplementsIDistributedCache = AssertTrue<
  RedisDistributedCache extends IDistributedCache ? true : false
>;
type _ImplementsICacheInvalidationBackplane = AssertTrue<
  RedisCacheInvalidationBackplane extends ICacheInvalidationBackplane
    ? true
    : false
>;
type _ImplementsICacheSerializer = AssertTrue<
  JsonCacheSerializer extends ICacheSerializer ? true : false
>;
/** Barrel type re-export pin (types erased at runtime; not in Object.keys). */
type _BarrelExportsRedisDistributedCacheDeps = AssertTrue<
  RedisDistributedCacheDeps extends {
    redis: unknown;
    options: unknown;
    serializer: unknown;
    logger: unknown;
  }
    ? true
    : false
>;

const _typeGates: [
  _ImplementsIDistributedCache,
  _ImplementsICacheInvalidationBackplane,
  _ImplementsICacheSerializer,
  _BarrelExportsRedisDistributedCacheDeps,
] = [true, true, true, true];
void _typeGates;

describe("construction + barrel", () => {
  it("barrel_exportsExactPublicSet", () => {
    expect(Object.keys(barrel).sort()).toEqual(
      [
        "INCREMENT_WITH_OPTIONAL_TTL",
        "JsonCacheSerializer",
        "REDIS_CACHE_DEFAULTS",
        "REDIS_CACHE_INSTRUMENTS",
        "REDIS_CACHE_METER_NAME",
        "REDIS_CACHE_METER_VERSION",
        "RELEASE_LOCK_IF_OWNER",
        "RedisCacheInvalidationBackplane",
        "RedisDistributedCache",
        "SET_ADD_WITH_OPTIONAL_TTL",
        "connectRedis",
        "createRedisCacheOptions",
      ].sort(),
    );
    expect(barrel.REDIS_CACHE_METER_NAME).toBe(REDIS_CACHE_METER_NAME);
    expect(barrel.REDIS_CACHE_DEFAULTS).toBe(REDIS_CACHE_DEFAULTS);
  });

  it("barrel_reexportsRedisDistributedCacheDeps_typeAssignable", () => {
    // Compile-time only: import type from barrel must accept a valid deps bag.
    const deps: RedisDistributedCacheDeps = {
      redis: createRedisTestDouble().asRedis(),
      options: createRedisCacheOptions({ defaultExpirationMs: 0 }),
      serializer: new JsonCacheSerializer(),
      logger: createNoOpTestLogger(),
    };
    expect(new RedisDistributedCache(deps)).toBeInstanceOf(
      RedisDistributedCache,
    );
  });

  it("composition_publicSurface_satisfiesIDistributedCache_endToEnd", async () => {
    const redis = createRedisTestDouble();
    const asPort: IDistributedCache = new RedisDistributedCache({
      redis: redis.asRedis(),
      options: createRedisCacheOptions({ defaultExpirationMs: 0 }),
      serializer: new JsonCacheSerializer(),
      logger: createNoOpTestLogger(),
    });

    expect((await asPort.set("k", "v")).success).toBe(true);
    expect((await asPort.get<string>("k")).data).toBe("v");
    expect((await asPort.setNx("k2", 1)).data).toBe(true);
    expect((await asPort.increment("c")).data).toBe(1);
    expect((await asPort.acquireLock("lk", "id", 1000)).data).toBe(true);
    expect((await asPort.releaseLock("lk", "id")).success).toBe(true);
    expect((await asPort.setAdd("s", "m")).data).toBe(true);
    expect((await asPort.setCardinality("s")).data).toBe(1);
    expect((await asPort.setContains("s", "m")).data).toBe(true);
    expect((await asPort.setRemove("s", "m")).data).toBe(true);
    expect((await asPort.remove("k")).success).toBe(true);
  });

  it("ctor_defaultExpirationMsNonFinite_throwsRangeError", () => {
    const redis = createRedisTestDouble();
    expect(
      () =>
        new RedisDistributedCache({
          redis: redis.asRedis(),
          options: createRedisCacheOptions({
            defaultExpirationMs: Number.NaN,
          }),
          serializer: new JsonCacheSerializer(),
          logger: createNoOpTestLogger(),
        }),
    ).toThrow(RangeError);
  });

  it("ctor_commandTimeoutMsNonPositive_throwsRangeError", () => {
    const redis = createRedisTestDouble();
    expect(
      () =>
        new RedisDistributedCache({
          redis: redis.asRedis(),
          options: createRedisCacheOptions({ commandTimeoutMs: 0 }),
          serializer: new JsonCacheSerializer(),
          logger: createNoOpTestLogger(),
        }),
    ).toThrow(RangeError);
  });

  it("ctor_throws_isRangeErrorNotD2Result", () => {
    const redis = createRedisTestDouble();
    try {
      new RedisDistributedCache({
        redis: redis.asRedis(),
        options: createRedisCacheOptions({ connectTimeoutMs: -1 }),
        serializer: new JsonCacheSerializer(),
        logger: createNoOpTestLogger(),
      });
      expect.fail("expected throw");
    } catch (err) {
      expect(err).toBeInstanceOf(RangeError);
      expect(err).not.toHaveProperty("success");
    }
  });

  it("connectRedis_falseyConnectionString_throws", async () => {
    const { connectRedis } = await import("../src/connect-redis.js");
    expect(() =>
      connectRedis(createRedisCacheOptions({ connectionString: "" })),
    ).toThrow("RedisCacheOptions.ConnectionString is required.");
  });

  it("connectRedis_falseyConnectionString_messageDoesNotEchoInput", async () => {
    const { connectRedis } = await import("../src/connect-redis.js");
    try {
      connectRedis(createRedisCacheOptions({ connectionString: "   " }));
      expect.fail("expected throw");
    } catch (err) {
      expect((err as Error).message).toBe(
        "RedisCacheOptions.ConnectionString is required.",
      );
      expect((err as Error).message).not.toContain("redis://");
    }
  });

  it("connectRedis_throws_isErrorNotD2Result", async () => {
    const { connectRedis } = await import("../src/connect-redis.js");
    try {
      connectRedis(createRedisCacheOptions({}));
      expect.fail("expected throw");
    } catch (err) {
      expect(err).toBeInstanceOf(Error);
      expect(err).not.toHaveProperty("success");
    }
  });

  it("backplaneCtor_duplicatesCommandClient_forSubscriber", async () => {
    const redis = createRedisTestDouble();
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    await bp.ready;
    expect(redis.children.length).toBe(1);
    await bp.dispose();
  });
});
