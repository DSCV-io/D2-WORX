// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { type Counter, metrics } from "@opentelemetry/api";

/**
 * OpenTelemetry meter name for the Redis distributed-cache implementation.
 * Hosts register this via MeterProvider setup so Redis-cache counters
 * reach OTLP / Prometheus exporters.
 */
// Byte-identical twin of .NET RedisCacheTelemetry: meter
// "D2.Shared.Caching.Distributed.Redis" v1.0.0, counters
// d2.cache.redis.{hits,misses,sets,removes,broadcasts,errors} with the same
// units and descriptions. Aggregate counters only - no tags, no spans
// (matches the .NET meter).
export const REDIS_CACHE_METER_NAME = "D2.Shared.Caching.Distributed.Redis";

const REDIS_CACHE_METER_VERSION = "1.0.0";

/** Bundle of the six aggregate counters owned by {@link RedisDistributedCache}. */
export interface RedisCacheCounters {
  hits: Counter;
  misses: Counter;
  sets: Counter;
  removes: Counter;
  broadcasts: Counter;
  errors: Counter;
}

/**
 * Creates the six Redis-cache counters against the current global
 * MeterProvider. Call from the {@link RedisDistributedCache} constructor
 * so counters bind after telemetry bootstrap (module-load bind permanently
 * no-ops when imported before setup).
 */
export function createRedisCacheCounters(): RedisCacheCounters {
  const meter = metrics.getMeter(
    REDIS_CACHE_METER_NAME,
    REDIS_CACHE_METER_VERSION,
  );

  return {
    hits: meter.createCounter("d2.cache.redis.hits", {
      unit: "{hit}",
      description: "Redis cache hits.",
    }),
    misses: meter.createCounter("d2.cache.redis.misses", {
      unit: "{miss}",
      description: "Redis cache misses.",
    }),
    sets: meter.createCounter("d2.cache.redis.sets", {
      unit: "{write}",
      description: "Redis cache writes.",
    }),
    removes: meter.createCounter("d2.cache.redis.removes", {
      unit: "{removal}",
      description: "Redis cache removals.",
    }),
    broadcasts: meter.createCounter("d2.cache.redis.broadcasts", {
      unit: "{broadcast}",
      description: "Invalidation messages published to backplane.",
    }),
    errors: meter.createCounter("d2.cache.redis.errors", {
      unit: "{error}",
      description: "Redis-side failures.",
    }),
  };
}
