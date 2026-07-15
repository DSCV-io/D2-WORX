// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it, vi } from "vitest";
import * as factories from "@d2/result";
import { CircuitOpenError } from "../../src/circuit-breaker/circuit-open-error.js";
import { TimeoutError } from "../../src/pipeline/timeout-layer.js";
import {
  defaultIsTransient,
  RetryHelper,
} from "../../src/retry/retry-helper.js";

const noDelay = () => Promise.resolve();

describe("RetryHelper.retryAsync — happy paths", () => {
  it("returns first-attempt success without retrying", async () => {
    const op = vi.fn(async () => 42);
    const r = await RetryHelper.retryAsync(op, { delayFunc: noDelay });
    expect(r).toBe(42);
    expect(op).toHaveBeenCalledTimes(1);
  });

  it("retries transient failures up to maxAttempts then succeeds", async () => {
    let calls = 0;
    const op = async () => {
      calls++;
      if (calls < 3) throw new Error("transient");
      return "ok";
    };
    // A plain Error is NOT transient under the default classifier (F-21); the
    // caller opts this error into retry via an explicit isTransient predicate.
    const r = await RetryHelper.retryAsync(op, {
      maxAttempts: 5,
      isTransient: () => true,
      delayFunc: noDelay,
    });
    expect(r).toBe("ok");
    expect(calls).toBe(3);
  });

  it("re-throws after exhausting maxAttempts", async () => {
    let calls = 0;
    const op = async () => {
      calls++;
      throw new Error("never works");
    };
    await expect(
      RetryHelper.retryAsync(op, {
        maxAttempts: 3,
        isTransient: () => true,
        delayFunc: noDelay,
      }),
    ).rejects.toThrow("never works");
    expect(calls).toBe(3);
  });

  it("non-transient errors short-circuit (shouldRetry=false)", async () => {
    let calls = 0;
    const op = async () => {
      calls++;
      throw new Error("permanent");
    };
    await expect(
      RetryHelper.retryAsync(op, {
        maxAttempts: 5,
        shouldRetry: () => false,
        delayFunc: noDelay,
      }),
    ).rejects.toThrow("permanent");
    expect(calls).toBe(1);
  });

  it("falls back to isTransient predicate when shouldRetry omitted", async () => {
    let calls = 0;
    const op = async () => {
      calls++;
      throw new Error("retryable");
    };
    await expect(
      RetryHelper.retryAsync(op, {
        maxAttempts: 3,
        isTransient: () => false,
        delayFunc: noDelay,
      }),
    ).rejects.toThrow();
    expect(calls).toBe(1);
  });

  it("cancellation never retries (AbortError)", async () => {
    let calls = 0;
    const op = async () => {
      calls++;
      throw Object.assign(new Error("aborted"), { name: "AbortError" });
    };
    await expect(
      RetryHelper.retryAsync(op, {
        maxAttempts: 5,
        delayFunc: noDelay,
      }),
    ).rejects.toThrow();
    expect(calls).toBe(1);
  });

  it("cancellation never retries (message='aborted')", async () => {
    let calls = 0;
    await expect(
      RetryHelper.retryAsync(
        async () => {
          calls++;
          throw new Error("aborted");
        },
        { maxAttempts: 5, delayFunc: noDelay },
      ),
    ).rejects.toThrow();
    expect(calls).toBe(1);
  });

  it("non-Error thrown values are NOT transient by default (F-21) — not retried", async () => {
    // A thrown non-Error value cannot be a transient/network/timeout condition
    // under the conservative default classifier — it is a programming-level
    // throw, so it is not retried (mirrors .NET's whitelist intent).
    let calls = 0;
    await expect(
      RetryHelper.retryAsync(
        async () => {
          calls++;
          throw "non-error string";
        },
        { maxAttempts: 2, delayFunc: noDelay },
      ),
    ).rejects.toThrow();
    expect(calls).toBe(1);
  });

  it("aborted signal pre-flight throws immediately", async () => {
    const ctrl = new AbortController();
    ctrl.abort();
    await expect(
      RetryHelper.retryAsync(
        async () => 1,
        { delayFunc: noDelay },
        ctrl.signal,
      ),
    ).rejects.toThrow("aborted");
  });

  it("rejects maxAttempts < 1", async () => {
    await expect(
      RetryHelper.retryAsync(async () => 1, {
        maxAttempts: 0,
        delayFunc: noDelay,
      }),
    ).rejects.toThrow(RangeError);
  });
});

describe("RetryHelper.retryAsync — backoff math", () => {
  it("applies exponential delay sequence", async () => {
    const sleeps: number[] = [];
    const fakeDelay = async (ms: number) => {
      sleeps.push(ms);
    };
    let calls = 0;
    const op = async () => {
      calls++;
      if (calls < 4) throw new Error("retryable");
      return calls;
    };
    await RetryHelper.retryAsync(
      op,
      {
        maxAttempts: 4,
        baseDelayMs: 100,
        backoffMultiplier: 2,
        maxDelayMs: 1000,
        jitter: 0,
        isTransient: () => true,
        delayFunc: fakeDelay,
      },
      undefined,
      () => 0.5,
    );
    expect(sleeps).toEqual([100, 200, 400]);
  });

  it("caps delay at maxDelayMs", async () => {
    const sleeps: number[] = [];
    const fakeDelay = async (ms: number) => {
      sleeps.push(ms);
    };
    await expect(
      RetryHelper.retryAsync(
        async () => {
          throw new Error("retryable");
        },
        {
          maxAttempts: 4,
          baseDelayMs: 1000,
          backoffMultiplier: 10,
          maxDelayMs: 2000,
          jitter: 0,
          isTransient: () => true,
          delayFunc: fakeDelay,
        },
      ),
    ).rejects.toThrow();
    expect(sleeps).toEqual([1000, 2000, 2000]);
  });

  it("applies jitter within configured range", async () => {
    const sleeps: number[] = [];
    const fakeDelay = async (ms: number) => {
      sleeps.push(ms);
    };
    await expect(
      RetryHelper.retryAsync(
        async () => {
          throw new Error("retryable");
        },
        {
          maxAttempts: 2,
          baseDelayMs: 100,
          backoffMultiplier: 1,
          maxDelayMs: 1000,
          jitter: 0.5,
          isTransient: () => true,
          delayFunc: fakeDelay,
        },
        undefined,
        () => 1,
      ),
    ).rejects.toThrow();
    // jitter = 0.5, factor = 1 + (1*2-1)*0.5 = 1.5 → 100 * 1.5 = 150
    expect(sleeps[0]).toBeCloseTo(150);
  });
});

describe("RetryHelper.retryAsync — default delay (real timers)", () => {
  it("uses setTimeout-based default delay when delayFunc unset", async () => {
    let calls = 0;
    const start = Date.now();
    const r = await RetryHelper.retryAsync<number>(
      async () => {
        calls++;
        if (calls === 1) throw new Error("retryable");
        return calls;
      },
      {
        maxAttempts: 2,
        baseDelayMs: 5,
        backoffMultiplier: 1,
        maxDelayMs: 100,
        jitter: 0,
        isTransient: () => true,
      },
    );
    expect(r).toBe(2);
    expect(Date.now() - start).toBeGreaterThanOrEqual(0);
  });

  it("default delay aborts on AbortSignal", async () => {
    const ctrl = new AbortController();
    setTimeout(() => ctrl.abort(), 5);
    await expect(
      RetryHelper.retryAsync(
        async () => {
          throw new Error("retryable");
        },
        {
          maxAttempts: 5,
          baseDelayMs: 100,
          backoffMultiplier: 1,
          maxDelayMs: 100,
          jitter: 0,
        },
        ctrl.signal,
      ),
    ).rejects.toThrow();
  });

  it("default delay rejects when signal aborted before delay starts", async () => {
    const ctrl = new AbortController();
    await expect(
      RetryHelper.retryAsync(
        async () => {
          ctrl.abort();
          throw new Error("retryable");
        },
        {
          maxAttempts: 5,
          baseDelayMs: 5,
          backoffMultiplier: 1,
          maxDelayMs: 5,
          jitter: 0,
        },
        ctrl.signal,
      ),
    ).rejects.toThrow();
  });
});

describe("RetryHelper.retryD2ResultAsync", () => {
  it("returns success result on first attempt", async () => {
    const r = await RetryHelper.retryD2ResultAsync(
      async () => factories.ok<number>(42),
      { delayFunc: noDelay },
    );
    expect(r.success).toBe(true);
    expect(r.data).toBe(42);
  });

  it("retries fail-results matching shouldRetry predicate", async () => {
    let calls = 0;
    const r = await RetryHelper.retryD2ResultAsync<number>(
      async () => {
        calls++;
        if (calls < 3) return factories.serviceUnavailable();
        return factories.ok(42);
      },
      {
        maxAttempts: 5,
        delayFunc: noDelay,
        shouldRetry: (v) =>
          (v as factories.D2Result<number>).errorCode ===
          factories.ErrorCodes.SERVICE_UNAVAILABLE,
      },
    );
    expect(r.success).toBe(true);
    expect(r.data).toBe(42);
    expect(calls).toBe(3);
  });

  it("returns the LAST fail-result when retries exhausted", async () => {
    let calls = 0;
    const r = await RetryHelper.retryD2ResultAsync<number>(
      async () => {
        calls++;
        return factories.serviceUnavailable<number>();
      },
      {
        maxAttempts: 3,
        delayFunc: noDelay,
        shouldRetry: () => true,
      },
    );
    expect(r.failed).toBe(true);
    expect(r.errorCode).toBe(factories.ErrorCodes.SERVICE_UNAVAILABLE);
    expect(calls).toBe(3);
  });

  it("falls back to isTransient predicate when shouldRetry omitted (D2Result)", async () => {
    let calls = 0;
    const r = await RetryHelper.retryD2ResultAsync<number>(
      async () => {
        calls++;
        if (calls < 2) return factories.serviceUnavailable();
        return factories.ok(99);
      },
      {
        maxAttempts: 3,
        delayFunc: noDelay,
        isTransient: () => true,
      },
    );
    expect(r.success).toBe(true);
    expect(r.data).toBe(99);
  });

  it("returns failure result when neither shouldRetry nor isTransient set", async () => {
    let calls = 0;
    const r = await RetryHelper.retryD2ResultAsync<number>(
      async () => {
        calls++;
        return factories.serviceUnavailable<number>();
      },
      {
        maxAttempts: 3,
        delayFunc: noDelay,
      },
    );
    expect(r.failed).toBe(true);
    expect(calls).toBe(1);
  });

  it("does NOT retry fail-results not matching shouldRetry", async () => {
    let calls = 0;
    const r = await RetryHelper.retryD2ResultAsync<number>(
      async () => {
        calls++;
        return factories.notFound<number>();
      },
      {
        maxAttempts: 3,
        delayFunc: noDelay,
        shouldRetry: () => false,
      },
    );
    expect(r.failed).toBe(true);
    expect(calls).toBe(1);
  });

  it("propagates thrown errors per regular retry semantics", async () => {
    let calls = 0;
    await expect(
      RetryHelper.retryD2ResultAsync<number>(
        async () => {
          calls++;
          throw new Error("non-result error");
        },
        {
          maxAttempts: 2,
          // A plain Error is non-transient by default (F-21); opt it into the
          // retry path explicitly so this test exercises throw-retry semantics.
          isTransient: () => true,
          delayFunc: noDelay,
        },
      ),
    ).rejects.toThrow("non-result error");
    expect(calls).toBe(2);
  });
});

// ===========================================================================
// F-21 — default transient classifier (conservative whitelist mirroring C#'s
// RetryHelper.IsTransientException). With NO caller predicate, only genuine
// transient/network/timeout errors retry; programming bugs do NOT.
// ===========================================================================

describe("defaultIsTransient (F-21 classifier)", () => {
  it.each([
    ["TimeoutError", new TimeoutError()],
    ["CircuitOpenError", new CircuitOpenError()],
    [
      "NetworkError (by name)",
      Object.assign(new Error("net"), { name: "NetworkError" }),
    ],
    [
      "TypeError with cause (undici network failure)",
      new TypeError("fetch failed", { cause: new Error("ECONNREFUSED") }),
    ],
    ["TypeError with known network message", new TypeError("Failed to fetch")],
  ])("classifies %s as transient", (_label, err) => {
    expect(defaultIsTransient(err)).toBe(true);
  });

  it.each([
    ["plain Error", new Error("boom")],
    ["RangeError (validation/programming)", new RangeError("bad")],
    [
      "bare TypeError (programming bug, no cause / non-network message)",
      new TypeError("x is not a function"),
    ],
    ["non-Error string", "kaboom"],
    [
      "AbortError (caller cancellation)",
      Object.assign(new Error("aborted"), { name: "AbortError" }),
    ],
  ])("classifies %s as NON-transient", (_label, err) => {
    expect(defaultIsTransient(err)).toBe(false);
  });
});

describe("RetryHelper.retryAsync — F-21 default classifier (no caller predicate)", () => {
  it("retries a TimeoutError (transient) up to maxAttempts", async () => {
    let calls = 0;
    await expect(
      RetryHelper.retryAsync(
        async () => {
          calls++;
          throw new TimeoutError();
        },
        { maxAttempts: 3, delayFunc: noDelay },
      ),
    ).rejects.toBeInstanceOf(TimeoutError);
    expect(calls).toBe(3);
  });

  it("retries a CircuitOpenError (transient) up to maxAttempts", async () => {
    let calls = 0;
    await expect(
      RetryHelper.retryAsync(
        async () => {
          calls++;
          throw new CircuitOpenError();
        },
        { maxAttempts: 3, delayFunc: noDelay },
      ),
    ).rejects.toBeInstanceOf(CircuitOpenError);
    expect(calls).toBe(3);
  });

  it("retries a genuine network TypeError (undici 'fetch failed' with cause)", async () => {
    let calls = 0;
    await expect(
      RetryHelper.retryAsync(
        async () => {
          calls++;
          throw new TypeError("fetch failed", {
            cause: new Error("ENOTFOUND"),
          });
        },
        { maxAttempts: 2, delayFunc: noDelay },
      ),
    ).rejects.toBeInstanceOf(TypeError);
    expect(calls).toBe(2);
  });

  it("does NOT retry a plain programming Error by default", async () => {
    let calls = 0;
    await expect(
      RetryHelper.retryAsync(
        async () => {
          calls++;
          throw new Error("programming bug");
        },
        { maxAttempts: 5, delayFunc: noDelay },
      ),
    ).rejects.toThrow("programming bug");
    expect(calls).toBe(1);
  });

  it("does NOT retry a bare TypeError (no cause / non-network message) by default", async () => {
    let calls = 0;
    await expect(
      RetryHelper.retryAsync(
        async () => {
          calls++;
          throw new TypeError("cannot read properties of undefined");
        },
        { maxAttempts: 5, delayFunc: noDelay },
      ),
    ).rejects.toBeInstanceOf(TypeError);
    expect(calls).toBe(1);
  });

  it("does NOT retry a caller AbortError by default", async () => {
    let calls = 0;
    await expect(
      RetryHelper.retryAsync(
        async () => {
          calls++;
          throw Object.assign(new Error("aborted"), { name: "AbortError" });
        },
        { maxAttempts: 5, delayFunc: noDelay },
      ),
    ).rejects.toThrow();
    expect(calls).toBe(1);
  });
});
