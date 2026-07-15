// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { AbortError, throwIfAborted } from "./abort.js";
import type { IResilientLayer } from "./i-resilient-layer.js";

/**
 * Rate-limiter options. Mirrors .NET `RateLimiterOptions`.
 * `acquisitionTimeoutMs <= 0` means reject-fast (non-blocking try).
 * Default: `maxConcurrency = 100`, `acquisitionTimeoutMs = 0`.
 */
export interface RateLimiterOptions {
  /**
   * Maximum number of concurrent in-flight operations.
   * Must be >= 1. Default: 100.
   */
  readonly maxConcurrency: number;
  /**
   * How long (ms) to wait for a permit before rejecting.
   * `<= 0` = reject immediately when all slots are taken.
   * Default: 0.
   */
  readonly acquisitionTimeoutMs: number;
}

/** Default rate-limiter options. Mirrors .NET `RateLimiterOptions` defaults. */
export const RATE_LIMITER_DEFAULTS: RateLimiterOptions = {
  maxConcurrency: 100,
  acquisitionTimeoutMs: 0,
};

/**
 * Thrown by {@link RateLimiterLayer} when a permit could not be acquired within
 * the configured `acquisitionTimeoutMs`. Mirrors .NET `RateLimitRejectedException`.
 */
export class RateLimitRejectedError extends Error {
  constructor(message = "rate limit exceeded: too many concurrent operations") {
    super(message);
    this.name = "RateLimitRejectedError";
  }
}

/**
 * Outcome of a permit-acquisition attempt:
 * - `granted` — a permit was acquired (the caller MUST release it);
 * - `rejected` — the acquisition window elapsed with no free permit;
 * - `aborted` — the caller's signal fired while waiting (no permit consumed).
 */
type AcquireOutcome = "granted" | "rejected" | "aborted";

/**
 * A deferred permit waiter: resolves `granted` when a permit is handed to it,
 * `rejected` when the acquisition timeout fires, or `aborted` when the caller's
 * signal fires first. A waiter that resolves `rejected`/`aborted` is spliced out
 * of the queue BEFORE resolving, so a later `release()` can never hand it a
 * phantom permit.
 */
interface Waiter {
  resolve: (outcome: AcquireOutcome) => void;
}

/**
 * Pipeline layer bounding the number of concurrent in-flight operations to
 * {@link RateLimiterOptions.maxConcurrency}. A caller that cannot acquire a
 * permit within the configured acquisition window is rejected via
 * {@link RateLimitRejectedError} rather than queued indefinitely. Mirrors
 * .NET `RateLimiterLayer<TKey, TValue>`.
 *
 * **Client-side, in-process only.** This is admission control for outbound
 * calls — it limits concurrent pressure from this process on an upstream. It
 * is NOT the server-side distributed rate-limit middleware.
 *
 * The concurrency gate is hand-rolled (counter + FIFO waiter queue) because
 * TS/Node has no `SemaphoreSlim` equivalent in the standard library.
 *
 * Permits are released in a `finally` block — released on BOTH success and
 * throw, so no permits leak on inner-op failures.
 */
export class RateLimiterLayer implements IResilientLayer {
  private readonly maxConcurrency: number;
  private readonly acquisitionTimeoutMs: number;
  private active = 0;
  private readonly waiters: Waiter[] = [];

  constructor(
    private readonly inner: IResilientLayer,
    opts?: RateLimiterOptions,
  ) {
    const o = opts ?? RATE_LIMITER_DEFAULTS;
    if (o.maxConcurrency < 1)
      throw new RangeError("maxConcurrency must be >= 1");
    this.maxConcurrency = o.maxConcurrency;
    this.acquisitionTimeoutMs = o.acquisitionTimeoutMs;
  }

  async execute<T>(
    key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    // Pre-flight: an already-aborted caller never enters the gate.
    throwIfAborted(signal);
    const outcome = await this.tryAcquire(signal);
    if (outcome === "aborted") throw new AbortError();
    if (outcome === "rejected") throw new RateLimitRejectedError();
    try {
      return await this.inner.execute(key, op, signal);
    } finally {
      this.release();
    }
  }

  /**
   * Attempts to acquire a concurrency permit.
   * - If a slot is free → increments active and resolves `granted` immediately.
   * - If `acquisitionTimeoutMs <= 0` → resolves `rejected` immediately (reject-fast).
   * - Otherwise → enqueues a waiter and races a permit grant against an
   *   acquisition-timeout timer AND (when supplied) the caller's abort signal.
   *   Resolves `granted` if a permit arrives first, `rejected` on timeout, or
   *   `aborted` if the caller's signal fires first. The waiter is always spliced
   *   out of the queue before resolving `rejected`/`aborted` so a later
   *   `release()` cannot hand it a phantom permit.
   */
  private tryAcquire(signal?: AbortSignal): Promise<AcquireOutcome> {
    if (this.active < this.maxConcurrency) {
      this.active++;
      return Promise.resolve("granted");
    }
    if (this.acquisitionTimeoutMs <= 0) return Promise.resolve("rejected");

    return new Promise<AcquireOutcome>((resolve) => {
      const waiter: Waiter = { resolve };
      this.waiters.push(waiter);

      const dropWaiter = (): void => {
        // Remove the waiter from the queue (it may already have been dequeued
        // if release() ran first, in which case splice is a no-op).
        const idx = this.waiters.indexOf(waiter);
        if (idx !== -1) this.waiters.splice(idx, 1);
      };

      // Forward-declared so the timer + abort callbacks can detach each other;
      // assigned once `timer` exists (the callbacks only run asynchronously).
      let cleanup = (): void => {};

      const onAbort = (): void => {
        dropWaiter();
        cleanup();
        resolve("aborted");
      };

      const timer = setTimeout(() => {
        dropWaiter();
        cleanup();
        resolve("rejected");
      }, this.acquisitionTimeoutMs);

      signal?.addEventListener("abort", onAbort, { once: true });

      cleanup = (): void => {
        clearTimeout(timer);
        signal?.removeEventListener("abort", onAbort);
      };

      // Wrap resolve so a permit grant cancels the timer + abort listener.
      waiter.resolve = (outcome: AcquireOutcome) => {
        cleanup();
        resolve(outcome);
      };
    });
  }

  /**
   * Releases a concurrency permit. Grants it to the first queued waiter
   * (FIFO), or simply decrements the active counter.
   */
  private release(): void {
    const next = this.waiters.shift();
    if (next !== undefined) next.resolve("granted");
    else this.active--;
  }

  /**
   * Exposed for testing — the number of currently active (in-flight) operations.
   * Not part of the public API contract.
   */
  get activeCount(): number {
    return this.active;
  }
}
