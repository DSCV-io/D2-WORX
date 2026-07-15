// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
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
// Instrument tuples below MUST match LocalCacheTelemetry.cs Counter registrations.
export const LOCAL_CACHE_METER_NAME = "D2.Shared.Caching.Local";

/** Meter version twin of .NET `LocalCacheTelemetry.SR_Meter` version. */
export const LOCAL_CACHE_METER_VERSION = "1.0.0";

/** Single instrument metadata row (name / unit / description). */
export interface CacheInstrumentMeta {
  readonly name: string;
  readonly unit: string;
  readonly description: string;
}

/**
 * Source-of-truth instrument metadata for createLocalCacheCounters and dual-runtime
 * parity tests. Must match .NET LocalCacheTelemetry.cs Counter registrations.
 */
export const LOCAL_CACHE_INSTRUMENTS: readonly CacheInstrumentMeta[] = [
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
];

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

  const create = (instrument: CacheInstrumentMeta): Counter =>
    meter.createCounter(instrument.name, {
      unit: instrument.unit,
      description: instrument.description,
    });

  return {
    hits: create(LOCAL_CACHE_INSTRUMENTS[0]!),
    misses: create(LOCAL_CACHE_INSTRUMENTS[1]!),
    sets: create(LOCAL_CACHE_INSTRUMENTS[2]!),
    removes: create(LOCAL_CACHE_INSTRUMENTS[3]!),
    evictions: create(LOCAL_CACHE_INSTRUMENTS[4]!),
  };
}
