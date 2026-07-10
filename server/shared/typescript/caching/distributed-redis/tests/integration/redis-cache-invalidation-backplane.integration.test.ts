// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { afterAll, beforeAll, describe, expect, it } from "vitest";
import type Redis from "ioredis";

import {
  createRedisCacheOptions,
  JsonCacheSerializer,
  RedisCacheInvalidationBackplane,
  RedisDistributedCache,
} from "../../src/index.js";
import { createNoOpTestLogger } from "../redis-double-test-harness.js";
import {
  assertNeverWithinTestBudget,
  createDeferredTestSignal,
  startRedisTestFixture,
  type RedisIntegrationFixture,
} from "./redis-test-harness.js";

describe("RedisCacheInvalidationBackplane integration", () => {
  let fixture: RedisIntegrationFixture;
  let redis: Redis;

  beforeAll(async () => {
    fixture = await startRedisTestFixture();
    redis = fixture.createCommandClient();
  }, 180_000);

  afterAll(async () => {
    await redis.quit();
    await fixture.container.stop();
  });

  it("backplane_publisherReceivesOwnMessage", async () => {
    const channel = fixture.uniqueChannel();
    const bp = new RedisCacheInvalidationBackplane(
      redis,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const signal = createDeferredTestSignal<string>();
    bp.subscribe((key) => {
      signal.resolve(key);
    });
    await bp.publishInvalidation("own-key");
    await expect(signal.promise).resolves.toBe("own-key");
    await bp.dispose();
  });

  it("backplane_multipleSubscribers_allReceive", async () => {
    const channel = fixture.uniqueChannel();
    const bp = new RedisCacheInvalidationBackplane(
      redis,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const a = createDeferredTestSignal<string>();
    const b = createDeferredTestSignal<string>();
    bp.subscribe((k) => a.resolve(k));
    bp.subscribe((k) => b.resolve(k));
    await bp.publishInvalidation("multi");
    await expect(a.promise).resolves.toBe("multi");
    await expect(b.promise).resolves.toBe("multi");
    await bp.dispose();
  });

  it("backplane_throwingHandler_doesNotBreakOthers", async () => {
    const channel = fixture.uniqueChannel();
    const bp = new RedisCacheInvalidationBackplane(
      redis,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const ok = createDeferredTestSignal<string>();
    bp.subscribe(() => {
      throw new Error("handler boom");
    });
    bp.subscribe((k) => ok.resolve(k));
    await bp.publishInvalidation("iso");
    await expect(ok.promise).resolves.toBe("iso");
    await bp.dispose();
  });

  it("subscriptionDispose_stopsFurtherDelivery", async () => {
    const channel = fixture.uniqueChannel();
    const bp = new RedisCacheInvalidationBackplane(
      redis,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    await bp.ready;
    let count = 0;
    const first = createDeferredTestSignal<void>();
    const sub = bp.subscribe(() => {
      count++;
      if (count === 1) {
        first.resolve();
      }
    });
    await bp.publishInvalidation("one");
    await first.promise;
    await sub[Symbol.asyncDispose]();
    await bp.publishInvalidation("two");
    // Absence of second delivery: attempt-budget poll fails if count rises.
    await assertNeverWithinTestBudget(() => count > 1);
    expect(count).toBe(1);
    await bp.dispose();
  });

  it("publish_afterBackplaneDispose_doesNotThrow", async () => {
    const channel = fixture.uniqueChannel();
    const bp = new RedisCacheInvalidationBackplane(
      redis,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    await bp.ready;
    await bp.dispose();
    await expect(bp.publishInvalidation("x")).resolves.toBeDefined();
  });

  it("backplane_andCache_shareCommandClient_coexist", async () => {
    const channel = fixture.uniqueChannel();
    const prefix = fixture.uniquePrefix();
    const bp = new RedisCacheInvalidationBackplane(
      redis,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const cache = new RedisDistributedCache({
      redis,
      options: createRedisCacheOptions({
        keyPrefix: prefix,
        defaultExpirationMs: 0,
      }),
      serializer: new JsonCacheSerializer(),
      logger: createNoOpTestLogger(),
      backplane: bp,
    });
    const got = createDeferredTestSignal<string>();
    bp.subscribe((k) => got.resolve(k));
    await cache.set("user", { n: 1 });
    expect((await cache.get<{ n: number }>("user")).data?.n).toBe(1);
    await cache.setAndBroadcast("user", { n: 2 });
    await expect(got.promise).resolves.toBe(`${prefix}user`);
    await bp.dispose();
  });

  it("backplane_crossInstance_publishA_receivedOnB", async () => {
    const channel = fixture.uniqueChannel();
    const redisB = fixture.createCommandClient();
    const a = new RedisCacheInvalidationBackplane(
      redis,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    const b = new RedisCacheInvalidationBackplane(
      redisB,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    await a.ready;
    await b.ready;
    const got = createDeferredTestSignal<string>();
    b.subscribe((k) => got.resolve(k));
    await a.publishInvalidation("cross");
    await expect(got.promise).resolves.toBe("cross");
    await a.dispose();
    await b.dispose();
    await redisB.quit();
  });

  it("publishMany_deliversEachKey", async () => {
    const channel = fixture.uniqueChannel();
    const bp = new RedisCacheInvalidationBackplane(
      redis,
      createRedisCacheOptions({ invalidationChannel: channel }),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const keys = new Set<string>();
    const done = createDeferredTestSignal<void>();
    bp.subscribe((k) => {
      keys.add(k);
      if (keys.size >= 2) {
        done.resolve();
      }
    });
    await bp.publishInvalidationMany(["k1", "k2"]);
    await done.promise;
    expect(keys.has("k1")).toBe(true);
    expect(keys.has("k2")).toBe(true);
    await bp.dispose();
  });
});
