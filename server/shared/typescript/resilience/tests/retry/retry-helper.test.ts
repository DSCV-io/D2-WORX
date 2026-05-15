// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it, vi } from "vitest";
import * as factories from "@d2/result";
import { RetryHelper } from "../../src/retry/retry-helper.js";

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
    const r = await RetryHelper.retryAsync(op, {
      maxAttempts: 5,
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

  it("non-Error thrown values use the default predicate path (retried)", async () => {
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
    expect(calls).toBe(2);
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
          delayFunc: noDelay,
        },
      ),
    ).rejects.toThrow("non-result error");
    expect(calls).toBe(2);
  });
});
