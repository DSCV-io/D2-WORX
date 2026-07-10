// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { afterAll, beforeAll, describe, expect, it } from "vitest";
import type Redis from "ioredis";

import { ErrorCodes, HttpStatusCode } from "@d2/result";

import {
  createRedisCacheOptions,
  JsonCacheSerializer,
  RedisDistributedCache,
} from "../../src/index.js";
import { createNoOpTestLogger } from "../redis-double-test-harness.js";
import {
  startRedisTestFixture,
  type RedisIntegrationFixture,
  waitUntilTestBudget,
} from "./redis-test-harness.js";

describe("RedisDistributedCache integration", () => {
  let fixture: RedisIntegrationFixture;
  let redis: Redis;
  let cache: RedisDistributedCache;
  let prefix: string;

  beforeAll(async () => {
    fixture = await startRedisTestFixture();
    redis = fixture.createCommandClient();
    prefix = fixture.uniquePrefix();
    cache = new RedisDistributedCache({
      redis,
      options: createRedisCacheOptions({
        connectionString: fixture.connectionString,
        keyPrefix: prefix,
        defaultExpirationMs: 60_000,
      }),
      serializer: new JsonCacheSerializer(),
      logger: createNoOpTestLogger(),
    });
  }, 180_000);

  afterAll(async () => {
    await redis.quit();
    await fixture.container.stop();
  });

  it("roundTrip_setGet", async () => {
    const key = `rt:${Date.now()}`;
    await cache.set(key, { displayName: "Ada" });
    const hit = await cache.get<{ displayName: string }>(key);
    expect(hit.success).toBe(true);
    expect(hit.data?.displayName).toBe("Ada");
  });

  it("getMany_partial_returnsSomeFound_status206", async () => {
    const a = `gm:a:${Date.now()}`;
    await cache.set(a, 1);
    const r = await cache.getMany<number>([a, `${a}:missing`]);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data?.get(a)).toBe(1);
  });

  it("setNx_concurrent_onlyOneWins", async () => {
    const key = `nx:${Date.now()}`;
    const results = await Promise.all(
      Array.from({ length: 20 }, (_, i) => cache.setNx(key, i)),
    );
    const wins = results.filter((r) => r.data === true);
    expect(wins.length).toBe(1);
  });

  it("increment_concurrent_buildsCorrectTotal", async () => {
    const key = `inc:${Date.now()}`;
    await Promise.all(Array.from({ length: 25 }, () => cache.increment(key)));
    const r = await cache.increment(key, 0);
    expect(r.data).toBe(25);
  });

  it("setAdd_concurrent_buildsCorrectCardinality", async () => {
    const key = `sa:${Date.now()}`;
    const members = Array.from({ length: 40 }, (_, i) => `m${i % 10}`);
    await Promise.all(members.map((m) => cache.setAdd(key, m)));
    const card = await cache.setCardinality(key);
    expect(card.data).toBe(10);
  });

  it("increment_wrongType_returnsConflict", async () => {
    const key = `wt:${Date.now()}`;
    await cache.set(key, "not-a-number");
    const r = await cache.increment(key);
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.CONFLICT);
    expect(r.statusCode).toBe(HttpStatusCode.Conflict);
  });

  it("releaseLock_wrongOwner_doesNotRelease", async () => {
    const key = `lk:${Date.now()}`;
    expect((await cache.acquireLock(key, "owner1", 5_000)).data).toBe(true);
    expect((await cache.releaseLock(key, "other")).success).toBe(true);
    expect((await cache.acquireLock(key, "owner2", 5_000)).data).toBe(false);
    expect((await cache.releaseLock(key, "owner1")).success).toBe(true);
    expect((await cache.acquireLock(key, "owner2", 5_000)).data).toBe(true);
  });

  it("increment_existingWithTtl_preservesTtl_notReset", async () => {
    const key = `ttl-inc:${Date.now()}`;
    await cache.increment(key, 1, 5_000);
    const before = (await cache.getTtl(key)).data!;
    await cache.increment(key, 1, 60_000);
    const after = (await cache.getTtl(key)).data!;
    expect(after).toBeLessThanOrEqual(before + 50);
    expect(after).toBeLessThan(30_000);
  });

  it("setAdd_existingSet_preservesTtl", async () => {
    const key = `ttl-sa:${Date.now()}`;
    await cache.setAdd(key, "a", 5_000);
    const before = (await cache.getTtl(key)).data!;
    await cache.setAdd(key, "b", 60_000);
    const after = (await cache.getTtl(key)).data!;
    expect(after).toBeLessThanOrEqual(before + 50);
    expect(after).toBeLessThan(30_000);
  });

  it("getTtl_threeOutcomes", async () => {
    const missing = `ttl-m:${Date.now()}`;
    const missingTtl = await cache.getTtl(missing);
    expect(missingTtl.success).toBe(false);
    expect(missingTtl.errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect(missingTtl.statusCode).toBe(HttpStatusCode.NotFound);

    const noTtl = `ttl-n:${Date.now()}`;
    const noTtlCache = new RedisDistributedCache({
      redis,
      options: createRedisCacheOptions({
        keyPrefix: prefix,
        defaultExpirationMs: 0,
      }),
      serializer: new JsonCacheSerializer(),
      logger: createNoOpTestLogger(),
    });
    await noTtlCache.set(noTtl, 1);
    expect((await noTtlCache.getTtl(noTtl)).data).toBeUndefined();

    const withTtl = `ttl-w:${Date.now()}`;
    await cache.set(withTtl, 1, 2_000);
    expect((await cache.getTtl(withTtl)).data).toBeGreaterThan(0);
  });

  it("lock_expires_allowsReacquire", async () => {
    const key = `lk-exp:${Date.now()}`;
    expect((await cache.acquireLock(key, "a", 200)).data).toBe(true);
    await waitUntilTestBudget(
      async () => {
        const again = await cache.acquireLock(key, "b", 1_000);

        return again.data === true;
      },
      40,
      25,
    );
  });

  it("get_whenRedisDown_returnsServiceUnavailable", async () => {
    const dead = fixture.createCommandClient();
    await dead.quit();
    const downCache = new RedisDistributedCache({
      redis: dead,
      options: createRedisCacheOptions({ keyPrefix: prefix }),
      serializer: new JsonCacheSerializer(),
      logger: createNoOpTestLogger(),
    });
    const downGet = await downCache.get("x");
    expect(downGet.success).toBe(false);
    expect(downGet.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
    expect(downGet.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    const downRelease = await downCache.releaseLock("x", "y");
    expect(downRelease.success).toBe(false);
    expect(downRelease.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
    expect(downRelease.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
  });
});
