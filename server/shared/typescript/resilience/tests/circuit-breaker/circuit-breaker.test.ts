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
