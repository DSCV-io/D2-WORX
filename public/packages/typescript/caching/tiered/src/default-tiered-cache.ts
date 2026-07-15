// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type {
  ICacheInvalidationBackplane,
  IDistributedCache,
  ILocalCache,
  ITieredCache,
} from "@d2/caching-abstractions";
import type { ILogger } from "@d2/logging";
import {
  ErrorCodes,
  HttpStatusCode,
  notFound,
  ok,
  someFound,
  type D2Result,
} from "@d2/result";

import {
  logL1InvalidationFailed,
  logL1WriteFailedAfterL2Success,
  TieredCacheOp,
  TIERED_ERROR_CODE_UNKNOWN,
} from "./tiered-cache-log.js";

/**
 * Pinned registration-error message thrown by `*AndBroadcast*` when no
 * backplane was passed at construction. Twin meaning of .NET
 * `InvalidOperationException` on missing `ICacheInvalidationBackplane`.
 */
export const BACKPLANE_NOT_REGISTERED_MESSAGE =
  "ICacheInvalidationBackplane is not registered. Use set / remove " +
  "(no broadcast), or pass a backplane to DefaultTieredCache.";

/** Constructor dependencies for {@link DefaultTieredCache}. */
export interface DefaultTieredCacheDeps {
  /** Local (in-process) L1 cache. */
  l1: ILocalCache;
  /** Distributed (cluster) L2 cache. */
  l2: IDistributedCache;
  /** Required structured logger. */
  logger: ILogger;
  /**
   * Optional invalidation backplane. Required for `*AndBroadcast*` and
   * for cluster L1 coherency subscribe. When present, the tiered cache
   * subscribes at construction for everyone-acts L1 drop.
   */
  backplane?: ICacheInvalidationBackplane;
}

/**
 * Composes one {@link ILocalCache} (L1) and one {@link IDistributedCache}
 * (L2) into a tiered cache. Reads check L1 first / fall through to L2 /
 * populate L1 on L2 hit. Writes go L2-first - L1 only writes if L2
 * succeeded - so partial-write states are impossible. Atomic primitives
 * route through L2 (the cluster source of truth) and invalidate L1 as a
 * side effect. Optional {@link ICacheInvalidationBackplane} wires up
 * cluster-wide L1 invalidation: this instance subscribes at construction
 * and drops L1 entries on every received invalidation (including its own,
 * per the universal everyone-acts rule).
 *
 * Pure composition - does not own or dispose L1, L2, or the backplane.
 * Disposing this instance only unsubscribes its backplane handler.
 *
 * @see ITieredCache port contracts on `@d2/caching-abstractions`.
 */
export class DefaultTieredCache implements ITieredCache, AsyncDisposable {
  private readonly l1: ILocalCache;
  private readonly l2: IDistributedCache;
  private readonly logger: ILogger;
  private readonly backplane: ICacheInvalidationBackplane | undefined;
  private readonly subscription: AsyncDisposable | undefined;
  private disposed = false;

  /**
   * Constructs a tiered cache. Throws `TypeError` when `l1`, `l2`, or
   * `logger` is nullish. When `backplane` is present, subscribes
   * synchronously for L1 invalidation.
   *
   * @param deps - L1, L2, logger, and optional backplane.
   */
  constructor(deps: DefaultTieredCacheDeps) {
    if (deps.l1 == null) {
      throw new TypeError("l1 is required");
    }

    if (deps.l2 == null) {
      throw new TypeError("l2 is required");
    }

    if (deps.logger == null) {
      throw new TypeError("logger is required");
    }

    this.l1 = deps.l1;
    this.l2 = deps.l2;
    this.logger = deps.logger;
    this.backplane = deps.backplane;

    if (deps.backplane !== undefined) {
      // Capture into locals for the lambda closure (subscription may
      // outlive the immediate ctor frame; avoid disposed-this races).
      const capturedLogger = deps.logger;
      const capturedL1 = deps.l1;

      this.subscription = deps.backplane.subscribe(async (key, signal) => {
        const result = await capturedL1.remove(key, signal);

        if (!result.success) {
          logL1InvalidationFailed(
            capturedLogger,
            key,
            result.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
          );
        }
      });
    }
  }

  // ----- ICacheBasic -----

  /**
   * Reads a single value: L1 first; on miss, L2 then populate L1.
   * L1 hard-fail is treated as a miss (fall through to L2).
   */
  async get<T>(key: string, signal?: AbortSignal): Promise<D2Result<T>> {
    const l1 = await this.l1.get<T>(key, signal);

    if (l1.success) {
      return l1;
    }

    const l2 = await this.l2.get<T>(key, signal);

    if (!l2.success) {
      return l2;
    }

    // Populate L1 with default L1 TTL (no L2 remaining-TTL atomic).
    // Ignore L1 set fail - match .NET fire-and-forget populate.
    await this.l1.set(key, l2.data as T, undefined, signal);

    return l2;
  }

  /**
   * Reads many keys: L1 partial hits first; missing keys from L2;
   * merge with the one-truth result ladder (see package README).
   */
  async getMany<T>(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>> {
    const l1 = await this.l1.getMany<T>(keys, signal);

    // All keys hit in L1 - early exit before missing-key work.
    if (l1.success && !l1.isPartialSuccess) {
      return l1;
    }

    // Hit map: ok OR someFound (isPartialSuccess alone - never gate on
    // success; real TS someFound has success: false).
    const l1Hits: Map<string, T> =
      l1.success || l1.isPartialSuccess
        ? new Map(l1.data ?? new Map())
        : new Map();

    const missing = keys.filter((k) => !l1Hits.has(k));

    if (missing.length === 0) {
      return l1;
    }

    const l2 = await this.l2.getMany<T>(missing, signal);

    if (isNotFoundResult(l2)) {
      return l1Hits.size === 0
        ? notFound()
        : someFound({ data: l1Hits as ReadonlyMap<string, T> });
    }

    // Hard fail (SU / VF / etc.) - not ok and not someFound.
    if (!l2.success && !l2.isPartialSuccess) {
      return l2;
    }

    // ok or someFound: merge L2 hits into L1 hits + populate L1.
    const l2Hits = l2.data ?? new Map<string, T>();
    const merged = new Map(l1Hits);
    const populate = new Map<string, T>();

    for (const [k, v] of l2Hits) {
      merged.set(k, v);
      populate.set(k, v);
    }

    if (populate.size > 0) {
      // Ignore L1 populate fail - match .NET no check on GetMany populate.
      await this.l1.setMany(populate, undefined, signal);
    }

    if (merged.size === keys.length) {
      return ok(merged as ReadonlyMap<string, T>);
    }

    return someFound({ data: merged as ReadonlyMap<string, T> });
  }

  /**
   * Returns whether a key is present: L1 `true` short-circuits; else L2.
   */
  async exists(key: string, signal?: AbortSignal): Promise<D2Result<boolean>> {
    const l1 = await this.l1.exists(key, signal);

    if (l1.success && l1.data === true) {
      return l1;
    }

    return this.l2.exists(key, signal);
  }

  /**
   * Returns remaining TTL from L2 only (cluster source of truth).
   */
  getTtl(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number | undefined>> {
    return this.l2.getTtl(key, signal);
  }

  /**
   * Writes L2-first, then L1 with the same value/expirationMs/signal.
   * L1 fail after L2 ok -> Warning log + return L2 success.
   */
  async set<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const l2 = await this.l2.set(key, value, expirationMs, signal);

    if (!l2.success) {
      return l2;
    }

    const l1 = await this.l1.set(key, value, expirationMs, signal);

    if (!l1.success) {
      logL1WriteFailedAfterL2Success(
        this.logger,
        TieredCacheOp.SET,
        key,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    return l2;
  }

  /**
   * Bulk write L2-first, then L1 with the same entries/expirationMs/signal.
   */
  async setMany<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const l2 = await this.l2.setMany(entries, expirationMs, signal);

    if (!l2.success) {
      return l2;
    }

    const l1 = await this.l1.setMany(entries, expirationMs, signal);

    if (!l1.success) {
      logL1WriteFailedAfterL2Success(
        this.logger,
        TieredCacheOp.SET_MANY,
        `${entries.size} entries`,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    return l2;
  }

  /**
   * Removes L2-first, then L1. L1 fail after L2 ok -> log + return L2 ok.
   */
  async remove(key: string, signal?: AbortSignal): Promise<D2Result> {
    const l2 = await this.l2.remove(key, signal);

    if (!l2.success) {
      return l2;
    }

    const l1 = await this.l1.remove(key, signal);

    if (!l1.success) {
      logL1WriteFailedAfterL2Success(
        this.logger,
        TieredCacheOp.REMOVE,
        key,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    return l2;
  }

  /**
   * Bulk remove L2-first, then L1.
   */
  async removeMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const l2 = await this.l2.removeMany(keys, signal);

    if (!l2.success) {
      return l2;
    }

    const l1 = await this.l1.removeMany(keys, signal);

    if (!l1.success) {
      logL1WriteFailedAfterL2Success(
        this.logger,
        TieredCacheOp.REMOVE_MANY,
        `${keys.length} keys`,
        l1.errorCode ?? TIERED_ERROR_CODE_UNKNOWN,
      );
    }

    return l2;
  }

  // ----- ICacheAtomic -----

  /**
   * Set-if-absent via L2; on took-write populate L1; on already-exists
   * drop L1. L1 side-effects are fire-and-forget (no fail log).
   */
  async setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    const l2 = await this.l2.setNx(key, value, expirationMs, signal);

    if (!l2.success) {
      return l2;
    }

    if (l2.data === true) {
      await this.l1.set(key, value, expirationMs, signal);
    } else {
      await this.l1.remove(key, signal);
    }

    return l2;
  }

  /**
   * Increment via L2 then always drop L1 (counters in L1 would diverge).
   */
  async increment(
    key: string,
    amount?: number,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    const l2 = await this.l2.increment(key, amount, expirationMs, signal);

    if (!l2.success) {
      return l2;
    }

    await this.l1.remove(key, signal);

    return l2;
  }

  /**
   * Pure L2 lock acquire - L1 is not involved.
   */
  acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    return this.l2.acquireLock(key, lockId, expirationMs, signal);
  }

  /**
   * Pure L2 lock release - ownership miss is ok; store down propagates.
   */
  releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    return this.l2.releaseLock(key, lockId, signal);
  }

  // ----- ICacheBroadcast -----

  /**
   * Tiered set then publish invalidation. Write fail -> no publish.
   * Missing backplane throws with {@link BACKPLANE_NOT_REGISTERED_MESSAGE}.
   */
  async setAndBroadcast<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const setResult = await this.set(key, value, expirationMs, signal);

    if (!setResult.success) {
      return setResult;
    }

    return this.publish(key, signal);
  }

  /**
   * Tiered setMany then bulk publish. Write fail -> no publish.
   */
  async setManyAndBroadcast<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const setResult = await this.setMany(entries, expirationMs, signal);

    if (!setResult.success) {
      return setResult;
    }

    return this.publishMany([...entries.keys()], signal);
  }

  /**
   * Tiered remove then publish. Write fail -> no publish.
   */
  async removeAndBroadcast(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const removeResult = await this.remove(key, signal);

    if (!removeResult.success) {
      return removeResult;
    }

    return this.publish(key, signal);
  }

  /**
   * Tiered removeMany then bulk publish. Write fail -> no publish.
   */
  async removeManyAndBroadcast(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const removeResult = await this.removeMany(keys, signal);

    if (!removeResult.success) {
      return removeResult;
    }

    return this.publishMany(keys, signal);
  }

  /**
   * Unsubscribes the backplane handler only. Does not dispose L1, L2,
   * or the backplane. Idempotent. Ops remain callable after dispose.
   */
  async dispose(): Promise<void> {
    if (this.disposed) {
      return;
    }

    this.disposed = true;

    if (this.subscription !== undefined) {
      await this.subscription[Symbol.asyncDispose]();
    }
  }

  /** Alias for {@link dispose}. */
  async [Symbol.asyncDispose](): Promise<void> {
    await this.dispose();
  }

  private async publish(key: string, signal?: AbortSignal): Promise<D2Result> {
    if (this.backplane === undefined) {
      throw new Error(BACKPLANE_NOT_REGISTERED_MESSAGE);
    }

    return this.backplane.publishInvalidation(key, signal);
  }

  private async publishMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (this.backplane === undefined) {
      throw new Error(BACKPLANE_NOT_REGISTERED_MESSAGE);
    }

    return this.backplane.publishInvalidationMany(keys, signal);
  }
}

function isNotFoundResult(r: D2Result<unknown>): boolean {
  return (
    !r.success &&
    (r.statusCode === HttpStatusCode.NotFound ||
      r.errorCode === ErrorCodes.NOT_FOUND)
  );
}
