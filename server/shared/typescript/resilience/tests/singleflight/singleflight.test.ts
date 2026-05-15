// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it, vi } from "vitest";
import { Singleflight } from "../../src/singleflight/singleflight.js";

describe("Singleflight", () => {
  it("dedupes concurrent calls with the same key", async () => {
    const sf = new Singleflight<string, number>();
    const factory = vi.fn(async () => {
      await new Promise((r) => setTimeout(r, 5));
      return 42;
    });
    const [r1, r2, r3] = await Promise.all([
      sf.do("k", factory),
      sf.do("k", factory),
      sf.do("k", factory),
    ]);
    expect(r1).toBe(42);
    expect(r2).toBe(42);
    expect(r3).toBe(42);
    expect(factory).toHaveBeenCalledTimes(1);
  });

  it("different keys execute independently", async () => {
    const sf = new Singleflight<string, number>();
    const factoryA = vi.fn(async () => 1);
    const factoryB = vi.fn(async () => 2);
    const [a, b] = await Promise.all([
      sf.do("a", factoryA),
      sf.do("b", factoryB),
    ]);
    expect(a).toBe(1);
    expect(b).toBe(2);
    expect(factoryA).toHaveBeenCalledTimes(1);
    expect(factoryB).toHaveBeenCalledTimes(1);
  });

  it("subsequent call after settle re-executes the factory", async () => {
    const sf = new Singleflight<string, number>();
    const factory = vi.fn(async () => 7);
    await sf.do("k", factory);
    await sf.do("k", factory);
    expect(factory).toHaveBeenCalledTimes(2);
  });

  it("propagates rejections + clears entry afterward", async () => {
    const sf = new Singleflight<string, number>();
    const factory = vi
      .fn()
      .mockRejectedValueOnce(new Error("boom"))
      .mockResolvedValueOnce(42);
    await expect(sf.do("k", factory)).rejects.toThrow("boom");
    expect(sf.inflightCount).toBe(0);
    const r = await sf.do("k", factory);
    expect(r).toBe(42);
  });

  it("inflightCount tracks in-flight ops", async () => {
    const sf = new Singleflight<string, number>();
    let resolve!: (v: number) => void;
    const p = sf.do("k", () => new Promise<number>((r) => (resolve = r)));
    expect(sf.inflightCount).toBe(1);
    resolve(1);
    await p;
    expect(sf.inflightCount).toBe(0);
  });
});
