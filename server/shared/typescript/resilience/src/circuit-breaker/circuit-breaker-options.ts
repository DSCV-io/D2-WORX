// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Circuit breaker config. Mirrors .NET `CircuitBreakerOptions`.
 */
export interface CircuitBreakerOptions {
  /** Failures (back-to-back) needed to trip from Closed → Open. */
  readonly failureThreshold: number;
  /** Cooldown (ms) Open spends before transitioning to HalfOpen. */
  readonly cooldownMs: number;
  /** Clock function returning a millisecond timestamp; tests inject a fake. */
  readonly nowFunc?: () => number;
}
