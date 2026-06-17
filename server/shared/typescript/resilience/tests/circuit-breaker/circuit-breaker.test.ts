// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { CircuitBreaker } from "../../src/circuit-breaker/circuit-breaker.js";
import { CircuitOpenError } from "../../src/circuit-breaker/circuit-open-error.js";
import { CircuitState } from "../../src/circuit-breaker/circuit-state.js";

class TestClock {
  now = 0;
  advance(ms: number): void {
    this.now += ms;
  }
}

describe("CircuitBreaker — Closed → Open → HalfOpen → Closed", () => {
  it("starts Closed and stays Closed on success", async () => {
    const cb = new CircuitBreaker<number>({
      failureThreshold: 2,
      cooldownMs: 1000,
    });
    expect(cb.currentState).toBe(CircuitState.Closed);
    expect(await cb.execute(async () => 1)).toBe(1);
    expect(cb.currentState).toBe(CircuitState.Closed);
  });

  it("trips Open after threshold consecutive failures", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 2,
      cooldownMs: 1000,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Closed);
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Open);
  });

  it("Open state rejects with CircuitOpenError", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 1000,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    await expect(cb.execute(async () => 1)).rejects.toThrow(CircuitOpenError);
  });

  it("transitions Open → HalfOpen after cooldown", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 100,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Open);
    clock.advance(100);
    expect(cb.currentState).toBe(CircuitState.HalfOpen);
  });

  it("HalfOpen success → Closed; failure → Open (cooldown re-armed)", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 100,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    clock.advance(100);
    expect(await cb.execute(async () => 1)).toBe(1);
    expect(cb.currentState).toBe(CircuitState.Closed);

    // Trip again
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Open);
    clock.advance(100);
    // HalfOpen failure → Open
    await expect(
      cb.execute(async () => Promise.reject(new Error("y"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Open);
  });

  it("rejects invalid options at construction", () => {
    expect(
      () => new CircuitBreaker({ failureThreshold: 0, cooldownMs: 100 }),
    ).toThrow(RangeError);
    expect(
      () => new CircuitBreaker({ failureThreshold: 1, cooldownMs: -1 }),
    ).toThrow(RangeError);
  });

  it("CircuitOpenError default message", () => {
    expect(new CircuitOpenError().message).toBe("circuit is open");
    expect(new CircuitOpenError("custom").message).toBe("custom");
  });

  it("uses Date.now when no nowFunc supplied", async () => {
    const cb = new CircuitBreaker({
      failureThreshold: 1,
      cooldownMs: 0,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    // cooldownMs=0 → immediately HalfOpen
    expect(cb.currentState).toBe(CircuitState.HalfOpen);
  });

  it("concurrent calls during state transitions remain consistent", async () => {
    const cb = new CircuitBreaker<number>({
      failureThreshold: 5,
      cooldownMs: 10000,
    });
    const results = await Promise.allSettled(
      [1, 2, 3, 4, 5].map(() =>
        cb.execute(async () => Promise.reject(new Error("x"))),
      ),
    );
    expect(results.every((r) => r.status === "rejected")).toBe(true);
    expect(cb.currentState).toBe(CircuitState.Open);
  });
});

// ===========================================================================
// F-17 — HalfOpen single-probe enforcement (C# Interlocked.CompareExchange
// parity). JS is single-threaded so a synchronous check-and-set on a boolean
// flag, performed BEFORE the first await, guarantees exactly one probe.
// ===========================================================================

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

describe("CircuitBreaker — F-17 HalfOpen single-probe", () => {
  it("only ONE of N concurrent HalfOpen callers runs the op; losers throw CircuitOpenError", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 100,
      nowFunc: () => clock.now,
    });

    // Trip → Open.
    await expect(
      cb.execute(async () => Promise.reject(new Error("trip"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Open);

    // Elapse cooldown → next execute will transition to HalfOpen.
    clock.advance(100);
    expect(cb.currentState).toBe(CircuitState.HalfOpen);

    // Fire N concurrent callers. The probe op is a deferred promise so the
    // winner stays in-flight while the losers arrive.
    let opInvocations = 0;
    const probe = deferred<number>();
    const op = (): Promise<number> => {
      opInvocations++;
      return probe.promise;
    };

    const settled = Promise.allSettled([
      cb.execute(op),
      cb.execute(op),
      cb.execute(op),
      cb.execute(op),
      cb.execute(op),
    ]);

    // Exactly one caller invoked the op (the single probe).
    expect(opInvocations).toBe(1);

    // Release the probe → it succeeds → Closed.
    probe.resolve(7);
    const results = await settled;

    // One fulfilled (the probe), four rejected with CircuitOpenError.
    const fulfilled = results.filter((r) => r.status === "fulfilled");
    const rejected = results.filter((r) => r.status === "rejected");
    expect(fulfilled).toHaveLength(1);
    expect((fulfilled[0] as PromiseFulfilledResult<number>).value).toBe(7);
    expect(rejected).toHaveLength(4);
    for (const r of rejected)
      expect((r as PromiseRejectedResult).reason).toBeInstanceOf(
        CircuitOpenError,
      );
    expect(cb.currentState).toBe(CircuitState.Closed);
  });

  it("concurrent HalfOpen losers receive the fallback when one is supplied", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 100,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("trip"))),
    ).rejects.toThrow();
    clock.advance(100);

    let opInvocations = 0;
    const probe = deferred<number>();
    const op = (): Promise<number> => {
      opInvocations++;
      return probe.promise;
    };
    const fallback = (): number => -1;

    const settled = Promise.all([
      cb.execute(op, fallback),
      cb.execute(op, fallback),
      cb.execute(op, fallback),
    ]);

    expect(opInvocations).toBe(1);

    probe.resolve(42);
    const results = await settled;
    // The probe winner returns 42; the two losers return the fallback (-1).
    expect(results.filter((v) => v === 42)).toHaveLength(1);
    expect(results.filter((v) => v === -1)).toHaveLength(2);
  });

  it("after a successful probe, the next probe slot is available again", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 100,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("trip"))),
    ).rejects.toThrow();
    clock.advance(100);
    // Probe succeeds → Closed → probeInFlight cleared.
    expect(await cb.execute(async () => 1)).toBe(1);
    expect(cb.currentState).toBe(CircuitState.Closed);

    // Trip again, elapse cooldown — a fresh probe is admitted (flag was reset).
    await expect(
      cb.execute(async () => Promise.reject(new Error("trip2"))),
    ).rejects.toThrow();
    clock.advance(100);
    expect(await cb.execute(async () => 2)).toBe(2);
  });
});

// ===========================================================================
// F-18 — fallback parameter (C# ExecuteAsync(operation, fallback?, ct) parity)
// ===========================================================================

describe("CircuitBreaker — F-18 fallback parameter", () => {
  it("Open + fallback → returns fallback value, no throw", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 10_000,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("trip"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Open);

    // Open: the op must NOT run; the fallback supplies the value.
    let opRan = false;
    const value = await cb.execute(
      async () => {
        opRan = true;
        return 1;
      },
      () => 99,
    );
    expect(value).toBe(99);
    expect(opRan).toBe(false);
  });

  it("Open + async fallback → awaits and returns the fallback value", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 10_000,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("trip"))),
    ).rejects.toThrow();
    const value = await cb.execute(
      async () => 1,
      () => Promise.resolve(123),
    );
    expect(value).toBe(123);
  });

  it("Open + NO fallback → throws CircuitOpenError", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 10_000,
      nowFunc: () => clock.now,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("trip"))),
    ).rejects.toThrow();
    await expect(cb.execute(async () => 1)).rejects.toThrow(CircuitOpenError);
  });
});

// ===========================================================================
// F-29 — isFailure value-based failure predicate (C# parity)
// ===========================================================================

describe("CircuitBreaker — F-29 isFailure value predicate", () => {
  it("a returned value satisfying isFailure trips the breaker WITHOUT throwing", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<{ ok: boolean }>({
      failureThreshold: 2,
      cooldownMs: 10_000,
      nowFunc: () => clock.now,
      isFailure: (r) => !r.ok,
    });

    // First failing return — counts, but is returned (no throw).
    const r1 = await cb.execute(async () => ({ ok: false }));
    expect(r1).toEqual({ ok: false });
    expect(cb.currentState).toBe(CircuitState.Closed);

    // Second failing return — threshold reached → Open.
    const r2 = await cb.execute(async () => ({ ok: false }));
    expect(r2).toEqual({ ok: false });
    expect(cb.currentState).toBe(CircuitState.Open);

    // Now Open → fast-fail.
    await expect(cb.execute(async () => ({ ok: true }))).rejects.toThrow(
      CircuitOpenError,
    );
  });

  it("a returned value NOT satisfying isFailure resets the failure count", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<{ ok: boolean }>({
      failureThreshold: 2,
      cooldownMs: 10_000,
      nowFunc: () => clock.now,
      isFailure: (r) => !r.ok,
    });
    await cb.execute(async () => ({ ok: false })); // count = 1
    await cb.execute(async () => ({ ok: true })); // success → count reset
    await cb.execute(async () => ({ ok: false })); // count = 1 again
    // Still Closed — the success in between reset the counter, so two
    // non-consecutive failures never reach the threshold.
    expect(cb.currentState).toBe(CircuitState.Closed);
  });

  it("thrown errors still count even when isFailure is supplied", async () => {
    const clock = new TestClock();
    const cb = new CircuitBreaker<{ ok: boolean }>({
      failureThreshold: 1,
      cooldownMs: 10_000,
      nowFunc: () => clock.now,
      isFailure: (r) => !r.ok,
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("boom"))),
    ).rejects.toThrow("boom");
    expect(cb.currentState).toBe(CircuitState.Open);
  });
});

// ===========================================================================
// F-19 — onStateChange callback (C# parity: fires on real transitions only)
// ===========================================================================

describe("CircuitBreaker — F-19 onStateChange callback", () => {
  it("fires on Closed→Open, Open→HalfOpen, HalfOpen→Closed and HalfOpen→Open", async () => {
    const clock = new TestClock();
    const transitions: [CircuitState, CircuitState][] = [];
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 100,
      nowFunc: () => clock.now,
      onStateChange: (from, to) => transitions.push([from, to]),
    });

    // Closed → Open.
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    // Open → HalfOpen (transition happens inside execute after cooldown) →
    // HalfOpen → Closed (probe succeeds).
    clock.advance(100);
    expect(await cb.execute(async () => 1)).toBe(1);

    // Trip again, then a HalfOpen failure → Open.
    await expect(
      cb.execute(async () => Promise.reject(new Error("y"))),
    ).rejects.toThrow();
    clock.advance(100);
    await expect(
      cb.execute(async () => Promise.reject(new Error("z"))),
    ).rejects.toThrow();

    expect(transitions).toEqual([
      [CircuitState.Closed, CircuitState.Open],
      [CircuitState.Open, CircuitState.HalfOpen],
      [CircuitState.HalfOpen, CircuitState.Closed],
      [CircuitState.Closed, CircuitState.Open],
      [CircuitState.Open, CircuitState.HalfOpen],
      [CircuitState.HalfOpen, CircuitState.Open],
    ]);
  });

  it("does NOT fire on an idempotent Closed→Closed (repeated success)", async () => {
    const transitions: [CircuitState, CircuitState][] = [];
    const cb = new CircuitBreaker<number>({
      failureThreshold: 2,
      cooldownMs: 100,
      onStateChange: (from, to) => transitions.push([from, to]),
    });
    expect(await cb.execute(async () => 1)).toBe(1);
    expect(await cb.execute(async () => 2)).toBe(2);
    expect(await cb.execute(async () => 3)).toBe(3);
    expect(transitions).toHaveLength(0);
  });
});

// ===========================================================================
// F-20 — reset() (C# Reset() parity)
// ===========================================================================

describe("CircuitBreaker — F-20 reset()", () => {
  it("reset from Open → Closed, clears the failure count, fires the callback", async () => {
    const clock = new TestClock();
    const transitions: [CircuitState, CircuitState][] = [];
    const cb = new CircuitBreaker<number>({
      failureThreshold: 2,
      cooldownMs: 10_000,
      nowFunc: () => clock.now,
      onStateChange: (from, to) => transitions.push([from, to]),
    });
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Open);

    cb.reset();
    expect(cb.currentState).toBe(CircuitState.Closed);
    // The Open→Closed transition fired.
    expect(transitions).toContainEqual([
      CircuitState.Open,
      CircuitState.Closed,
    ]);

    // Counter was cleared: a single failure must NOT immediately re-open
    // (threshold is 2).
    await expect(
      cb.execute(async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    expect(cb.currentState).toBe(CircuitState.Closed);
  });

  it("reset when already Closed is a no-op and does NOT fire the callback", () => {
    const transitions: [CircuitState, CircuitState][] = [];
    const cb = new CircuitBreaker<number>({
      failureThreshold: 1,
      cooldownMs: 100,
      onStateChange: (from, to) => transitions.push([from, to]),
    });
    cb.reset();
    expect(cb.currentState).toBe(CircuitState.Closed);
    expect(transitions).toHaveLength(0);
  });
});
