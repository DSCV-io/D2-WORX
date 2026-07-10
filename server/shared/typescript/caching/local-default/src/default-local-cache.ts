// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  createLocalCacheOptions,
  InputFailures,
  type ILocalCache,
  type LocalCacheOptions,
} from "@d2/caching-abstractions";
import { conflict, notFound, ok, someFound, type D2Result } from "@d2/result";
import { falsey } from "@d2/utilities";

import {
  createLocalCacheCounters,
  type LocalCacheCounters,
} from "./local-cache-telemetry.js";

interface CacheEntry {
  value: unknown;
  expiresAt?: number;
}

interface LockEntry {
  lockId: string;
  expiresAt: number;
}

const DISPOSED_MESSAGE = "DefaultLocalCache is disposed.";

/**
 * In-process implementation of {@link ILocalCache}. Values and counters
 * live in an LRU `Map`; locks live in a dedicated lock `Map`. Every
 * operation body is fully synchronous (methods are `async` only for
 * signature ergonomics) so check-then-write windows are atomic on the
 * Node event loop.
 *
 * Per-call failures return `@d2/result` shapes (`notFound`,
 * `validationFailed` via {@link InputFailures}, `conflict`). Lifecycle
 * misuse (ops after {@link dispose}) throws a plain `Error` with the
 * pinned message `"DefaultLocalCache is disposed."`.
 *
 * Counters bind to the global OpenTelemetry MeterProvider at
 * construction time. Construct caches after telemetry setup, or the
 * counters bind to the no-op meter for the life of the instance.
 *
 * `signal` parameters are accepted for interface parity and ignored:
 * every operation completes synchronously in process.
 *
 * Clock note: the injected clock must be non-decreasing. A backwards
 * jump can transiently resurrect an expired-but-unevicted entry.
 *
 * @see ICacheBasic / ICacheAtomic port contracts on `@d2/caching-abstractions`.
 */
export class DefaultLocalCache implements ILocalCache, Disposable {
  private readonly options: LocalCacheOptions;
  private readonly clock: () => number;
  private readonly counters: LocalCacheCounters;
  private readonly store = new Map<string, CacheEntry>();
  private readonly locks = new Map<string, LockEntry>();
  private disposed = false;

  /**
   * Constructs a local cache. Merges `options` over
   * `LOCAL_CACHE_DEFAULTS` via `createLocalCacheOptions`, then rejects
   * structurally broken numbers with `RangeError`.
   *
   * @param options - Partial options; omitted fields take defaults.
   * @param clock - Epoch-ms clock (defaults to the system epoch-ms clock).
   *   Sole time source for value TTL, lock expiry, and `getTtl` remaining
   *   arithmetic.
   */
  constructor(
    options?: Partial<LocalCacheOptions>,
    clock: () => number = Date.now,
  ) {
    const merged = createLocalCacheOptions(options);

    if (!Number.isSafeInteger(merged.maxEntries) || merged.maxEntries < 0) {
      throw new RangeError(
        `LocalCacheOptions.maxEntries must be a non-negative safe ` +
          `integer, got ${String(merged.maxEntries)}.`,
      );
    }

    if (!Number.isFinite(merged.defaultExpirationMs)) {
      throw new RangeError(
        `LocalCacheOptions.defaultExpirationMs must be a finite number, ` +
          `got ${String(merged.defaultExpirationMs)}.`,
      );
    }

    this.options = merged;
    this.clock = clock;
    this.counters = createLocalCacheCounters();
  }

  /**
   * Clears both maps and marks the instance disposed. Idempotent.
   * Subsequent operations throw `"DefaultLocalCache is disposed."`.
   */
  dispose(): void {
    if (this.disposed) {
      return;
    }

    this.store.clear();
    this.locks.clear();
    this.disposed = true;
  }

  /** Delegates to {@link dispose} for `using` declarations. */
  [Symbol.dispose](): void {
    this.dispose();
  }

  /** @inheritdoc */
  async get<T>(key: string, signal?: AbortSignal): Promise<D2Result<T>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<T>("key");
    }

    const entry = this.readLive(this.prefixed(key));

    if (entry === undefined) {
      this.counters.misses.add(1);

      return notFound();
    }

    this.counters.hits.add(1);

    return ok(entry.value as T);
  }

  /** @inheritdoc */
  async getMany<T>(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(keys)) {
      return InputFailures.required<ReadonlyMap<string, T>>("keys");
    }

    for (const key of keys) {
      if (falsey(key)) {
        return InputFailures.required<ReadonlyMap<string, T>>("keys");
      }
    }

    const hits = new Map<string, T>();
    let hitCount = 0;

    for (const key of keys) {
      const entry = this.readLive(this.prefixed(key));

      if (entry !== undefined) {
        hits.set(key, entry.value as T);
        hitCount++;
      }
    }

    this.counters.hits.add(hitCount);
    this.counters.misses.add(keys.length - hitCount);

    if (hitCount === 0) {
      return notFound();
    }

    if (hitCount === keys.length) {
      return ok(hits);
    }

    return someFound({ data: hits });
  }

  /** @inheritdoc */
  async exists(key: string, signal?: AbortSignal): Promise<D2Result<boolean>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    const entry = this.readLive(this.prefixed(key));

    return ok(entry !== undefined);
  }

  /** @inheritdoc */
  async getTtl(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number | undefined>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<number | undefined>("key");
    }

    const entry = this.readLive(this.prefixed(key));

    if (entry === undefined) {
      return notFound();
    }

    if (entry.expiresAt === undefined) {
      return ok(undefined);
    }

    return ok(entry.expiresAt - this.clock());
  }

  /** @inheritdoc */
  async set<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.required("expirationMs");
    }

    this.setCore(this.prefixed(key), value, expirationMs);
    this.counters.sets.add(1);

    return ok();
  }

  /** @inheritdoc */
  async setMany<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(entries)) {
      return InputFailures.required("entries");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.required("expirationMs");
    }

    for (const key of entries.keys()) {
      if (falsey(key)) {
        return InputFailures.required("entries");
      }
    }

    for (const [key, value] of entries) {
      this.setCore(this.prefixed(key), value, expirationMs);
    }

    this.counters.sets.add(entries.size);

    return ok();
  }

  /** @inheritdoc */
  async remove(key: string, signal?: AbortSignal): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    this.store.delete(this.prefixed(key));
    this.counters.removes.add(1);

    return ok();
  }

  /** @inheritdoc */
  async removeMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(keys)) {
      return InputFailures.required("keys");
    }

    for (const key of keys) {
      if (falsey(key)) {
        return InputFailures.required("keys");
      }
    }

    for (const key of keys) {
      this.store.delete(this.prefixed(key));
    }

    this.counters.removes.add(keys.length);

    return ok();
  }

  /** @inheritdoc */
  async setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.required<boolean>("expirationMs");
    }

    const prefixed = this.prefixed(key);
    const entry = this.readLive(prefixed);

    if (entry !== undefined) {
      return ok(false);
    }

    this.setCore(prefixed, value, expirationMs);
    this.counters.sets.add(1);

    return ok(true);
  }

  /** @inheritdoc */
  async increment(
    key: string,
    amount?: number,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<number>("key");
    }

    if (this.isBadMs(expirationMs)) {
      return InputFailures.required<number>("expirationMs");
    }

    if (amount !== undefined && !Number.isSafeInteger(amount)) {
      return InputFailures.required<number>("amount");
    }

    const n = amount ?? 1;
    const prefixed = this.prefixed(key);
    const entry = this.readLive(prefixed);

    if (entry !== undefined) {
      if (
        typeof entry.value !== "number" ||
        !Number.isSafeInteger(entry.value)
      ) {
        return conflict<number>();
      }

      const next = entry.value + n;

      // JS numbers lose integer precision outside ±MAX_SAFE_INTEGER.
      // Refuse before write so the stored counter stays exact.
      if (!Number.isSafeInteger(next)) {
        return InputFailures.required<number>("amount");
      }

      // Preserve expiresAt verbatim; ignore expirationMs on existing path.
      this.touch(prefixed, { value: next, expiresAt: entry.expiresAt });

      return ok(next);
    }

    this.setCore(prefixed, n, expirationMs);
    this.counters.sets.add(1);

    return ok(n);
  }

  /** @inheritdoc */
  async acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (falsey(lockId)) {
      return InputFailures.required<boolean>("lockId");
    }

    if (!Number.isFinite(expirationMs) || expirationMs <= 0) {
      return InputFailures.required<boolean>("expirationMs");
    }

    const prefixed = this.prefixed(key);
    const now = this.clock();
    const existing = this.locks.get(prefixed);

    if (existing !== undefined && existing.expiresAt > now) {
      return ok(false);
    }

    this.locks.set(prefixed, {
      lockId,
      expiresAt: now + expirationMs,
    });

    return ok(true);
  }

  /** @inheritdoc */
  async releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    void signal;
    this.throwIfDisposed();

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    if (falsey(lockId)) {
      return InputFailures.required("lockId");
    }

    const prefixed = this.prefixed(key);
    const existing = this.locks.get(prefixed);

    if (existing !== undefined && existing.lockId === lockId) {
      this.locks.delete(prefixed);
    }

    return ok();
  }

  private throwIfDisposed(): void {
    if (this.disposed) {
      throw new Error(DISPOSED_MESSAGE);
    }
  }

  private prefixed(key: string): string {
    return falsey(this.options.keyPrefix) ? key : this.options.keyPrefix + key;
  }

  private isBadMs(expirationMs: number | undefined): boolean {
    return (
      expirationMs !== undefined &&
      (!Number.isFinite(expirationMs) || expirationMs <= 0)
    );
  }

  private readLive(prefixed: string): CacheEntry | undefined {
    const entry = this.store.get(prefixed);

    if (entry === undefined) {
      return undefined;
    }

    if (this.isExpired(entry)) {
      this.store.delete(prefixed);
      this.counters.evictions.add(1);

      return undefined;
    }

    this.touch(prefixed, entry);

    return entry;
  }

  private isExpired(entry: CacheEntry): boolean {
    return entry.expiresAt !== undefined && entry.expiresAt <= this.clock();
  }

  private touch(prefixed: string, entry: CacheEntry): void {
    this.store.delete(prefixed);
    this.store.set(prefixed, entry);
  }

  private setCore(
    prefixed: string,
    value: unknown,
    expirationMs: number | undefined,
  ): void {
    const effective = expirationMs ?? this.options.defaultExpirationMs;
    const expiresAt = effective > 0 ? this.clock() + effective : undefined;
    const entry: CacheEntry =
      expiresAt === undefined ? { value } : { value, expiresAt };

    // Unconditional overwrite (expired existing is replace, not eviction).
    // delete+set (via touch) repositions to MRU - Map.set alone keeps order.
    this.touch(prefixed, entry);
    this.enforceCapacity();
  }

  private enforceCapacity(): void {
    while (this.store.size > this.options.maxEntries && this.store.size > 0) {
      this.evictHead();
    }
  }

  private evictHead(): void {
    // Caller (enforceCapacity) only invokes when store.size > 0.
    const first = this.store.keys().next().value as string;

    this.store.delete(first);
    this.counters.evictions.add(1);
  }
}
