// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { afterEach, beforeEach, describe, expect, it } from "vitest";

import { DefaultLocalCache, LOCAL_CACHE_METER_NAME } from "../src/index.js";
import {
  createMetricTestHarness,
  type MetricTestHarness,
} from "./metric-test-harness.js";

describe("DefaultLocalCache telemetry", () => {
  let harness: MetricTestHarness;

  beforeEach(() => {
    harness = createMetricTestHarness();
  });

  afterEach(async () => {
    await harness.teardown();
  });

  it("localCacheMeterName_pinsD2SharedCachingLocal", () => {
    expect(LOCAL_CACHE_METER_NAME).toBe("DcsvIo.D2.Caching.Local");
  });

  it("meter_scopeNameAndVersion_pinnedFromCollectedMetrics", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    await cache.get("k");

    const metrics = await harness.collect();
    const scope = metrics.scopeMetrics.find(
      (s) => s.scope.name === LOCAL_CACHE_METER_NAME,
    );

    expect(scope).toBeDefined();
    expect(scope!.scope.version).toBe("1.0.0");
    cache.dispose();
  });

  it.each([
    {
      name: "d2.cache.local.hits",
      unit: "{hit}",
      description: "Local cache hits.",
    },
    {
      name: "d2.cache.local.misses",
      unit: "{miss}",
      description: "Local cache misses.",
    },
    {
      name: "d2.cache.local.sets",
      unit: "{write}",
      description: "Local cache writes.",
    },
    {
      name: "d2.cache.local.removes",
      unit: "{removal}",
      description: "Local cache removals (explicit).",
    },
    {
      name: "d2.cache.local.evictions",
      unit: "{eviction}",
      description: "Entries evicted by capacity / expiration.",
    },
  ] as const)(
    "instruments_pinNamesUnitsDescriptions_theory ($name)",
    async ({ name, unit, description }) => {
      const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

      await cache.set("k", 1);
      await cache.get("k");
      await cache.get("miss");
      await cache.remove("k");

      const metrics = await harness.collect();
      let found = false;

      for (const scope of metrics.scopeMetrics) {
        if (scope.scope.name !== LOCAL_CACHE_METER_NAME) {
          continue;
        }

        for (const metric of scope.metrics) {
          if (metric.descriptor.name === name) {
            expect(metric.descriptor.unit).toBe(unit);
            expect(metric.descriptor.description).toBe(description);
            found = true;
          }
        }
      }

      // Evictions may be zero until capacity/expiry; instrument still exists
      // after createCounter at construction. Force a capacity eviction so the
      // descriptor is always present for every instrument in the theory.
      if (name === "d2.cache.local.evictions") {
        const small = new DefaultLocalCache({
          maxEntries: 1,
          defaultExpirationMs: 0,
        });

        await small.set("a", 1);
        await small.set("b", 2);
        small.dispose();
      }

      const metrics2 = await harness.collect();

      for (const scope of metrics2.scopeMetrics) {
        if (scope.scope.name !== LOCAL_CACHE_METER_NAME) {
          continue;
        }

        for (const metric of scope.metrics) {
          if (metric.descriptor.name === name) {
            expect(metric.descriptor.unit).toBe(unit);
            expect(metric.descriptor.description).toBe(description);
            found = true;
          }
        }
      }

      expect(found).toBe(true);
      cache.dispose();
    },
  );

  it("get_hit_incrementsHitsOnly", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    await cache.get("k");

    expect(await harness.counterValue("d2.cache.local.hits")).toBe(1);
    expect(await harness.counterValue("d2.cache.local.misses")).toBe(0);
    cache.dispose();
  });

  it("get_miss_incrementsMissesOnly", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.get("missing");

    expect(await harness.counterValue("d2.cache.local.misses")).toBe(1);
    expect(await harness.counterValue("d2.cache.local.hits")).toBe(0);
    cache.dispose();
  });

  it("getMany_addsHitsAndMissesAggregates", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("a", 1);
    await cache.getMany(["a", "b", "c"]);

    expect(await harness.counterValue("d2.cache.local.hits")).toBe(1);
    expect(await harness.counterValue("d2.cache.local.misses")).toBe(2);
    cache.dispose();
  });

  it("exists_neverTouchesHitsOrMisses", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    await cache.exists("k");
    await cache.exists("missing");

    expect(await harness.counterValue("d2.cache.local.hits")).toBe(0);
    expect(await harness.counterValue("d2.cache.local.misses")).toBe(0);
    cache.dispose();
  });

  it("getTtl_neverTouchesHitsOrMisses", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    await cache.getTtl("k");
    await cache.getTtl("missing");

    expect(await harness.counterValue("d2.cache.local.hits")).toBe(0);
    expect(await harness.counterValue("d2.cache.local.misses")).toBe(0);
    cache.dispose();
  });

  it("set_incrementsSetsByOne", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);

    expect(await harness.counterValue("d2.cache.local.sets")).toBe(1);
    cache.dispose();
  });

  it("setMany_addsSetsByEntryCount", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.setMany(
      new Map([
        ["a", 1],
        ["b", 2],
        ["c", 3],
      ]),
    );

    expect(await harness.counterValue("d2.cache.local.sets")).toBe(3);
    cache.dispose();
  });

  it("setNx_written_incrementsSets", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.setNx("k", 1);

    expect(await harness.counterValue("d2.cache.local.sets")).toBe(1);
    cache.dispose();
  });

  it("setNx_existing_doesNotIncrementSets", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    const afterSet = await harness.counterValue("d2.cache.local.sets");

    await cache.setNx("k", 2);

    expect(await harness.counterValue("d2.cache.local.sets")).toBe(afterSet);
    cache.dispose();
  });

  it("increment_newKey_incrementsSets", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.increment("k");

    expect(await harness.counterValue("d2.cache.local.sets")).toBe(1);
    cache.dispose();
  });

  it("increment_existing_doesNotIncrementSets", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    const afterSet = await harness.counterValue("d2.cache.local.sets");

    await cache.increment("k");

    expect(await harness.counterValue("d2.cache.local.sets")).toBe(afterSet);
    cache.dispose();
  });

  it("remove_incrementsRemovesEvenWhenAbsent", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.remove("missing");

    expect(await harness.counterValue("d2.cache.local.removes")).toBe(1);
    cache.dispose();
  });

  it("removeMany_addsRemovesByKeyCount", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.removeMany(["a", "b", "c"]);

    expect(await harness.counterValue("d2.cache.local.removes")).toBe(3);
    cache.dispose();
  });

  it("capacityEviction_incrementsEvictions_perEvictedEntry", async () => {
    const cache = new DefaultLocalCache({
      maxEntries: 2,
      defaultExpirationMs: 0,
    });

    await cache.set("a", 1);
    await cache.set("b", 2);
    await cache.set("c", 3);

    expect(await harness.counterValue("d2.cache.local.evictions")).toBe(1);
    cache.dispose();
  });

  it("expiredOnAccess_get_incrementsEvictionsAndMisses", async () => {
    let now = 0;
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 1, 10);
    now = 10;
    await cache.get("k");

    expect(await harness.counterValue("d2.cache.local.evictions")).toBe(1);
    expect(await harness.counterValue("d2.cache.local.misses")).toBe(1);
    cache.dispose();
  });

  it("exists_expired_incrementsEvictionsOnly", async () => {
    let now = 0;
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 1, 10);
    now = 10;
    await cache.exists("k");

    expect(await harness.counterValue("d2.cache.local.evictions")).toBe(1);
    expect(await harness.counterValue("d2.cache.local.hits")).toBe(0);
    expect(await harness.counterValue("d2.cache.local.misses")).toBe(0);
    cache.dispose();
  });

  it("set_overwriteExpiredEntry_doesNotCountEviction", async () => {
    let now = 0;
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 }, () => now);

    await cache.set("k", 1, 10);
    now = 10;
    await cache.set("k", 2);

    expect(await harness.counterValue("d2.cache.local.evictions")).toBe(0);
    cache.dispose();
  });

  it("explicitRemove_doesNotIncrementEvictions", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    await cache.remove("k");

    expect(await harness.counterValue("d2.cache.local.evictions")).toBe(0);
    expect(await harness.counterValue("d2.cache.local.removes")).toBe(1);
    cache.dispose();
  });

  it("validationFailure_movesNoCounters_theory", async () => {
    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.get("");
    await cache.set("", 1);
    await cache.increment("", 1.5);
    await cache.acquireLock("", "", 0);

    expect(await harness.counterValue("d2.cache.local.hits")).toBe(0);
    expect(await harness.counterValue("d2.cache.local.misses")).toBe(0);
    expect(await harness.counterValue("d2.cache.local.sets")).toBe(0);
    expect(await harness.counterValue("d2.cache.local.removes")).toBe(0);
    expect(await harness.counterValue("d2.cache.local.evictions")).toBe(0);
    cache.dispose();
  });

  it("noMeterProvider_allOpsSucceed_noThrow", async () => {
    await harness.teardown();

    const cache = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await cache.set("k", 1);
    await cache.get("k");
    await cache.exists("k");
    await cache.getTtl("k");
    await cache.setMany(new Map([["a", 1]]));
    await cache.remove("a");
    await cache.removeMany(["k"]);
    await cache.setNx("n", 1);
    await cache.increment("c");
    await cache.acquireLock("lk", "id", 100);
    await cache.releaseLock("lk", "id");
    cache.dispose();

    harness = createMetricTestHarness();
  });

  it("twoInstances_aggregateIntoOneMetricStream", async () => {
    const a = new DefaultLocalCache({ defaultExpirationMs: 0 });
    const b = new DefaultLocalCache({ defaultExpirationMs: 0 });

    await a.set("k", 1);
    await b.set("k", 2);

    expect(await harness.counterValue("d2.cache.local.sets")).toBe(2);
    a.dispose();
    b.dispose();
  });
});
