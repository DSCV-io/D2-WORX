// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  TIMEOUT_DEFAULTS,
  TimeoutError,
  TimeoutLayer,
} from "../../src/pipeline/timeout-layer.js";
import type { IResilientLayer } from "../../src/pipeline/i-resilient-layer.js";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** A trivial layer that runs the op directly (no wrapping). */
class TerminalLayer implements IResilientLayer {
  execute<T>(
    _key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    return op(signal);
  }
}

/** Returns a deferred: an unresolved Promise plus its `resolve`/`reject` handles. */
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

function layerWith(durationMs: number): TimeoutLayer {
  return new TimeoutLayer(new TerminalLayer(), { durationMs });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("TimeoutLayer — construction", () => {
  it("uses TIMEOUT_DEFAULTS when no opts supplied", () => {
    // Internal opts not directly inspectable; verify defaults via behavior:
    // just ensure the layer is created without error.
    expect(() => new TimeoutLayer(new TerminalLayer())).not.toThrow();
  });

  it("exports TIMEOUT_DEFAULTS with durationMs = 10 000", () => {
    expect(TIMEOUT_DEFAULTS.durationMs).toBe(10_000);
  });
});

describe("TimeoutLayer — disabled (pass-through)", () => {
  it("durationMs = 0 → pass-through, op runs and resolves", async () => {
    const layer = layerWith(0);
    expect(await layer.execute("k", async () => 42)).toBe(42);
  });

  it("durationMs < 0 → pass-through, op runs and resolves", async () => {
    const layer = layerWith(-1);
    expect(await layer.execute("k", async () => "ok")).toBe("ok");
  });

  it("durationMs = 0 → pass-through, op error propagates normally", async () => {
    const layer = layerWith(0);
    await expect(
      layer.execute("k", async () => Promise.reject(new Error("inner"))),
    ).rejects.toThrow("inner");
  });
});

describe("TimeoutLayer — op completes before deadline", () => {
  it("op resolves before deadline → returns value, no TimeoutError", async () => {
    const layer = layerWith(5_000);
    // Op resolves before the 5-second deadline.
    expect(await layer.execute("k", async () => 99)).toBe(99);
  });

  it("op rejects before deadline → propagates error (not TimeoutError)", async () => {
    const layer = layerWith(5_000);
    await expect(
      layer.execute("k", async () => {
        throw new Error("inner-error");
      }),
    ).rejects.toThrow("inner-error");
    await expect(
      layer.execute("k", async () => {
        throw new Error("inner-error");
      }),
    ).rejects.not.toThrow(TimeoutError);
  });
});

describe("TimeoutLayer — timeout fires", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("op hangs past deadline → throws TimeoutError (not a generic Error)", async () => {
    const d = deferred<number>();
    const layer = layerWith(100);
    // Attach a no-op catch BEFORE advancing timers so Node.js sees the
    // rejection handler registered synchronously (prevents PromiseRejectionHandledWarning).
    const p = layer.execute("k", () => d.promise);
    const caught = p.catch((e) => e);
    await vi.advanceTimersByTimeAsync(101);
    expect(await caught).toBeInstanceOf(TimeoutError);
    d.resolve(0);
  });

  it("TimeoutError name is 'TimeoutError'", async () => {
    const d = deferred<number>();
    const layer = layerWith(100);
    const p = layer.execute("k", () => d.promise);
    const caught = p.catch((e) => e);
    await vi.advanceTimersByTimeAsync(101);
    expect(((await caught) as Error).name).toBe("TimeoutError");
    d.resolve(0);
  });

  it("TimeoutError message contains the duration", async () => {
    const d = deferred<number>();
    const layer = layerWith(250);
    const p = layer.execute("k", () => d.promise);
    const caught = p.catch((e) => e);
    await vi.advanceTimersByTimeAsync(251);
    expect(((await caught) as Error).message).toMatch(/250/);
    d.resolve(0);
  });
});

describe("TimeoutLayer — caller-abort is NOT masked as TimeoutError", () => {
  it("caller aborts before timeout → throws AbortError (not TimeoutError)", async () => {
    // Simulate a caller abort by having the inner op throw an AbortError.
    // The TimeoutLayer must propagate it as-is — not swallow it as TimeoutError.
    const layer = layerWith(10_000);
    const abortErr = Object.assign(new Error("aborted"), {
      name: "AbortError",
    });
    await expect(
      layer.execute("k", async () => Promise.reject(abortErr)),
    ).rejects.toMatchObject({ name: "AbortError" });
  });

  it("inner error that is not TimeoutError is propagated unchanged", async () => {
    const layer = layerWith(10_000);
    const cause = new RangeError("bad input");
    await expect(
      layer.execute("k", () => Promise.reject(cause)),
    ).rejects.toThrow(RangeError);
  });
});

describe("TimeoutLayer — key forwarded to inner", () => {
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
    const layer = new TimeoutLayer(recordKey, { durationMs: 1_000 });
    await layer.execute("my-key", async () => 1);
    expect(seen).toEqual(["my-key"]);
  });
});

describe("TimeoutLayer — TimeoutError properties", () => {
  it("TimeoutError is an instance of Error", () => {
    expect(new TimeoutError()).toBeInstanceOf(Error);
  });

  it("default message is meaningful", () => {
    expect(new TimeoutError().message).toBe("operation timed out");
  });

  it("custom message overrides default", () => {
    expect(new TimeoutError("custom msg").message).toBe("custom msg");
  });
});
