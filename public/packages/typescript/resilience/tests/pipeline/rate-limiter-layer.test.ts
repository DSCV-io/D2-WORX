// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  RATE_LIMITER_DEFAULTS,
  RateLimitRejectedError,
  RateLimiterLayer,
} from "../../src/pipeline/rate-limiter-layer.js";
import type { IResilientLayer } from "../../src/pipeline/i-resilient-layer.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

class TerminalLayer implements IResilientLayer {
  execute<T>(
    _key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    return op(signal);
  }
}

/** Returns a deferred promise + its handles. */
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

function layerWith(
  maxConcurrency: number,
  acquisitionTimeoutMs = 0,
): RateLimiterLayer {
  return new RateLimiterLayer(new TerminalLayer(), {
    maxConcurrency,
    acquisitionTimeoutMs,
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("RateLimiterLayer — construction", () => {
  it("constructs with default options", () => {
    expect(() => new RateLimiterLayer(new TerminalLayer())).not.toThrow();
  });

  it("RATE_LIMITER_DEFAULTS exports expected defaults", () => {
    expect(RATE_LIMITER_DEFAULTS.maxConcurrency).toBe(100);
    expect(RATE_LIMITER_DEFAULTS.acquisitionTimeoutMs).toBe(0);
  });

  it("maxConcurrency = 0 → throws RangeError at construction (fail-loud)", () => {
    expect(
      () =>
        new RateLimiterLayer(new TerminalLayer(), {
          maxConcurrency: 0,
          acquisitionTimeoutMs: 0,
        }),
    ).toThrow(RangeError);
  });

  it("maxConcurrency < 0 → throws RangeError at construction", () => {
    expect(
      () =>
        new RateLimiterLayer(new TerminalLayer(), {
          maxConcurrency: -1,
          acquisitionTimeoutMs: 0,
        }),
    ).toThrow(RangeError);
  });

  it("maxConcurrency = 1 → constructs successfully", () => {
    expect(
      () =>
        new RateLimiterLayer(new TerminalLayer(), {
          maxConcurrency: 1,
          acquisitionTimeoutMs: 0,
        }),
    ).not.toThrow();
  });
});

describe("RateLimiterLayer — admit up to maxConcurrency", () => {
  it("single call under limit → admitted and result returned", async () => {
    const layer = layerWith(2);
    expect(await layer.execute("k", async () => 42)).toBe(42);
  });

  it("maxConcurrency concurrent calls → all admitted", async () => {
    const layer = layerWith(3);
    const d1 = deferred<number>();
    const d2 = deferred<number>();
    const d3 = deferred<number>();

    const p1 = layer.execute("k", () => d1.promise);
    const p2 = layer.execute("k", () => d2.promise);
    const p3 = layer.execute("k", () => d3.promise);

    // All three are inside the gate (none rejected yet).
    expect(layer.activeCount).toBe(3);

    d1.resolve(1);
    d2.resolve(2);
    d3.resolve(3);
    expect(await p1).toBe(1);
    expect(await p2).toBe(2);
    expect(await p3).toBe(3);
  });

  it("(maxConcurrency + 1)th call with acquisitionTimeout=0 → rejected immediately", async () => {
    const layer = layerWith(2, 0);
    const d1 = deferred<number>();
    const d2 = deferred<number>();

    layer.execute("k", () => d1.promise);
    layer.execute("k", () => d2.promise);

    // Third call should be rejected synchronously (before any await).
    await expect(layer.execute("k", async () => 0)).rejects.toThrow(
      RateLimitRejectedError,
    );

    d1.resolve(1);
    d2.resolve(2);
  });
});

describe("RateLimiterLayer — admit on release", () => {
  it("(N+1)th caller admitted after one of the N slots releases", async () => {
    const layer = layerWith(2, 100);
    const d1 = deferred<number>();
    const d2 = deferred<number>();

    const p1 = layer.execute("k", () => d1.promise);
    const p2 = layer.execute("k", () => d2.promise);

    // Start a third caller — it must wait.
    const p3 = layer.execute("k", async () => 3);

    // Release one slot — p3 should be admitted.
    d1.resolve(1);
    await p1;

    expect(await p3).toBe(3);

    d2.resolve(2);
    await p2;
  });

  it("release restores the permit — subsequent call is admitted", async () => {
    const layer = layerWith(1);
    expect(await layer.execute("k", async () => "first")).toBe("first");
    // Slot must be released now.
    expect(layer.activeCount).toBe(0);
    expect(await layer.execute("k", async () => "second")).toBe("second");
  });
});

describe("RateLimiterLayer — reject when acquisition timeout elapses", () => {
  it("queued caller rejected after acquisitionTimeoutMs with no release", async () => {
    const layer = layerWith(1, 10);
    const d = deferred<number>();

    // Fill the slot.
    const holder = layer.execute("k", () => d.promise);

    // This caller must time out waiting for the slot.
    await expect(layer.execute("k", async () => 0)).rejects.toThrow(
      RateLimitRejectedError,
    );

    // Clean up the holder.
    d.resolve(1);
    await holder;
  });
});

describe("RateLimiterLayer — release on throw (no permit leak)", () => {
  it("inner op throws → permit released → next call admitted", async () => {
    const layer = layerWith(1);

    await expect(
      layer.execute("k", async () => {
        throw new Error("inner-boom");
      }),
    ).rejects.toThrow("inner-boom");

    // Permit must be released.
    expect(layer.activeCount).toBe(0);

    // Next call must succeed.
    expect(await layer.execute("k", async () => "recovered")).toBe("recovered");
  });

  it("multiple inner throws → active count always returns to 0", async () => {
    const layer = layerWith(3);
    await Promise.allSettled([
      layer.execute("k", async () => {
        throw new Error("a");
      }),
      layer.execute("k", async () => {
        throw new Error("b");
      }),
      layer.execute("k", async () => {
        throw new Error("c");
      }),
    ]);
    expect(layer.activeCount).toBe(0);
  });
});

describe("RateLimiterLayer — concurrency stress: never exceeds maxConcurrency", () => {
  it("100 concurrent callers, maxConcurrency = 5 → never more than 5 active", async () => {
    const layer = layerWith(5, 10_000);
    let maxObserved = 0;

    const ops = Array.from({ length: 100 }, (_, i) =>
      layer.execute(
        `k${i}`,
        () =>
          new Promise<number>((resolve) => {
            const current = layer.activeCount;
            if (current > maxObserved) maxObserved = current;
            // Yield to the event loop, then resolve.
            Promise.resolve().then(() => resolve(i));
          }),
      ),
    );

    await Promise.all(ops);
    expect(maxObserved).toBeLessThanOrEqual(5);
  });
});

describe("RateLimiterLayer — RateLimitRejectedError properties", () => {
  it("is an instance of Error", () => {
    expect(new RateLimitRejectedError()).toBeInstanceOf(Error);
  });

  it("name is 'RateLimitRejectedError'", () => {
    expect(new RateLimitRejectedError().name).toBe("RateLimitRejectedError");
  });

  it("default message is meaningful", () => {
    expect(new RateLimitRejectedError().message).toBe(
      "rate limit exceeded: too many concurrent operations",
    );
  });

  it("custom message overrides default", () => {
    expect(new RateLimitRejectedError("custom").message).toBe("custom");
  });
});

describe("RateLimiterLayer — key forwarded to inner", () => {
  it("passes the key through to the inner layer", async () => {
    const seen: string[] = [];
    const recordKey: IResilientLayer = {
      execute<T>(
        key: string,
        op: (signal?: AbortSignal) => Promise<T>,
        signal?: AbortSignal,
      ) {
        seen.push(key);
        return op(signal);
      },
    };
    const layer = new RateLimiterLayer(recordKey, {
      maxConcurrency: 5,
      acquisitionTimeoutMs: 0,
    });
    await layer.execute("my-key", async () => 1);
    expect(seen).toEqual(["my-key"]);
  });
});

// ---------------------------------------------------------------------------
// Regression: splice-before-resolve race — timed-out waiter must not receive
// a phantom permit from a subsequent release().
// ---------------------------------------------------------------------------
// This test pins the correctness of the waiter-timeout-vs-release race path in
// tryAcquire(): when the acquisition timer fires first (waiter rejected), a
// subsequent release() must NOT hand the already-timed-out waiter a second
// phantom permit (which would leak an active count and starve future callers).
//
// Mechanism: timer callback splices the waiter out of the queue BEFORE calling
// resolve(false). release() dequeues via shift() — after the splice, the waiter
// is no longer in the queue, so shift() returns undefined and active-- runs
// instead of granting the phantom permit.

describe("RateLimiterLayer — splice-before-resolve race (regression)", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("timed-out waiter does not receive a phantom permit from a subsequent release()", async () => {
    // maxConcurrency=1: sole slot taken by holderDeferred.
    // acquisitionTimeoutMs=5: waiter times out after 5ms.
    const layer = layerWith(1, 5);
    const holder = deferred<number>();

    // Fill the sole slot — stays in-flight while holderDeferred is unsettled.
    const holderCall = layer.execute("k", () => holder.promise);
    expect(layer.activeCount).toBe(1);

    // Start a second caller — it queues as a waiter (slot is full).
    // Attach .catch before advancing timers to avoid PromiseRejectionHandledWarning.
    const waiterCall = layer.execute("k", async () => 0);
    const waiterCaught = waiterCall.catch((e: unknown) => e);

    // Advance fake timers past the 5ms acquisition timeout.
    // The waiter's timer fires: waiter spliced out, resolve(false) called →
    // waiterCall rejects with RateLimitRejectedError.
    await vi.advanceTimersByTimeAsync(10);
    const waiterResult = await waiterCaught;
    expect(waiterResult).toBeInstanceOf(RateLimitRejectedError);

    // Now release the holder. release() calls shift() — the waiter was already
    // spliced out by the timer, so shift() returns undefined → active-- runs.
    // active goes from 1 to 0: no phantom permit handed to the timed-out waiter.
    holder.resolve(1);
    await holderCall;

    // activeCount must be 0 (clean slate, no phantom permit leaked).
    expect(layer.activeCount).toBe(0);

    // Behavioral proof: a fresh execute() is admitted immediately, confirming
    // the semaphore is in a clean state (not stuck at 1 due to a phantom permit).
    expect(await layer.execute("k", async () => "fresh")).toBe("fresh");
    expect(layer.activeCount).toBe(0);
  });
});
