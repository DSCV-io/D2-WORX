// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type {
  ICacheInvalidationBackplane,
  IDistributedCache,
  ILocalCache,
} from "@dcsv-io/d2-caching-abstractions";
import type { ILogger, LogBindings } from "@dcsv-io/d2-logging";
import {
  notFound,
  ok,
  serviceUnavailable,
  someFound,
  type D2Result,
} from "@dcsv-io/d2-result";

/** No-op logger for unit tests (required ctor seam). */
export class NoOpTestLogger implements ILogger {
  trace(_message: string, _bindings?: LogBindings): void {}
  debug(_message: string, _bindings?: LogBindings): void {}
  info(_message: string, _bindings?: LogBindings): void {}
  warn(_message: string, _bindings?: LogBindings): void {}
  error(_message: string, _bindings?: LogBindings): void {}
  fatal(_message: string, _bindings?: LogBindings): void {}
  child(_bindings: LogBindings): ILogger {
    return this;
  }
}

/** Factory for {@link NoOpTestLogger}. */
export function createNoOpTestLogger(): NoOpTestLogger {
  return new NoOpTestLogger();
}

/** Capturing logger for Warning-shape pins. */
export class CapturingTestLogger implements ILogger {
  readonly warnings: Array<{ message: string; bindings?: LogBindings }> = [];
  readonly errors: Array<{ message: string; bindings?: LogBindings }> = [];

  trace(_message: string, _bindings?: LogBindings): void {}
  debug(_message: string, _bindings?: LogBindings): void {}
  info(_message: string, _bindings?: LogBindings): void {}
  warn(message: string, bindings?: LogBindings): void {
    this.warnings.push({ message, bindings });
  }
  error(message: string, bindings?: LogBindings): void {
    this.errors.push({ message, bindings });
  }
  fatal(_message: string, _bindings?: LogBindings): void {}
  child(_bindings: LogBindings): ILogger {
    return this;
  }
}

/** Factory for {@link CapturingTestLogger}. */
export function createCapturingTestLogger(): CapturingTestLogger {
  return new CapturingTestLogger();
}

type TestCallRecord = {
  method: string;
  args: unknown[];
  signal?: AbortSignal;
};

/**
 * Faithful in-memory {@link ILocalCache} double with call recording and
 * force-fail hooks. Replace-trigger: prefer IT when a branch is exercised
 * against real DefaultLocalCache.
 */
export class LocalCacheTestDouble implements ILocalCache {
  readonly calls: TestCallRecord[] = [];
  readonly store = new Map<string, unknown>();
  disposed = false;
  forceFailNext?: string;
  forceFailMethods = new Set<string>();
  /** One-shot canned result override per method name. */
  nextResult: Partial<Record<string, D2Result<unknown>>> = {};

  private record(method: string, args: unknown[], signal?: AbortSignal): void {
    this.calls.push({ method, args, signal });
  }

  private maybeOverride<T>(method: string): D2Result<T> | undefined {
    const canned = this.nextResult[method];

    if (canned !== undefined) {
      delete this.nextResult[method];

      return canned as D2Result<T>;
    }

    if (this.forceFailNext === method || this.forceFailMethods.has(method)) {
      if (this.forceFailNext === method) {
        this.forceFailNext = undefined;
      }

      return serviceUnavailable({
        errorCode: "L1_DOWN",
      }) as D2Result<T>;
    }

    return undefined;
  }

  async get<T>(key: string, signal?: AbortSignal): Promise<D2Result<T>> {
    this.record("get", [key], signal);
    const override = this.maybeOverride<T>("get");

    if (override !== undefined) {
      return override;
    }

    if (!this.store.has(key)) {
      return notFound();
    }

    return ok(this.store.get(key) as T);
  }

  async getMany<T>(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>> {
    this.record("getMany", [keys], signal);
    const override = this.maybeOverride<ReadonlyMap<string, T>>("getMany");

    if (override !== undefined) {
      return override;
    }

    const hits = new Map<string, T>();

    for (const key of keys) {
      if (this.store.has(key)) {
        hits.set(key, this.store.get(key) as T);
      }
    }

    if (hits.size === 0) {
      return notFound();
    }

    if (hits.size === keys.length) {
      return ok(hits as ReadonlyMap<string, T>);
    }

    return someFound({ data: hits as ReadonlyMap<string, T> });
  }

  async exists(key: string, signal?: AbortSignal): Promise<D2Result<boolean>> {
    this.record("exists", [key], signal);
    const override = this.maybeOverride<boolean>("exists");

    if (override !== undefined) {
      return override;
    }

    return ok(this.store.has(key));
  }

  async getTtl(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number | undefined>> {
    this.record("getTtl", [key], signal);
    const override = this.maybeOverride<number | undefined>("getTtl");

    if (override !== undefined) {
      return override;
    }

    if (!this.store.has(key)) {
      return notFound();
    }

    return ok(undefined);
  }

  async set<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("set", [key, value, expirationMs], signal);
    const override = this.maybeOverride<void>("set");

    if (override !== undefined) {
      return override;
    }

    this.store.set(key, value);

    return ok();
  }

  async setMany<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("setMany", [entries, expirationMs], signal);
    const override = this.maybeOverride<void>("setMany");

    if (override !== undefined) {
      return override;
    }

    for (const [k, v] of entries) {
      this.store.set(k, v);
    }

    return ok();
  }

  async remove(key: string, signal?: AbortSignal): Promise<D2Result> {
    this.record("remove", [key], signal);
    const override = this.maybeOverride<void>("remove");

    if (override !== undefined) {
      return override;
    }

    this.store.delete(key);

    return ok();
  }

  async removeMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("removeMany", [keys], signal);
    const override = this.maybeOverride<void>("removeMany");

    if (override !== undefined) {
      return override;
    }

    for (const key of keys) {
      this.store.delete(key);
    }

    return ok();
  }

  async setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    this.record("setNx", [key, value, expirationMs], signal);
    const override = this.maybeOverride<boolean>("setNx");

    if (override !== undefined) {
      return override;
    }

    if (this.store.has(key)) {
      return ok(false);
    }

    this.store.set(key, value);

    return ok(true);
  }

  async increment(
    key: string,
    amount = 1,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    this.record("increment", [key, amount, expirationMs], signal);
    const override = this.maybeOverride<number>("increment");

    if (override !== undefined) {
      return override;
    }

    const current = this.store.has(key) ? Number(this.store.get(key)) : 0;
    const next = current + amount;
    this.store.set(key, next);

    return ok(next);
  }

  async acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    this.record("acquireLock", [key, lockId, expirationMs], signal);

    return ok(true);
  }

  async releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("releaseLock", [key, lockId], signal);

    return ok();
  }

  markDisposed(): void {
    this.disposed = true;
  }

  called(method: string): boolean {
    return this.calls.some((c) => c.method === method);
  }

  callsOf(method: string): TestCallRecord[] {
    return this.calls.filter((c) => c.method === method);
  }
}

/** Factory for {@link LocalCacheTestDouble}. */
export function createLocalCacheTestDouble(): LocalCacheTestDouble {
  return new LocalCacheTestDouble();
}

/**
 * Faithful in-memory {@link IDistributedCache} double with call recording.
 * Implements getMany ladder, setNx true/false, increment, locks, and
 * ICacheSet stubs. Replace-trigger: prefer IT against real Redis.
 */
export class DistributedCacheTestDouble implements IDistributedCache {
  readonly calls: TestCallRecord[] = [];
  readonly store = new Map<string, unknown>();
  readonly locks = new Map<string, string>();
  disposed = false;
  forceFailNext?: string;
  forceFailMethods = new Set<string>();
  /** When set, getMany returns someFound with only these keys. */
  getManyPartialOnly?: Set<string>;
  /** One-shot canned result override per method name. */
  nextResult: Partial<Record<string, D2Result<unknown>>> = {};

  private record(method: string, args: unknown[], signal?: AbortSignal): void {
    this.calls.push({ method, args, signal });
  }

  private maybeOverride<T>(method: string): D2Result<T> | undefined {
    const canned = this.nextResult[method];

    if (canned !== undefined) {
      delete this.nextResult[method];

      return canned as D2Result<T>;
    }

    if (this.forceFailNext === method || this.forceFailMethods.has(method)) {
      if (this.forceFailNext === method) {
        this.forceFailNext = undefined;
      }

      return serviceUnavailable({
        errorCode: "L2_DOWN",
      }) as D2Result<T>;
    }

    return undefined;
  }

  async get<T>(key: string, signal?: AbortSignal): Promise<D2Result<T>> {
    this.record("get", [key], signal);
    const override = this.maybeOverride<T>("get");

    if (override !== undefined) {
      return override;
    }

    if (!this.store.has(key)) {
      return notFound();
    }

    return ok(this.store.get(key) as T);
  }

  async getMany<T>(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>> {
    this.record("getMany", [keys], signal);
    const override = this.maybeOverride<ReadonlyMap<string, T>>("getMany");

    if (override !== undefined) {
      return override;
    }

    const hits = new Map<string, T>();

    for (const key of keys) {
      if (this.getManyPartialOnly !== undefined) {
        if (this.getManyPartialOnly.has(key) && this.store.has(key)) {
          hits.set(key, this.store.get(key) as T);
        }
      } else if (this.store.has(key)) {
        hits.set(key, this.store.get(key) as T);
      }
    }

    if (hits.size === 0) {
      return notFound();
    }

    if (hits.size === keys.length) {
      return ok(hits as ReadonlyMap<string, T>);
    }

    return someFound({ data: hits as ReadonlyMap<string, T> });
  }

  async exists(key: string, signal?: AbortSignal): Promise<D2Result<boolean>> {
    this.record("exists", [key], signal);
    const override = this.maybeOverride<boolean>("exists");

    if (override !== undefined) {
      return override;
    }

    return ok(this.store.has(key));
  }

  async getTtl(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number | undefined>> {
    this.record("getTtl", [key], signal);
    const override = this.maybeOverride<number | undefined>("getTtl");

    if (override !== undefined) {
      return override;
    }

    if (!this.store.has(key)) {
      return notFound();
    }

    return ok(5000);
  }

  async set<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("set", [key, value, expirationMs], signal);
    const override = this.maybeOverride<void>("set");

    if (override !== undefined) {
      return override;
    }

    this.store.set(key, value);

    return ok();
  }

  async setMany<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("setMany", [entries, expirationMs], signal);
    const override = this.maybeOverride<void>("setMany");

    if (override !== undefined) {
      return override;
    }

    for (const [k, v] of entries) {
      this.store.set(k, v);
    }

    return ok();
  }

  async remove(key: string, signal?: AbortSignal): Promise<D2Result> {
    this.record("remove", [key], signal);
    const override = this.maybeOverride<void>("remove");

    if (override !== undefined) {
      return override;
    }

    this.store.delete(key);

    return ok();
  }

  async removeMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("removeMany", [keys], signal);
    const override = this.maybeOverride<void>("removeMany");

    if (override !== undefined) {
      return override;
    }

    for (const key of keys) {
      this.store.delete(key);
    }

    return ok();
  }

  async setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    this.record("setNx", [key, value, expirationMs], signal);
    const override = this.maybeOverride<boolean>("setNx");

    if (override !== undefined) {
      return override;
    }

    if (this.store.has(key)) {
      return ok(false);
    }

    this.store.set(key, value);

    return ok(true);
  }

  async increment(
    key: string,
    amount = 1,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    this.record("increment", [key, amount, expirationMs], signal);
    const override = this.maybeOverride<number>("increment");

    if (override !== undefined) {
      return override;
    }

    const current = this.store.has(key) ? Number(this.store.get(key)) : 0;
    const next = current + amount;
    this.store.set(key, next);

    return ok(next);
  }

  async acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    this.record("acquireLock", [key, lockId, expirationMs], signal);
    const override = this.maybeOverride<boolean>("acquireLock");

    if (override !== undefined) {
      return override;
    }

    if (this.locks.has(key)) {
      return ok(false);
    }

    this.locks.set(key, lockId);

    return ok(true);
  }

  async releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("releaseLock", [key, lockId], signal);
    const override = this.maybeOverride<void>("releaseLock");

    if (override !== undefined) {
      return override;
    }

    if (this.locks.get(key) === lockId) {
      this.locks.delete(key);
    }

    return ok();
  }

  // ICacheSet stubs (tiered does not call these)
  async setAdd(
    key: string,
    member: string,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    this.record("setAdd", [key, member, expirationMs], signal);

    return ok(true);
  }

  async setRemove(
    key: string,
    member: string,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    this.record("setRemove", [key, member], signal);

    return ok(true);
  }

  async setContains(
    key: string,
    member: string,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    this.record("setContains", [key, member], signal);

    return ok(false);
  }

  async setCardinality(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    this.record("setCardinality", [key], signal);

    return ok(0);
  }

  // Broadcast stubs - tiered must NOT call these
  async setAndBroadcast<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("setAndBroadcast", [key, value, expirationMs], signal);

    return ok();
  }

  async setManyAndBroadcast<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("setManyAndBroadcast", [entries, expirationMs], signal);

    return ok();
  }

  async removeAndBroadcast(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("removeAndBroadcast", [key], signal);

    return ok();
  }

  async removeManyAndBroadcast(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    this.record("removeManyAndBroadcast", [keys], signal);

    return ok();
  }

  markDisposed(): void {
    this.disposed = true;
  }

  called(method: string): boolean {
    return this.calls.some((c) => c.method === method);
  }

  callsOf(method: string): TestCallRecord[] {
    return this.calls.filter((c) => c.method === method);
  }
}

/** Factory for {@link DistributedCacheTestDouble}. */
export function createDistributedCacheTestDouble(): DistributedCacheTestDouble {
  return new DistributedCacheTestDouble();
}

type TestInvalidationHandler = (
  key: string,
  signal?: AbortSignal,
) => void | Promise<void>;

/**
 * Faithful multi-handler {@link ICacheInvalidationBackplane} double.
 * Everyone-acts: publish delivers to all handlers including publisher.
 * Dispose unsubscribes. Replace-trigger: prefer IT against real Redis.
 */
export class BackplaneTestDouble implements ICacheInvalidationBackplane {
  readonly handlers = new Set<TestInvalidationHandler>();
  /**
   * Per-subscription AbortController — mirrors production backplane where
   * handlers receive the **subscription** signal, not the publish signal.
   */
  private readonly handlerControllers = new Map<
    TestInvalidationHandler,
    AbortController
  >();
  readonly published: string[] = [];
  readonly publishedMany: string[][] = [];
  /** Signals forwarded into `publishInvalidation*` (publish contract pin). */
  readonly publishedSignals: Array<AbortSignal | undefined> = [];
  subscribeCount = 0;
  disposed = false;
  forceFailPublish = false;

  subscribe(handler: TestInvalidationHandler): AsyncDisposable {
    this.subscribeCount++;
    this.handlers.add(handler);
    const ac = new AbortController();
    this.handlerControllers.set(handler, ac);
    let unsubscribed = false;

    return {
      [Symbol.asyncDispose]: async () => {
        if (unsubscribed) {
          return;
        }

        unsubscribed = true;
        ac.abort();
        this.handlers.delete(handler);
        this.handlerControllers.delete(handler);
      },
    };
  }

  async publishInvalidation(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (this.forceFailPublish) {
      return serviceUnavailable({ errorCode: "BACKPLANE_DOWN" });
    }

    this.published.push(key);
    this.publishedSignals.push(signal);
    await this.deliver(key);

    return ok();
  }

  async publishInvalidationMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (this.forceFailPublish) {
      return serviceUnavailable({ errorCode: "BACKPLANE_DOWN" });
    }

    this.publishedMany.push([...keys]);
    this.publishedSignals.push(signal);

    for (const key of keys) {
      this.published.push(key);
      await this.deliver(key);
    }

    return ok();
  }

  async dispose(): Promise<void> {
    this.disposed = true;

    for (const ac of this.handlerControllers.values()) {
      ac.abort();
    }

    this.handlerControllers.clear();
    this.handlers.clear();
  }

  async [Symbol.asyncDispose](): Promise<void> {
    await this.dispose();
  }

  /**
   * Delivers to handlers with each handler's **subscription** AbortSignal
   * (real Redis backplane contract), never the publish call's signal.
   */
  private async deliver(key: string): Promise<void> {
    for (const handler of [...this.handlers]) {
      try {
        const subSignal = this.handlerControllers.get(handler)?.signal;
        await handler(key, subSignal);
      } catch {
        // Handler isolation - continue other handlers.
      }
    }
  }
}

/** Factory for {@link BackplaneTestDouble}. */
export function createBackplaneTestDouble(): BackplaneTestDouble {
  return new BackplaneTestDouble();
}
