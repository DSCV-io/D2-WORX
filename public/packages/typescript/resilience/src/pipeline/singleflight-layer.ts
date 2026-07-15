// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { Singleflight } from "../singleflight/singleflight.js";
import { raceAbort } from "./abort.js";
import type { IResilientLayer } from "./i-resilient-layer.js";

/**
 * Layer wrapping an inner layer with key-based singleflight dedup. Mirrors
 * .NET `SingleflightLayer<TKey, TValue>` + `Singleflight.ExecuteAsync`.
 *
 * Per-caller cancellation affects ONLY that caller's wait, never the shared
 * operation: the deduplicated inner op runs with NO signal (`undefined`, the JS
 * analogue of `CancellationToken.None`), so one caller aborting cannot cancel
 * the shared work that the other waiters still depend on. Each caller's wait is
 * raced against that caller's own signal — an aborting caller's promise rejects
 * with `AbortError` while the shared op continues and the remaining waiters
 * receive its result.
 */
export class SingleflightLayer implements IResilientLayer {
  private readonly sf = new Singleflight<string, unknown>();

  constructor(private readonly inner: IResilientLayer) {}

  execute<T>(
    key: string,
    op: (signal?: AbortSignal) => Promise<T>,
    signal?: AbortSignal,
  ): Promise<T> {
    // The shared run gets NO signal — one caller's abort must not cancel it.
    const shared = this.sf.do(key, () =>
      this.inner.execute(key, op, undefined),
    ) as Promise<T>;
    // Each caller's wait IS cancellable by that caller's signal.
    return raceAbort(shared, signal);
  }
}
