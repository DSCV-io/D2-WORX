// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { ErrorCodes, HttpStatusCode } from "@dcsv-io/d2-result";
import { afterEach, describe, expect, it } from "vitest";

import {
  createRedisCacheOptions,
  JsonCacheSerializer,
  RedisCacheInvalidationBackplane,
  RedisDistributedCache,
} from "../src/index.js";
import { BACKPLANE_NOT_REGISTERED_MESSAGE } from "../src/redis-distributed-cache.js";
import type { ICacheSerializer } from "@dcsv-io/d2-caching-abstractions";
import type { D2Result } from "@dcsv-io/d2-result";
import { fail, ok } from "@dcsv-io/d2-result";
import { createMetricTestHarness } from "./metric-test-harness.js";
import {
  createNoOpTestLogger,
  createRedisTestDouble,
  type RedisTestDouble,
} from "./redis-double-test-harness.js";

function makeCache(
  redis: RedisTestDouble,
  opts?: {
    keyPrefix?: string;
    defaultExpirationMs?: number;
    backplane?: RedisCacheInvalidationBackplane;
    serializer?: ICacheSerializer;
  },
): RedisDistributedCache {
  return new RedisDistributedCache({
    redis: redis.asRedis(),
    options: createRedisCacheOptions({
      keyPrefix: opts?.keyPrefix ?? "",
      defaultExpirationMs: opts?.defaultExpirationMs ?? 0,
    }),
    serializer: opts?.serializer ?? new JsonCacheSerializer(),
    logger: createNoOpTestLogger(),
    backplane: opts?.backplane,
  });
}

class FailingSerializeTestSerializer implements ICacheSerializer {
  readonly contentType = "application/json";
  serialize<T>(_value: T): D2Result<Uint8Array> {
    return fail({
      statusCode: HttpStatusCode.InternalServerError,
      errorCode: ErrorCodes.COULD_NOT_BE_SERIALIZED,
    });
  }
  deserialize<T>(_bytes: Uint8Array): D2Result<T> {
    return ok(null as T);
  }
}

class FailingDeserializeTestSerializer implements ICacheSerializer {
  readonly contentType = "application/json";
  serialize<T>(value: T): D2Result<Uint8Array> {
    return new JsonCacheSerializer().serialize(value);
  }
  deserialize<T>(_bytes: Uint8Array): D2Result<T> {
    return fail({
      statusCode: HttpStatusCode.InternalServerError,
      errorCode: ErrorCodes.COULD_NOT_BE_DESERIALIZED,
    });
  }
}

describe("RedisDistributedCache unit", () => {
  let harness: ReturnType<typeof createMetricTestHarness> | undefined;

  afterEach(async () => {
    if (harness !== undefined) {
      await harness.teardown();
      harness = undefined;
    }
  });

  it.each(["", "   "])(
    "get_falseyKey_returnsValidationFailedFieldKey_theory (%j)",
    async (key) => {
      const cache = makeCache(createRedisTestDouble());
      const r = await cache.get(key);
      expect(r.success).toBe(false);
      expect(r.inputErrors?.[0]?.field).toBe("key");
    },
  );

  it("get_hit_returnsOkWithValue", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.set("k", { a: 1 });
    const r = await cache.get<{ a: number }>("k");
    expect(r.success).toBe(true);
    expect(r.data).toEqual({ a: 1 });
  });

  it("get_miss_returnsNotFound", async () => {
    const r = await makeCache(createRedisTestDouble()).get("missing");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect(r.statusCode).toBe(HttpStatusCode.NotFound);
  });

  it("get_whenRedisDown_returnsServiceUnavailable", async () => {
    const redis = createRedisTestDouble();
    redis.throwOnNext = new Error("ECONNREFUSED");
    const r = await makeCache(redis).get("k");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
    expect(r.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
  });

  it("get_deserializeFail_returnsBubbleFailCouldNotBeDeserialized", async () => {
    const redis = createRedisTestDouble();
    const cache = makeCache(redis, {
      serializer: new FailingDeserializeTestSerializer(),
    });
    await redis.set("k", "x");
    const r = await cache.get("k");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.COULD_NOT_BE_DESERIALIZED);
  });

  it("get_keyPrefix_readsPrefixedKey", async () => {
    const redis = createRedisTestDouble();
    const cache = makeCache(redis, { keyPrefix: "p:" });
    await cache.set("k", 1);
    expect(redis.store.has("p:k")).toBe(true);
    expect((await cache.get<number>("k")).data).toBe(1);
  });

  it("get_hit_incrementsHitsCounter", async () => {
    harness = createMetricTestHarness();
    const cache = makeCache(createRedisTestDouble());
    await cache.set("k", 1);
    await cache.get("k");
    expect(await harness.counterValue("d2.cache.redis.hits")).toBe(1);
  });

  it("get_abortedSignal_returnsCanceled_withoutTouchingRedis", async () => {
    const redis = createRedisTestDouble();
    const ac = new AbortController();
    ac.abort();
    const r = await makeCache(redis).get("k", ac.signal);
    expect(r.success).toBe(false);
    expect(redis.store.size).toBe(0);
  });

  it("getMany_falseyKeys_returnsValidationFailedFieldKeys_theory", async () => {
    const r = await makeCache(createRedisTestDouble()).getMany([]);
    expect(r.inputErrors?.[0]?.field).toBe("keys");
  });

  it("getMany_allHit_returnsOkMap", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.set("a", 1);
    await cache.set("b", 2);
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.success).toBe(true);
    expect(r.data?.get("a")).toBe(1);
    expect(r.data?.get("b")).toBe(2);
  });

  it("getMany_noneHit_returnsNotFound", async () => {
    const r = await makeCache(createRedisTestDouble()).getMany(["x", "y"]);
    expect(r.success).toBe(false);
  });

  it("getMany_partial_returnsSomeFound_status206", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.set("a", 1);
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.success).toBe(false);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.statusCode).toBe(HttpStatusCode.PartialContent);
    expect(r.data?.get("a")).toBe(1);
  });

  it("getMany_mapKeysAreCallerKeys_notPrefixed", async () => {
    const cache = makeCache(createRedisTestDouble(), { keyPrefix: "p:" });
    await cache.set("k", 1);
    const r = await cache.getMany<number>(["k"]);
    expect(r.data?.has("k")).toBe(true);
    expect(r.data?.has("p:k")).toBe(false);
  });

  it("getMany_deserFailSkipsEntry", async () => {
    const redis = createRedisTestDouble();
    const good = new JsonCacheSerializer();
    await redis.set("a", new TextDecoder().decode(good.serialize(1).data!));
    await redis.set("b", "not-json");
    const r = await makeCache(redis).getMany<number>(["a", "b"]);
    expect(r.isPartialSuccess || r.success).toBe(true);
    expect(r.data?.has("a")).toBe(true);
    expect(r.data?.has("b")).toBe(false);
  });

  it("getMany_whenRedisDown_returnsServiceUnavailable", async () => {
    const redis = createRedisTestDouble();
    redis.throwOnNext = new Error("down");
    const r = await makeCache(redis).getMany(["a"]);
    expect(r.success).toBe(false);
  });

  it("getMany_abortedSignal_returnsCanceled", async () => {
    const ac = new AbortController();
    ac.abort();
    const r = await makeCache(createRedisTestDouble()).getMany(
      ["a"],
      ac.signal,
    );
    expect(r.success).toBe(false);
  });

  it("exists_falseyKey_returnsValidationFailed", async () => {
    const r = await makeCache(createRedisTestDouble()).exists("");
    expect(r.inputErrors?.[0]?.field).toBe("key");
  });

  it("exists_present_okTrue", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.set("k", 1);
    expect((await cache.exists("k")).data).toBe(true);
  });

  it("exists_absent_okFalse_neverNotFound", async () => {
    const r = await makeCache(createRedisTestDouble()).exists("x");
    expect(r.success).toBe(true);
    expect(r.data).toBe(false);
  });

  it("exists_whenRedisDown_returnsServiceUnavailable", async () => {
    const redis = createRedisTestDouble();
    redis.throwOnNext = new Error("down");
    expect((await makeCache(redis).exists("k")).success).toBe(false);
  });

  it("getTtl_falseyKey_returnsValidationFailed", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).getTtl("")).inputErrors?.[0]
        ?.field,
    ).toBe("key");
  });

  it("getTtl_absent_notFound", async () => {
    expect((await makeCache(createRedisTestDouble()).getTtl("x")).success).toBe(
      false,
    );
  });

  it("getTtl_noExpiry_okUndefined", async () => {
    const cache = makeCache(createRedisTestDouble(), {
      defaultExpirationMs: 0,
    });
    await cache.set("k", 1);
    const r = await cache.getTtl("k");
    expect(r.success).toBe(true);
    expect(r.data).toBeUndefined();
  });

  it("getTtl_withTtl_okMs", async () => {
    let now = 1_000;
    const redis = createRedisTestDouble(() => now);
    const cache = makeCache(redis, { defaultExpirationMs: 5_000 });
    await cache.set("k", 1, 5_000);
    const r = await cache.getTtl("k");
    expect(r.success).toBe(true);
    expect(r.data).toBe(5_000);
    now = 1_500;
    expect((await cache.getTtl("k")).data).toBe(4_500);
  });

  it("getTtl_whenRedisDown_returnsServiceUnavailable", async () => {
    const redis = createRedisTestDouble();
    redis.throwOnNext = new Error("down");
    expect((await makeCache(redis).getTtl("k")).success).toBe(false);
  });

  it("set_falseyKey_returnsValidationFailed", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).set("", 1)).inputErrors?.[0]
        ?.field,
    ).toBe("key");
  });

  it("set_badExpirationMs_returnsValidationFailed", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).set("k", 1, 0)).inputErrors?.[0]
        ?.field,
    ).toBe("expirationMs");
  });

  it("set_defaultTtl_appliesPxFromDefaults", async () => {
    const now = 0;
    const redis = createRedisTestDouble(() => now);
    const cache = makeCache(redis, { defaultExpirationMs: 2_000 });
    await cache.set("k", 1);
    expect((await cache.getTtl("k")).data).toBe(2_000);
  });

  it("set_explicitTtl_appliesPx", async () => {
    const redis = createRedisTestDouble(() => 0);
    const cache = makeCache(redis, { defaultExpirationMs: 9_000 });
    await cache.set("k", 1, 100);
    expect((await cache.getTtl("k")).data).toBe(100);
  });

  it("set_nonPositiveEffectiveTtl_setsWithoutPx", async () => {
    const redis = createRedisTestDouble(() => 0);
    const cache = makeCache(redis, { defaultExpirationMs: 0 });
    await cache.set("k", 1);
    expect((await cache.getTtl("k")).data).toBeUndefined();
  });

  it("set_serializeFail_returnsBubbleFail", async () => {
    const r = await makeCache(createRedisTestDouble(), {
      serializer: new FailingSerializeTestSerializer(),
    }).set("k", 1);
    expect(r.errorCode).toBe(ErrorCodes.COULD_NOT_BE_SERIALIZED);
  });

  it("set_incrementsSetsCounter", async () => {
    harness = createMetricTestHarness();
    await makeCache(createRedisTestDouble()).set("k", 1);
    expect(await harness.counterValue("d2.cache.redis.sets")).toBe(1);
  });

  it("setMany_falseyEntries_returnsValidationFailed", async () => {
    const r = await makeCache(createRedisTestDouble()).setMany(new Map());
    expect(r.inputErrors?.[0]?.field).toBe("entries");
  });

  it("setMany_writesAllEntries", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.setMany(
      new Map([
        ["a", 1],
        ["b", 2],
      ]),
    );
    expect((await cache.get<number>("a")).data).toBe(1);
    expect((await cache.get<number>("b")).data).toBe(2);
  });

  it("setMany_serializeFail_returnsBubbleFail", async () => {
    const r = await makeCache(createRedisTestDouble(), {
      serializer: new FailingSerializeTestSerializer(),
    }).setMany(new Map([["a", 1]]));
    expect(r.errorCode).toBe(ErrorCodes.COULD_NOT_BE_SERIALIZED);
  });

  it("setMany_pipelineCommandError_returnsServiceUnavailable_doesNotIncrementSets", async () => {
    // ioredis Pipeline.exec resolves with [err, result] tuples (does not
    // throw). Force a per-command error via the double and assert SU + no
    // sets counter bump — fails if production always ok() after exec.
    harness = createMetricTestHarness();
    const redis = createRedisTestDouble();
    redis.throwOnNext = new Error("pipeline set fail");
    const r = await makeCache(redis).setMany(
      new Map([
        ["a", 1],
        ["b", 2],
      ]),
    );
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
    expect(r.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(await harness.counterValue("d2.cache.redis.sets")).toBe(0);
  });

  it("remove_falseyKey_returnsValidationFailed", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).remove("")).inputErrors?.[0]
        ?.field,
    ).toBe("key");
  });

  it("remove_absent_returnsOk_idempotent", async () => {
    expect((await makeCache(createRedisTestDouble()).remove("x")).success).toBe(
      true,
    );
  });

  it("remove_alwaysIncrementsRemovesCounter", async () => {
    harness = createMetricTestHarness();
    await makeCache(createRedisTestDouble()).remove("x");
    expect(await harness.counterValue("d2.cache.redis.removes")).toBe(1);
  });

  it("removeMany_falseyKeys_returnsValidationFailed", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).removeMany([])).inputErrors?.[0]
        ?.field,
    ).toBe("keys");
  });

  it("removeMany_mixed_returnsOk", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.set("a", 1);
    expect((await cache.removeMany(["a", "b"])).success).toBe(true);
  });

  it("setNx_newKey_returnsOkTrue", async () => {
    expect((await makeCache(createRedisTestDouble()).setNx("k", 1)).data).toBe(
      true,
    );
  });

  it("setNx_existing_returnsOkFalse_dataIsFalseNotUndefined", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.setNx("k", 1);
    const r = await cache.setNx("k", 2);
    expect(r.success).toBe(true);
    expect(r.data).toBe(false);
  });

  it("setNx_existing_doesNotIncrementSets", async () => {
    harness = createMetricTestHarness();
    const cache = makeCache(createRedisTestDouble());
    await cache.setNx("k", 1);
    await cache.setNx("k", 2);
    expect(await harness.counterValue("d2.cache.redis.sets")).toBe(1);
  });

  it("setNx_serializeFail_returnsBubbleFail", async () => {
    const r = await makeCache(createRedisTestDouble(), {
      serializer: new FailingSerializeTestSerializer(),
    }).setNx("k", 1);
    expect(r.errorCode).toBe(ErrorCodes.COULD_NOT_BE_SERIALIZED);
  });

  it("increment_newKey_defaultAmount_returnsOk1", async () => {
    expect((await makeCache(createRedisTestDouble()).increment("c")).data).toBe(
      1,
    );
  });

  it("increment_newKey_explicitTtl_applied", async () => {
    const redis = createRedisTestDouble(() => 0);
    const cache = makeCache(redis);
    await cache.increment("c", 1, 500);
    expect((await cache.getTtl("c")).data).toBe(500);
  });

  it("increment_wrongType_returnsConflict", async () => {
    const redis = createRedisTestDouble();
    await redis.set("s", "hello");
    // mark as set kind by using store directly for WRONGTYPE on incr of string non-int
    redis.store.set("setkey", {
      value: "",
      kind: "set",
      members: new Set(["m"]),
    });
    const r = await makeCache(redis).increment("setkey");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.CONFLICT);
    expect(r.statusCode).toBe(HttpStatusCode.Conflict);
  });

  it("increment_nonIntegerString_returnsConflict", async () => {
    const redis = createRedisTestDouble();
    await redis.set("s", "hello");
    const r = await makeCache(redis).increment("s");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.CONFLICT);
    expect(r.statusCode).toBe(HttpStatusCode.Conflict);
  });

  it.each([
    Number.NaN,
    Number.POSITIVE_INFINITY,
    Number.NEGATIVE_INFINITY,
  ] as const)(
    "increment_nonFiniteAmount_returnsValidationFailedFieldAmount (%s)",
    async (amount) => {
      const r = await makeCache(createRedisTestDouble()).increment("c", amount);

      expect(r.success).toBe(false);
      expect(r.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
      expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
      expect(r.inputErrors?.[0]?.field).toBe("amount");
    },
  );

  it("increment_nonIntegerAmount_returnsValidationFailedFieldAmount", async () => {
    const r = await makeCache(createRedisTestDouble()).increment("c", 0.5);

    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.inputErrors?.[0]?.field).toBe("amount");
  });

  it("increment_resultOverflowsSafeInteger_luaReversesAtomically_returnsValidationFailed", async () => {
    const redis = createRedisTestDouble();
    const cache = makeCache(redis);

    expect((await cache.increment("c", Number.MAX_SAFE_INTEGER)).data).toBe(
      Number.MAX_SAFE_INTEGER,
    );

    const r = await cache.increment("c", 1);

    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
    expect(r.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(r.inputErrors?.[0]?.field).toBe("amount");
    // Atomic Lua reverse — no client DECRBY; store unchanged.
    expect(redis.decrbyCalls).toEqual([]);
    expect(await redis.get("c")).toBe(String(Number.MAX_SAFE_INTEGER));
  });

  it("increment_resultUnderflowsSafeInteger_luaReversesAtomically_returnsValidationFailed", async () => {
    const redis = createRedisTestDouble();
    const cache = makeCache(redis);

    expect((await cache.increment("c", Number.MIN_SAFE_INTEGER)).data).toBe(
      Number.MIN_SAFE_INTEGER,
    );

    const r = await cache.increment("c", -1);

    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
    expect(r.inputErrors?.[0]?.field).toBe("amount");
    expect(redis.decrbyCalls).toEqual([]);
    expect(await redis.get("c")).toBe(String(Number.MIN_SAFE_INTEGER));
  });

  it("acquireLock_missingOrInvalidExpirationMs_returnsValidationFailed", async () => {
    const r = await makeCache(createRedisTestDouble()).acquireLock(
      "lk",
      "id",
      0,
    );
    expect(r.inputErrors?.[0]?.field).toBe("expirationMs");
  });

  it("acquireLock_new_returnsOkTrue", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).acquireLock("lk", "id", 1000))
        .data,
    ).toBe(true);
  });

  it("acquireLock_held_returnsOkFalse", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.acquireLock("lk", "id1", 1000);
    expect((await cache.acquireLock("lk", "id2", 1000)).data).toBe(false);
  });

  it("releaseLock_wrongOwner_returnsOk", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.acquireLock("lk", "id1", 1000);
    expect((await cache.releaseLock("lk", "id2")).success).toBe(true);
    expect((await cache.acquireLock("lk", "id2", 1000)).data).toBe(false);
  });

  it("releaseLock_neverHeld_returnsOk", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).releaseLock("lk", "id"))
        .success,
    ).toBe(true);
  });

  it("releaseLock_whenRedisDown_returnsServiceUnavailable", async () => {
    const redis = createRedisTestDouble();
    redis.throwOnNext = new Error("down");
    expect((await makeCache(redis).releaseLock("lk", "id")).success).toBe(
      false,
    );
  });

  it("setAndBroadcast_withoutBackplane_throwsRegistrationError", async () => {
    const cache = makeCache(createRedisTestDouble());
    await expect(cache.setAndBroadcast("k", 1)).rejects.toThrow(
      BACKPLANE_NOT_REGISTERED_MESSAGE,
    );
    await expect(
      cache.setManyAndBroadcast(new Map([["k", 1]])),
    ).rejects.toThrow(BACKPLANE_NOT_REGISTERED_MESSAGE);
    await expect(cache.removeAndBroadcast("k")).rejects.toThrow(
      BACKPLANE_NOT_REGISTERED_MESSAGE,
    );
    await expect(cache.removeManyAndBroadcast(["k"])).rejects.toThrow(
      BACKPLANE_NOT_REGISTERED_MESSAGE,
    );
  });

  it("setAndBroadcast_publishesPrefixedKey", async () => {
    const redis = createRedisTestDouble();
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const cache = makeCache(redis, { keyPrefix: "p:", backplane: bp });
    await cache.setAndBroadcast("k", 1);
    expect(redis.published.some((p) => p.message === "p:k")).toBe(true);
    await bp.dispose();
  });

  it("setAdd_newMember_okTrue", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).setAdd("s", "m")).data,
    ).toBe(true);
  });

  it("setAdd_explicitTtl_appliedOnCreate", async () => {
    const redis = createRedisTestDouble(() => 0);
    const cache = makeCache(redis, { defaultExpirationMs: 0 });
    expect((await cache.setAdd("s", "m", 4_000)).data).toBe(true);
    expect((await cache.getTtl("s")).data).toBe(4_000);
  });

  it("setAdd_duplicate_okFalse", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.setAdd("s", "m");
    expect((await cache.setAdd("s", "m")).data).toBe(false);
  });

  it("setCardinality_absent_okZero", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).setCardinality("s")).data,
    ).toBe(0);
  });

  it("setRemove_idempotentFalse", async () => {
    expect(
      (await makeCache(createRedisTestDouble()).setRemove("s", "m")).data,
    ).toBe(false);
  });

  it("setContains_trueAndFalse", async () => {
    const cache = makeCache(createRedisTestDouble());
    await cache.setAdd("s", "m");
    expect((await cache.setContains("s", "m")).data).toBe(true);
    expect((await cache.setContains("s", "x")).data).toBe(false);
  });

  it("ops_abortedSignal_returnsCanceled", async () => {
    const ac = new AbortController();
    ac.abort();
    const cache = makeCache(createRedisTestDouble());
    const signal = ac.signal;
    const canceledSet = await cache.set("k", 1, undefined, signal);
    expect(canceledSet.success).toBe(false);
    expect(canceledSet.errorCode).toBe(ErrorCodes.CANCELED);
    expect(canceledSet.statusCode).toBe(HttpStatusCode.BadRequest);
    expect((await cache.remove("k", signal)).success).toBe(false);
    expect((await cache.setNx("k", 1, undefined, signal)).success).toBe(false);
    expect((await cache.increment("c", 1, undefined, signal)).success).toBe(
      false,
    );
    expect((await cache.acquireLock("l", "i", 1, signal)).success).toBe(false);
    expect((await cache.releaseLock("l", "i", signal)).success).toBe(false);
    expect((await cache.setAdd("s", "m", undefined, signal)).success).toBe(
      false,
    );
    expect((await cache.setCardinality("s", signal)).success).toBe(false);
    expect((await cache.setRemove("s", "m", signal)).success).toBe(false);
    expect((await cache.setContains("s", "m", signal)).success).toBe(false);
    expect((await cache.exists("k", signal)).success).toBe(false);
    expect((await cache.getTtl("k", signal)).success).toBe(false);
    expect(
      (await cache.setMany(new Map([["a", 1]]), undefined, signal)).success,
    ).toBe(false);
    expect((await cache.removeMany(["a"], signal)).success).toBe(false);
  });

  it("ctor_nullDeps_throwTypeError", () => {
    const redis = createRedisTestDouble();
    const base = {
      redis: redis.asRedis(),
      options: createRedisCacheOptions({ defaultExpirationMs: 0 }),
      serializer: new JsonCacheSerializer(),
      logger: createNoOpTestLogger(),
    };

    expect(
      () =>
        new RedisDistributedCache({
          ...base,
          redis: null as unknown as typeof base.redis,
        }),
    ).toThrow(TypeError);
    expect(
      () =>
        new RedisDistributedCache({
          ...base,
          options: null as unknown as typeof base.options,
        }),
    ).toThrow(TypeError);
    expect(
      () =>
        new RedisDistributedCache({
          ...base,
          serializer: null as unknown as typeof base.serializer,
        }),
    ).toThrow(TypeError);
    expect(
      () =>
        new RedisDistributedCache({
          ...base,
          logger: null as unknown as typeof base.logger,
        }),
    ).toThrow(TypeError);
  });

  it("ctor_connectTimeoutRetriesAndChannelGuards_throwRangeError", () => {
    const redis = createRedisTestDouble();
    const logger = createNoOpTestLogger();
    const serializer = new JsonCacheSerializer();

    expect(
      () =>
        new RedisDistributedCache({
          redis: redis.asRedis(),
          options: createRedisCacheOptions({ connectRetries: -1 }),
          serializer,
          logger,
        }),
    ).toThrow(RangeError);
    expect(
      () =>
        new RedisDistributedCache({
          redis: redis.asRedis(),
          options: createRedisCacheOptions({ invalidationChannel: "   " }),
          serializer,
          logger,
        }),
    ).toThrow(RangeError);
  });

  it("get_bufferPayload_deserializes", async () => {
    const redis = createRedisTestDouble();
    const cache = makeCache(redis);
    const originalGet = redis.get.bind(redis);
    (
      redis as unknown as {
        get: (key: string) => Promise<string | Buffer | null>;
      }
    ).get = async (key: string) => {
      const text = await originalGet(key);

      return text === null ? null : Buffer.from(text);
    };
    await redis.set(
      "k",
      new TextDecoder().decode(
        new JsonCacheSerializer().serialize({ n: 1 }).data!,
      ),
    );
    const r = await cache.get<{ n: number }>("k");
    expect(r.success).toBe(true);
    expect(r.data).toEqual({ n: 1 });
  });

  it("ops_whenRedisDown_returnServiceUnavailable_matrix", async () => {
    const down = async (
      op: (c: RedisDistributedCache) => Promise<{
        success: boolean;
        errorCode?: string;
        statusCode?: number;
      }>,
    ) => {
      const redis = createRedisTestDouble();
      redis.throwOnNext = new Error("down");
      const r = await op(makeCache(redis));
      expect(r.success).toBe(false);
      expect(r.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
      expect(r.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    };

    await down((c) => c.set("k", 1));
    await down((c) => c.setMany(new Map([["a", 1]])));
    await down((c) => c.remove("k"));
    await down((c) => c.removeMany(["k"]));
    await down((c) => c.setNx("k", 1));
    await down((c) => c.increment("c"));
    await down((c) => c.acquireLock("l", "id", 1_000));
    await down((c) => c.setAdd("s", "m"));
    await down((c) => c.setCardinality("s"));
    await down((c) => c.setRemove("s", "m"));
    await down((c) => c.setContains("s", "m"));
  });

  it("increment_nonErrorThrow_returnsServiceUnavailable", async () => {
    const redis = createRedisTestDouble();
    redis.throwOnNext = "string-throw" as unknown as Error;
    const r = await makeCache(redis).increment("c");
    expect(r.success).toBe(false);
  });

  it("falseyFieldValidation_matrix", async () => {
    const cache = makeCache(createRedisTestDouble());
    expect((await cache.setNx("", 1)).inputErrors?.[0]?.field).toBe("key");
    expect((await cache.increment("")).inputErrors?.[0]?.field).toBe("key");
    expect((await cache.acquireLock("", "id", 1)).inputErrors?.[0]?.field).toBe(
      "key",
    );
    expect((await cache.acquireLock("k", "", 1)).inputErrors?.[0]?.field).toBe(
      "lockId",
    );
    expect((await cache.releaseLock("", "id")).inputErrors?.[0]?.field).toBe(
      "key",
    );
    expect((await cache.releaseLock("k", "")).inputErrors?.[0]?.field).toBe(
      "lockId",
    );
    expect((await cache.setAdd("", "m")).inputErrors?.[0]?.field).toBe("key");
    expect((await cache.setAdd("s", "")).inputErrors?.[0]?.field).toBe(
      "member",
    );
    expect((await cache.setCardinality("")).inputErrors?.[0]?.field).toBe(
      "key",
    );
    expect((await cache.setRemove("", "m")).inputErrors?.[0]?.field).toBe(
      "key",
    );
    expect((await cache.setRemove("s", "")).inputErrors?.[0]?.field).toBe(
      "member",
    );
    expect((await cache.setContains("", "m")).inputErrors?.[0]?.field).toBe(
      "key",
    );
    expect((await cache.setContains("s", "")).inputErrors?.[0]?.field).toBe(
      "member",
    );
    expect(
      (await cache.setNx("k", 1, Number.NaN)).inputErrors?.[0]?.field,
    ).toBe("expirationMs");
    expect((await cache.increment("c", 1, -5)).inputErrors?.[0]?.field).toBe(
      "expirationMs",
    );
    expect((await cache.setAdd("s", "m", 0)).inputErrors?.[0]?.field).toBe(
      "expirationMs",
    );
    expect(
      (await cache.setMany(new Map([["a", 1]]), Number.NaN)).inputErrors?.[0]
        ?.field,
    ).toBe("expirationMs");
  });

  it("setNx_withPositiveTtl_usesPxNx", async () => {
    const redis = createRedisTestDouble(() => 0);
    const cache = makeCache(redis, { defaultExpirationMs: 2_000 });
    expect((await cache.setNx("k", 1)).data).toBe(true);
    expect((await cache.getTtl("k")).data).toBe(2_000);
  });

  it("setMany_withPositiveTtl_usesPx", async () => {
    const redis = createRedisTestDouble(() => 0);
    const cache = makeCache(redis, { defaultExpirationMs: 3_000 });
    expect(
      (
        await cache.setMany(
          new Map([
            ["a", 1],
            ["b", 2],
          ]),
        )
      ).success,
    ).toBe(true);
    expect((await cache.getTtl("a")).data).toBe(3_000);
  });

  it("broadcast_setFailure_shortCircuitsBeforePublish", async () => {
    const redis = createRedisTestDouble();
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const cache = makeCache(redis, { backplane: bp });
    const r = await cache.setAndBroadcast("", 1);
    expect(r.success).toBe(false);
    expect(redis.published).toHaveLength(0);
    await bp.dispose();
  });

  it("broadcast_allFourSuccessPaths_publishPrefixed", async () => {
    const redis = createRedisTestDouble();
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const cache = makeCache(redis, { keyPrefix: "p:", backplane: bp });
    expect((await cache.setAndBroadcast("a", 1)).success).toBe(true);
    expect((await cache.setManyAndBroadcast(new Map([["b", 2]]))).success).toBe(
      true,
    );
    expect((await cache.removeAndBroadcast("a")).success).toBe(true);
    expect((await cache.removeManyAndBroadcast(["b"])).success).toBe(true);
    const messages = redis.published.map((p) => p.message).sort();
    expect(messages).toEqual(["p:a", "p:a", "p:b", "p:b"].sort());
    await bp.dispose();
  });

  it("broadcast_setManyAndRemoveFailure_shortCircuits", async () => {
    const redis = createRedisTestDouble();
    const bp = new RedisCacheInvalidationBackplane(
      redis.asRedis(),
      createRedisCacheOptions(),
      createNoOpTestLogger(),
    );
    await bp.ready;
    const cache = makeCache(redis, {
      backplane: bp,
      serializer: new FailingSerializeTestSerializer(),
    });
    expect((await cache.setManyAndBroadcast(new Map([["k", 1]]))).success).toBe(
      false,
    );
    // removeMany with falsey keys short-circuits.
    const emptyCache = makeCache(redis, { backplane: bp });
    expect((await emptyCache.removeManyAndBroadcast([])).success).toBe(false);
    expect((await emptyCache.removeAndBroadcast("")).success).toBe(false);
    await bp.dispose();
  });
});
