// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { CircuitBreakerOptions } from "../circuit-breaker/circuit-breaker-options.js";
import type { RetryOptions } from "../retry/retry-options.js";
import { CircuitBreakerLayer } from "./circuit-breaker-layer.js";
import type { IResilientLayer } from "./i-resilient-layer.js";
import { RetryLayer } from "./retry-layer.js";
import { SingleflightLayer } from "./singleflight-layer.js";

/** Final composed pipeline. Outer-first ordering. */
export class ResilientPipeline {
  constructor(private readonly head: IResilientLayer) {}

  execute<T>(key: string, op: () => Promise<T>): Promise<T> {
    return this.head.execute(key, op);
  }
}

/** Innermost layer — runs the supplied op directly. */
class TerminalLayer implements IResilientLayer {
  execute<T>(_key: string, op: () => Promise<T>): Promise<T> {
    return op();
  }
}

/**
 * Builder for {@link ResilientPipeline}. Outer-first ordering — the
 * first layer added is the outermost wrapper. Mirrors .NET
 * `ResilientPipelineBuilder`.
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

  build(): ResilientPipeline {
    let head: IResilientLayer = new TerminalLayer();
    // Build innermost-out so order matches insertion (first added = outermost).
    for (let i = this.layers.length - 1; i >= 0; i--)
      head = this.layers[i]!(head);
    return new ResilientPipeline(head);
  }
}
