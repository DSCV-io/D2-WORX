// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { D2Result } from "@d2/result";

import { RETRY_DEFAULTS } from "./retry-defaults.js";
import type { RetryOptions } from "./retry-options.js";

const sr_emptyAbort = (signal: AbortSignal | undefined): boolean =>
  signal?.aborted === true;

function defaultDelay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (sr_emptyAbort(signal)) return reject(new Error("aborted"));
    const handle = setTimeout(resolve, ms);
    signal?.addEventListener(
      "abort",
      () => {
        clearTimeout(handle);
        reject(new Error("aborted"));
      },
      { once: true },
    );
  });
}

function isCancellation(err: unknown): boolean {
  if (err instanceof Error) {
    if (err.name === "AbortError") return true;
    if (err.message === "aborted") return true;
  }
  return false;
}

function computeDelay<T>(
  attempt: number,
  opts: RetryOptions<T>,
  rng: () => number,
): number {
  const base = opts.baseDelayMs * opts.backoffMultiplier ** (attempt - 1);
  const capped = Math.min(base, opts.maxDelayMs);
  if (opts.jitter <= 0) return capped;
  const factor = 1 + (rng() * 2 - 1) * opts.jitter;
  return Math.max(0, capped * factor);
}

interface InternalCallOpts<T> {
  readonly opts: RetryOptions<T>;
  readonly signal?: AbortSignal;
  readonly rng: () => number;
}

/**
 * Retry helper. Mirrors .NET `RetryHelper`. Two entry points:
 * `retryAsync` (throw-based) and `retryD2ResultAsync`
 * (D2Result-aware — only retries when the failed result has the supplied
 * transient predicate satisfied).
 */
export const RetryHelper = {
  /**
   * Retry an async function. Re-throws when no policy match remains
   * (max attempts exhausted, non-transient, or canceled).
   */
  async retryAsync<T>(
    op: (attempt: number) => Promise<T>,
    opts: Partial<RetryOptions<T>> = {},
    signal?: AbortSignal,
    rng: () => number = Math.random,
  ): Promise<T> {
    const merged = mergeOptions(opts);
    return runRetry<T>(op, { opts: merged, signal, rng });
  },

  /**
   * Retry a function returning a `D2Result<T>`. Only retries when the
   * returned result is failure AND the supplied `shouldRetry` (or
   * `isTransient`) predicate returns true. Mirrors the .NET
   * `RetryD2ResultAsync` carve-out.
   */
  async retryD2ResultAsync<T>(
    op: (attempt: number) => Promise<D2Result<T>>,
    opts: Partial<RetryOptions<D2Result<T>>> = {},
    signal?: AbortSignal,
    rng: () => number = Math.random,
  ): Promise<D2Result<T>> {
    const merged = mergeOptions(opts);
    return runRetry<D2Result<T>>(
      async (a) => {
        const r = await op(a);
        if (r.failed && shouldRetryValue(r, merged)) {
          // Use a sentinel error to drive the retry loop without losing
          // the typed result on the final attempt.
          throw new RetryableResultMarker(r);
        }
        return r;
      },
      { opts: merged, signal, rng },
    ).catch((e) => {
      if (e instanceof RetryableResultMarker) return e.result as D2Result<T>;
      throw e;
    });
  },
} as const;

class RetryableResultMarker {
  constructor(readonly result: unknown) {}
}

function shouldRetryValue<T>(value: T, opts: RetryOptions<T>): boolean {
  if (opts.shouldRetry) return opts.shouldRetry(value);
  if (opts.isTransient) return opts.isTransient(value);
  return false;
}

function shouldRetryError<T>(err: unknown, opts: RetryOptions<T>): boolean {
  if (isCancellation(err)) return false;
  if (opts.shouldRetry) return opts.shouldRetry(err);
  if (opts.isTransient) return opts.isTransient(err);
  return true;
}

function mergeOptions<T>(opts: Partial<RetryOptions<T>>): RetryOptions<T> {
  return {
    maxAttempts: opts.maxAttempts ?? RETRY_DEFAULTS.maxAttempts,
    baseDelayMs: opts.baseDelayMs ?? RETRY_DEFAULTS.baseDelayMs,
    backoffMultiplier:
      opts.backoffMultiplier ?? RETRY_DEFAULTS.backoffMultiplier,
    maxDelayMs: opts.maxDelayMs ?? RETRY_DEFAULTS.maxDelayMs,
    jitter: opts.jitter ?? RETRY_DEFAULTS.jitter,
    shouldRetry: opts.shouldRetry,
    isTransient: opts.isTransient,
    delayFunc: opts.delayFunc ?? defaultDelay,
  };
}

async function runRetry<T>(
  op: (attempt: number) => Promise<T>,
  ctx: InternalCallOpts<T>,
): Promise<T> {
  const { opts, signal, rng } = ctx;
  if (opts.maxAttempts < 1) throw new RangeError("maxAttempts must be ≥ 1");
  let attempt = 0;
  while (true) {
    attempt++;
    if (sr_emptyAbort(signal)) throw new Error("aborted");
    try {
      return await op(attempt);
    } catch (e) {
      if (e instanceof RetryableResultMarker) {
        // RetryableResult is always retryable until exhaustion.
        if (attempt === opts.maxAttempts) throw e;
      } else if (!shouldRetryError(e, opts) || attempt === opts.maxAttempts) {
        throw e;
      }
      const delay = computeDelay(attempt, opts, rng);
      await opts.delayFunc!(delay, signal);
    }
  }
}
