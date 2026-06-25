// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------
//
// AbortSignal-threading + genuine-cancellation proofs (C# parity with the
// linked-CancellationToken model). Each test asserts an OBSERVABLE cancellation
// effect: an inner op's `signal.aborted` flips, a caller abort surfaces as
// `AbortError` (not `TimeoutError`), or — the headline — one Singleflight caller
// aborting does NOT cancel the shared op for the other waiters.
//
// Deterministic: fake timers + awaited deferreds, no real-clock races.
// -----------------------------------------------------------------------

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { IResilientLayer } from "../../src/pipeline/i-resilient-layer.js";
import { RateLimiterLayer } from "../../src/pipeline/rate-limiter-layer.js";
import { ResilientPipelineBuilder } from "../../src/pipeline/resilient-pipeline.js";
import { TimeoutLayer } from "../../src/pipeline/timeout-layer.js";
import { Singleflight } from "../../src/singleflight/singleflight.js";

const noDelay = (): Promise<void> => Promise.resolve();

/** Runs the op directly, forwarding the threaded signal. */
class TerminalLayer implements IResilientLayer {
  execute<T>(
    _key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    return op(signal);
  }
}

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

// ===========================================================================
// TimeoutLayer — genuinely CANCELS the inner op on expiry
// ===========================================================================

describe("TimeoutLayer — cancels the inner op on timeout (C# linked-CTS parity)", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("on timeout, the inner op's signal is aborted", async () => {
    let observed: AbortSignal | undefined;
    const inner = deferred<number>();
    const layer = new TimeoutLayer(new TerminalLayer(), { durationMs: 100 });

    const p = layer.execute("k", (signal) => {
      observed = signal;
      return inner.promise; // hang — never resolves on its own
    });
    const caught = p.catch((e: unknown) => e);

    // Before the deadline the inner signal is live.
    expect(observed).toBeDefined();
    expect(observed?.aborted).toBe(false);

    await vi.advanceTimersByTimeAsync(101);

    // The deadline fired → the inner op's signal was aborted (cooperative
    // cancellation), and the layer rejected with TimeoutError.
    expect(observed?.aborted).toBe(true);
    expect((await caught) as Error).toBeInstanceOf(Error);
    expect(((await caught) as Error).name).toBe("TimeoutError");

    inner.resolve(0); // release the dangling promise
  });

  it("a cooperative inner op (fetch-like) observes the abort and stops", async () => {
    // Models a `fetch`: rejects its OWN AbortError once its signal aborts.
    const layer = new TimeoutLayer(new TerminalLayer(), { durationMs: 50 });
    let innerSignal: AbortSignal | undefined;

    const p = layer.execute("k", (signal) => {
      innerSignal = signal;
      return new Promise<number>((_resolve, reject) => {
        signal?.addEventListener("abort", () =>
          reject(Object.assign(new Error("aborted"), { name: "AbortError" })),
        );
      });
    });
    const caught = p.catch((e: unknown) => e);

    await vi.advanceTimersByTimeAsync(51);

    // The fetch-like op was actually canceled via its signal …
    expect(innerSignal?.aborted).toBe(true);
    // … and the layer still surfaces TimeoutError (the timer won the race,
    // deterministically — a timeout, not a caller abort).
    expect(((await caught) as Error).name).toBe("TimeoutError");
  });

  it("inner op that resolves before the deadline → its signal is NOT aborted", async () => {
    let observed: AbortSignal | undefined;
    const layer = new TimeoutLayer(new TerminalLayer(), { durationMs: 1_000 });

    const value = await layer.execute("k", async (signal) => {
      observed = signal;
      return 42;
    });

    expect(value).toBe(42);
    expect(observed?.aborted).toBe(false);
  });
});

// ===========================================================================
// TimeoutLayer — caller abort is NOT masked + cancels the inner op
// ===========================================================================

describe("TimeoutLayer — caller abort propagates as AbortError (not TimeoutError)", () => {
  it("caller aborts a hanging op → AbortError, and the inner signal is aborted", async () => {
    const controller = new AbortController();
    let observed: AbortSignal | undefined;
    const inner = deferred<number>();
    const layer = new TimeoutLayer(new TerminalLayer(), { durationMs: 10_000 });

    const p = layer.execute(
      "k",
      (signal) => {
        observed = signal;
        // Cooperative: reject AbortError when the (linked) signal aborts.
        return new Promise<number>((_res, reject) => {
          signal?.addEventListener("abort", () =>
            reject(Object.assign(new Error("aborted"), { name: "AbortError" })),
          );
          void inner.promise; // keep a ref; never resolves
        });
      },
      controller.signal,
    );
    const caught = p.catch((e: unknown) => e);

    // Caller cancels before the 10s timeout.
    controller.abort();

    const err = (await caught) as Error;
    expect(err.name).toBe("AbortError");
    // The caller abort propagated through the linked controller to the inner op.
    expect(observed?.aborted).toBe(true);

    inner.resolve(0);
  });

  it("caller aborted AND the timer fires (non-cooperative op) → caller cancellation wins (AbortError)", async () => {
    // Edge: the caller has aborted, but the inner op ignores its signal and
    // hangs, so the deadline TIMER is what settles. C# prioritizes the caller
    // (`when (timeoutCts && !ct)` fails when ct fired) — so this surfaces
    // AbortError, NOT TimeoutError.
    vi.useFakeTimers();
    try {
      const controller = new AbortController();
      controller.abort(); // caller already canceled
      const inner = deferred<number>();
      const layer = new TimeoutLayer(new TerminalLayer(), { durationMs: 50 });

      // Op ignores its signal entirely → only the timer can settle the race.
      const p = layer.execute("k", () => inner.promise, controller.signal);
      const caught = p.catch((e: unknown) => e);

      await vi.advanceTimersByTimeAsync(51);

      expect(((await caught) as Error).name).toBe("AbortError");
      inner.resolve(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it("caller signal already aborted → inner op sees an aborted signal + AbortError", async () => {
    const controller = new AbortController();
    controller.abort();
    let observed: AbortSignal | undefined;
    const layer = new TimeoutLayer(new TerminalLayer(), { durationMs: 1_000 });

    const p = layer.execute(
      "k",
      (signal) => {
        observed = signal;
        return new Promise<number>((_res, reject) => {
          if (signal?.aborted)
            reject(Object.assign(new Error("aborted"), { name: "AbortError" }));
        });
      },
      controller.signal,
    );

    await expect(p).rejects.toMatchObject({ name: "AbortError" });
    expect(observed?.aborted).toBe(true);
  });
});

// ===========================================================================
// TimeoutLayer — no AbortController / listener leak
// ===========================================================================

describe("TimeoutLayer — no listener leak on the caller signal", () => {
  it("removes its caller-signal listener once the op settles normally", async () => {
    const controller = new AbortController();
    const added: string[] = [];
    const removed: string[] = [];
    const realAdd = controller.signal.addEventListener.bind(controller.signal);
    const realRemove = controller.signal.removeEventListener.bind(
      controller.signal,
    );
    vi.spyOn(controller.signal, "addEventListener").mockImplementation(
      (type, listener, opts) => {
        added.push(type as string);
        return realAdd(type, listener, opts);
      },
    );
    vi.spyOn(controller.signal, "removeEventListener").mockImplementation(
      (type, listener, opts) => {
        removed.push(type as string);
        return realRemove(type, listener, opts);
      },
    );

    const layer = new TimeoutLayer(new TerminalLayer(), { durationMs: 1_000 });
    await layer.execute("k", async () => 1, controller.signal);

    // The 'abort' listener the layer attached was detached on settle.
    expect(added).toContain("abort");
    expect(removed).toContain("abort");
  });
});

// ===========================================================================
// Singleflight — ONE caller aborting does NOT cancel the shared op
// ===========================================================================

describe("Singleflight — one caller aborts; shared op survives for the others", () => {
  it("aborting caller rejects AbortError; shared op runs once; other waiters still resolve", async () => {
    let executions = 0;
    const shared = deferred<number>();
    const pipeline = new ResilientPipelineBuilder().useSingleflight().build();

    const sharedOp = (signal?: AbortSignal): Promise<number> => {
      executions++;
      // The shared op must be handed NO caller signal — it cannot be canceled
      // by any single caller (mirrors CancellationToken.None on the .NET side).
      expect(signal).toBeUndefined();
      return shared.promise;
    };

    const c1 = new AbortController();
    const c2 = new AbortController();
    const c3 = new AbortController();

    const p1 = pipeline.execute("shared", sharedOp, c1.signal);
    const p2 = pipeline.execute("shared", sharedOp, c2.signal);
    const p3 = pipeline.execute("shared", sharedOp, c3.signal);

    const caught2 = p2.catch((e: unknown) => e);

    // Caller #2 bails out.
    c2.abort();

    // #2's wait was canceled …
    expect(((await caught2) as Error).name).toBe("AbortError");

    // … but the SHARED op is still running for #1 and #3.
    shared.resolve(99);
    expect(await p1).toBe(99);
    expect(await p3).toBe(99);

    // Exactly one real execution despite the abort + three callers.
    expect(executions).toBe(1);
  });

  it("a caller whose signal is ALREADY aborted gets AbortError but the shared op still runs for others", async () => {
    let executions = 0;
    const shared = deferred<number>();
    const pipeline = new ResilientPipelineBuilder().useSingleflight().build();

    const sharedOp = (): Promise<number> => {
      executions++;
      return shared.promise;
    };

    // Caller #1 starts the shared op.
    const c1 = new AbortController();
    const p1 = pipeline.execute("shared", sharedOp, c1.signal);

    // Caller #2 joins with an ALREADY-aborted signal → its wait short-circuits.
    const c2 = new AbortController();
    c2.abort();
    await expect(
      pipeline.execute("shared", sharedOp, c2.signal),
    ).rejects.toMatchObject({ name: "AbortError" });

    // The shared op is unaffected — still one execution, #1 still resolves.
    shared.resolve(5);
    expect(await p1).toBe(5);
    expect(executions).toBe(1);
  });

  it("when the shared op REJECTS, a waiting caller (with a signal) receives the rejection", async () => {
    // Covers the raceAbort rejection-forwarding path: the shared op fails and
    // the caller is awaiting THROUGH raceAbort (it supplied a signal), so the
    // original error must propagate (not be swallowed into AbortError).
    const shared = deferred<number>();
    const pipeline = new ResilientPipelineBuilder().useSingleflight().build();
    const controller = new AbortController();

    const p = pipeline.execute(
      "shared",
      () => shared.promise,
      controller.signal,
    );
    const caught = p.catch((e: unknown) => e);

    shared.reject(new RangeError("shared boom"));

    expect((await caught) as Error).toBeInstanceOf(RangeError);
    expect(((await caught) as Error).message).toBe("shared boom");
  });

  it("the underlying Singleflight ran the factory exactly once and cleared on settle", async () => {
    // Lower-level mirror: prove the dedup primitive itself is untouched by the
    // per-caller cancellation (cancellation is the LAYER's concern, not the
    // primitive's).
    const sf = new Singleflight<string, number>();
    const shared = deferred<number>();
    let runs = 0;
    const a = sf.do("k", () => {
      runs++;
      return shared.promise;
    });
    const b = sf.do("k", () => {
      runs++;
      return shared.promise;
    });
    expect(sf.inflightCount).toBe(1);
    shared.resolve(7);
    expect(await a).toBe(7);
    expect(await b).toBe(7);
    expect(runs).toBe(1);
    expect(sf.inflightCount).toBe(0);
  });
});

// ===========================================================================
// Retry — caller abort stops retrying (not classified transient)
// ===========================================================================

describe("Retry — caller abort stops retrying", () => {
  it("a caller abort propagates AbortError and does not re-attempt", async () => {
    let calls = 0;
    const controller = new AbortController();
    const pipeline = new ResilientPipelineBuilder()
      .useRetries({ maxAttempts: 5, delayFunc: noDelay })
      .build();

    const p = pipeline.execute(
      "k",
      async () => {
        calls++;
        // The op aborts the caller signal then throws AbortError — a
        // cancellation must NOT be retried.
        controller.abort();
        throw Object.assign(new Error("aborted"), { name: "AbortError" });
      },
      controller.signal,
    );

    await expect(p).rejects.toMatchObject({ name: "AbortError" });
    expect(calls).toBe(1);
  });

  it("pre-aborted caller signal short-circuits before the first attempt", async () => {
    let calls = 0;
    const controller = new AbortController();
    controller.abort();
    const pipeline = new ResilientPipelineBuilder()
      .useRetries({ maxAttempts: 3, delayFunc: noDelay })
      .build();

    await expect(
      pipeline.execute(
        "k",
        async () => {
          calls++;
          return 1;
        },
        controller.signal,
      ),
    ).rejects.toMatchObject({ name: "AbortError", message: "aborted" });
    expect(calls).toBe(0);
  });
});

// ===========================================================================
// Retry + per-attempt timeout — each attempt gets its OWN aborting signal
// ===========================================================================

describe("Retry with inner per-attempt timeout — each attempt's signal cancels independently", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("first attempt times out (its signal aborted), retry runs a fresh non-aborted attempt", async () => {
    const observedSignals: (AbortSignal | undefined)[] = [];
    let calls = 0;
    const firstAttempt = deferred<number>();
    const pipeline = new ResilientPipelineBuilder()
      .useRetries({ maxAttempts: 2, delayFunc: noDelay })
      .useTimeout({ durationMs: 50 }) // per-attempt
      .build();

    const p = pipeline.execute("k", (signal) => {
      calls++;
      observedSignals.push(signal);
      if (calls === 1) return firstAttempt.promise; // hang → times out
      return Promise.resolve(42); // second attempt succeeds
    });

    await vi.advanceTimersByTimeAsync(100);

    expect(await p).toBe(42);
    expect(calls).toBe(2);
    // First attempt's per-attempt signal was aborted by its timeout …
    expect(observedSignals[0]?.aborted).toBe(true);
    // … the second attempt got a FRESH, non-aborted per-attempt signal.
    expect(observedSignals[1]?.aborted).toBe(false);
    // The two attempts have distinct signals (fresh linked controller each).
    expect(observedSignals[0]).not.toBe(observedSignals[1]);

    firstAttempt.resolve(0);
  });
});

// ===========================================================================
// RateLimiter — caller abort while waiting for a permit
// ===========================================================================

describe("RateLimiterLayer — caller abort while waiting for a permit", () => {
  it("aborting a queued waiter rejects AbortError without consuming a permit", async () => {
    const layer = new RateLimiterLayer(new TerminalLayer(), {
      maxConcurrency: 1,
      acquisitionTimeoutMs: 10_000,
    });
    const holder = deferred<number>();
    const controller = new AbortController();

    // Fill the sole slot.
    const holderCall = layer.execute("k", () => holder.promise);
    expect(layer.activeCount).toBe(1);

    // Second caller queues as a waiter, then aborts.
    const waiterCall = layer.execute("k", async () => 0, controller.signal);
    const waiterCaught = waiterCall.catch((e: unknown) => e);
    controller.abort();

    expect(((await waiterCaught) as Error).name).toBe("AbortError");
    // No phantom permit consumed: the slot is still held by the holder only.
    expect(layer.activeCount).toBe(1);

    // Release the holder; the layer returns to a clean slate (the aborted
    // waiter was spliced out — it gets no phantom permit).
    holder.resolve(1);
    await holderCall;
    expect(layer.activeCount).toBe(0);

    // A fresh call is admitted immediately (semaphore not stuck).
    expect(await layer.execute("k", async () => "fresh")).toBe("fresh");
    expect(layer.activeCount).toBe(0);
  });

  it("a pre-aborted caller never enters the gate", async () => {
    const layer = new RateLimiterLayer(new TerminalLayer(), {
      maxConcurrency: 1,
      acquisitionTimeoutMs: 0,
    });
    const controller = new AbortController();
    controller.abort();
    let calls = 0;

    await expect(
      layer.execute(
        "k",
        async () => {
          calls++;
          return 1;
        },
        controller.signal,
      ),
    ).rejects.toMatchObject({ name: "AbortError" });
    expect(calls).toBe(0);
    expect(layer.activeCount).toBe(0);
  });
});

// ===========================================================================
// CircuitBreaker — caller abort propagates
// ===========================================================================

describe("CircuitBreakerLayer — caller abort propagates as AbortError", () => {
  it("a caller abort surfaces from the breaker as AbortError", async () => {
    const controller = new AbortController();
    const pipeline = new ResilientPipelineBuilder()
      .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 10_000 })
      .build();

    const p = pipeline.execute(
      "k",
      async (signal) => {
        controller.abort();
        if (signal?.aborted)
          throw Object.assign(new Error("aborted"), { name: "AbortError" });
        return 1;
      },
      controller.signal,
    );

    await expect(p).rejects.toMatchObject({ name: "AbortError" });
  });
});

// ===========================================================================
// Pipeline — caller signal threads end-to-end
// ===========================================================================

describe("ResilientPipeline — caller signal threads to the inner op", () => {
  it("the signal reaches the innermost op through RL → Retry → CB", async () => {
    let observed: AbortSignal | undefined;
    const controller = new AbortController();
    // No Singleflight here: SF deliberately hands the SHARED op NO signal (the
    // uncancellable-by-one-caller guarantee), so it would mask the threading.
    // The other layers pass the caller signal straight through to the op.
    const pipeline = new ResilientPipelineBuilder()
      .useRateLimiter({ maxConcurrency: 5, acquisitionTimeoutMs: 0 })
      .useRetries({ maxAttempts: 2, delayFunc: noDelay })
      .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 30_000 })
      .build();

    await pipeline.execute(
      "k",
      async (signal) => {
        observed = signal;
        return "ok";
      },
      controller.signal,
    );

    // Through RL → Retry → CB the SAME caller signal arrived at the op
    // (no inner timeout layer here, so it is the caller's own signal).
    expect(observed).toBe(controller.signal);
  });

  it("Singleflight outermost hands the shared op NO signal (uncancellable-by-one-caller)", async () => {
    const sentinel = new AbortController().signal;
    let observed: AbortSignal | undefined = sentinel; // proves the op DID run
    const controller = new AbortController();
    const pipeline = new ResilientPipelineBuilder()
      .useSingleflight()
      .useRetries({ maxAttempts: 2, delayFunc: noDelay })
      .build();

    await pipeline.execute(
      "k",
      async (signal) => {
        observed = signal;
        return "ok";
      },
      controller.signal,
    );

    // The shared op ran (observed reassigned away from the sentinel) and got
    // `undefined` — one caller cannot cancel the shared work.
    expect(observed).not.toBe(sentinel);
    expect(observed).toBeUndefined();
  });

  it("PassThrough forwards the caller signal to the op", async () => {
    let observed: AbortSignal | undefined;
    const controller = new AbortController();
    const { ResilientPipeline } =
      await import("../../src/pipeline/resilient-pipeline.js");
    await ResilientPipeline.PassThrough.execute(
      "k",
      async (signal) => {
        observed = signal;
        return 1;
      },
      controller.signal,
    );
    expect(observed).toBe(controller.signal);
  });
});
