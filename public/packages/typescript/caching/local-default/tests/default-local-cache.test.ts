// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { LocalCacheOptions } from "@d2/caching-abstractions";
import { TK } from "@d2/i18n-keys";
import { ErrorCodes, HttpStatusCode, type D2Result } from "@d2/result";
import { describe, expect, it } from "vitest";

import { DefaultLocalCache } from "../src/index.js";

function newCache(
  partial?: Partial<LocalCacheOptions>,
  clock?: () => number,
): DefaultLocalCache {
  return new DefaultLocalCache(partial, clock);
}

function expectValidationFailed(
  result: D2Result<unknown>,
  field: string,
  errorTk: typeof TK.common.errors.NOT_NULL_VIOLATION = TK.common.errors
    .NOT_NULL_VIOLATION,
): void {
  expect(result.success).toBe(false);
  expect(result.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
  expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  expect(result.statusCode).toBe(400);
  expect(result.inputErrors).toHaveLength(1);
  expect(result.inputErrors[0]?.field).toBe(field);
  expect(result.inputErrors[0]?.errors).toEqual([errorTk]);
}

function expectInvalidField(result: D2Result<unknown>, field: string): void {
  expectValidationFailed(result, field, TK.common.errors.VALIDATION_FAILED);
}

describe("DefaultLocalCache unit matrix", () => {
  // -----------------------------------------------------------------------
  // GET
  // -----------------------------------------------------------------------

  it.each(["", "   ", "\t"] as const)(
    "get_falseyKey_returnsValidationFailedFieldKey_theory (%j)",
    async (key) => {
      const cache = newCache();
      const result = await cache.get(key);

      expectValidationFailed(result, "key");
      cache.dispose();
    },
  );

  it("get_hit_returnsOkWithValue", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", "hello");
    const result = await cache.get<string>("k");

    expect(result.success).toBe(true);
    expect(result.data).toBe("hello");
    cache.dispose();
  });

  it("get_miss_returnsNotFound", async () => {
    const cache = newCache();
    const result = await cache.get("missing");

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("get_hit_refreshesLruRecency", async () => {
    const now = 0;
    const cache = newCache(
      { maxEntries: 2, defaultExpirationMs: 0 },
      () => now,
    );

    await cache.set("a", 1);
    await cache.set("b", 2);
    await cache.get("a");
    await cache.set("c", 3);

    const a = await cache.get<number>("a");
    const b = await cache.get<number>("b");
    const c = await cache.get<number>("c");

    expect(a.success).toBe(true);
    expect(b.success).toBe(false);
    expect(c.success).toBe(true);
    cache.dispose();
  });

  it("get_expiredKey_returnsNotFound_afterClockAdvance", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", "v", 100);
    now = 100;
    const result = await cache.get("k");

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("get_exactlyAtExpiry_returnsNotFound", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", "v", 50);
    now = 50;
    const result = await cache.get("k");

    expect(result.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("get_oneMsBeforeExpiry_returnsOkHit", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", "v", 50);
    now = 49;
    const result = await cache.get<string>("k");

    expect(result.success).toBe(true);
    expect(result.data).toBe("v");
    cache.dispose();
  });

  it("get_storedObject_returnsSameReferenceIdentity", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const obj = { a: 1 };

    await cache.set("k", obj);
    const result = await cache.get<{ a: number }>("k");

    expect(result.success).toBe(true);
    expect(result.data).toBe(obj);
    cache.dispose();
  });

  it("keys_areCaseSensitiveOrdinal", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("Key", 1);
    const upper = await cache.get<number>("Key");
    const lower = await cache.get<number>("key");

    expect(upper.success).toBe(true);
    expect(lower.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("keys_unicodeNormalizationForms_nfcNfd_areDistinctKeys", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const nfc = "e\u0301".normalize("NFC");
    const nfd = "e\u0301".normalize("NFD");

    await cache.set(nfc, "nfc");
    const nfcHit = await cache.get<string>(nfc);
    const nfdHit = await cache.get<string>(nfd);

    expect(nfcHit.success).toBe(true);
    expect(nfdHit.errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect(nfc).not.toBe(nfd);
    cache.dispose();
  });

  it("keys_surrogatePairKey_roundTrips", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const key = "emoji-\u{1F600}-key";

    await cache.set(key, "ok");
    const hit = await cache.get<string>(key);
    await cache.remove(key);
    const miss = await cache.get(key);

    expect(hit.success).toBe(true);
    expect(hit.data).toBe("ok");
    expect(miss.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("get_abortedSignal_isIgnored_stillReturnsHit", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const controller = new AbortController();

    controller.abort();
    await cache.set("k", "v");
    const result = await cache.get<string>("k", controller.signal);

    expect(result.success).toBe(true);
    expect(result.data).toBe("v");
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // GETMANY
  // -----------------------------------------------------------------------

  it("getMany_emptyArray_returnsValidationFailedFieldKeys", async () => {
    const cache = newCache();
    const result = await cache.getMany([]);

    expectValidationFailed(result, "keys");
    cache.dispose();
  });

  it.each(["", "  "] as const)(
    "getMany_containsFalseyElement_returnsValidationFailedFieldKeys_theory (%j)",
    async (bad) => {
      const cache = newCache();
      const result = await cache.getMany(["ok", bad]);

      expectValidationFailed(result, "keys");
      cache.dispose();
    },
  );

  it("getMany_duplicateKeys_hitCountedPerOccurrence_mapDeduped", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", "v");
    const result = await cache.getMany<string>(["k", "k", "miss"]);

    expect(result.success).toBe(false);
    expect(result.isPartialSuccess).toBe(true);
    expect(result.statusCode).toBe(HttpStatusCode.PartialContent);
    expect(result.data?.size).toBe(1);
    expect(result.data?.get("k")).toBe("v");
    cache.dispose();
  });

  it("getMany_allHit_returnsOkWithAllKeys", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("a", 1);
    await cache.set("b", 2);
    const result = await cache.getMany<number>(["a", "b"]);

    expect(result.success).toBe(true);
    expect(result.data?.get("a")).toBe(1);
    expect(result.data?.get("b")).toBe(2);
    cache.dispose();
  });

  it("getMany_partialHit_returnsSomeFound_isPartialSuccessTrue_status206_onlyHitKeys", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("a", 1);
    const result = await cache.getMany<number>(["a", "b"]);

    expect(result.success).toBe(false);
    expect(result.isPartialSuccess).toBe(true);
    expect(result.statusCode).toBe(206);
    expect(result.errorCode).toBe(ErrorCodes.SOME_FOUND);
    expect([...result.data!.keys()]).toEqual(["a"]);
    cache.dispose();
  });

  it("getMany_allMiss_returnsNotFound", async () => {
    const cache = newCache();
    const result = await cache.getMany(["x", "y"]);

    expect(result.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("getMany_resultMapKeys_areCallerKeysNotPrefixed", async () => {
    const cache = newCache({
      keyPrefix: "ns:",
      defaultExpirationMs: 0,
    });

    await cache.set("bare", "v");
    const result = await cache.getMany<string>(["bare"]);

    expect(result.success).toBe(true);
    expect(result.data?.has("bare")).toBe(true);
    expect(result.data?.has("ns:bare")).toBe(false);
    cache.dispose();
  });

  it("getMany_expiredEntriesCountAsMisses_andEvict", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("live", 1, 1_000);
    await cache.set("dead", 2, 50);
    now = 50;
    const result = await cache.getMany<number>(["live", "dead"]);

    expect(result.isPartialSuccess).toBe(true);
    expect(result.data?.has("live")).toBe(true);
    expect(result.data?.has("dead")).toBe(false);
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // EXISTS
  // -----------------------------------------------------------------------

  it("exists_falseyKey_returnsValidationFailedFieldKey", async () => {
    const cache = newCache();
    const result = await cache.exists("");

    expectValidationFailed(result, "key");
    cache.dispose();
  });

  it("exists_present_returnsOkTrue", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    const result = await cache.exists("k");

    expect(result.success).toBe(true);
    expect(result.data).toBe(true);
    cache.dispose();
  });

  it("exists_absent_returnsOkFalse_neverNotFound", async () => {
    const cache = newCache();
    const result = await cache.exists("missing");

    expect(result.success).toBe(true);
    expect(result.data).toBe(false);
    expect(result.errorCode).toBeUndefined();
    cache.dispose();
  });

  it("exists_expired_returnsOkFalse_andEvicts", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 1, 10);
    now = 10;
    const result = await cache.exists("k");

    expect(result.success).toBe(true);
    expect(result.data).toBe(false);
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // GETTTL
  // -----------------------------------------------------------------------

  it("getTtl_falseyKey_returnsValidationFailedFieldKey", async () => {
    const cache = newCache();
    const result = await cache.getTtl("  ");

    expectValidationFailed(result, "key");
    cache.dispose();
  });

  it("getTtl_absentKey_returnsNotFound", async () => {
    const cache = newCache();
    const result = await cache.getTtl("missing");

    expect(result.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("getTtl_keyWithTtl_returnsRemainingMs", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 1, 300_000);
    now = 60_000;
    const result = await cache.getTtl("k");

    expect(result.success).toBe(true);
    expect(result.data).toBe(240_000);
    cache.dispose();
  });

  it("getTtl_keyWithoutExpiry_returnsOkUndefined", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    const result = await cache.getTtl("k");

    expect(result.success).toBe(true);
    expect(result.data).toBeUndefined();
    cache.dispose();
  });

  it("getTtl_expired_returnsNotFound_afterAdvance", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 1, 100);
    now = 100;
    const result = await cache.getTtl("k");

    expect(result.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // SET
  // -----------------------------------------------------------------------

  it("set_falseyKey_returnsValidationFailedFieldKey", async () => {
    const cache = newCache();
    const result = await cache.set("", 1);

    expectValidationFailed(result, "key");
    cache.dispose();
  });

  it("set_zeroExpirationMs_returnsValidationFailedFieldExpirationMs", async () => {
    const cache = newCache();
    const result = await cache.set("k", 1, 0);

    expectInvalidField(result, "expirationMs");
    cache.dispose();
  });

  it("set_negativeExpirationMs_returnsValidationFailed", async () => {
    const cache = newCache();
    const result = await cache.set("k", 1, -1);

    expectInvalidField(result, "expirationMs");
    cache.dispose();
  });

  it("set_nanExpirationMs_returnsValidationFailed", async () => {
    const cache = newCache();
    const result = await cache.set("k", 1, Number.NaN);

    expectInvalidField(result, "expirationMs");
    cache.dispose();
  });

  it("set_infinityExpirationMs_returnsValidationFailed", async () => {
    const cache = newCache();
    const result = await cache.set("k", 1, Number.POSITIVE_INFINITY);

    expectInvalidField(result, "expirationMs");
    cache.dispose();
  });

  it("set_newKey_okAndGetRoundTrips", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    const setR = await cache.set("k", 42);
    const getR = await cache.get<number>("k");

    expect(setR.success).toBe(true);
    expect(getR.data).toBe(42);
    cache.dispose();
  });

  it("set_overwrite_replacesValue", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", "a");
    await cache.set("k", "b");
    const result = await cache.get<string>("k");

    expect(result.data).toBe("b");
    cache.dispose();
  });

  it("set_undefinedValue_roundTripsAsOkUndefinedHit_existsTrue", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", undefined);
    const getR = await cache.get("k");
    const existsR = await cache.exists("k");

    expect(getR.success).toBe(true);
    expect(getR.data).toBeUndefined();
    expect(existsR.data).toBe(true);
    cache.dispose();
  });

  it("set_omittedExpirationMs_usesDefaultExpiration", async () => {
    const now = 0;
    const cache = newCache(undefined, () => now);

    await cache.set("k", 1);
    const ttl = await cache.getTtl("k");

    expect(ttl.data).toBe(3_600_000);
    cache.dispose();
  });

  it("set_explicitExpirationMs_overridesDefault", async () => {
    const now = 0;
    const cache = newCache(undefined, () => now);

    await cache.set("k", 1, 5_000);
    const ttl = await cache.getTtl("k");

    expect(ttl.data).toBe(5_000);
    cache.dispose();
  });

  it("set_withDefaultExpirationZero_storesWithoutExpiry", async () => {
    const now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 1);
    const ttl = await cache.getTtl("k");

    expect(ttl.success).toBe(true);
    expect(ttl.data).toBeUndefined();
    cache.dispose();
  });

  it("set_atCapacity_evictsLeastRecentlyUsed_synchronously", async () => {
    const now = 0;
    const cache = newCache(
      { maxEntries: 3, defaultExpirationMs: 0 },
      () => now,
    );

    await cache.set("k1", 1);
    await cache.set("k2", 2);
    await cache.set("k3", 3);
    await cache.get("k1");
    await cache.set("k4", 4);

    expect((await cache.get("k1")).success).toBe(true);
    expect((await cache.get("k2")).errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect((await cache.get("k3")).success).toBe(true);
    expect((await cache.get("k4")).success).toBe(true);
    cache.dispose();
  });

  // Regression: write-path must MRU-refresh. Bare Map.set on overwrite keeps
  // insertion order and wrongly evicts the hot key under capacity pressure.
  it("set_overwrite_refreshesLruRecency", async () => {
    const now = 0;
    const cache = newCache(
      { maxEntries: 3, defaultExpirationMs: 0 },
      () => now,
    );

    await cache.set("k1", 1);
    await cache.set("k2", 2);
    await cache.set("k3", 3);
    await cache.set("k1", 11);
    await cache.set("k4", 4);

    expect((await cache.get("k1")).success).toBe(true);
    expect((await cache.get("k1")).data).toBe(11);
    expect((await cache.get("k2")).errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect((await cache.get("k3")).success).toBe(true);
    expect((await cache.get("k4")).success).toBe(true);
    cache.dispose();
  });

  it("setMany_overwrite_refreshesLruRecency", async () => {
    const now = 0;
    const cache = newCache(
      { maxEntries: 3, defaultExpirationMs: 0 },
      () => now,
    );

    await cache.set("k1", 1);
    await cache.set("k2", 2);
    await cache.set("k3", 3);
    await cache.setMany(new Map([["k1", 11]]));
    await cache.set("k4", 4);

    expect((await cache.get("k1")).success).toBe(true);
    expect((await cache.get("k1")).data).toBe(11);
    expect((await cache.get("k2")).errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect((await cache.get("k3")).success).toBe(true);
    expect((await cache.get("k4")).success).toBe(true);
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // SETMANY
  // -----------------------------------------------------------------------

  it("setMany_emptyMap_returnsValidationFailedFieldEntries", async () => {
    const cache = newCache();
    const result = await cache.setMany(new Map());

    expectValidationFailed(result, "entries");
    cache.dispose();
  });

  it("setMany_containsFalseyKey_returnsValidationFailedFieldEntries", async () => {
    const cache = newCache();
    const result = await cache.setMany(new Map([["", 1]]));

    expectValidationFailed(result, "entries");
    cache.dispose();
  });

  it("setMany_invalidExpirationMs_returnsValidationFailedFieldExpirationMs", async () => {
    const cache = newCache();
    const result = await cache.setMany(new Map([["k", 1]]), 0);

    expectInvalidField(result, "expirationMs");
    cache.dispose();
  });

  it("setMany_validationOrder_expirationMsBeforePerKeyScan", async () => {
    const cache = newCache();
    const result = await cache.setMany(new Map([["", 1]]), 0);

    expectInvalidField(result, "expirationMs");
    cache.dispose();
  });

  it("setMany_writesAllEntries_roundTrip", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.setMany(
      new Map([
        ["a", 1],
        ["b", 2],
      ]),
    );
    expect((await cache.get<number>("a")).data).toBe(1);
    expect((await cache.get<number>("b")).data).toBe(2);
    cache.dispose();
  });

  it("setMany_appliesSharedTtlToEveryEntry", async () => {
    const now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.setMany(
      new Map([
        ["a", 1],
        ["b", 2],
      ]),
      100,
    );
    expect((await cache.getTtl("a")).data).toBe(100);
    expect((await cache.getTtl("b")).data).toBe(100);
    cache.dispose();
  });

  it("setMany_overflowsCapacity_evictsLru", async () => {
    const now = 0;
    const cache = newCache(
      { maxEntries: 2, defaultExpirationMs: 0 },
      () => now,
    );

    await cache.setMany(
      new Map([
        ["a", 1],
        ["b", 2],
        ["c", 3],
      ]),
    );
    const a = await cache.get("a");
    const b = await cache.get("b");
    const c = await cache.get("c");

    expect(a.errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect(b.success).toBe(true);
    expect(c.success).toBe(true);
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // REMOVE / REMOVEMANY
  // -----------------------------------------------------------------------

  it("remove_falseyKey_returnsValidationFailedFieldKey", async () => {
    const cache = newCache();
    const result = await cache.remove("");

    expectValidationFailed(result, "key");
    cache.dispose();
  });

  it("removeMany_emptyArray_returnsValidationFailedFieldKeys", async () => {
    const cache = newCache();
    const result = await cache.removeMany([]);

    expectValidationFailed(result, "keys");
    cache.dispose();
  });

  it("removeMany_containsFalseyElement_returnsValidationFailedFieldKeys", async () => {
    const cache = newCache();
    const result = await cache.removeMany(["ok", ""]);

    expectValidationFailed(result, "keys");
    cache.dispose();
  });

  it("remove_existing_removesAndSubsequentGetNotFound", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    const rem = await cache.remove("k");
    const get = await cache.get("k");

    expect(rem.success).toBe(true);
    expect(get.errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("remove_absentKey_isIdempotentOk", async () => {
    const cache = newCache();
    const result = await cache.remove("missing");

    expect(result.success).toBe(true);
    cache.dispose();
  });

  it("removeMany_removesAll", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("a", 1);
    await cache.set("b", 2);
    await cache.removeMany(["a", "b"]);
    expect((await cache.get("a")).errorCode).toBe(ErrorCodes.NOT_FOUND);
    expect((await cache.get("b")).errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  it("removeMany_mixOfPresentAndAbsent_okAndIdempotent", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("a", 1);
    const result = await cache.removeMany(["a", "missing"]);

    expect(result.success).toBe(true);
    expect((await cache.get("a")).errorCode).toBe(ErrorCodes.NOT_FOUND);
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // SETNX
  // -----------------------------------------------------------------------

  it("setNx_falseyKey_returnsValidationFailedFieldKey", async () => {
    const cache = newCache();
    const result = await cache.setNx("", 1);

    expectValidationFailed(result, "key");
    cache.dispose();
  });

  it.each([0, -1, Number.NaN] as const)(
    "setNx_invalidExpirationMs_returnsValidationFailed_theory (%s)",
    async (ms) => {
      const cache = newCache();
      const result = await cache.setNx("k", 1, ms);

      expectInvalidField(result, "expirationMs");
      cache.dispose();
    },
  );

  it("setNx_newKey_writesAndReturnsOkTrue", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const result = await cache.setNx("k", "v");

    expect(result.success).toBe(true);
    expect(result.data).toBe(true);
    expect((await cache.get<string>("k")).data).toBe("v");
    cache.dispose();
  });

  it("setNx_existingLiveKey_returnsOkFalse_dataIsFalseNotUndefined", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", "orig");
    const result = await cache.setNx("k", "new");

    expect(result.success).toBe(true);
    expect(result.data).toBe(false);
    expect(result.data).not.toBeUndefined();
    expect((await cache.get<string>("k")).data).toBe("orig");
    cache.dispose();
  });

  it("setNx_expiredKey_treatedAsAbsent_writesTrue", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", "old", 10);
    now = 10;
    const result = await cache.setNx("k", "new");

    expect(result.data).toBe(true);
    expect((await cache.get<string>("k")).data).toBe("new");
    cache.dispose();
  });

  it("setNx_omittedExpirationMs_usesDefault", async () => {
    const now = 0;
    const cache = newCache(undefined, () => now);
    await cache.setNx("k", 1);
    const ttl = await cache.getTtl("k");

    expect(ttl.data).toBe(3_600_000);
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // INCREMENT
  // -----------------------------------------------------------------------

  it("increment_falseyKey_returnsValidationFailedFieldKey", async () => {
    const cache = newCache();
    const result = await cache.increment("");

    expectValidationFailed(result, "key");
    cache.dispose();
  });

  it("increment_invalidExpirationMs_returnsValidationFailedFieldExpirationMs", async () => {
    const cache = newCache();
    const result = await cache.increment("k", 1, 0);

    expectInvalidField(result, "expirationMs");
    cache.dispose();
  });

  it.each([Number.NaN, Number.POSITIVE_INFINITY] as const)(
    "increment_nonFiniteAmount_returnsValidationFailedFieldAmount (%s)",
    async (amount) => {
      const cache = newCache();
      const result = await cache.increment("k", amount);

      expectInvalidField(result, "amount");
      cache.dispose();
    },
  );

  it("increment_nonIntegerAmount_returnsValidationFailedFieldAmount", async () => {
    const cache = newCache();
    const result = await cache.increment("k", 0.5);

    expectInvalidField(result, "amount");
    cache.dispose();
  });

  it("increment_negativeAmount_subtracts", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", 10);
    const result = await cache.increment("k", -4);

    expect(result.data).toBe(6);
    cache.dispose();
  });

  it("increment_zeroAmount_returnsCurrentValue", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", 7);
    const result = await cache.increment("k", 0);

    expect(result.data).toBe(7);
    cache.dispose();
  });

  it("increment_existingNonNumericValue_returnsConflict", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", "str");
    const result = await cache.increment("k");

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.CONFLICT);
    expect(result.statusCode).toBe(409);
    cache.dispose();
  });

  it("increment_existingNonIntegerNumber_returnsConflict", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", 0.5);
    const result = await cache.increment("k");

    expect(result.errorCode).toBe(ErrorCodes.CONFLICT);
    cache.dispose();
  });

  it("increment_existingUndefinedValue_returnsConflict", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", undefined);
    const result = await cache.increment("k");

    expect(result.errorCode).toBe(ErrorCodes.CONFLICT);
    cache.dispose();
  });

  it("increment_conflictWireShape_status409_errorCodeConflict_defaultTkMessage", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", {});
    const result = await cache.increment("k");

    expect(result.statusCode).toBe(HttpStatusCode.Conflict);
    expect(result.errorCode).toBe(ErrorCodes.CONFLICT);
    expect(result.messages).toEqual([TK.common.errors.CONFLICT]);
    cache.dispose();
  });

  it("increment_existingKeyWithTtl_preservesTtl_notResetToDefault", async () => {
    let now = 0;
    const cache = newCache(undefined, () => now);

    await cache.set("k", 1, 120_000);
    now = 30_000;
    await cache.increment("k");
    const ttl = await cache.getTtl("k");

    expect(ttl.data).toBeLessThanOrEqual(90_000);
    expect(ttl.data).not.toBe(3_600_000);
    expect(ttl.data).toBe(90_000);
    cache.dispose();
  });

  it("increment_existingKeyWithTtl_ignoresExpirationMsArg", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 1, 200);
    now = 50;
    await cache.increment("k", 1, 999_999);
    const ttl = await cache.getTtl("k");

    expect(ttl.data).toBe(150);
    cache.dispose();
  });

  it("increment_existingKeyWithoutTtl_staysWithoutTtl", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    await cache.increment("k");
    const ttl = await cache.getTtl("k");

    expect(ttl.data).toBeUndefined();
    cache.dispose();
  });

  it("increment_newKey_omittedExpirationMs_usesDefaultExpiration", async () => {
    const now = 0;
    const cache = newCache(undefined, () => now);
    await cache.increment("k");
    const ttl = await cache.getTtl("k");

    expect(ttl.data).toBe(3_600_000);
    cache.dispose();
  });

  it("increment_newKey_explicitExpirationMs_applied", async () => {
    const now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);
    await cache.increment("k", 1, 500);
    const ttl = await cache.getTtl("k");

    expect(ttl.data).toBe(500);
    cache.dispose();
  });

  it("increment_expiredNumericKey_recreatesFromAmount", async () => {
    let now = 0;
    const cache = newCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 99, 10);
    now = 10;
    const result = await cache.increment("k", 5);

    expect(result.data).toBe(5);
    cache.dispose();
  });

  it("increment_newKey_defaultAmount_returnsOk1", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const result = await cache.increment("k");

    expect(result.data).toBe(1);
    cache.dispose();
  });

  it("increment_newKey_customAmount_returnsAmount", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });
    const result = await cache.increment("k", 7);

    expect(result.data).toBe(7);
    cache.dispose();
  });

  it("increment_existingNumeric_addsAndReturnsSum", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", 3);
    const result = await cache.increment("k", 4);

    expect(result.data).toBe(7);
    cache.dispose();
  });

  it("increment_resultOverflowsSafeInteger_returnsValidationFailedFieldAmount", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", Number.MAX_SAFE_INTEGER);
    const result = await cache.increment("k", 1);

    expectInvalidField(result, "amount");
    // Store must be unchanged (refuse before write).
    const get = await cache.get<number>("k");

    expect(get.data).toBe(Number.MAX_SAFE_INTEGER);
    cache.dispose();
  });

  it("increment_resultUnderflowsSafeInteger_returnsValidationFailedFieldAmount", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.set("k", Number.MIN_SAFE_INTEGER);
    const result = await cache.increment("k", -1);

    expectInvalidField(result, "amount");
    const get = await cache.get<number>("k");

    expect(get.data).toBe(Number.MIN_SAFE_INTEGER);
    cache.dispose();
  });

  // -----------------------------------------------------------------------
  // LOCKS
  // -----------------------------------------------------------------------

  it("acquireLock_falseyKey_returnsValidationFailedFieldKey", async () => {
    const cache = newCache();
    const result = await cache.acquireLock("", "id", 100);

    expectValidationFailed(result, "key");
    cache.dispose();
  });

  it("acquireLock_falseyLockId_returnsValidationFailedFieldLockId", async () => {
    const cache = newCache();
    const result = await cache.acquireLock("k", "", 100);

    expectValidationFailed(result, "lockId");
    cache.dispose();
  });

  it.each([0, -1] as const)(
    "acquireLock_zeroOrNegativeExpirationMs_returnsValidationFailedFieldExpirationMs (%s)",
    async (ms) => {
      const cache = newCache();
      const result = await cache.acquireLock("k", "id", ms);

      expectInvalidField(result, "expirationMs");
      cache.dispose();
    },
  );

  it.each([Number.NaN, Number.POSITIVE_INFINITY] as const)(
    "acquireLock_nonFiniteExpirationMs_returnsValidationFailed (%s)",
    async (ms) => {
      const cache = newCache();
      const result = await cache.acquireLock("k", "id", ms);

      expectInvalidField(result, "expirationMs");
      cache.dispose();
    },
  );

  it.each([
    {
      key: "",
      lockId: "",
      expirationMs: 0,
      field: "key",
    },
    {
      key: "k",
      lockId: "",
      expirationMs: 0,
      field: "lockId",
    },
  ] as const)(
    "acquireLock_validationOrder_keyThenLockIdThenExpiration_theory ($field)",
    async ({ key, lockId, expirationMs, field }) => {
      const cache = newCache();
      const result = await cache.acquireLock(key, lockId, expirationMs);

      expectValidationFailed(result, field);
      cache.dispose();
    },
  );

  it("releaseLock_falseyKeyOrLockId_returnsValidationFailed_theory", async () => {
    const cache = newCache();
    const r1 = await cache.releaseLock("", "id");
    const r2 = await cache.releaseLock("k", "  ");

    expectValidationFailed(r1, "key");
    expectValidationFailed(r2, "lockId");
    cache.dispose();
  });

  it("acquireLock_firstCaller_returnsOkTrue", async () => {
    const cache = newCache();
    const result = await cache.acquireLock("k", "id1", 1_000);

    expect(result.success).toBe(true);
    expect(result.data).toBe(true);
    cache.dispose();
  });

  it("acquireLock_whileHeld_returnsOkFalse", async () => {
    const cache = newCache();

    await cache.acquireLock("k", "id1", 1_000);
    const result = await cache.acquireLock("k", "id2", 1_000);

    expect(result.data).toBe(false);
    cache.dispose();
  });

  it("acquireLock_sameLockIdWhileHeld_returnsOkFalse", async () => {
    const cache = newCache();

    await cache.acquireLock("k", "id1", 1_000);
    const result = await cache.acquireLock("k", "id1", 1_000);

    expect(result.data).toBe(false);
    cache.dispose();
  });

  it("releaseLock_correctLockId_releases_thenThirdPartyCanAcquire", async () => {
    const cache = newCache();

    await cache.acquireLock("k", "id1", 1_000);
    await cache.releaseLock("k", "id1");
    const result = await cache.acquireLock("k", "id2", 1_000);

    expect(result.data).toBe(true);
    cache.dispose();
  });

  it("releaseLock_wrongLockId_isNoOp_lockStillHeld", async () => {
    const cache = newCache();

    await cache.acquireLock("k", "id1", 1_000);
    const rel = await cache.releaseLock("k", "wrong");
    const still = await cache.acquireLock("k", "id2", 1_000);

    expect(rel.success).toBe(true);
    expect(still.data).toBe(false);
    cache.dispose();
  });

  it("releaseLock_neverHeldKey_isNoOpOk", async () => {
    const cache = newCache();
    const result = await cache.releaseLock("k", "id");

    expect(result.success).toBe(true);
    cache.dispose();
  });

  it("releaseLock_expiredLock_correctId_removesEntryOk", async () => {
    let now = 0;
    const cache = newCache(undefined, () => now);

    await cache.acquireLock("k", "id1", 10);
    now = 10;
    const rel = await cache.releaseLock("k", "id1");
    const acq = await cache.acquireLock("k", "id2", 10);

    expect(rel.success).toBe(true);
    expect(acq.data).toBe(true);
    cache.dispose();
  });

  it("releaseLock_alwaysReturnsPlainOk", async () => {
    const cache = newCache();
    const result = await cache.releaseLock("never", "held");

    expect(result.success).toBe(true);
    expect(result.data).toBeUndefined();
    cache.dispose();
  });

  it("acquireLock_afterExpiry_allowsReacquisition", async () => {
    let now = 0;
    const cache = newCache(undefined, () => now);

    await cache.acquireLock("k", "id1", 50);
    now = 51;
    const result = await cache.acquireLock("k", "id2", 50);

    expect(result.data).toBe(true);
    cache.dispose();
  });

  it("acquireLock_exactlyAtExpiry_isReacquirable", async () => {
    let now = 0;
    const cache = newCache(undefined, () => now);

    await cache.acquireLock("k", "id1", 50);
    now = 50;
    const result = await cache.acquireLock("k", "id2", 50);

    expect(result.data).toBe(true);
    cache.dispose();
  });

  it("acquireLock_deniedAttempt_doesNotExtendHoldersExpiry", async () => {
    let now = 0;
    const cache = newCache(undefined, () => now);

    await cache.acquireLock("k", "id1", 100);
    now = 40;
    const denied = await cache.acquireLock("k", "id2", 1_000);

    expect(denied.data).toBe(false);
    now = 100;
    const acq = await cache.acquireLock("k", "id2", 50);

    expect(acq.data).toBe(true);
    cache.dispose();
  });

  it("acquireLock_lockStoreIsIndependentOfValueStore", async () => {
    const cache = newCache({ defaultExpirationMs: 0 });

    await cache.acquireLock("k", "id1", 1_000);
    await cache.set("k", "value");
    await cache.remove("k");
    const stillHeld = await cache.acquireLock("k", "id2", 1_000);

    expect(stillHeld.data).toBe(false);
    cache.dispose();
  });

  it("acquireLock_keyPrefix_appliesToLockStore", async () => {
    const a = newCache({ keyPrefix: "a:" });
    const b = newCache({ keyPrefix: "b:" });

    await a.acquireLock("k", "id", 1_000);
    const bAcq = await b.acquireLock("k", "id", 1_000);

    expect(bAcq.data).toBe(true);
    a.dispose();
    b.dispose();
  });

  // -----------------------------------------------------------------------
  // KEYPREFIX
  // -----------------------------------------------------------------------

  it("keyPrefix_appliesTransparently_getWithBareKeyHitsSetWithBareKey", async () => {
    const cache = newCache({
      keyPrefix: "pfx:",
      defaultExpirationMs: 0,
    });

    await cache.set("bare", 1);
    const hit = await cache.get<number>("bare");

    expect(hit.data).toBe(1);
    cache.dispose();
  });

  it("keyPrefix_isolatesNamespacesAcrossInstances", async () => {
    const a = newCache({ keyPrefix: "a:", defaultExpirationMs: 0 });
    const b = newCache({ keyPrefix: "b:", defaultExpirationMs: 0 });

    await a.set("k", "A");
    await b.set("k", "B");
    expect((await a.get<string>("k")).data).toBe("A");
    expect((await b.get<string>("k")).data).toBe("B");
    a.dispose();
    b.dispose();
  });

  it("keyPrefix_whitespaceOnlyPrefix_behavesAsNoPrefix", async () => {
    const cache = newCache({
      keyPrefix: "   ",
      defaultExpirationMs: 0,
    });

    await cache.set("k", 1);
    const hit = await cache.get<number>("k");

    expect(hit.data).toBe(1);
    cache.dispose();
  });
});
