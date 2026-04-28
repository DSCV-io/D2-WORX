import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { Increment } from "@d2/cache-redis";
import {
  startRedis,
  stopRedis,
  getRedis,
  flushRedis,
  createTestContext,
} from "./redis-test-helpers.js";

describe("DistributedCache.Increment", () => {
  beforeAll(startRedis, 60_000);
  afterAll(stopRedis);
  beforeEach(flushRedis);

  it("should increment by default amount (1)", async () => {
    const redis = getRedis();
    await redis.set("counter", "5");

    const handler = new Increment(redis, createTestContext());
    const result = await handler.handleAsync({ key: "counter" });

    expect(result).toBeSuccess();
    expect(result.data?.newValue).toBe(6);
  });

  it("should increment by custom amount", async () => {
    const redis = getRedis();
    await redis.set("counter", "10");

    const handler = new Increment(redis, createTestContext());
    const result = await handler.handleAsync({ key: "counter", amount: 5 });

    expect(result).toBeSuccess();
    expect(result.data?.newValue).toBe(15);
  });

  it("should set TTL on the first increment when key has none", async () => {
    const redis = getRedis();
    const handler = new Increment(redis, createTestContext());
    await handler.handleAsync({ key: "counter", expirationMs: 60_000 });

    const pttl = await redis.pttl("counter");
    expect(pttl).toBeGreaterThan(0);
    expect(pttl).toBeLessThanOrEqual(60_000);
  });

  it("should create key with initial value when key does not exist", async () => {
    const redis = getRedis();
    const handler = new Increment(redis, createTestContext());
    const result = await handler.handleAsync({ key: "new-counter", amount: 3 });

    expect(result).toBeSuccess();
    expect(result.data?.newValue).toBe(3);
  });

  // ---------------------------------------------------------------------------
  // Sliding-window contract (PEXPIRE NX semantics).
  //
  // The handler MUST set the TTL only on the call that creates the key, and
  // MUST leave the TTL alone on every subsequent increment. Without this
  // guarantee, sliding-window rate limits and throttles silently break:
  // every event would push the window forward, the counter would never
  // expire, and the per-window cap would never be enforced — an attacker
  // sending requests slightly faster than the window would never trip the
  // limit. These two tests enforce the contract end-to-end against real
  // Redis. They are load-bearing for OtpRateLimitStore and
  // SignInThrottleStore correctness.
  // ---------------------------------------------------------------------------

  it("does NOT extend the TTL on subsequent increments (sliding-window contract)", async () => {
    const redis = getRedis();
    const handler = new Increment(redis, createTestContext());

    await handler.handleAsync({ key: "sliding-counter", expirationMs: 60_000 });
    await new Promise((r) => setTimeout(r, 200));
    await handler.handleAsync({ key: "sliding-counter", expirationMs: 60_000 });

    const pttl = await redis.pttl("sliding-counter");
    // Second call MUST NOT have refreshed the TTL — at least 200ms have passed
    // since the first call set it, so PTTL must reflect that elapsed time.
    expect(pttl).toBeLessThan(60_000 - 100);
    // Sanity bound — TTL is still in the original ~60s window, not negative.
    expect(pttl).toBeGreaterThan(60_000 - 5_000);
  });

  it("counter still increments correctly across multiple calls", async () => {
    const redis = getRedis();
    const handler = new Increment(redis, createTestContext());

    const r1 = await handler.handleAsync({ key: "multi-counter", expirationMs: 60_000 });
    const r2 = await handler.handleAsync({ key: "multi-counter", expirationMs: 60_000 });
    const r3 = await handler.handleAsync({ key: "multi-counter", expirationMs: 60_000 });

    // Counting is independent of TTL semantics — every call still increments.
    expect(r1.data?.newValue).toBe(1);
    expect(r2.data?.newValue).toBe(2);
    expect(r3.data?.newValue).toBe(3);
  });
});
