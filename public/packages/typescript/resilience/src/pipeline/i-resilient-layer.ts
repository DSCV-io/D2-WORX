// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * One layer in a {@link ResilientPipeline}. Wraps the inner async op with its
 * own resilience concern (singleflight / breaker / retry / timeout / rate-limit)
 * and delegates downward.
 *
 * Mirrors .NET `IResilientLayer<TKey, TValue>.WrapAsync(key, next, ct)`: the
 * caller threads an optional {@link AbortSignal} (≈ `CancellationToken`) and the
 * op receives the signal it should observe for cooperative cancellation. A layer
 * may replace the signal it passes inward (e.g. {@link TimeoutLayer} hands the op
 * a signal linked to both the caller signal and its own timeout) — exactly as the
 * .NET `TimeoutLayer` passes a linked `CancellationToken` down.
 */
export interface IResilientLayer {
  /**
   * @param key Per-call key (used by Singleflight; ignored by other layers).
   * @param op  The inner op. Receives the {@link AbortSignal} it should observe
   *            (may be `undefined` when no cancellation is in play).
   * @param signal Optional caller cancellation signal threaded down the stack.
   */
  execute<T>(
    key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T>;
}
