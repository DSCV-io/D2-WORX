// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { RetryHelper } from "../retry/retry-helper.js";
import type { RetryOptions } from "../retry/retry-options.js";
import type { IResilientLayer } from "./i-resilient-layer.js";

/** Layer wrapping an inner layer with retry policy. */
export class RetryLayer implements IResilientLayer {
  constructor(
    private readonly inner: IResilientLayer,
    private readonly opts: Partial<RetryOptions<unknown>>,
  ) {}

  execute<T>(key: string, op: () => Promise<T>): Promise<T> {
    return RetryHelper.retryAsync<T>(
      () => this.inner.execute(key, op),
      this.opts as Partial<RetryOptions<T>>,
    );
  }
}
