// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Configuration knobs for an {@link ILocalCache} implementation.
 * Defaults are tuned for typical microservice workloads (100K entries,
 * 1-hour default TTL).
 *
 * Per-entry size accounting is intentionally NOT exposed — the default
 * implementation always counts entries as size 1 so `maxEntries`
 * behaves as the literal entry-count cap most callers expect.
 *
 * No options validation on this POCO (mirrors .NET — no range check /
 * ValidateOnStart). The factory does not fail on absurd values;
 * enforcement belongs in the local-default implementation.
 */
export interface LocalCacheOptions {
  /**
   * Maximum number of entries before LRU-ish eviction kicks in.
   * Default 100_000.
   */
  maxEntries: number;

  /**
   * Default TTL (milliseconds) applied to entries written without an
   * explicit expiration. Default 3_600_000 (1 hour).
   */
  defaultExpirationMs: number;

  /**
   * Optional key prefix automatically prepended to every cache key.
   * Useful when multiple caches share a process and the caller wants
   * namespace isolation (e.g. `"jwks:"`) without coordinating keys
   * explicitly. Default is empty string (no prefix).
   */
  keyPrefix: string;
}

// Numeric twin of .NET LocalCacheOptions (MaxEntries=100_000, DefaultExpiration=1h, KeyPrefix="").
export const LOCAL_CACHE_DEFAULTS: Readonly<LocalCacheOptions> = {
  maxEntries: 100_000,
  defaultExpirationMs: 3_600_000,
  keyPrefix: "",
};

/**
 * Builds a mutable {@link LocalCacheOptions} by merging an optional
 * partial over {@link LOCAL_CACHE_DEFAULTS}. Always returns a fresh
 * object (not shared with the defaults constant).
 */
export function createLocalCacheOptions(
  partial?: Partial<LocalCacheOptions>,
): LocalCacheOptions {
  return {
    maxEntries: partial?.maxEntries ?? LOCAL_CACHE_DEFAULTS.maxEntries,
    defaultExpirationMs:
      partial?.defaultExpirationMs ?? LOCAL_CACHE_DEFAULTS.defaultExpirationMs,
    keyPrefix: partial?.keyPrefix ?? LOCAL_CACHE_DEFAULTS.keyPrefix,
  };
}
