// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CircuitState } from "./circuit-state.js";

/**
 * Circuit breaker config. Mirrors .NET `CircuitBreakerOptions` (plus the
 * `isFailure` / `onStateChange` seams .NET passes as `CircuitBreaker<T>` ctor
 * parameters — colocated here so the single options object carries the full
 * breaker configuration).
 */
export interface CircuitBreakerOptions<T = unknown> {
  /** Failures (back-to-back) needed to trip from Closed → Open. */
  readonly failureThreshold: number;
  /** Cooldown (ms) Open spends before transitioning to HalfOpen. */
  readonly cooldownMs: number;
  /** Clock function returning a millisecond timestamp; tests inject a fake. */
  readonly nowFunc?: () => number;
  /**
   * Value-based failure predicate. Thrown errors ALWAYS count as failures;
   * this predicate adds value-based failures for operations that surface a
   * failure WITHOUT throwing (e.g. `(r) => !r.success` for a `D2Result`).
   * A returned value satisfying the predicate increments the failure counter
   * and is then returned to the caller unchanged (it is NOT re-thrown).
   * Mirrors .NET `CircuitBreaker<T>`'s `isFailure` ctor parameter.
   */
  readonly isFailure?: (result: T) => boolean;
  /**
   * Callback fired synchronously on every REAL state transition (an
   * idempotent Closed→Closed on repeated success does NOT fire). Mirrors
   * .NET `CircuitBreaker<T>`'s `onStateChange` ctor parameter; the canonical
   * observability seam (the breaker emits no spans/metrics/logs of its own).
   *
   * Footgun: a THROWING callback propagates out of `execute()` and REPLACES
   * the upstream error (or the operation's return) — a buggy logger/metric
   * emitter here can swap a meaningful upstream failure with its own error,
   * making outage diagnosis painful. Keep the body to non-throwing log/metric
   * calls, or wrap it in your own try/catch. Mirrors the .NET remark.
   */
  readonly onStateChange?: (from: CircuitState, to: CircuitState) => void;
}
