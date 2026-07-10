// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  createRedisCacheOptions,
  RedisCacheInvalidationBackplane,
} from "../src/index.js";
import {
  createNoOpTestLogger,
  createRedisTestDouble,
} from "./redis-double-test-harness.js";

function makeBackplane(redis = createRedisTestDouble()) {
  const bp = new RedisCacheInvalidationBackplane(
    redis.asRedis(),
    createRedisCacheOptions({ invalidationChannel: "d2:cache:invalidations" }),
    createNoOpTestLogger(),
  );

  return { redis, bp };
}

describe("RedisCacheInvalidationBackplane unit", () => {
  it("publishInvalidation_falseyKey_returnsValidationFailed", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    const r = await bp.publishInvalidation("");
    expect(r.inputErrors?.[0]?.field).toBe("key");
    await bp.dispose();
  });

  it("publishInvalidationMany_falseyKeys_returnsValidationFailed", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    const r = await bp.publishInvalidationMany([]);
    expect(r.inputErrors?.[0]?.field).toBe("keys");
    await bp.dispose();
  });

  it("null_handler_throws", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    expect(() =>
      bp.subscribe(null as unknown as (key: string) => void),
    ).toThrow(TypeError);
    await bp.dispose();
  });

  it("backplane_ready_resolvesBeforeFirstHandlerDelivery", async () => {
    // Hold child SUBSCRIBE so channelReady stays false; pre-ready messages
    // must not deliver, then post-ready publish must deliver (trap pin).
    let release!: () => void;
    const hold = new Promise<void>((r) => {
      release = r;
    });
    const redis = createRedisTestDouble();
    redis.childSubscribeHold = hold;
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    let delivered = false;
    bp.subscribe(() => {
      delivered = true;
    });
    await redis.publish("d2:cache:invalidations", "pre-ready");
    expect(delivered).toBe(false);
    expect(bp.ready).toBeInstanceOf(Promise);
    release();
    await bp.ready;
    await bp.publishInvalidation("post-ready");
    expect(delivered).toBe(true);
    await bp.dispose();
  });

  it("publish_afterBackplaneDispose_doesNotThrow", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    await bp.dispose();
    await expect(bp.publishInvalidation("k")).resolves.toBeDefined();
    await expect(bp.publishInvalidationMany(["a", "b"])).resolves.toBeDefined();
  });

  it("subscribe_afterDispose_throws", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    await bp.dispose();
    expect(() => bp.subscribe(() => undefined)).toThrow(/disposed/);
  });

  it("backplaneDispose_quitsOwnedSubscriber_notCommandClient", async () => {
    const { redis, bp } = makeBackplane();
    await bp.ready;
    await bp.dispose();
    expect(redis.quitCalls).toEqual([]);
    expect(redis.children[0]?.quitCalls).toEqual(["subscriber"]);
  });

  it("dispose_isIdempotent", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    await bp.dispose();
    await bp.dispose();
    await bp[Symbol.asyncDispose]();
  });

  it("subscriptionDispose_stopsFurtherDelivery", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    let count = 0;
    const sub = bp.subscribe(() => {
      count++;
    });
    await bp.publishInvalidation("a");
    expect(count).toBe(1);
    await sub[Symbol.asyncDispose]();
    await bp.publishInvalidation("b");
    expect(count).toBe(1);
    await bp.dispose();
  });

  it("backplane_multipleSubscribers_allReceive", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    const keys: string[] = [];
    bp.subscribe((k) => {
      keys.push(`1:${k}`);
    });
    bp.subscribe((k) => {
      keys.push(`2:${k}`);
    });
    await bp.publishInvalidation("k");
    expect(keys.sort()).toEqual(["1:k", "2:k"]);
    await bp.dispose();
  });

  it("backplane_throwingHandler_doesNotBreakOthers", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    let other = false;
    bp.subscribe(() => {
      throw new Error("boom");
    });
    bp.subscribe(() => {
      other = true;
    });
    await bp.publishInvalidation("k");
    expect(other).toBe(true);
    await bp.dispose();
  });

  it("publish_abortedSignal_returnsCanceled", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    const ac = new AbortController();
    ac.abort();
    expect((await bp.publishInvalidation("k", ac.signal)).success).toBe(false);
    expect((await bp.publishInvalidationMany(["k"], ac.signal)).success).toBe(
      false,
    );
    await bp.dispose();
  });

  it("publish_whenRedisDown_returnsServiceUnavailable", async () => {
    const redis = createRedisTestDouble();
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    await bp.ready;
    redis.throwOnNext = new Error("down");
    expect((await bp.publishInvalidation("k")).success).toBe(false);
    redis.throwOnNext = new Error("down");
    expect((await bp.publishInvalidationMany(["k"])).success).toBe(false);
    await bp.dispose();
  });

  it("ctor_nullGuards_throwTypeError", () => {
    const redis = createRedisTestDouble();
    const options = createRedisCacheOptions();
    const logger = createNoOpTestLogger();

    expect(
      () =>
        new RedisCacheInvalidationBackplane(
          null as unknown as ReturnType<typeof redis.asRedis>,
          options,
          logger,
        ),
    ).toThrow(TypeError);
    expect(
      () =>
        new RedisCacheInvalidationBackplane(
          redis.asRedis(),
          null as unknown as typeof options,
          logger,
        ),
    ).toThrow(TypeError);
    expect(
      () =>
        new RedisCacheInvalidationBackplane(
          redis.asRedis(),
          options,
          null as unknown as typeof logger,
        ),
    ).toThrow(TypeError);
  });

  it("ctor_falseyInvalidationChannel_throwsRangeError", () => {
    const redis = createRedisTestDouble();
    expect(
      () =>
        new RedisCacheInvalidationBackplane(
          redis.asRedis(),
          createRedisCacheOptions({ invalidationChannel: "" }),
          createNoOpTestLogger(),
        ),
    ).toThrow(RangeError);
  });

  it("backplane_preReadyMessage_isDropped", async () => {
    let release!: () => void;
    const hold = new Promise<void>((r) => {
      release = r;
    });
    const redis = createRedisTestDouble();
    redis.childSubscribeHold = hold;
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    let delivered = 0;
    bp.subscribe(() => {
      delivered++;
    });
    // Message event registered; channelReady still false until subscribe resolves.
    await redis.publish("d2:cache:invalidations", "pre-ready");
    expect(delivered).toBe(0);
    release();
    await bp.ready;
    await bp.publishInvalidation("post-ready");
    expect(delivered).toBe(1);
    await bp.dispose();
  });

  it("backplane_wrongChannelMessage_isIgnored", async () => {
    const { redis, bp } = makeBackplane();
    await bp.ready;
    let delivered = 0;
    bp.subscribe(() => {
      delivered++;
    });
    await redis.publish("other-channel", "k");
    expect(delivered).toBe(0);
    await bp.dispose();
  });

  it("backplane_handlerThrowAfterAbort_isSwallowedQuietly", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    let release!: () => void;
    const gate = new Promise<void>((r) => {
      release = r;
    });
    const sub = bp.subscribe(async (_key, signal) => {
      await gate;

      if (signal?.aborted === true) {
        throw new Error("after-abort");
      }
    });
    const pub = bp.publishInvalidation("k");
    await sub[Symbol.asyncDispose]();
    release();
    await pub;
    // Flush the isolated handler microtask (no wall-clock sleep - section 1.33 unit ban).
    await Promise.resolve();
    await Promise.resolve();
    await bp.dispose();
  });

  it("subscriptionDispose_isIdempotent", async () => {
    const { bp } = makeBackplane();
    await bp.ready;
    const sub = bp.subscribe(() => undefined);
    await sub[Symbol.asyncDispose]();
    await sub[Symbol.asyncDispose]();
    await bp.dispose();
  });

  it("dispose_swallowsUnsubscribeAndQuitErrors", async () => {
    const redis = createRedisTestDouble();
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const child = redis.children[0]!;
    child.throwOnUnsubscribe = new Error("unsub-fail");
    child.throwOnQuit = new Error("quit-fail");
    await bp.dispose();
  });
});
