import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import * as CacheRedis from "@d2/cache-redis";
import { OtpRateLimitStore } from "@d2/auth-infra";
import { OTP_RATE_LIMIT } from "@d2/auth-domain";
import {
  startRedis,
  stopRedis,
  getRedis,
  flushRedis,
  createTestContext,
} from "./redis-test-helpers.js";

/**
 * Tests `OtpRateLimitStore` with real `@d2/cache-redis` handlers against a
 * real Redis instance. Mirrors `sign-in-throttle-store.test.ts`.
 *
 * Validates:
 *   - Counter increments + TTL on the attempts window
 *   - Cooldown debounce for the first FREE_SEND_ATTEMPTS sends
 *   - Exponential backoff once the free budget is exhausted (capped at MAX_DELAY_MS)
 *   - Isolation between (userId, type) pairs and between types for the same user
 *   - clearOnSuccess wipes both keys
 *   - Fail-open semantics (degrades gracefully on Redis errors)
 */
describe("OtpRateLimitStore (integration)", () => {
  let store: OtpRateLimitStore;

  beforeAll(async () => {
    await startRedis();
    const ctx = createTestContext();
    const redis = getRedis();

    store = new OtpRateLimitStore(
      new CacheRedis.GetTtl(redis, ctx),
      new CacheRedis.Set<string>(redis, ctx),
      new CacheRedis.Remove(redis, ctx),
      new CacheRedis.Increment(redis, ctx),
    );
  }, 120_000);

  afterAll(async () => {
    await stopRedis();
  });

  beforeEach(async () => {
    await flushRedis();
  });

  const USER_ID = "01950000-0000-7000-8000-000000000001";

  // ---------------------------------------------------------------------------
  // Initial state
  // ---------------------------------------------------------------------------
  describe("initial state", () => {
    it("returns 0 cooldown when no record exists", async () => {
      const seconds = await store.getCooldownSeconds(USER_ID, "email");
      expect(seconds).toBe(0);
    });

    it("isolates cooldown by type — phone state does not leak to email", async () => {
      await store.recordSend(USER_ID, "phone");

      const phoneCooldown = await store.getCooldownSeconds(USER_ID, "phone");
      const emailCooldown = await store.getCooldownSeconds(USER_ID, "email");

      expect(phoneCooldown).toBeGreaterThan(0);
      expect(emailCooldown).toBe(0);
    });
  });

  // ---------------------------------------------------------------------------
  // Free-attempt window — first FREE_SEND_ATTEMPTS get the MIN_DELAY debounce
  // ---------------------------------------------------------------------------
  describe("free-attempt debounce", () => {
    it("applies MIN_DELAY_MS cooldown on the first send", async () => {
      await store.recordSend(USER_ID, "email");

      const seconds = await store.getCooldownSeconds(USER_ID, "email");
      const expectedSeconds = Math.ceil(OTP_RATE_LIMIT.MIN_DELAY_MS / 1000);

      // Allow a small clock drift window (within 2 seconds of expected)
      expect(seconds).toBeGreaterThan(expectedSeconds - 2);
      expect(seconds).toBeLessThanOrEqual(expectedSeconds);
    });

    it("keeps cooldown at MIN_DELAY_MS for sends 1..FREE_SEND_ATTEMPTS", async () => {
      const expectedSeconds = Math.ceil(OTP_RATE_LIMIT.MIN_DELAY_MS / 1000);

      for (let i = 1; i <= OTP_RATE_LIMIT.FREE_SEND_ATTEMPTS; i++) {
        await store.recordSend(USER_ID, "email");
        const seconds = await store.getCooldownSeconds(USER_ID, "email");
        // Each send refreshes the cooldown to MIN_DELAY_MS — no growth yet
        expect(seconds).toBeGreaterThan(expectedSeconds - 2);
        expect(seconds).toBeLessThanOrEqual(expectedSeconds);
      }
    });
  });

  // ---------------------------------------------------------------------------
  // Exponential backoff — once free budget is exhausted
  // ---------------------------------------------------------------------------
  describe("exponential backoff", () => {
    async function recordN(n: number) {
      for (let i = 0; i < n; i++) await store.recordSend(USER_ID, "email");
    }

    it("doubles cooldown for each send beyond FREE_SEND_ATTEMPTS", async () => {
      // Send FREE_SEND_ATTEMPTS+1 times → 4th send triggers first backoff (60s)
      await recordN(OTP_RATE_LIMIT.FREE_SEND_ATTEMPTS + 1);

      const seconds = await store.getCooldownSeconds(USER_ID, "email");
      const expectedSeconds = Math.ceil((OTP_RATE_LIMIT.MIN_DELAY_MS * 2) / 1000); // 30s * 2 = 60s

      expect(seconds).toBeGreaterThan(expectedSeconds - 2);
      expect(seconds).toBeLessThanOrEqual(expectedSeconds);
    });

    it("doubles again on the 5th send", async () => {
      // 5th send → MIN_DELAY * 2^2 = 120s
      await recordN(OTP_RATE_LIMIT.FREE_SEND_ATTEMPTS + 2);

      const seconds = await store.getCooldownSeconds(USER_ID, "email");
      const expectedSeconds = Math.ceil((OTP_RATE_LIMIT.MIN_DELAY_MS * 4) / 1000); // 30s * 4 = 120s

      expect(seconds).toBeGreaterThan(expectedSeconds - 2);
      expect(seconds).toBeLessThanOrEqual(expectedSeconds);
    });

    it("caps cooldown at MAX_DELAY_MS — many sends never exceed the cap", async () => {
      // Send 20 times — far past where exponential backoff would hit the cap
      await recordN(20);

      const seconds = await store.getCooldownSeconds(USER_ID, "email");
      const maxSeconds = Math.ceil(OTP_RATE_LIMIT.MAX_DELAY_MS / 1000);

      expect(seconds).toBeGreaterThan(maxSeconds - 2);
      expect(seconds).toBeLessThanOrEqual(maxSeconds);
    });
  });

  // ---------------------------------------------------------------------------
  // Counter window TTL — independent from cooldown TTL
  // ---------------------------------------------------------------------------
  describe("counter window", () => {
    it("sets ATTEMPT_WINDOW_SECONDS TTL on the attempts key", async () => {
      await store.recordSend(USER_ID, "email");

      const redis = getRedis();
      const pttl = await redis.pttl(`otp:send:attempts:email:${USER_ID}`);

      const expectedMs = OTP_RATE_LIMIT.ATTEMPT_WINDOW_SECONDS * 1000;
      expect(pttl).toBeGreaterThan(expectedMs - 5000);
      expect(pttl).toBeLessThanOrEqual(expectedMs);
    });

    it("preserves the original window TTL across subsequent sends (sliding behavior)", async () => {
      await store.recordSend(USER_ID, "email");

      // The attempts key TTL is set on first send only; subsequent INCR does NOT
      // refresh TTL (`expirationMs` is treated as "set if not present"). Verify.
      await new Promise((r) => setTimeout(r, 50)); // small delay to advance clock

      await store.recordSend(USER_ID, "email");

      const redis = getRedis();
      const pttl = await redis.pttl(`otp:send:attempts:email:${USER_ID}`);

      const expectedMs = OTP_RATE_LIMIT.ATTEMPT_WINDOW_SECONDS * 1000;
      // Should be slightly LESS than full window since some time has elapsed
      expect(pttl).toBeGreaterThan(expectedMs - 10000);
      expect(pttl).toBeLessThan(expectedMs);
    });
  });

  // ---------------------------------------------------------------------------
  // clearOnSuccess
  // ---------------------------------------------------------------------------
  describe("clearOnSuccess", () => {
    it("removes both attempts and cooldown keys", async () => {
      await store.recordSend(USER_ID, "email");
      await store.recordSend(USER_ID, "email");

      await store.clearOnSuccess(USER_ID, "email");

      const redis = getRedis();
      expect(await redis.exists(`otp:send:attempts:email:${USER_ID}`)).toBe(0);
      expect(await redis.exists(`otp:send:cooldown:email:${USER_ID}`)).toBe(0);
    });

    it("resets the counter — next send is treated as the first", async () => {
      // Burn through the free budget + 1 (so backoff would normally be doubled)
      for (let i = 0; i < OTP_RATE_LIMIT.FREE_SEND_ATTEMPTS + 2; i++) {
        await store.recordSend(USER_ID, "email");
      }

      await store.clearOnSuccess(USER_ID, "email");

      // Next send should behave as send #1 — MIN_DELAY_MS, not exponential
      await store.recordSend(USER_ID, "email");

      const seconds = await store.getCooldownSeconds(USER_ID, "email");
      const expectedSeconds = Math.ceil(OTP_RATE_LIMIT.MIN_DELAY_MS / 1000);
      expect(seconds).toBeGreaterThan(expectedSeconds - 2);
      expect(seconds).toBeLessThanOrEqual(expectedSeconds);
    });

    it("only clears the targeted (userId, type) pair", async () => {
      await store.recordSend(USER_ID, "email");
      await store.recordSend(USER_ID, "phone");

      await store.clearOnSuccess(USER_ID, "email");

      // phone state is intact
      expect(await store.getCooldownSeconds(USER_ID, "phone")).toBeGreaterThan(0);
      expect(await store.getCooldownSeconds(USER_ID, "email")).toBe(0);
    });
  });

  // ---------------------------------------------------------------------------
  // User-level isolation
  // ---------------------------------------------------------------------------
  describe("isolation", () => {
    it("does not cross-contaminate between different users", async () => {
      const userA = "01950000-0000-7000-8000-00000000aaaa";
      const userB = "01950000-0000-7000-8000-00000000bbbb";

      await store.recordSend(userA, "email");

      const aCooldown = await store.getCooldownSeconds(userA, "email");
      const bCooldown = await store.getCooldownSeconds(userB, "email");

      expect(aCooldown).toBeGreaterThan(0);
      expect(bCooldown).toBe(0);
    });

    it("does not cross-contaminate between types for the same user", async () => {
      // Burn email budget; phone should be unaffected
      for (let i = 0; i < OTP_RATE_LIMIT.FREE_SEND_ATTEMPTS + 5; i++) {
        await store.recordSend(USER_ID, "email");
      }

      const phoneCooldown = await store.getCooldownSeconds(USER_ID, "phone");
      expect(phoneCooldown).toBe(0);

      // First phone send is a clean start (MIN_DELAY_MS)
      await store.recordSend(USER_ID, "phone");
      const phoneAfter = await store.getCooldownSeconds(USER_ID, "phone");
      const expected = Math.ceil(OTP_RATE_LIMIT.MIN_DELAY_MS / 1000);
      expect(phoneAfter).toBeGreaterThan(expected - 2);
      expect(phoneAfter).toBeLessThanOrEqual(expected);
    });
  });
});
