// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it, vi } from "vitest";
import { CircuitOpenError } from "../../src/circuit-breaker/circuit-open-error.js";
import { ResilientPipelineBuilder } from "../../src/pipeline/resilient-pipeline.js";

const noDelay = () => Promise.resolve();

describe("ResilientPipelineBuilder", () => {
  it("empty pipeline runs the inner op", async () => {
    const p = new ResilientPipelineBuilder().build();
    expect(await p.execute("k", async () => 7)).toBe(7);
  });

  it("singleflight layer dedupes concurrent calls", async () => {
    const factory = vi.fn(async () => {
      await new Promise((r) => setTimeout(r, 5));
      return 42;
    });
    const p = new ResilientPipelineBuilder().useSingleflight().build();
    const [a, b] = await Promise.all([
      p.execute("k", factory),
      p.execute("k", factory),
    ]);
    expect(a).toBe(42);
    expect(b).toBe(42);
    expect(factory).toHaveBeenCalledTimes(1);
  });

  it("circuit breaker layer trips on consecutive failures", async () => {
    const p = new ResilientPipelineBuilder()
      .useCircuitBreaker({ failureThreshold: 1, cooldownMs: 10000 })
      .build();
    await expect(
      p.execute("k", async () => Promise.reject(new Error("x"))),
    ).rejects.toThrow();
    await expect(p.execute("k", async () => 1)).rejects.toThrow(
      CircuitOpenError,
    );
  });

  it("retry layer retries on transient errors", async () => {
    let calls = 0;
    const p = new ResilientPipelineBuilder()
      .useRetries({
        maxAttempts: 3,
        isTransient: () => true,
        delayFunc: noDelay,
      })
      .build();
    const r = await p.execute("k", async () => {
      calls++;
      if (calls < 3) throw new Error("transient");
      return calls;
    });
    expect(r).toBe(3);
  });

  it("composed pipeline — singleflight outer, retry inner", async () => {
    let calls = 0;
    const p = new ResilientPipelineBuilder()
      .useSingleflight()
      .useRetries({
        maxAttempts: 3,
        isTransient: () => true,
        delayFunc: noDelay,
      })
      .build();
    const r = await p.execute("k", async () => {
      calls++;
      if (calls < 2) throw new Error("transient");
      return calls;
    });
    expect(r).toBe(2);
  });

  it("execute on key with no breaker entry yet creates one lazily", async () => {
    const p = new ResilientPipelineBuilder()
      .useCircuitBreaker({ failureThreshold: 5, cooldownMs: 100 })
      .build();
    expect(await p.execute("a", async () => 1)).toBe(1);
    expect(await p.execute("b", async () => 2)).toBe(2);
  });
});
