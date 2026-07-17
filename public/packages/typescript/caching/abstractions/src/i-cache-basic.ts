// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { D2Result } from "@dcsv-io/d2-result";

/**
 * Core read + write surface common to every cache flavor (local,
 * distributed, tiered). Every method returns `D2Result` / `D2Result<T>`.
 * Cache misses surface as `notFound`; partial bulk hits as `someFound`
 * (callers discriminate via `success` / `isPartialSuccess` / `errorCode`).
 * Generic infrastructure failures (couldn't reach the backing store,
 * serializer threw, etc.) surface as `serviceUnavailable` /
 * `unhandledException`.
 *
 * Falsey inputs (missing key, missing keys collection, missing entries
 * map) return `validationFailed` with an `InputError` naming the
 * offending parameter (built via `InputFailures.required(...)`).
 * Implementations never throw for **per-call** caller mistakes —
 * every per-call failure shape is observable on the result.
 *
 * **Behavior per marker:**
 * - {@link ILocalCache}: in-process reads/writes. Sub-microsecond per op.
 * - {@link IDistributedCache}: every read and write hits the remote store.
 *   No L1 buffer. Predictable freshness, network-bound latency.
 * - {@link ITieredCache}: reads check L1 first / fall through to L2 /
 *   populate L1. Writes go L2-first — L1 only writes if L2 succeeded —
 *   so partial-write states are impossible and the result is binary
 *   (success or the L2 failure bubbled up).
 *
 * **PII:** keep PII out of cache **keys** — keys leak into logs, traces,
 * and store inspection tooling. Hash user-supplied identifiers first.
 */
export interface ICacheBasic {
  /**
   * Reads a single value by key.
   *
   * @returns `ok(value)` on hit, `notFound` on miss, failure on
   *   backing-store error.
   */
  get<T>(key: string, signal?: AbortSignal): Promise<D2Result<T>>;

  /**
   * Reads many keys in one round-trip.
   *
   * @returns `ok(map)` when every key hit, `someFound(partial)` when
   *   some hit and some missed, `notFound` when none hit, or a failure
   *   on backing-store error. Callers may build the input via
   *   `new Map(Object.entries(record))` when they hold a record.
   */
  getMany<T>(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>>;

  /**
   * Returns whether a key is currently present.
   *
   * @returns `ok(true|false)`; failure on backing-store error.
   */
  exists(key: string, signal?: AbortSignal): Promise<D2Result<boolean>>;

  /**
   * Returns the remaining TTL for a key in milliseconds.
   *
   * @returns `notFound` if the key is absent. `ok(undefined)` if the key
   *   exists with no expiration set. `ok(ms)` with the remaining time
   *   when expiration is set. Failure on backing-store error.
   */
  getTtl(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number | undefined>>;

  /**
   * Writes (or overwrites) a value.
   *
   * @param expirationMs - Optional TTL in milliseconds; omitted means
   *   use the cache's default.
   * @returns `ok` on success; failure on backing-store error.
   */
  set<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result>;

  /**
   * Writes many entries in one round-trip.
   *
   * @param entries - Key/value pairs to store (`ReadonlyMap`).
   * @param expirationMs - Optional TTL applied to every entry.
   * @returns `ok` on success; failure on backing-store error.
   */
  setMany<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result>;

  /**
   * Removes a key. **Idempotent** — succeeds whether the key existed
   * or not.
   *
   * @returns `ok`; failure on backing-store error.
   */
  remove(key: string, signal?: AbortSignal): Promise<D2Result>;

  /**
   * Removes many keys. **Idempotent**.
   *
   * @returns `ok`; failure on backing-store error.
   */
  removeMany(keys: readonly string[], signal?: AbortSignal): Promise<D2Result>;
}
