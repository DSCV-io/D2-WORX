// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { InMemoryMessageIdempotencyStore } from "../src/idempotency/message-idempotency-store.js";

describe("InMemoryMessageIdempotencyStore", () => {
  it("reports an unseen id as not-seen", async () => {
    const store = new InMemoryMessageIdempotencyStore();
    const seen = await store.hasSeen("m1");
    expect(seen.failed).toBe(false);
    expect(seen.data).toBe(false);
  });

  it("marks an id then reports it seen", async () => {
    const store = new InMemoryMessageIdempotencyStore();
    const mark = await store.markSeen("m1");
    expect(mark.failed).toBe(false);

    const seen = await store.hasSeen("m1");
    expect(seen.data).toBe(true);
  });

  it("expires a mark after the TTL (deterministic clock)", async () => {
    let nowMs = 1000;
    const store = new InMemoryMessageIdempotencyStore(500, () => nowMs);
    await store.markSeen("m1");

    nowMs = 1499;
    expect((await store.hasSeen("m1")).data).toBe(true);

    nowMs = 1500; // expiry is <= now → expired
    const afterExpiry = await store.hasSeen("m1");
    expect(afterExpiry.data).toBe(false);

    // A second read confirms the expired entry was purged (still not-seen).
    expect((await store.hasSeen("m1")).data).toBe(false);
  });

  it("fails (ServiceUnavailable) on a falsey message id", async () => {
    const store = new InMemoryMessageIdempotencyStore();
    expect((await store.hasSeen("")).failed).toBe(true);
    expect((await store.hasSeen("   ")).failed).toBe(true);
    expect((await store.markSeen("")).failed).toBe(true);
  });

  it("enforces the hard entry cap, evicting the oldest-marked id first", async () => {
    // now() fixed + a huge TTL so nothing EXPIRES — any eviction here is the
    // hard cap doing its job (unbounded-growth pin: the pre-fix lazy-only store
    // retained every id, so the oldest would still report seen).
    const store = new InMemoryMessageIdempotencyStore(1_000_000, () => 0, 2);
    await store.markSeen("a");
    await store.markSeen("b");
    await store.markSeen("c"); // exceeds the cap of 2 → oldest ("a") evicted

    expect((await store.hasSeen("a")).data).toBe(false); // evicted by the cap
    expect((await store.hasSeen("b")).data).toBe(true);
    expect((await store.hasSeen("c")).data).toBe(true);
  });

  it("sweeps elapsed entries on write (front-to-back, stops at the first live one)", async () => {
    let nowMs = 0;
    const store = new InMemoryMessageIdempotencyStore(500, () => nowMs);
    await store.markSeen("a"); // now 0 → expiry 500
    nowMs = 100;
    await store.markSeen("b"); // now 100 → expiry 600

    // now 550: "a" (500) has elapsed, "b" (600) is still live. A fresh write
    // sweeps "a" (delete branch) and stops at "b" (break branch) — so expired
    // ids don't linger to the process restart.
    nowMs = 550;
    await store.markSeen("c");

    expect((await store.hasSeen("b")).data).toBe(true);
    expect((await store.hasSeen("c")).data).toBe(true);
  });
});
