import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import * as CacheRedis from "@d2/cache-redis";
import { InvalidateUserSessionCache } from "@d2/auth-app";
import { createSecondaryStorage } from "@d2/auth-infra";
import {
  startRedis,
  stopRedis,
  getRedis,
  flushRedis,
  createTestContext,
} from "./redis-test-helpers.js";

/**
 * Integration tests for InvalidateUserSessionCache.
 *
 * Uses real Redis (via Testcontainers) to verify the handler correctly
 * reads BetterAuth's `active-sessions-{userId}` list, deletes each
 * individual session token cache entry, and leaves the active-sessions
 * list intact (user stays signed in).
 *
 * Data is seeded via `createSecondaryStorage` to match how BetterAuth
 * actually writes session data (same serialization path).
 */
describe("InvalidateUserSessionCache (integration)", () => {
  const userId = "01234567-89ab-cdef-0123-456789abcdef";
  const activeSessionsKey = `active-sessions-${userId}`;

  let handler: InstanceType<typeof InvalidateUserSessionCache>;
  let storage: ReturnType<typeof createSecondaryStorage>;

  beforeAll(async () => {
    await startRedis();
    const ctx = createTestContext();
    const redis = getRedis();

    const cacheGet = new CacheRedis.Get<string>(redis, ctx);
    const cacheSet = new CacheRedis.Set<string>(redis, ctx);
    const cacheRemove = new CacheRedis.Remove(redis, ctx);

    handler = new InvalidateUserSessionCache(cacheGet, cacheRemove, ctx);
    storage = createSecondaryStorage({ get: cacheGet, set: cacheSet, remove: cacheRemove });
  }, 120_000);

  afterAll(async () => {
    await stopRedis();
  });

  beforeEach(async () => {
    await flushRedis();
  });

  /** Seeds session data in Redis using the same path BetterAuth uses. */
  async function seedSessions(
    tokens: { token: string; expiresAt: number }[],
    sessionData?: Record<string, string>,
  ): Promise<void> {
    // Seed the active-sessions list (BetterAuth stores this via secondaryStorage.set)
    await storage.set(activeSessionsKey, JSON.stringify(tokens), 86_400);

    // Seed individual session token entries
    for (const { token } of tokens) {
      const value =
        sessionData?.[token] ??
        JSON.stringify({ session: { id: `s-${token.slice(0, 4)}`, token }, user: { name: "Old" } });
      await storage.set(token, value, 86_400);
    }
  }

  it("should delete individual session token cache entries", async () => {
    const redis = getRedis();
    const tok1 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const tok2 = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    await seedSessions([
      { token: tok1, expiresAt: Date.now() + 86_400_000 },
      { token: tok2, expiresAt: Date.now() + 86_400_000 },
    ]);

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(await redis.get(tok1)).toBeNull();
    expect(await redis.get(tok2)).toBeNull();
  });

  it("should preserve the active-sessions list (user stays signed in)", async () => {
    const redis = getRedis();
    const tok = "cccccccccccccccccccccccccccccccc";

    await seedSessions([{ token: tok, expiresAt: Date.now() + 86_400_000 }]);

    await handler.handleAsync({ userId });

    // Token entry gone, but list still exists
    expect(await redis.get(tok)).toBeNull();
    expect(await redis.get(activeSessionsKey)).not.toBeNull();
  });

  it("should no-op when no active-sessions key exists", async () => {
    const result = await handler.handleAsync({ userId });
    expect(result.success).toBe(true);
  });

  it("should no-op when active-sessions list is empty", async () => {
    await storage.set(activeSessionsKey, JSON.stringify([]), 86_400);

    const result = await handler.handleAsync({ userId });
    expect(result.success).toBe(true);
  });

  it("should handle malformed JSON in active-sessions list gracefully", async () => {
    await storage.set(activeSessionsKey, "not-valid-json{{{", 86_400);

    const result = await handler.handleAsync({ userId });
    expect(result.success).toBe(true);
  });

  it("should handle single session", async () => {
    const redis = getRedis();
    const tok = "dddddddddddddddddddddddddddddddd";

    await seedSessions([{ token: tok, expiresAt: Date.now() + 86_400_000 }]);

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(await redis.get(tok)).toBeNull();
  });

  it("should handle expired sessions in the list (still deletes the key)", async () => {
    const redis = getRedis();
    const expiredTok = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    await seedSessions([{ token: expiredTok, expiresAt: Date.now() - 86_400_000 }]);

    const result = await handler.handleAsync({ userId });

    expect(result.success).toBe(true);
    expect(await redis.get(expiredTok)).toBeNull();
  });

  it("should reject invalid userId", async () => {
    const result = await handler.handleAsync({ userId: "" });
    expect(result.success).toBe(false);
  });
});
