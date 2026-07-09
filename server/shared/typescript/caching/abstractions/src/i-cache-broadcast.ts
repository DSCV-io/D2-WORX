// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { D2Result } from "@d2/result";

/**
 * Write surface that publishes invalidation messages via a registered
 * {@link ICacheInvalidationBackplane} after the underlying write
 * completes. Subscribers (typically tiered caches in other instances)
 * drop their L1 copy of the affected key on receipt.
 *
 * Implemented by both {@link IDistributedCache} (so callers writing
 * directly to L2 can still bust other instances' L1) and
 * {@link ITieredCache} (which writes both tiers and broadcasts in one
 * call).
 *
 * **Registration error carve-out:** `*AndBroadcast*` methods **throw**
 * (not `D2Result`) when no `ICacheInvalidationBackplane` was registered
 * with the cache. Registration error is a construction-time / wiring
 * concern, not a per-call failure. Use the plain `set` / `remove` if
 * you don't intend to broadcast.
 */
export interface ICacheBroadcast {
  /**
   * Writes a value AND publishes an invalidation message via the
   * registered {@link ICacheInvalidationBackplane}.
   *
   * @throws When no backplane was registered with the cache.
   */
  setAndBroadcast<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result>;

  /**
   * Bulk-write counterpart of {@link setAndBroadcast}.
   *
   * @throws When no backplane was registered with the cache.
   */
  setManyAndBroadcast<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result>;

  /**
   * Removes a key AND publishes an invalidation message so other
   * instances drop their L1 copies.
   *
   * @throws When no backplane was registered with the cache.
   */
  removeAndBroadcast(key: string, signal?: AbortSignal): Promise<D2Result>;

  /**
   * Bulk-remove + broadcast invalidation per key.
   *
   * @throws When no backplane was registered with the cache.
   */
  removeManyAndBroadcast(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result>;
}
