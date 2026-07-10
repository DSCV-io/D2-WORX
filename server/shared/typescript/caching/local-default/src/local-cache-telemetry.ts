// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { type Counter, metrics } from "@opentelemetry/api";

/**
 * OpenTelemetry meter name for the default local-cache implementation.
 * Hosts register this via MeterProvider setup so local-cache counters
 * reach OTLP / Prometheus exporters.
 */
// Byte-identical twin of .NET LocalCacheTelemetry: meter "D2.Shared.Caching.Local" v1.0.0,
// counters d2.cache.local.{hits,misses,sets,removes,evictions} with the same units and
// descriptions. Aggregate counters only - no tags, no spans, no logs (matches the .NET meter).
export const LOCAL_CACHE_METER_NAME = "D2.Shared.Caching.Local";

const LOCAL_CACHE_METER_VERSION = "1.0.0";

/** Bundle of the five aggregate counters owned by {@link DefaultLocalCache}. */
export interface LocalCacheCounters {
  hits: Counter;
  misses: Counter;
  sets: Counter;
  removes: Counter;
  evictions: Counter;
}

/**
 * Creates the five local-cache counters against the current global
 * MeterProvider. Call from the {@link DefaultLocalCache} constructor so
 * counters bind after telemetry bootstrap (module-load bind permanently
 * no-ops when imported before setup).
 */
export function createLocalCacheCounters(): LocalCacheCounters {
  const meter = metrics.getMeter(
    LOCAL_CACHE_METER_NAME,
    LOCAL_CACHE_METER_VERSION,
  );

  return {
    hits: meter.createCounter("d2.cache.local.hits", {
      unit: "{hit}",
      description: "Local cache hits.",
    }),
    misses: meter.createCounter("d2.cache.local.misses", {
      unit: "{miss}",
      description: "Local cache misses.",
    }),
    sets: meter.createCounter("d2.cache.local.sets", {
      unit: "{write}",
      description: "Local cache writes.",
    }),
    removes: meter.createCounter("d2.cache.local.removes", {
      unit: "{removal}",
      description: "Local cache removals (explicit).",
    }),
    evictions: meter.createCounter("d2.cache.local.evictions", {
      unit: "{eviction}",
      description: "Entries evicted by capacity / expiration.",
    }),
  };
}
