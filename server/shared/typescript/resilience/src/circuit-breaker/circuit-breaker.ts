// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CircuitBreakerOptions } from "./circuit-breaker-options.js";
import { CircuitOpenError } from "./circuit-open-error.js";
import { CircuitState } from "./circuit-state.js";

/**
 * Three-state circuit breaker. Mirrors .NET `CircuitBreaker<T>`.
 * Closed → after `failureThreshold` consecutive failures → Open. Open →
 * after `cooldownMs` → HalfOpen. HalfOpen → success → Closed (resets
 * counter); HalfOpen → failure → Open (cooldown re-armed).
 */
export class CircuitBreaker<T> {
  private state: CircuitState = CircuitState.Closed;
  private consecutiveFailures = 0;
  private openedAt = 0;
  private readonly now: () => number;

  constructor(private readonly opts: CircuitBreakerOptions) {
    if (opts.failureThreshold < 1)
      throw new RangeError("failureThreshold must be ≥ 1");
    if (opts.cooldownMs < 0) throw new RangeError("cooldownMs must be ≥ 0");
    this.now = opts.nowFunc ?? Date.now;
  }

  /** Current state — for tests + observability. */
  get currentState(): CircuitState {
    if (this.state === CircuitState.Open && this.cooldownExpired())
      return CircuitState.HalfOpen;
    return this.state;
  }

  async execute(op: () => Promise<T>): Promise<T> {
    const visible = this.currentState;
    if (visible === CircuitState.Open) throw new CircuitOpenError();
    if (visible === CircuitState.HalfOpen) {
      this.state = CircuitState.HalfOpen;
    }

    try {
      const result = await op();
      this.onSuccess();
      return result;
    } catch (err) {
      this.onFailure();
      throw err;
    }
  }

  private cooldownExpired(): boolean {
    return this.now() - this.openedAt >= this.opts.cooldownMs;
  }

  private onSuccess(): void {
    this.consecutiveFailures = 0;
    this.state = CircuitState.Closed;
  }

  private onFailure(): void {
    if (this.state === CircuitState.HalfOpen) {
      // Single failure in HalfOpen re-arms the cooldown.
      this.openedAt = this.now();
      this.state = CircuitState.Open;
      return;
    }
    this.consecutiveFailures++;
    if (this.consecutiveFailures >= this.opts.failureThreshold) {
      this.openedAt = this.now();
      this.state = CircuitState.Open;
    }
  }
}
