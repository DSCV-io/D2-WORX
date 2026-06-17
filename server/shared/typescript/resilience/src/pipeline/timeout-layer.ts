// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { AbortError, linkAbort } from "./abort.js";
import type { IResilientLayer } from "./i-resilient-layer.js";

/**
 * Timeout options. Mirrors .NET `TimeoutOptions`.
 * `durationMs <= 0` disables the timeout — the layer becomes a pass-through.
 * Default: 10 000 ms.
 */
export interface TimeoutOptions {
  /** Wall-clock timeout in milliseconds. `<= 0` = disabled (pass-through). */
  readonly durationMs: number;
}

/** Default timeout options. Mirrors .NET `TimeoutOptions` defaults. */
export const TIMEOUT_DEFAULTS: TimeoutOptions = {
  durationMs: 10_000,
};

/**
 * Thrown when an operation exceeds the configured timeout. Mirrors the
 * C# `TimeoutException` the .NET `TimeoutLayer` throws.
 *
 * Distinct from a caller-abort (`AbortError` / message `"aborted"`) so that
 * outer retry layers can retry timeouts (transient) without treating them as
 * caller-initiated cancellations (which are never retried).
 */
export class TimeoutError extends Error {
  constructor(message = "operation timed out") {
    super(message);
    this.name = "TimeoutError";
  }
}

/**
 * Pipeline layer that bounds the inner operation with a wall-clock deadline AND
 * genuinely cancels it on expiry. Mirrors .NET `TimeoutLayer<TKey, TValue>`,
 * which links a `CancellationTokenSource` to the caller's token and passes the
 * linked token down.
 *
 * The op is handed a linked {@link AbortSignal} that aborts when EITHER the
 * caller's signal fires OR the timeout elapses. On expiry the layer aborts that
 * signal (so a cooperative op — e.g. a `fetch` — is actually canceled and its
 * socket released) AND rejects with {@link TimeoutError}. The deadline is still
 * deterministic: the inner promise is raced against the timer, so a
 * non-cooperative op (one that ignores its signal) still times out.
 *
 * A CALLER-initiated abort propagates as the caller's {@link AbortError}, NOT
 * masked as {@link TimeoutError} — preserving the abort-vs-timeout distinction
 * so an outer retry layer retries timeouts (transient) but never a caller abort.
 *
 * Position at TWO positions to express separate total-request and per-attempt
 * deadlines:
 * ```
 * builder
 *   .useRateLimiter(...)                      // outermost
 *   .useTimeout({ durationMs: 30_000 })       // total: bounds all retries
 *   .useRetries(...)
 *   .useCircuitBreaker(...)
 *   .useTimeout({ durationMs: 5_000 })        // per-attempt: inside retry loop
 *   .build()
 * ```
 *
 * `durationMs <= 0` disables the timeout — the layer is a pass-through (no timer
 * is created and the caller signal is forwarded unchanged).
 */
export class TimeoutLayer implements IResilientLayer {
  private readonly opts: TimeoutOptions;

  constructor(
    private readonly inner: IResilientLayer,
    opts?: TimeoutOptions,
  ) {
    this.opts = opts ?? TIMEOUT_DEFAULTS;
  }

  execute<T>(
    key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    if (this.opts.durationMs <= 0) return this.inner.execute(key, op, signal);

    const { durationMs } = this.opts;

    // Linked controller: aborts on the caller's signal OR on our timer — the
    // signal the inner op observes for cooperative cancellation.
    const linked = linkAbort(signal);

    return new Promise<T>((resolve, reject) => {
      let settled = false;

      const finish = (): void => {
        clearTimeout(timer);
        linked.dispose();
      };

      const timer = setTimeout(() => {
        if (settled) return;
        settled = true;
        // Cancel the inner op cooperatively, THEN reject deterministically.
        linked.controller.abort();
        finish();
        // Mirror C#'s `when (timeoutCts.IsCancellationRequested &&
        // !ct.IsCancellationRequested)`: if the caller also aborted, a caller
        // cancellation wins over the timeout (AbortError, not TimeoutError).
        reject(
          signal?.aborted === true
            ? new AbortError()
            : new TimeoutError(
                `Operation exceeded the configured timeout of ${durationMs} ms.`,
              ),
        );
      }, durationMs);

      this.inner.execute(key, op, linked.controller.signal).then(
        (value) => {
          if (settled) return;
          settled = true;
          finish();
          resolve(value);
        },
        (err: unknown) => {
          if (settled) return;
          settled = true;
          finish();
          // A caller-initiated abort must surface as the caller's AbortError,
          // never be reshaped into a TimeoutError. If the caller's signal is
          // the one that fired, normalize to AbortError; otherwise propagate
          // the inner error unchanged.
          reject(signal?.aborted === true ? new AbortError() : err);
        },
      );
    });
  }
}
