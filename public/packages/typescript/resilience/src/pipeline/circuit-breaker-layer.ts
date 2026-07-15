// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { CircuitBreaker } from "../circuit-breaker/circuit-breaker.js";
import type { CircuitBreakerOptions } from "../circuit-breaker/circuit-breaker-options.js";
import type { IResilientLayer } from "./i-resilient-layer.js";

/**
 * Layer wrapping an inner layer with a per-key circuit breaker. Mirrors .NET
 * `CircuitBreakerLayer<TKey, TValue>` → `CircuitBreaker.ExecuteAsync(next, ct)`.
 *
 * The caller signal threads to the inner op; a caller abort surfaces from the
 * inner op as `AbortError`. As in .NET (`catch { RecordFailure(); throw; }`),
 * the breaker counts an aborted attempt as a failure and re-throws it.
 *
 * The layer calls `breaker.execute(op)` with NO fallback — exactly as the .NET
 * `CircuitBreakerLayer` calls `ExecuteAsync(next, ct)` without one — so an open
 * (or probe-slot-taken) breaker throws `CircuitOpenError` to the pipeline
 * boundary for the caller to map. The breaker's `isFailure` / `onStateChange`
 * seams (when set on the supplied options) thread through unchanged.
 */
export class CircuitBreakerLayer implements IResilientLayer {
  private readonly breakers = new Map<string, CircuitBreaker<unknown>>();

  constructor(
    private readonly inner: IResilientLayer,
    private readonly opts: CircuitBreakerOptions,
  ) {}

  execute<T>(
    key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    let breaker = this.breakers.get(key);
    if (breaker === undefined) {
      breaker = new CircuitBreaker<unknown>(this.opts);
      this.breakers.set(key, breaker);
    }
    return breaker.execute(() =>
      this.inner.execute(key, op, signal),
    ) as Promise<T>;
  }
}
