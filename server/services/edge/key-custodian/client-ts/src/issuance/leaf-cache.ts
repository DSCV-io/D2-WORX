// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { LeafSnapshot } from "./workload-leaf-material.js";

/**
 * Single-value leaf cache. The TS twin of the .NET `WorkloadLeafCache` — holds at
 * most one live leaf snapshot; readers get it only while it is unexpired
 * (`notAfter > now`). `peekRaw` exposes the possibly-expired snapshot so the
 * serve-stale / failure-logging paths can inspect the not-after.
 */
export class WorkloadLeafCache {
  #current: LeafSnapshot | undefined;

  /** Publish a new snapshot, superseding any prior one. */
  set(snapshot: LeafSnapshot): void {
    this.#current = snapshot;
  }

  /**
   * Returns the cached snapshot iff it is still valid at `nowMs` (`notAfter > now`);
   * otherwise undefined (expired / never set).
   */
  tryGet(nowMs: number): LeafSnapshot | undefined {
    const current = this.#current;

    if (current === undefined) return undefined;

    return current.notAfter.epochMilliseconds > nowMs ? current : undefined;
  }

  /** The raw cached snapshot regardless of expiry (for stale inspection / logging). */
  peekRaw(): LeafSnapshot | undefined {
    return this.#current;
  }
}
