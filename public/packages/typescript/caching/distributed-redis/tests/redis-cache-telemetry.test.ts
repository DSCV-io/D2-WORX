// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it } from "vitest";

import {
  createRedisCacheOptions,
  JsonCacheSerializer,
  REDIS_CACHE_METER_NAME,
  RedisDistributedCache,
} from "../src/index.js";
import { createRedisCacheCounters } from "../src/redis-cache-telemetry.js";
import { createMetricTestHarness } from "./metric-test-harness.js";
import {
  createNoOpTestLogger,
  createRedisTestDouble,
} from "./redis-double-test-harness.js";

describe("RedisCacheTelemetry", () => {
  let harness: ReturnType<typeof createMetricTestHarness> | undefined;

  afterEach(async () => {
    if (harness !== undefined) {
      await harness.teardown();
      harness = undefined;
    }
  });

  it("meter_name_isDotNetTwin", () => {
    expect(REDIS_CACHE_METER_NAME).toBe("D2.Shared.Caching.Distributed.Redis");
  });

  it("six_instruments_registerWithExpectedTuples", async () => {
    harness = createMetricTestHarness();
    const counters = createRedisCacheCounters();
    counters.hits.add(1);
    counters.misses.add(1);
    counters.sets.add(1);
    counters.removes.add(1);
    counters.broadcasts.add(1);
    counters.errors.add(1);

    const metrics = await harness.collect();
    const scope = metrics.scopeMetrics.find(
      (s) => s.scope.name === REDIS_CACHE_METER_NAME,
    );
    expect(scope).toBeDefined();
    expect(scope!.scope.version).toBe("1.0.0");

    const byName = new Map(
      scope!.metrics.map((m) => [m.descriptor.name, m.descriptor]),
    );
    expect(byName.get("d2.cache.redis.hits")?.unit).toBe("{hit}");
    expect(byName.get("d2.cache.redis.hits")?.description).toBe(
      "Redis cache hits.",
    );
    expect(byName.get("d2.cache.redis.misses")?.unit).toBe("{miss}");
    expect(byName.get("d2.cache.redis.misses")?.description).toBe(
      "Redis cache misses.",
    );
    expect(byName.get("d2.cache.redis.sets")?.unit).toBe("{write}");
    expect(byName.get("d2.cache.redis.sets")?.description).toBe(
      "Redis cache writes.",
    );
    expect(byName.get("d2.cache.redis.removes")?.unit).toBe("{removal}");
    expect(byName.get("d2.cache.redis.removes")?.description).toBe(
      "Redis cache removals.",
    );
    expect(byName.get("d2.cache.redis.broadcasts")?.unit).toBe("{broadcast}");
    expect(byName.get("d2.cache.redis.broadcasts")?.description).toBe(
      "Invalidation messages published to backplane.",
    );
    expect(byName.get("d2.cache.redis.errors")?.unit).toBe("{error}");
    expect(byName.get("d2.cache.redis.errors")?.description).toBe(
      "Redis-side failures.",
    );
  });

  it("cache_ops_incrementCounters", async () => {
    harness = createMetricTestHarness();
    const redis = createRedisTestDouble();
    const cache = new RedisDistributedCache({
      redis: redis.asRedis(),
      options: createRedisCacheOptions({ defaultExpirationMs: 0 }),
      serializer: new JsonCacheSerializer(),
      logger: createNoOpTestLogger(),
    });

    await cache.set("k", 1);
    await cache.get<number>("k");
    await cache.get<number>("missing");
    await cache.remove("k");

    expect(await harness.counterValue("d2.cache.redis.sets")).toBe(1);
    expect(await harness.counterValue("d2.cache.redis.hits")).toBe(1);
    expect(await harness.counterValue("d2.cache.redis.misses")).toBe(1);
    expect(await harness.counterValue("d2.cache.redis.removes")).toBe(1);
  });

  it("counters_noMeterProvider_areNoOpSafe", () => {
    const counters = createRedisCacheCounters();
    expect(() => counters.hits.add(1)).not.toThrow();
  });
});
