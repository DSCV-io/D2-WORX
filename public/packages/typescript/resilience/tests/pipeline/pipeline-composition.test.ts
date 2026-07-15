// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------
//
// Adversarial composition tests proving layer order-of-operations across the
// full pipeline — each test makes a non-vacuous assertion that the observable
// outcome CHANGES depending on layer order, proving the layers nest correctly.
// Mirrors the C# `PipelineCompositionTests`.
//
// All proofs use deterministic handshakes (deferred promises / call counters)
// — no real `setTimeout` races.
// -----------------------------------------------------------------------

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RateLimitRejectedError } from "../../src/pipeline/rate-limiter-layer.js";
import {
  ResilientPipeline,
  ResilientPipelineBuilder,
} from "../../src/pipeline/resilient-pipeline.js";
import { TimeoutError } from "../../src/pipeline/timeout-layer.js";

const noDelay = () => Promise.resolve();

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function deferred<T>(): {
  promise: Promise<T>;
  resolve: (v: T) => void;
  reject: (e: unknown) => void;
} {
  let resolve!: (v: T) => void;
  let reject!: (e: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

// ---------------------------------------------------------------------------
// PassThrough sentinel
// ---------------------------------------------------------------------------

describe("ResilientPipeline.PassThrough", () => {
  it("executes the op directly — no wrapping", async () => {
    const result = await ResilientPipeline.PassThrough.execute(
      "k",
      async () => 7,
    );
    expect(result).toBe(7);
  });

  it("propagates errors from the op unchanged", async () => {
    await expect(
      ResilientPipeline.PassThrough.execute("k", async () => {
        throw new Error("raw");
      }),
    ).rejects.toThrow("raw");
  });

  it("each call to PassThrough returns an independent zero-layer pipeline", () => {
    const a = ResilientPipeline.PassThrough;
    const b = ResilientPipeline.PassThrough;
    // Two distinct instances (value semantics — not the same reference).
    expect(a).not.toBe(b);
  });
});

// ---------------------------------------------------------------------------
// CB ↔ Retry order-sensitivity
// ---------------------------------------------------------------------------
// PROOF: CB-outside-Retry trips after N total failures across all attempts;
// CB-inside-Retry trips during a single burst of N consecutive per-op failures.
// The observable difference: total real-op call counts diverge.

describe("CB ↔ Retry order-sensitivity", () => {
  it("CB outer, Retry inner — CB trips on 3rd real failure (3 op calls, CB then open)", async () => {
    // CB threshold = 3. Retry maxAttempts = 2.
    // Outer → inner: CB → Retry → op.
    // Attempt 1 (CB lets through): Retry runs op twice → both fail → 2 real calls → CB sees 1 top-level failure.
    // Attempt 2 (CB lets through): Retry runs op twice → both fail → 2 real calls → CB sees 2nd failure.
    // Attempt 3 ... wait — CB threshold = 3, so CB opens only after 3 CB-level failures.
    //
    // Simpler: CB threshold=1 (trips on first failure), Retry=2.
    // CB outer: first retry-sequence (2 op calls) counts as ONE CB failure → CB open → 2 total calls.
    // CB inner: CB trips mid-retry when op fails → the CB failure happens INSIDE retry → subsequent retried call hits open CB → rethrows immediately → 1 real call + 1 CB-open error.
    let opCalls = 0;
    const cbOuter = new ResilientPipelineBuilder()
      .useCircuitBreaker({ failureThreshold: 1, cooldownMs: 30_000 })
      .useRetries({
        maxAttempts: 2,
        isTransient: () => true,
        delayFunc: noDelay,
      })
      .build();

    await expect(
      cbOuter.execute("k", async () => {
        opCalls++;
        throw new Error("fail");
      }),
    ).rejects.toThrow();

    const cbOuterCalls = opCalls;

    opCalls = 0;
    const cbInner = new ResilientPipelineBuilder()
      .useRetries({
        maxAttempts: 3,
        isTransient: () => true,
        delayFunc: noDelay,
      })
      .useCircuitBreaker({ failureThreshold: 1, cooldownMs: 30_000 })
      .build();

    await expect(
      cbInner.execute("k", async () => {
        opCalls++;
        throw new Error("fail");
      }),
    ).rejects.toThrow();

    const cbInnerCalls = opCalls;

    // CB-outer: retry exhausted all 2 attempts (2 real calls) then CB trips.
    // CB-inner: first attempt trips the CB; second attempt hits the open CB without calling op.
    //           So op called exactly once, then retry + open CB on subsequent attempt.
    // The key property: the call counts ARE different.
    expect(cbOuterCalls).not.toBe(cbInnerCalls);
    // Specifically: CB-outer = maxAttempts (2) real calls; CB-inner = 1 real call (first).
    expect(cbOuterCalls).toBe(2);
    expect(cbInnerCalls).toBe(1);
  });
});

// ---------------------------------------------------------------------------
// RateLimiter outermost — inner never runs when rejected
// ---------------------------------------------------------------------------
// PROOF: RL at outermost position short-circuits before any inner layer runs.

describe("RateLimiter outermost short-circuit", () => {
  it("RL outer: rejected caller never reaches the inner op", async () => {
    let innerCalls = 0;
    const layer = new ResilientPipelineBuilder()
      .useRateLimiter({ maxConcurrency: 1, acquisitionTimeoutMs: 0 })
      .useRetries({ maxAttempts: 3, delayFunc: noDelay })
      .build();

    // Fill the single slot with a hanging op.
    const d = deferred<number>();
    layer.execute("k", () => d.promise);

    // Second caller → rejected by rate limiter before inner op / retry runs.
    await expect(
      layer.execute("k", async () => {
        innerCalls++;
        return 0;
      }),
    ).rejects.toThrow(RateLimitRejectedError);

    expect(innerCalls).toBe(0);
    d.resolve(1);
  });
});

// ---------------------------------------------------------------------------
// Total-vs-per-attempt timeout
// ---------------------------------------------------------------------------
// PROOF: total timeout (outer) fires once across all retries;
// per-attempt timeout (inner) fires per retry and lets retry re-attempt.

describe("Total-vs-per-attempt timeout", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("per-attempt timeout: inner op times out, outer retry re-attempts", async () => {
    let calls = 0;
    const firstAttempt = deferred<number>();
    const layer = new ResilientPipelineBuilder()
      .useRetries({ maxAttempts: 2, delayFunc: noDelay })
      .useTimeout({ durationMs: 50 }) // per-attempt
      .build();

    const p = layer.execute("k", async () => {
      calls++;
      if (calls === 1)
        // First attempt hangs → will time out.
        return firstAttempt.promise;
      // Second attempt succeeds immediately.
      return 42;
    });

    // Advance time past the per-attempt timeout → first attempt times out →
    // retry kicks in → second attempt resolves immediately.
    await vi.advanceTimersByTimeAsync(100);

    expect(await p).toBe(42);
    expect(calls).toBe(2); // timed out once, succeeded on retry
    // Resolve the hanging first-attempt promise so it doesn't leak.
    firstAttempt.resolve(0);
  });

  it("total timeout (outer): fires and terminates pipeline before retry exhaustion", async () => {
    // PROOF: without a total timeout, 10 maxAttempts would all complete.
    // With a total timeout shorter than the per-attempt budget × maxAttempts,
    // the total timeout terminates the pipeline early.
    //
    // Scenario: total = 50ms; per-attempt = 200ms (only 1 per-attempt can
    // complete before total fires). No retry budget matters — total terminates.
    const inner = deferred<number>();
    const layer = new ResilientPipelineBuilder()
      .useTimeout({ durationMs: 50 }) // total (outer)
      .useRetries({ maxAttempts: 10, delayFunc: noDelay })
      .useTimeout({ durationMs: 200 }) // per-attempt (inner, much longer)
      .build();

    const p = layer.execute("k", () => inner.promise);
    // Register rejection handler before advancing timers to avoid
    // PromiseRejectionHandledWarning from Node.js.
    const caught = p.catch((e) => e);

    // Advance 60ms → total fires at 50ms, terminating the pipeline.
    await vi.advanceTimersByTimeAsync(60);

    // Must be a TimeoutError (from the total timeout, not the per-attempt).
    expect(await caught).toBeInstanceOf(TimeoutError);
    // Resolve/reject the inner so no dangling promise warnings.
    inner.resolve(0);
  });
});

// ---------------------------------------------------------------------------
// Singleflight dedup across full stack
// ---------------------------------------------------------------------------
// PROOF: SF outermost dedupes 10 concurrent callers to exactly 1 real-op
// execution, even with retry and CB below it.

describe("Singleflight dedup across full stack", () => {
  it("SF outer: 10 concurrent callers → exactly 1 real op execution", async () => {
    let executions = 0;
    const d = deferred<number>();

    const layer = new ResilientPipelineBuilder()
      .useSingleflight()
      .useRetries({ maxAttempts: 2, delayFunc: noDelay })
      .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 10_000 })
      .build();

    const callers = Array.from({ length: 10 }, () =>
      layer.execute("shared-key", () => {
        executions++;
        return d.promise;
      }),
    );

    d.resolve(99);
    const results = await Promise.all(callers);

    // Only 1 real execution despite 10 concurrent callers.
    expect(executions).toBe(1);
    // All 10 callers received the same result.
    expect(results.every((r) => r === 99)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Canonical stack ordering
// ---------------------------------------------------------------------------
// PROOF: the canonical stack (SF → RL → Retry → CB → per-attempt timeout)
// composes without error and admits a successful call.

describe("Canonical stack composition", () => {
  it("SF → RL → Retry → CB → PerAttemptTimeout: successful call returns result", async () => {
    const layer = new ResilientPipelineBuilder()
      .useSingleflight()
      .useRateLimiter({ maxConcurrency: 10, acquisitionTimeoutMs: 0 })
      .useRetries({ maxAttempts: 2, delayFunc: noDelay })
      .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 30_000 })
      .useTimeout({ durationMs: 5_000 })
      .build();

    expect(await layer.execute("k", async () => "full-stack")).toBe(
      "full-stack",
    );
  });

  it("full-stack: transient failure retried, eventual success", async () => {
    let calls = 0;
    const layer = new ResilientPipelineBuilder()
      .useRateLimiter({ maxConcurrency: 5, acquisitionTimeoutMs: 0 })
      .useRetries({
        maxAttempts: 3,
        isTransient: () => true,
        delayFunc: noDelay,
      })
      .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 30_000 })
      .useTimeout({ durationMs: 5_000 })
      .build();

    const result = await layer.execute("k", async () => {
      calls++;
      if (calls < 2) throw new Error("transient");
      return "recovered";
    });

    expect(result).toBe("recovered");
    expect(calls).toBe(2);
  });
});

// ---------------------------------------------------------------------------
// useTimeout called twice — two independent TimeoutLayer instances
// ---------------------------------------------------------------------------
// PROOF: calling useTimeout twice produces two independent layers
// (total outer + per-attempt inner).

describe("useTimeout × 2 yields two independent TimeoutLayer instances", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("inner per-attempt timeout fires, outer total timeout does not", async () => {
    let calls = 0;
    const firstAttempt = deferred<number>();
    const layer = new ResilientPipelineBuilder()
      .useTimeout({ durationMs: 1_000 }) // total — generous
      .useRetries({ maxAttempts: 2, delayFunc: noDelay })
      .useTimeout({ durationMs: 50 }) // per-attempt — tight
      .build();

    const p = layer.execute("k", async () => {
      calls++;
      if (calls === 1) return firstAttempt.promise; // first: hang
      return 77; // second: immediate
    });

    // Advance past per-attempt (50ms) but not past total (1000ms).
    await vi.advanceTimersByTimeAsync(100);

    expect(await p).toBe(77);
    expect(calls).toBe(2);
    // Resolve the hanging first-attempt promise so it doesn't leak.
    firstAttempt.resolve(0);
  });
});

// ---------------------------------------------------------------------------
// ResilientPipelineBuilder — useRateLimiter / useTimeout appear in exports
// ---------------------------------------------------------------------------

describe("Builder method presence", () => {
  it("useTimeout is callable and returns the builder (fluent)", () => {
    const builder = new ResilientPipelineBuilder();
    expect(builder.useTimeout({ durationMs: 100 })).toBe(builder);
  });

  it("useRateLimiter is callable and returns the builder (fluent)", () => {
    const builder = new ResilientPipelineBuilder();
    expect(
      builder.useRateLimiter({ maxConcurrency: 5, acquisitionTimeoutMs: 0 }),
    ).toBe(builder);
  });

  it("both can be chained without error", () => {
    expect(() =>
      new ResilientPipelineBuilder()
        .useRateLimiter({ maxConcurrency: 2, acquisitionTimeoutMs: 0 })
        .useTimeout({ durationMs: 100 })
        .useRetries({ maxAttempts: 2 })
        .build(),
    ).not.toThrow();
  });
});
