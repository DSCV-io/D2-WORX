// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { D2Result } from "@dcsv-io/d2-result";

import { AbortError } from "../pipeline/abort.js";
import { RETRY_DEFAULTS } from "./retry-defaults.js";
import type { RetryOptions } from "./retry-options.js";

const sr_emptyAbort = (signal: AbortSignal | undefined): boolean =>
  signal?.aborted === true;

function defaultDelay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    if (sr_emptyAbort(signal)) return reject(new AbortError());
    const handle = setTimeout(resolve, ms);
    signal?.addEventListener(
      "abort",
      () => {
        clearTimeout(handle);
        reject(new AbortError());
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

/**
 * Well-known fetch network-failure messages. A network-level `fetch` failure
 * throws a `TypeError` whose message is one of these (undici uses
 * `"fetch failed"`; browsers vary). Matched only as a SECONDARY signal — see
 * {@link defaultIsTransient} — so a plain programming `TypeError` (different
 * message, no `cause`) is never misclassified as transient.
 */
const sr_fetchNetworkMessages: ReadonlySet<string> = new Set([
  "fetch failed", // undici / Node fetch
  "Failed to fetch", // Chromium
  "NetworkError when attempting to fetch resource.", // Firefox
  "Load failed", // Safari / WebKit
]);

/**
 * Default transient-error classifier — the conservative whitelist used when a
 * caller supplies NEITHER `shouldRetry` NOR `isTransient`. Mirrors the INTENT
 * of .NET `RetryHelper.IsTransientException` (HTTP ≥500/429/408,
 * `SocketException`, `TimeoutException`, `TaskCanceledException-from-timeout`,
 * `CircuitOpenException`): retry ONLY genuine transient / network / timeout
 * conditions, NEVER arbitrary programming bugs.
 *
 * The JS error taxonomy differs from .NET's (no `HttpRequestException` /
 * `SocketException` types), so the TS transient set — matched by error `name`
 * to stay robust across module-instance / structural-error boundaries, the
 * same convention as {@link isCancellation} — is:
 *
 * - `TimeoutError` (our {@link import("../pipeline/timeout-layer.js").TimeoutError}'s
 *   `name`) — the per-attempt-timeout analogue of .NET's `TimeoutException`.
 * - `CircuitOpenError` (`name`) — mirrors .NET's `CircuitOpenException`
 *   (the retry-OUTSIDE-CB restart-recovery composition).
 * - A genuine fetch/undici NETWORK failure — a `TypeError` carrying a `cause`
 *   (undici attaches a system error, e.g. `ECONNREFUSED` / `ENOTFOUND` /
 *   `ECONNRESET`) OR whose message is a well-known fetch network-failure
 *   string ({@link sr_fetchNetworkMessages}). A bare `TypeError` with neither
 *   signal is a programming bug → NOT transient.
 * - `name === "NetworkError"` — the DOM/whatwg network-error name.
 *
 * Everything else (plain `Error`, `RangeError`, assertion failures, a
 * programming `TypeError`) is NON-transient and is NOT retried by default.
 * A caller cancellation (`AbortError` / message `"aborted"`) is handled BEFORE
 * this classifier by {@link isCancellation} and is never retried.
 */
export function defaultIsTransient(err: unknown): boolean {
  if (!(err instanceof Error)) return false;
  if (err.name === "TimeoutError") return true;
  if (err.name === "CircuitOpenError") return true;
  if (err.name === "NetworkError") return true;
  if (err instanceof TypeError) {
    // Network-level fetch failure — distinguished from a programming TypeError
    // by an attached `cause` (undici system error) or a known network message.
    if (err.cause !== undefined) return true;
    return sr_fetchNetworkMessages.has(err.message);
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
  // No caller-supplied predicate → conservative default whitelist (mirrors
  // .NET's `IsTransientException`): retry only genuine transient/network/
  // timeout conditions, never programming bugs.
  return defaultIsTransient(err);
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
    if (sr_emptyAbort(signal)) throw new AbortError();
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
