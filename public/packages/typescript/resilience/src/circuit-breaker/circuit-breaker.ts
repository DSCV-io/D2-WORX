// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { CircuitBreakerOptions } from "./circuit-breaker-options.js";
import { CircuitOpenError } from "./circuit-open-error.js";
import { CircuitState } from "./circuit-state.js";

/**
 * Three-state circuit breaker. Mirrors .NET `CircuitBreaker<T>`.
 * Closed → after `failureThreshold` consecutive failures → Open. Open →
 * after `cooldownMs` → HalfOpen. HalfOpen → success → Closed (resets
 * counter); HalfOpen → failure → Open (cooldown re-armed).
 *
 * HalfOpen admits exactly ONE probe at a time: the first caller to reach the
 * HalfOpen slot runs the operation; concurrent callers (who observe the probe
 * already in flight) receive the `fallback` result (when one is supplied) or a
 * {@link CircuitOpenError}. JS is single-threaded, so a plain boolean flag —
 * check-and-set synchronously BEFORE the first `await` — is the structural
 * equivalent of .NET's lock-free `Interlocked.CompareExchange` on its
 * probe-in-flight flag.
 *
 * Both thrown errors and returned values satisfying {@link CircuitBreakerOptions.isFailure}
 * count as failures (the value is still returned, not re-thrown).
 */
export class CircuitBreaker<T> {
  private state: CircuitState = CircuitState.Closed;
  private consecutiveFailures = 0;
  private openedAt = 0;
  private probeInFlight = false;
  private readonly now: () => number;

  constructor(private readonly opts: CircuitBreakerOptions<T>) {
    if (opts.failureThreshold < 1)
      throw new RangeError("failureThreshold must be ≥ 1");
    if (opts.cooldownMs < 0) throw new RangeError("cooldownMs must be ≥ 0");
    this.now = opts.nowFunc ?? Date.now;
  }

  /**
   * Current state — for tests + observability. A pure read: when the circuit
   * is Open and the cooldown has elapsed it REPORTS HalfOpen (the state the
   * next {@link execute} would transition into) without mutating or firing
   * {@link CircuitBreakerOptions.onStateChange}; the actual transition (and the
   * callback) happen inside {@link execute}.
   */
  get currentState(): CircuitState {
    if (this.state === CircuitState.Open && this.cooldownExpired())
      return CircuitState.HalfOpen;
    return this.state;
  }

  /**
   * Executes `op` through the breaker. When the circuit is Open — or HalfOpen
   * with a probe already in flight — returns `fallback()` if supplied, else
   * throws {@link CircuitOpenError}. Mirrors .NET
   * `ExecuteAsync(operation, fallback?, ct)`.
   */
  async execute(
    op: () => Promise<T>,
    fallback?: () => T | Promise<T>,
  ): Promise<T> {
    // Open → HalfOpen once the cooldown has elapsed. Done here (not in the
    // getter) so the transition mutates state AND fires onStateChange exactly
    // once, mirroring .NET's ExecuteAsync Open→HalfOpen CompareExchange.
    if (this.state === CircuitState.Open && this.cooldownExpired())
      this.transitionTo(CircuitState.HalfOpen);

    // Fast-fail when Open.
    if (this.state === CircuitState.Open) {
      if (fallback !== undefined) return fallback();
      throw new CircuitOpenError();
    }

    // HalfOpen: admit exactly one probe. The check-and-set is synchronous
    // (before any await), so concurrent callers cannot both win the slot.
    if (this.state === CircuitState.HalfOpen) {
      if (this.probeInFlight) {
        if (fallback !== undefined) return fallback();
        throw new CircuitOpenError();
      }
      this.probeInFlight = true;
    }

    // Closed, or the HalfOpen probe winner: run the operation.
    try {
      const result = await op();
      if (this.opts.isFailure?.(result) === true) {
        // A returned-but-failed value counts toward the breaker WITHOUT
        // throwing — the caller still receives the value.
        this.onFailure();
        return result;
      }
      this.onSuccess();
      return result;
    } catch (err) {
      this.onFailure();
      throw err;
    }
  }

  /**
   * Manually resets the circuit to {@link CircuitState.Closed} and clears the
   * failure count + probe flag. Fires {@link CircuitBreakerOptions.onStateChange}
   * only when the state actually changed. Mirrors .NET `Reset()`.
   */
  reset(): void {
    this.consecutiveFailures = 0;
    this.probeInFlight = false;
    this.transitionTo(CircuitState.Closed);
  }

  private cooldownExpired(): boolean {
    return this.now() - this.openedAt >= this.opts.cooldownMs;
  }

  private onSuccess(): void {
    this.consecutiveFailures = 0;
    this.probeInFlight = false;
    this.transitionTo(CircuitState.Closed);
  }

  private onFailure(): void {
    this.probeInFlight = false;
    if (this.state === CircuitState.HalfOpen) {
      // Single failure in HalfOpen re-arms the cooldown.
      this.openedAt = this.now();
      this.transitionTo(CircuitState.Open);
      return;
    }
    this.consecutiveFailures++;
    if (this.consecutiveFailures >= this.opts.failureThreshold) {
      this.openedAt = this.now();
      this.transitionTo(CircuitState.Open);
    }
  }

  /**
   * Sets the state and fires {@link CircuitBreakerOptions.onStateChange} only
   * on a REAL transition (no-op when already in `to`) — mirrors .NET's
   * `prev != newState` guard around every `onStateChange` invocation.
   */
  private transitionTo(to: CircuitState): void {
    const from = this.state;
    if (from === to) return;
    this.state = to;
    this.opts.onStateChange?.(from, to);
  }
}
