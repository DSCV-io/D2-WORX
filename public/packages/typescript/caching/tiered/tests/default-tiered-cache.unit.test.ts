// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { ErrorCodes, fail, HttpStatusCode, ok, someFound } from "@d2/result";
import { describe, expect, it } from "vitest";

import {
  BACKPLANE_NOT_REGISTERED_MESSAGE,
  DefaultTieredCache,
} from "../src/index.js";
import {
  TieredCacheOp,
  TIERED_ERROR_CODE_UNKNOWN,
} from "../src/tiered-cache-log.js";
import {
  createBackplaneTestDouble,
  createCapturingTestLogger,
  createDistributedCacheTestDouble,
  createLocalCacheTestDouble,
  createNoOpTestLogger,
  type BackplaneTestDouble,
  type CapturingTestLogger,
  type DistributedCacheTestDouble,
  type LocalCacheTestDouble,
} from "./tiered-double-test-harness.js";

const L1_INV_MSG = "Tiered cache L1 invalidation handler failed.";
const L1_WRITE_MSG = "Tiered cache L1 write failed after L2 success.";

function build(opts?: {
  l1?: LocalCacheTestDouble;
  l2?: DistributedCacheTestDouble;
  backplane?: BackplaneTestDouble;
  logger?: CapturingTestLogger;
}): {
  cache: DefaultTieredCache;
  l1: LocalCacheTestDouble;
  l2: DistributedCacheTestDouble;
  backplane?: BackplaneTestDouble;
  logger: CapturingTestLogger | ReturnType<typeof createNoOpTestLogger>;
} {
  const l1 = opts?.l1 ?? createLocalCacheTestDouble();
  const l2 = opts?.l2 ?? createDistributedCacheTestDouble();
  const logger = opts?.logger ?? createCapturingTestLogger();
  const backplane = opts?.backplane;
  const cache = new DefaultTieredCache({
    l1,
    l2,
    logger,
    backplane,
  });

  return { cache, l1, l2, backplane, logger };
}

describe("DefaultTieredCache - get", () => {
  it("get_l1Hit_doesNotCallL2", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("k", "from-l1");
    const r = await cache.get<string>("k");
    expect(r.success).toBe(true);
    expect(r.data).toBe("from-l1");
    expect(l2.called("get")).toBe(false);
  });

  it("get_l1Miss_l2Hit_populatesL1_returnsL2", async () => {
    const { cache, l1, l2 } = build();
    l2.store.set("k", "from-l2");
    const r = await cache.get<string>("k");
    expect(r.success).toBe(true);
    expect(r.data).toBe("from-l2");
    expect(l1.store.get("k")).toBe("from-l2");
    // Populate uses default L1 TTL (undefined expirationMs).
    const setCall = l1.callsOf("set")[0];
    expect(setCall?.args[2]).toBeUndefined();
  });

  it("get_l1Miss_l2NotFound_returnsNotFound_noPopulate", async () => {
    const { cache, l1 } = build();
    const r = await cache.get<string>("missing");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect(l1.called("set")).toBe(false);
  });

  it("get_l1Miss_l2Fail_propagates", async () => {
    const { cache, l2 } = build();
    l2.forceFailNext = "get";
    const r = await cache.get<string>("k");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe("L2_DOWN");
  });

  it("get_l1Fail_fallsThroughToL2", async () => {
    const { cache, l1, l2 } = build();
    l1.forceFailNext = "get";
    l2.store.set("k", "via-l2");
    const r = await cache.get<string>("k");
    expect(r.success).toBe(true);
    expect(r.data).toBe("via-l2");
  });

  it("get_forwardsAbortSignalToL1AndL2", async () => {
    const { cache, l1, l2 } = build();
    const ac = new AbortController();
    l2.store.set("k", "v");
    await cache.get("k", ac.signal);
    expect(l1.callsOf("get")[0]?.signal).toBe(ac.signal);
    expect(l2.callsOf("get")[0]?.signal).toBe(ac.signal);
  });
});

describe("DefaultTieredCache - getMany", () => {
  it("getMany_allL1Hits_doesNotCallL2", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("a", 1);
    l1.store.set("b", 2);
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.success).toBe(true);
    expect(r.data?.get("a")).toBe(1);
    expect(l2.called("getMany")).toBe(false);
  });

  it("getMany_partialL1_fetchesMissingFromL2_merges", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("a", 1);
    l2.store.set("b", 2);
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.success).toBe(true);
    expect(r.data?.get("a")).toBe(1);
    expect(r.data?.get("b")).toBe(2);
    expect(l1.store.get("b")).toBe(2);
  });

  it("getMany_l1ServiceUnavailable_treatedAsMissAll_fetchesAllFromL2", async () => {
    const { cache, l1, l2 } = build();
    l1.forceFailNext = "getMany";
    l2.store.set("a", 1);
    l2.store.set("b", 2);
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.success).toBe(true);
    expect(r.data?.size).toBe(2);
    expect(l2.callsOf("getMany")[0]?.args[0]).toEqual(["a", "b"]);
  });

  it("getMany_l1SomeFound_l2NotFound_preservesL1Hits_returnsSomeFound", async () => {
    const { cache, l1 } = build();
    l1.store.set("a", 1);
    // b missing from both
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.success).toBe(false);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data?.get("a")).toBe(1);
    expect(r.data?.has("b")).toBe(false);
  });

  it("getMany_l2NotFound_withL1Hits_returnsSomeFound", async () => {
    const { cache, l1 } = build();
    l1.store.set("hit", "v");
    const r = await cache.getMany<string>(["hit", "miss"]);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data?.get("hit")).toBe("v");
  });

  it("getMany_l2NotFound_noL1Hits_returnsNotFound", async () => {
    const { cache } = build();
    const r = await cache.getMany<string>(["x", "y"]);
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect(r.isPartialSuccess).toBe(false);
  });

  it("getMany_l2ServiceUnavailable_propagates_evenWithL1Hits", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("a", 1);
    l2.forceFailNext = "getMany";
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.success).toBe(false);
    expect(r.isPartialSuccess).toBe(false);
    expect(r.errorCode).toBe("L2_DOWN");
  });

  it("getMany_l2SomeFound_withL1Hits_merges", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("a", 1);
    l2.store.set("b", 2);
    // c missing from L2 too -> L2 someFound for [b,c]
    const r = await cache.getMany<number>(["a", "b", "c"]);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data?.get("a")).toBe(1);
    expect(r.data?.get("b")).toBe(2);
    expect(r.data?.has("c")).toBe(false);
  });

  it("getMany_mergeAllKeys_returnsOk", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("a", 1);
    l2.store.set("b", 2);
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.success).toBe(true);
    expect(r.isPartialSuccess).toBe(false);
    expect(r.data?.size).toBe(2);
  });

  it("getMany_mergePartial_returnsSomeFound", async () => {
    const { cache, l2 } = build();
    l2.store.set("a", 1);
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data?.get("a")).toBe(1);
  });

  it("getMany_forwardsAbortSignalToL1AndL2", async () => {
    const { cache, l1, l2 } = build();
    const ac = new AbortController();
    l2.store.set("a", 1);
    await cache.getMany(["a", "b"], ac.signal);
    expect(l1.callsOf("getMany")[0]?.signal).toBe(ac.signal);
    expect(l2.callsOf("getMany")[0]?.signal).toBe(ac.signal);
  });

  it("getMany_l1SomeFound_allKeysInData_returnsL1_defensiveEmptyMissing", async () => {
    // someFound (not isOkAll) but data already contains every key ->
    // missing is empty -> return L1 as-is (defensive branch).
    const { cache, l1, l2 } = build();
    const data = new Map([
      ["a", 1],
      ["b", 2],
    ]);
    l1.nextResult.getMany = someFound({ data });
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data?.size).toBe(2);
    expect(l2.called("getMany")).toBe(false);
  });

  it("getMany_l1SomeFound_undefinedData_treatedAsEmptyHits", async () => {
    const { cache, l1, l2 } = build();
    l1.nextResult.getMany = someFound({ data: undefined });
    l2.store.set("a", 1);
    const r = await cache.getMany<number>(["a"]);
    expect(r.success).toBe(true);
    expect(r.data?.get("a")).toBe(1);
  });

  it("getMany_l2Ok_undefinedData_mergesEmpty", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("a", 1);
    l2.nextResult.getMany = ok(undefined);
    const r = await cache.getMany<number>(["a", "b"]);
    expect(r.isPartialSuccess).toBe(true);
    expect(r.data?.get("a")).toBe(1);
  });

  it("getMany_l2NotFound_viaErrorCodeOnly", async () => {
    const { cache, l2 } = build();
    l2.nextResult.getMany = fail({
      statusCode: HttpStatusCode.BadRequest,
      errorCode: ErrorCodes.NOT_FOUND,
    });
    const r = await cache.getMany<string>(["x"]);
    expect(r.errorCode).toBe(ErrorCodes.NOT_FOUND);
  });
});

describe("DefaultTieredCache - exists / getTtl", () => {
  it("exists_l1True_doesNotCallL2", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("k", "v");
    const r = await cache.exists("k");
    expect(r.data).toBe(true);
    expect(l2.called("exists")).toBe(false);
  });

  it("exists_l1False_queriesL2", async () => {
    const { cache, l2 } = build();
    l2.store.set("k", "v");
    const r = await cache.exists("k");
    expect(r.data).toBe(true);
    expect(l2.called("exists")).toBe(true);
  });

  it("exists_l1Fail_queriesL2", async () => {
    const { cache, l1, l2 } = build();
    l1.forceFailNext = "exists";
    l2.store.set("k", "v");
    const r = await cache.exists("k");
    expect(r.data).toBe(true);
  });

  it("getTtl_neverCallsL1_delegatesL2", async () => {
    const { cache, l1, l2 } = build();
    l2.store.set("k", "v");
    const r = await cache.getTtl("k");
    expect(r.success).toBe(true);
    expect(r.data).toBe(5000);
    expect(l1.called("getTtl")).toBe(false);
  });

  it("exists_forwardsAbortSignal", async () => {
    const { cache, l2 } = build();
    const ac = new AbortController();
    await cache.exists("k", ac.signal);
    expect(l2.callsOf("exists")[0]?.signal).toBe(ac.signal);
  });

  it("getTtl_forwardsAbortSignalToL2", async () => {
    const { cache, l2 } = build();
    const ac = new AbortController();
    await cache.getTtl("k", ac.signal);
    expect(l2.callsOf("getTtl")[0]?.signal).toBe(ac.signal);
  });
});

describe("DefaultTieredCache - set / setMany / remove / removeMany", () => {
  it("set_writesL2ThenL1", async () => {
    const { cache, l1, l2 } = build();
    expect((await cache.set("k", "v")).success).toBe(true);
    expect(l2.store.get("k")).toBe("v");
    expect(l1.store.get("k")).toBe("v");
    const l2Idx = l2.calls.findIndex((c) => c.method === "set");
    const l1Idx = l1.calls.findIndex((c) => c.method === "set");
    expect(l2Idx).toBeGreaterThanOrEqual(0);
    expect(l1Idx).toBeGreaterThanOrEqual(0);
  });

  it("set_whenL2Fails_doesNotCallL1_returnsL2", async () => {
    const { cache, l1, l2 } = build();
    l2.forceFailNext = "set";
    const r = await cache.set("k", "v");
    expect(r.success).toBe(false);
    expect(l1.called("set")).toBe(false);
  });

  it("set_whenL1FailsAfterL2Ok_returnsOk_logsWarning", async () => {
    const logger = createCapturingTestLogger();
    const { cache, l1, l2 } = build({ logger });
    l1.forceFailNext = "set";
    const r = await cache.set("k", "v");
    expect(r.success).toBe(true);
    expect(l2.store.get("k")).toBe("v");
    expect(logger.warnings).toHaveLength(1);
    expect(logger.warnings[0]?.message).toBe(L1_WRITE_MSG);
    expect(logger.warnings[0]?.bindings).toEqual({
      operation: TieredCacheOp.SET,
      keyOrCount: "k",
      errorCode: "L1_DOWN",
    });
    expect(logger.errors).toHaveLength(0);
  });

  it("set_passesExpirationMsToL1AndL2", async () => {
    const { cache, l1, l2 } = build();
    await cache.set("k", "v", 1234);
    expect(l2.callsOf("set")[0]?.args[2]).toBe(1234);
    expect(l1.callsOf("set")[0]?.args[2]).toBe(1234);
  });

  it("set_forwardsAbortSignalToL1AndL2", async () => {
    const { cache, l1, l2 } = build();
    const ac = new AbortController();
    await cache.set("k", "v", undefined, ac.signal);
    expect(l2.callsOf("set")[0]?.signal).toBe(ac.signal);
    expect(l1.callsOf("set")[0]?.signal).toBe(ac.signal);
  });

  it("setMany_whenL2Fails_doesNotCallL1_returnsL2", async () => {
    const { cache, l1, l2 } = build();
    l2.forceFailNext = "setMany";
    const r = await cache.setMany(new Map([["a", 1]]));
    expect(r.success).toBe(false);
    expect(l1.called("setMany")).toBe(false);
  });

  it("setMany_whenL1FailsAfterL2Ok_returnsOk_logsWarning", async () => {
    const logger = createCapturingTestLogger();
    const { cache, l1 } = build({ logger });
    l1.forceFailNext = "setMany";
    const entries = new Map([
      ["a", 1],
      ["b", 2],
    ]);
    const r = await cache.setMany(entries);
    expect(r.success).toBe(true);
    expect(logger.warnings[0]?.bindings).toEqual({
      operation: TieredCacheOp.SET_MANY,
      keyOrCount: "2 entries",
      errorCode: "L1_DOWN",
    });
  });

  it("setMany_passesExpirationMsToL1AndL2", async () => {
    const { cache, l1, l2 } = build();
    await cache.setMany(new Map([["a", 1]]), 999);
    expect(l2.callsOf("setMany")[0]?.args[1]).toBe(999);
    expect(l1.callsOf("setMany")[0]?.args[1]).toBe(999);
  });

  it("remove_whenL2Fails_doesNotCallL1_returnsL2", async () => {
    const { cache, l1, l2 } = build();
    l2.forceFailNext = "remove";
    const r = await cache.remove("k");
    expect(r.success).toBe(false);
    expect(l1.called("remove")).toBe(false);
  });

  it("remove_whenL1FailsAfterL2Ok_returnsOk_logsWarning", async () => {
    const logger = createCapturingTestLogger();
    const { cache, l1, l2 } = build({ logger });
    l2.store.set("k", "v");
    l1.forceFailNext = "remove";
    const r = await cache.remove("k");
    expect(r.success).toBe(true);
    expect(logger.warnings[0]?.bindings).toEqual({
      operation: TieredCacheOp.REMOVE,
      keyOrCount: "k",
      errorCode: "L1_DOWN",
    });
  });

  it("removeMany_whenL2Fails_doesNotCallL1_returnsL2", async () => {
    const { cache, l1, l2 } = build();
    l2.forceFailNext = "removeMany";
    const r = await cache.removeMany(["a", "b"]);
    expect(r.success).toBe(false);
    expect(l1.called("removeMany")).toBe(false);
  });

  it("removeMany_whenL1FailsAfterL2Ok_returnsOk_logsWarning", async () => {
    const logger = createCapturingTestLogger();
    const { cache, l1 } = build({ logger });
    l1.forceFailNext = "removeMany";
    const r = await cache.removeMany(["a", "b", "c"]);
    expect(r.success).toBe(true);
    expect(logger.warnings[0]?.bindings).toEqual({
      operation: TieredCacheOp.REMOVE_MANY,
      keyOrCount: "3 keys",
      errorCode: "L1_DOWN",
    });
  });
});

describe("DefaultTieredCache - atomics", () => {
  it("setNx_tookWrite_populatesL1", async () => {
    const { cache, l1, l2 } = build();
    const r = await cache.setNx("k", "v", 100);
    expect(r.data).toBe(true);
    expect(l2.store.get("k")).toBe("v");
    expect(l1.store.get("k")).toBe("v");
  });

  it("setNx_alreadyExists_dropsL1", async () => {
    const { cache, l1, l2 } = build();
    l2.store.set("k", "existing");
    l1.store.set("k", "stale");
    const r = await cache.setNx("k", "new");
    expect(r.data).toBe(false);
    expect(l1.store.has("k")).toBe(false);
    expect(l1.called("remove")).toBe(true);
  });

  it("setNx_l2Fail_doesNotTouchL1", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("k", "keep");
    l2.forceFailNext = "setNx";
    const r = await cache.setNx("k", "v");
    expect(r.success).toBe(false);
    expect(l1.store.get("k")).toBe("keep");
  });

  it("setNx_passesExpirationMsToL2AndL1Populate", async () => {
    const { cache, l1, l2 } = build();
    await cache.setNx("k", "v", 777);
    expect(l2.callsOf("setNx")[0]?.args[2]).toBe(777);
    expect(l1.callsOf("set")[0]?.args[2]).toBe(777);
  });

  it("increment_success_alwaysRemovesL1", async () => {
    const { cache, l1 } = build();
    l1.store.set("c", 100);
    const r = await cache.increment("c", 5);
    expect(r.data).toBe(5);
    expect(l1.store.has("c")).toBe(false);
  });

  it("increment_l2Fail_doesNotTouchL1", async () => {
    const { cache, l1, l2 } = build();
    l1.store.set("c", 100);
    l2.forceFailNext = "increment";
    const r = await cache.increment("c");
    expect(r.success).toBe(false);
    expect(l1.store.get("c")).toBe(100);
  });

  it("increment_passesAmountAndExpirationMsToL2", async () => {
    const { cache, l2 } = build();
    await cache.increment("c", 3, 50);
    expect(l2.callsOf("increment")[0]?.args).toEqual(["c", 3, 50]);
  });

  it("acquireLock_delegatesToL2_only", async () => {
    const { cache, l1, l2 } = build();
    const r = await cache.acquireLock("lk", "id", 1000);
    expect(r.data).toBe(true);
    expect(l2.called("acquireLock")).toBe(true);
    expect(l1.called("acquireLock")).toBe(false);
  });

  it("releaseLock_delegatesToL2_only", async () => {
    const { cache, l1, l2 } = build();
    await cache.acquireLock("lk", "id", 1000);
    const r = await cache.releaseLock("lk", "id");
    expect(r.success).toBe(true);
    expect(l2.called("releaseLock")).toBe(true);
    expect(l1.called("releaseLock")).toBe(false);
  });

  it("releaseLock_whenL2ServiceUnavailable_propagates", async () => {
    const { cache, l2 } = build();
    l2.forceFailNext = "releaseLock";
    const r = await cache.releaseLock("lk", "id");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe("L2_DOWN");
  });

  it("setNx_forwardsAbortSignalToL2AndL1SideEffect", async () => {
    // Took-write → L1.set with same signal; already-exists → L1.remove.
    const { cache, l1, l2 } = build();
    const tookWrite = new AbortController();
    await cache.setNx("k1", "v", undefined, tookWrite.signal);
    expect(l2.callsOf("setNx")[0]?.signal).toBe(tookWrite.signal);
    expect(l1.callsOf("set")[0]?.signal).toBe(tookWrite.signal);

    const alreadyExists = new AbortController();
    l2.store.set("k2", "existing");
    l1.store.set("k2", "stale");
    await cache.setNx("k2", "new", undefined, alreadyExists.signal);
    expect(l2.callsOf("setNx")[1]?.signal).toBe(alreadyExists.signal);
    expect(l1.callsOf("remove")[0]?.signal).toBe(alreadyExists.signal);
  });

  it("increment_forwardsAbortSignalToL2AndL1Remove", async () => {
    const { cache, l1, l2 } = build();
    const ac = new AbortController();
    l1.store.set("c", 1);
    await cache.increment("c", 1, undefined, ac.signal);
    expect(l2.callsOf("increment")[0]?.signal).toBe(ac.signal);
    expect(l1.callsOf("remove")[0]?.signal).toBe(ac.signal);
  });

  it("acquireLock_forwardsAbortSignalToL2", async () => {
    const { cache, l2 } = build();
    const ac = new AbortController();
    await cache.acquireLock("lk", "id", 1000, ac.signal);
    expect(l2.callsOf("acquireLock")[0]?.signal).toBe(ac.signal);
  });

  it("releaseLock_forwardsAbortSignalToL2", async () => {
    const { cache, l2 } = build();
    const ac = new AbortController();
    await cache.releaseLock("lk", "id", ac.signal);
    expect(l2.callsOf("releaseLock")[0]?.signal).toBe(ac.signal);
  });
});

describe("DefaultTieredCache - broadcast", () => {
  it("setAndBroadcast_withoutBackplane_throwsRegistrationError", async () => {
    const { cache } = build();
    await expect(cache.setAndBroadcast("k", "v")).rejects.toThrow(
      BACKPLANE_NOT_REGISTERED_MESSAGE,
    );
  });

  it("setManyAndBroadcast_withoutBackplane_throwsRegistrationError", async () => {
    const { cache } = build();
    await expect(
      cache.setManyAndBroadcast(new Map([["a", 1]])),
    ).rejects.toThrow(BACKPLANE_NOT_REGISTERED_MESSAGE);
  });

  it("removeAndBroadcast_withoutBackplane_throwsRegistrationError", async () => {
    const { cache } = build();
    await expect(cache.removeAndBroadcast("k")).rejects.toThrow(
      BACKPLANE_NOT_REGISTERED_MESSAGE,
    );
  });

  it("removeManyAndBroadcast_withoutBackplane_throwsRegistrationError", async () => {
    const { cache } = build();
    await expect(cache.removeManyAndBroadcast(["a"])).rejects.toThrow(
      BACKPLANE_NOT_REGISTERED_MESSAGE,
    );
  });

  it("setAndBroadcast_publishesCallerKey_afterSuccessfulSet", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache, l1, l2 } = build({ backplane });
    const r = await cache.setAndBroadcast("caller-key", "v");
    expect(r.success).toBe(true);
    expect(l2.store.get("caller-key")).toBe("v");
    expect(backplane.published).toEqual(["caller-key"]);
    // Everyone-acts: own L1 dropped by subscription after set populated it.
    expect(l1.store.has("caller-key")).toBe(false);
    // Must NOT call L2 *AndBroadcast*.
    expect(l2.called("setAndBroadcast")).toBe(false);
  });

  it("setAndBroadcast_whenSetFails_doesNotPublish", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache, l2 } = build({ backplane });
    l2.forceFailNext = "set";
    const r = await cache.setAndBroadcast("k", "v");
    expect(r.success).toBe(false);
    expect(backplane.published).toHaveLength(0);
  });

  it("setManyAndBroadcast_whenSetManyFails_doesNotPublish", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache, l2 } = build({ backplane });
    l2.forceFailNext = "setMany";
    const r = await cache.setManyAndBroadcast(new Map([["a", 1]]));
    expect(r.success).toBe(false);
    expect(backplane.published).toHaveLength(0);
  });

  it("removeAndBroadcast_whenRemoveFails_doesNotPublish", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache, l2 } = build({ backplane });
    l2.forceFailNext = "remove";
    const r = await cache.removeAndBroadcast("k");
    expect(r.success).toBe(false);
    expect(backplane.published).toHaveLength(0);
  });

  it("removeManyAndBroadcast_whenRemoveManyFails_doesNotPublish", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache, l2 } = build({ backplane });
    l2.forceFailNext = "removeMany";
    const r = await cache.removeManyAndBroadcast(["a", "b"]);
    expect(r.success).toBe(false);
    expect(backplane.published).toHaveLength(0);
  });

  it("setAndBroadcast_whenPublishFails_returnsBackplaneResult", async () => {
    const backplane = createBackplaneTestDouble();
    backplane.forceFailPublish = true;
    const { cache } = build({ backplane });
    const r = await cache.setAndBroadcast("k", "v");
    expect(r.success).toBe(false);
    expect(r.errorCode).toBe("BACKPLANE_DOWN");
  });

  it("setManyAndBroadcast_publishesEntryKeys", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache } = build({ backplane });
    const entries = new Map([
      ["x", 1],
      ["y", 2],
    ]);
    expect((await cache.setManyAndBroadcast(entries)).success).toBe(true);
    expect(backplane.publishedMany[0]?.sort()).toEqual(["x", "y"]);
  });

  it("removeAndBroadcast_publishesAfterRemove", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache, l2 } = build({ backplane });
    l2.store.set("k", "v");
    expect((await cache.removeAndBroadcast("k")).success).toBe(true);
    expect(backplane.published).toEqual(["k"]);
    expect(l2.store.has("k")).toBe(false);
  });

  it("removeManyAndBroadcast_publishesKeys", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache } = build({ backplane });
    expect((await cache.removeManyAndBroadcast(["a", "b"])).success).toBe(true);
    expect(backplane.publishedMany[0]).toEqual(["a", "b"]);
  });

  it("setAndBroadcast_forwardsAbortSignalToPublish", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache } = build({ backplane });
    const ac = new AbortController();
    // Production passes the caller's signal into publishInvalidation — pin
    // that arg, not the subscription signal handlers receive on deliver.
    expect(
      (await cache.setAndBroadcast("k", "v", undefined, ac.signal)).success,
    ).toBe(true);
    expect(backplane.published[0]).toBe("k");
    expect(backplane.publishedSignals[0]).toBe(ac.signal);
  });

  it("subscription_handlerReceivesSubscriptionSignal_notPublishSignal", async () => {
    const backplane = createBackplaneTestDouble();
    build({ backplane });
    const ac = new AbortController();
    let seen: AbortSignal | undefined;
    backplane.subscribe((_k, s) => {
      seen = s;
    });
    await backplane.publishInvalidation("k", ac.signal);
    expect(seen).toBeDefined();
    expect(seen).not.toBe(ac.signal);
    expect(seen!.aborted).toBe(false);
  });
});

describe("DefaultTieredCache - subscription / dispose", () => {
  it("subscription_onInvalidation_removesL1", async () => {
    const backplane = createBackplaneTestDouble();
    const { l1 } = build({ backplane });
    l1.store.set("k", "v");
    await backplane.publishInvalidation("k");
    expect(l1.store.has("k")).toBe(false);
  });

  it("subscription_l1RemoveFail_logsWarning_continues", async () => {
    const logger = createCapturingTestLogger();
    const backplane = createBackplaneTestDouble();
    const l1 = createLocalCacheTestDouble();
    l1.forceFailMethods.add("remove");
    build({ l1, backplane, logger });
    await backplane.publishInvalidation("bad-key");
    expect(logger.warnings).toHaveLength(1);
    expect(logger.warnings[0]?.message).toBe(L1_INV_MSG);
    expect(logger.warnings[0]?.bindings).toEqual({
      key: "bad-key",
      errorCode: "L1_DOWN",
    });
  });

  it("dispose_unsubscribesBackplane_stopsL1Drops", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache, l1 } = build({ backplane });
    l1.store.set("k", "v");
    await cache.dispose();
    await backplane.publishInvalidation("k");
    expect(l1.store.get("k")).toBe("v");
  });

  it("dispose_idempotent", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache } = build({ backplane });
    await cache.dispose();
    await cache.dispose();
    await cache[Symbol.asyncDispose]();
  });

  it("dispose_doesNotDisposeL1L2OrBackplane", async () => {
    const backplane = createBackplaneTestDouble();
    const { cache, l1, l2 } = build({ backplane });
    await cache.dispose();
    expect(l1.disposed).toBe(false);
    expect(l2.disposed).toBe(false);
    expect(backplane.disposed).toBe(false);
  });

  it("ops_afterDispose_stillDelegateToL1L2", async () => {
    const { cache, l2 } = build();
    await cache.dispose();
    expect((await cache.set("k", "v")).success).toBe(true);
    expect(l2.store.get("k")).toBe("v");
    expect((await cache.get<string>("k")).data).toBe("v");
  });
});

describe("DefaultTieredCache - pinned constants", () => {
  it("backplane_message_exactPin", () => {
    expect(BACKPLANE_NOT_REGISTERED_MESSAGE).toBe(
      "ICacheInvalidationBackplane is not registered. Use set / remove " +
        "(no broadcast), or pass a backplane to DefaultTieredCache.",
    );
  });

  it("operation_closedSet_loggedOnAllFourWritePaths", async () => {
    const logger = createCapturingTestLogger();
    const l1 = createLocalCacheTestDouble();
    l1.forceFailMethods.add("set");
    l1.forceFailMethods.add("setMany");
    l1.forceFailMethods.add("remove");
    l1.forceFailMethods.add("removeMany");
    const { cache } = build({ l1, logger });
    await cache.set("k", "v");
    await cache.setMany(new Map([["a", 1]]));
    await cache.remove("k");
    await cache.removeMany(["a"]);
    const ops = logger.warnings.map((w) => w.bindings?.operation);
    expect(ops.sort()).toEqual(
      [
        TieredCacheOp.REMOVE,
        TieredCacheOp.REMOVE_MANY,
        TieredCacheOp.SET,
        TieredCacheOp.SET_MANY,
      ].sort(),
    );
  });

  it("l1Fail_withoutErrorCode_logsUnknown_allFourWritePaths", async () => {
    const logger = createCapturingTestLogger();
    const l1 = createLocalCacheTestDouble();
    const noCode = fail({
      statusCode: HttpStatusCode.ServiceUnavailable,
    });
    l1.nextResult.set = noCode;
    l1.nextResult.setMany = noCode;
    l1.nextResult.remove = noCode;
    l1.nextResult.removeMany = noCode;
    const { cache } = build({ l1, logger });
    expect((await cache.set("k", "v")).success).toBe(true);
    expect((await cache.setMany(new Map([["a", 1]]))).success).toBe(true);
    expect((await cache.remove("k")).success).toBe(true);
    expect((await cache.removeMany(["a"])).success).toBe(true);
    expect(logger.warnings).toHaveLength(4);

    for (const w of logger.warnings) {
      expect(w.bindings?.errorCode).toBe(TIERED_ERROR_CODE_UNKNOWN);
    }
  });

  it("subscription_l1RemoveFail_withoutErrorCode_logsUnknown", async () => {
    const logger = createCapturingTestLogger();
    const backplane = createBackplaneTestDouble();
    const l1 = createLocalCacheTestDouble();
    l1.nextResult.remove = fail({
      statusCode: HttpStatusCode.ServiceUnavailable,
    });
    build({ l1, backplane, logger });
    await backplane.publishInvalidation("k");
    expect(logger.warnings[0]?.bindings?.errorCode).toBe(
      TIERED_ERROR_CODE_UNKNOWN,
    );
  });
});
