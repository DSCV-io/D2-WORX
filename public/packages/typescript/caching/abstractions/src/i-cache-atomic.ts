// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { D2Result } from "@d2/result";

/**
 * Atomic primitives — set-if-absent, increment, lock acquire/release.
 * Implemented by every cache flavor ({@link ILocalCache},
 * {@link IDistributedCache}, {@link ITieredCache}); the atomicity
 * scope depends on the implementing cache.
 *
 * **Scope per marker:**
 * - {@link ILocalCache}: per-process. Atomicity guaranteed within this
 *   instance only.
 * - {@link IDistributedCache}: cluster-wide. Atomicity enforced by the
 *   remote store (e.g. Redis SETNX / INCR).
 * - {@link ITieredCache}: cluster-wide. Routes through L2 (the source
 *   of truth); L1 is invalidated or refreshed as a side effect.
 *
 * Callers pick the scope by injecting the appropriate marker interface.
 * The same atomic op called via `ILocalCache` coordinates within one
 * process; via `IDistributedCache` or `ITieredCache` it coordinates
 * across the cluster.
 *
 * **Counter width:** `number` (not `bigint`) is an intentional TS
 * ergonomic delta vs .NET `long`. Implementations must stay within
 * `Number.MAX_SAFE_INTEGER`.
 */
export interface ICacheAtomic {
  /**
   * Sets a value only if the key is not already present (atomic).
   *
   * @returns `ok(true)` if the value was written. `ok(false)` if the
   *   key already existed (no write). Failure on backing-store error.
   */
  setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;

  /**
   * Atomically increments a numeric counter and returns the new value.
   * Creates the key if absent (initial value = 0 + amount).
   *
   * **TTL-on-create-only:** the optional `expirationMs` is applied
   * **only when the key is created**; subsequent increments on an
   * existing key **preserve the existing TTL**.
   *
   * @param amount - Amount to add (may be negative). Default `1`.
   * @returns `ok(newValue)` on success. `conflict` if the existing key
   *   holds a non-numeric value (Redis WRONGTYPE parity). Failure on
   *   backing-store error.
   */
  increment(
    key: string,
    amount?: number,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>>;

  /**
   * Attempts to acquire a lock on the given key. The caller-supplied
   * `lockId` identifies the holder and is required for release. Locks
   * expire automatically after `expirationMs` to prevent indefinite
   * hold by a crashed process.
   *
   * @param expirationMs - **Required** lock TTL in milliseconds —
   *   locks must auto-expire (matches .NET `TimeSpan expiration` with
   *   no default).
   * @returns `ok(true)` on acquisition. `ok(false)` if the lock is
   *   held by someone else. Failure on backing-store error.
   */
  acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;

  /**
   * Releases a lock previously acquired with {@link acquireLock}.
   * Releasing a lock you don't hold (or one that's already expired) is
   * a no-op rather than an error — release is **idempotent**.
   *
   * @returns `ok`; failure on backing-store error.
   */
  releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result>;
}
