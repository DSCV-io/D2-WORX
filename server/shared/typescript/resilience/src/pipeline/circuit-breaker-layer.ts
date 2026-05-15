// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { CircuitBreaker } from "../circuit-breaker/circuit-breaker.js";
import type { CircuitBreakerOptions } from "../circuit-breaker/circuit-breaker-options.js";
import type { IResilientLayer } from "./i-resilient-layer.js";

/** Layer wrapping an inner layer with a per-key circuit breaker. */
export class CircuitBreakerLayer implements IResilientLayer {
  private readonly breakers = new Map<string, CircuitBreaker<unknown>>();

  constructor(
    private readonly inner: IResilientLayer,
    private readonly opts: CircuitBreakerOptions,
  ) {}

  execute<T>(key: string, op: () => Promise<T>): Promise<T> {
    let breaker = this.breakers.get(key);
    if (breaker === undefined) {
      breaker = new CircuitBreaker<unknown>(this.opts);
      this.breakers.set(key, breaker);
    }
    return breaker.execute(() => this.inner.execute(key, op)) as Promise<T>;
  }
}
