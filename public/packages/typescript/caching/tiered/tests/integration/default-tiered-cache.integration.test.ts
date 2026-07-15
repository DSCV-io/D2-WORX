// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { afterAll, beforeAll, describe, expect, it } from "vitest";

import {
  startTieredTestFixture,
  waitUntilTestBudget,
  type TieredIntegrationFixture,
} from "./tiered-test-harness.js";

describe("DefaultTieredCache integration", () => {
  let fixture: TieredIntegrationFixture;

  beforeAll(async () => {
    fixture = await startTieredTestFixture();
  }, 180_000);

  afterAll(async () => {
    await fixture.container.stop();
  });

  it("get_l1Miss_populatesFromL2", async () => {
    const ctx = await fixture.createTieredContext();

    try {
      expect((await ctx.l2.set("k", "from-L2")).success).toBe(true);
      const get1 = await ctx.tiered.get<string>("k");
      expect(get1.success).toBe(true);
      expect(get1.data).toBe("from-L2");
      const l1Direct = await ctx.l1.get<string>("k");
      expect(l1Direct.success).toBe(true);
      expect(l1Direct.data).toBe("from-L2");
    } finally {
      await ctx.dispose();
    }
  });

  it("set_writesToBoth", async () => {
    const ctx = await fixture.createTieredContext();

    try {
      expect((await ctx.tiered.set("k", "v")).success).toBe(true);
      expect((await ctx.l1.get<string>("k")).data).toBe("v");
      expect((await ctx.l2.get<string>("k")).data).toBe("v");
    } finally {
      await ctx.dispose();
    }
  });

  it("remove_removesFromBoth", async () => {
    const ctx = await fixture.createTieredContext();

    try {
      await ctx.tiered.set("k", "v");
      await ctx.tiered.remove("k");
      expect((await ctx.l1.exists("k")).data).toBe(false);
      expect((await ctx.l2.exists("k")).data).toBe(false);
    } finally {
      await ctx.dispose();
    }
  });

  it("setAndBroadcast_otherInstanceL1Drops", async () => {
    const channel = fixture.uniqueChannel();
    const keyPrefix = fixture.uniquePrefix();
    const instanceA = await fixture.createTieredContext({
      channel,
      keyPrefix,
    });
    const instanceB = await fixture.createTieredContext({
      channel,
      keyPrefix,
    });

    try {
      expect((await instanceB.l1.set("k", "stale-on-B")).success).toBe(true);
      const setResult = await instanceA.tiered.setAndBroadcast(
        "k",
        "fresh-from-A",
      );
      expect(setResult.success).toBe(true);

      await waitUntilTestBudget(async () => {
        const bL1 = await instanceB.l1.get<string>("k");

        return !bL1.success;
      });

      const bL1 = await instanceB.l1.get<string>("k");
      expect(bL1.success).toBe(false);
      const bTiered = await instanceB.tiered.get<string>("k");
      expect(bTiered.data).toBe("fresh-from-A");
    } finally {
      await instanceA.dispose();
      await instanceB.dispose();
    }
  });

  it("setAndBroadcast_ownInstanceL1AlsoDrops", async () => {
    const ctx = await fixture.createTieredContext();

    try {
      expect((await ctx.tiered.setAndBroadcast("k", "v")).success).toBe(true);

      await waitUntilTestBudget(async () => {
        const l1 = await ctx.l1.get<string>("k");

        return !l1.success;
      });

      const l1 = await ctx.l1.get<string>("k");
      expect(l1.success).toBe(false);
      const tiered = await ctx.tiered.get<string>("k");
      expect(tiered.data).toBe("v");
      const l1AfterRead = await ctx.l1.get<string>("k");
      expect(l1AfterRead.data).toBe("v");
    } finally {
      await ctx.dispose();
    }
  });

  it("increment_routesThroughL2_invalidatesL1", async () => {
    const ctx = await fixture.createTieredContext();

    try {
      expect((await ctx.l1.set("counter", 100)).success).toBe(true);
      const inc = await ctx.tiered.increment("counter");
      expect(inc.success).toBe(true);
      expect(inc.data).toBe(1);
      const l1 = await ctx.l1.get<number>("counter");
      expect(l1.success).toBe(false);
    } finally {
      await ctx.dispose();
    }
  });

  it("removeAndBroadcast_otherInstancesDropL1", async () => {
    const channel = fixture.uniqueChannel();
    const keyPrefix = fixture.uniquePrefix();
    const instanceA = await fixture.createTieredContext({
      channel,
      keyPrefix,
    });
    const instanceB = await fixture.createTieredContext({
      channel,
      keyPrefix,
    });

    try {
      await instanceA.tiered.set("k", "shared");
      await instanceB.l1.set("k", "shared-on-B");
      await instanceA.tiered.removeAndBroadcast("k");

      await waitUntilTestBudget(async () => {
        const bL1 = await instanceB.l1.get<string>("k");

        return !bL1.success;
      });

      const bL1 = await instanceB.l1.get<string>("k");
      expect(bL1.success).toBe(false);
      const bTiered = await instanceB.tiered.get<string>("k");
      expect(bTiered.success).toBe(false);
    } finally {
      await instanceA.dispose();
      await instanceB.dispose();
    }
  });
});
