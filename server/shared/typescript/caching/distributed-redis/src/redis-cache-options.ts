// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Configuration knobs for the Redis-backed distributed cache and the
 * Redis-backed invalidation backplane. Twin of .NET `RedisCacheOptions`
 * with millisecond-number durations instead of `TimeSpan`.
 *
 * `connectionString` is **SECRET when credentials are embedded** - never
 * log or echo it. It is required only when calling {@link connectRedis}.
 */
export interface RedisCacheOptions {
  /**
   * ioredis connection URL / string. Required for {@link connectRedis}.
   * SECRET when credentials are embedded - never log.
   */
  connectionString?: string;

  /** Default TTL (ms) applied when a write omits `expirationMs`. Default 1 hour. */
  defaultExpirationMs: number;

  /** Optional prefix prepended to every store key and broadcast payload. */
  keyPrefix: string;

  /**
   * Pub/sub channel for cache invalidation. Default
   * `"d2:cache:invalidations"` (byte-equal to .NET).
   */
  invalidationChannel: string;

  /** Per-command timeout (ms). Default 2000. */
  commandTimeoutMs: number;

  /** Connect timeout (ms). Default 5000. */
  connectTimeoutMs: number;

  /** Connect retry count for the initial connection. Default 3. */
  connectRetries: number;

  /**
   * When true, abort if the initial connection fails. Default false -
   * cache calls return `serviceUnavailable` until Redis is reachable.
   */
  abortOnConnectFail: boolean;
}

/** Defaults twin of .NET `RedisCacheOptions`. */
export const REDIS_CACHE_DEFAULTS: Readonly<RedisCacheOptions> = {
  defaultExpirationMs: 3_600_000,
  keyPrefix: "",
  invalidationChannel: "d2:cache:invalidations",
  commandTimeoutMs: 2_000,
  connectTimeoutMs: 5_000,
  connectRetries: 3,
  abortOnConnectFail: false,
};

/**
 * Merges a partial options bag over {@link REDIS_CACHE_DEFAULTS}.
 * Does not throw - structural guards run in cache/backplane/connect paths.
 */
export function createRedisCacheOptions(
  partial?: Partial<RedisCacheOptions>,
): RedisCacheOptions {
  return {
    ...REDIS_CACHE_DEFAULTS,
    ...partial,
  };
}
