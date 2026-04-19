import Redis from "ioredis";
import type { HandlerContext } from "@d2/handler";
import * as CacheRedis from "@d2/cache-redis";
import { createSecondaryStorage, OtpRateLimitStore, SignInThrottleStore } from "@d2/auth-infra";

export interface RedisSetup {
  /** Redis-backed BetterAuth secondary storage adapter. */
  secondaryStorage: ReturnType<typeof createSecondaryStorage>;
  /** Sign-in throttle store (Redis-backed counters). */
  throttleStore: SignInThrottleStore;
  /** OTP send rate limit store (Redis-backed counters per user/type). */
  otpRateLimitStore: OtpRateLimitStore;
  /** Individual cache handlers exposed for pre-auth singletons that need them. */
  handlers: {
    get: CacheRedis.Get<string>;
    set: CacheRedis.Set<string>;
    remove: CacheRedis.Remove;
    exists: CacheRedis.Exists;
    getTtl: CacheRedis.GetTtl;
    increment: CacheRedis.Increment;
  };
}

/**
 * Creates all Redis-backed infrastructure: secondary storage, throttle store,
 * and individual cache handlers for pre-auth singletons.
 */
export function createRedisSetup(redis: Redis, serviceContext: HandlerContext): RedisSetup {
  const redisGet = new CacheRedis.Get<string>(redis, serviceContext);
  const redisSet = new CacheRedis.Set<string>(redis, serviceContext);
  const redisRemove = new CacheRedis.Remove(redis, serviceContext);
  const redisExists = new CacheRedis.Exists(redis, serviceContext);
  const redisGetTtl = new CacheRedis.GetTtl(redis, serviceContext);
  const redisIncrement = new CacheRedis.Increment(redis, serviceContext);

  const secondaryStorage = createSecondaryStorage({
    get: redisGet,
    set: redisSet,
    remove: redisRemove,
  });

  const throttleStore = new SignInThrottleStore(
    redisExists,
    redisGetTtl,
    redisSet,
    redisRemove,
    redisIncrement,
  );

  const otpRateLimitStore = new OtpRateLimitStore(
    redisGetTtl,
    redisSet,
    redisRemove,
    redisIncrement,
  );

  return {
    secondaryStorage,
    throttleStore,
    otpRateLimitStore,
    handlers: {
      get: redisGet,
      set: redisSet,
      remove: redisRemove,
      exists: redisExists,
      getTtl: redisGetTtl,
      increment: redisIncrement,
    },
  };
}
