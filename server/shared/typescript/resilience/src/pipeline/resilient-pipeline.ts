// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CircuitBreakerOptions } from "../circuit-breaker/circuit-breaker-options.js";
import type { RetryOptions } from "../retry/retry-options.js";
import { CircuitBreakerLayer } from "./circuit-breaker-layer.js";
import type { IResilientLayer } from "./i-resilient-layer.js";
import type { RateLimiterOptions } from "./rate-limiter-layer.js";
import { RateLimiterLayer } from "./rate-limiter-layer.js";
import { RetryLayer } from "./retry-layer.js";
import { SingleflightLayer } from "./singleflight-layer.js";
import type { TimeoutOptions } from "./timeout-layer.js";
import { TimeoutLayer } from "./timeout-layer.js";

/**
 * Final composed pipeline. Outer-first ordering.
 *
 * Use {@link ResilientPipelineBuilder} to compose layers. The canonical
 * full-stack order (outermost → innermost) mirrors the .NET standard:
 * ```
 * Singleflight → RateLimiter → TotalTimeout → Retry → CircuitBreaker → PerAttemptTimeout
 * ```
 *
 * **Order matters.** `CircuitBreaker` outside `Retry` trips after N total
 * failures across all attempts; `CircuitBreaker` inside `Retry` trips after
 * one burst of N consecutive per-attempt failures (re-arms on every outer
 * retry). Use `PassThrough` for a zero-layer pipeline that still executes the
 * op (suitable as a bypass sentinel).
 */
export class ResilientPipeline {
  constructor(private readonly head: IResilientLayer) {}

  /**
   * Executes `op` through the configured layer stack. The optional `signal`
   * (≈ .NET `CancellationToken`) threads down every layer; the op receives the
   * signal it should observe for cooperative cancellation (a {@link TimeoutLayer}
   * substitutes a signal that ALSO fires on its deadline).
   */
  execute<T>(
    key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    return this.head.execute(key, op, signal);
  }

  /**
   * A zero-layer pass-through pipeline. Runs the op directly with no
   * resilience wrapping. Mirrors .NET `ResilientPipeline.PassThrough`.
   *
   * Use as an explicit bypass: the caller opts for a raw call while retaining
   * the same `ResilientPipeline` call-site interface.
   */
  static get PassThrough(): ResilientPipeline {
    return new ResilientPipeline(new TerminalLayer());
  }
}

/** Innermost layer — runs the supplied op directly with the threaded signal. */
class TerminalLayer implements IResilientLayer {
  execute<T>(
    _key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    return op(signal);
  }
}

/**
 * Builder for {@link ResilientPipeline}. Outer-first ordering — the
 * first layer added is the outermost wrapper. Mirrors .NET
 * `ResilientPipelineBuilder`.
 *
 * Canonical full-stack composition (add in this order):
 * ```
 * new ResilientPipelineBuilder()
 *   .useSingleflight()                                   // outermost (optional)
 *   .useRateLimiter({ maxConcurrency: 10 })              // admission control
 *   .useTimeout({ durationMs: 30_000 })                  // total-request budget
 *   .useRetries({ maxAttempts: 3 })
 *   .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 30_000 })
 *   .useTimeout({ durationMs: 5_000 })                   // per-attempt deadline
 *   .build()
 * ```
 */
export class ResilientPipelineBuilder {
  private readonly layers: ((inner: IResilientLayer) => IResilientLayer)[] = [];

  useSingleflight(): this {
    this.layers.push((inner) => new SingleflightLayer(inner));
    return this;
  }

  useCircuitBreaker(opts: CircuitBreakerOptions): this {
    this.layers.push((inner) => new CircuitBreakerLayer(inner, opts));
    return this;
  }

  useRetries(opts: Partial<RetryOptions<unknown>>): this {
    this.layers.push((inner) => new RetryLayer(inner, opts));
    return this;
  }

  /**
   * Adds a {@link TimeoutLayer} at the current position. Call twice to express
   * both a total-request timeout (outer) and a per-attempt timeout (inner,
   * below `useRetries`).
   *
   * `opts.durationMs <= 0` disables the timeout (pass-through for this position).
   */
  useTimeout(opts?: TimeoutOptions): this {
    this.layers.push((inner) => new TimeoutLayer(inner, opts));
    return this;
  }

  /**
   * Adds a {@link RateLimiterLayer} at the current position. Place outermost
   * (after `useSingleflight` if used) for admission-control semantics — reject
   * callers before they consume retry or timeout budget.
   *
   * `opts.maxConcurrency` must be >= 1 (validated at construction).
   */
  useRateLimiter(opts?: RateLimiterOptions): this {
    this.layers.push((inner) => new RateLimiterLayer(inner, opts));
    return this;
  }

  build(): ResilientPipeline {
    let head: IResilientLayer = new TerminalLayer();
    // Build innermost-out so order matches insertion (first added = outermost).
    for (let i = this.layers.length - 1; i >= 0; i--)
      head = this.layers[i]!(head);
    return new ResilientPipeline(head);
  }
}
