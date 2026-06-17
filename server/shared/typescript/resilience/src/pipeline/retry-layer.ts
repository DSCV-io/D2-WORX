// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { RetryHelper } from "../retry/retry-helper.js";
import type { RetryOptions } from "../retry/retry-options.js";
import type { IResilientLayer } from "./i-resilient-layer.js";

/**
 * Layer wrapping an inner layer with retry policy. Mirrors .NET
 * `RetryLayer<TKey, TValue>` → `RetryHelper.RetryAsync((_, c) => next(c), …, ct)`.
 *
 * The caller signal threads through to {@link RetryHelper.retryAsync}: it
 * pre-flight-aborts, aborts the inter-attempt backoff delay, and (because
 * `RetryHelper` never classifies a cancellation as transient) propagates a
 * caller abort as `AbortError` instead of retrying it. The same signal is passed
 * to the inner op on every attempt; when a per-attempt-timeout layer sits inner
 * to this one, that inner layer derives its own per-attempt linked signal so
 * each attempt gets a fresh deadline.
 */
export class RetryLayer implements IResilientLayer {
  constructor(
    private readonly inner: IResilientLayer,
    private readonly opts: Partial<RetryOptions<unknown>>,
  ) {}

  execute<T>(
    key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    return RetryHelper.retryAsync<T>(
      () => this.inner.execute(key, op, signal),
      this.opts as Partial<RetryOptions<T>>,
      signal,
    );
  }
}
