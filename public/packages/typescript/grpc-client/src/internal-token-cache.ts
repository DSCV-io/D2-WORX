// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { InternalTokenSnapshot } from "./types.js";

/**
 * Skew applied when checking whether a cached token is still usable. We
 * treat a token as expired this many ms BEFORE its actual expiry so a
 * concurrent request never races the wall clock.
 */
const _DEFAULT_EXPIRY_SKEW_MS = 5_000;

/**
 * Lead window for proactive refresh-ahead. Tokens older than
 * `expiresAtMs − refreshLeadMs` (but not yet past the hard-expiry skew)
 * are served immediately AND trigger a fire-and-forget background re-mint
 * so the next call finds a fresh token without waiting for expiry.
 *
 * Must be larger than `_DEFAULT_EXPIRY_SKEW_MS` so the aging window
 * (`expiresAtMs − refreshLeadMs` ≤ now < `expiresAtMs − skewMs`) is
 * non-empty.
 */
const _DEFAULT_REFRESH_LEAD_MS = 60_000;

/** Result of {@link InternalTokenCache.tryGet}. */
export interface TryGetResult {
  /**
   * The cached snapshot when the token is still usable (fresh OR aging);
   * absent when the cache is empty or the token has hard-expired
   * (past `expiresAtMs − skewMs`).
   */
  readonly snapshot?: InternalTokenSnapshot;
  /**
   * `true` when the token is in the refresh-ahead window — still valid to
   * serve, but the caller should fire a background re-mint. Always `false`
   * when `snapshot` is `undefined`.
   */
  readonly shouldRefreshAhead: boolean;
}

/**
 * Single-slot cache for the BFF's internal boundary token (its OAuth
 * `client_credentials` token for calls to Edge), with proactive
 * refresh-ahead to avoid expiry latency on hot paths.
 *
 * **Three states per read** (see {@link tryGet}):
 * - **Fresh** (`now < expiresAtMs − refreshLeadMs`): serve the token; no
 *   refresh signal.
 * - **Aging** (`expiresAtMs − refreshLeadMs ≤ now < expiresAtMs − skewMs`):
 *   the token is still valid — serve it AND set `shouldRefreshAhead = true`
 *   so the interceptor fires a fire-and-forget background re-mint.
 * - **Expired** (`now ≥ expiresAtMs − skewMs`): cache miss → caller mints
 *   synchronously.
 *
 * The JS event loop's single-threaded property serializes reads + writes,
 * so no atomic primitive is needed — assigning the snapshot in one
 * statement IS atomic from JS's perspective.
 *
 * The `@d2/resilience` Singleflight layer on the token client ensures that
 * N concurrent callers all entering the aging or expired window collapse to
 * ONE upstream OAuth call, regardless of how many fire-and-forget refreshes
 * are triggered.
 */
export class InternalTokenCache {
  private current: InternalTokenSnapshot | undefined;
  private readonly skewMs: number;
  private readonly refreshLeadMs: number;
  private readonly clock: () => number;

  constructor(
    opts: {
      skewMs?: number;
      refreshLeadMs?: number;
      clock?: () => number;
    } = {},
  ) {
    this.skewMs = opts.skewMs ?? _DEFAULT_EXPIRY_SKEW_MS;
    this.refreshLeadMs = opts.refreshLeadMs ?? _DEFAULT_REFRESH_LEAD_MS;
    this.clock = opts.clock ?? Date.now;
    if (this.refreshLeadMs <= this.skewMs) {
      throw new TypeError(
        `InternalTokenCache: refreshLeadMs (${this.refreshLeadMs}) must be greater than skewMs (${this.skewMs}) to maintain a non-empty aging window`,
      );
    }
  }

  /**
   * Returns a {@link TryGetResult} describing the current cache state.
   *
   * - `snapshot` is defined when the token is still usable (fresh or aging).
   * - `shouldRefreshAhead` is `true` only when the token is in the aging
   *   window — callers should fire a fire-and-forget re-mint WITHOUT awaiting
   *   it and WITHOUT blocking the in-flight request.
   */
  tryGet(): TryGetResult {
    const snapshot = this.current;
    if (snapshot === undefined)
      return { snapshot: undefined, shouldRefreshAhead: false };
    const now = this.clock();
    // Hard-expiry boundary: treat the token as expired `skewMs` before its
    // actual expiry to avoid racing the wall clock.
    const hardExpiryMs = snapshot.expiresAtMs - this.skewMs;
    if (now >= hardExpiryMs)
      return { snapshot: undefined, shouldRefreshAhead: false };
    // Refresh-ahead boundary: token is valid but has entered the lead window.
    const refreshAheadMs = snapshot.expiresAtMs - this.refreshLeadMs;
    const shouldRefreshAhead = now >= refreshAheadMs;
    return { snapshot, shouldRefreshAhead };
  }

  /** Stores a snapshot, replacing any prior entry. */
  set(snapshot: InternalTokenSnapshot): void {
    this.current = snapshot;
  }

  /** Drops the cache (test seam + invalidate-on-401 path). */
  clear(): void {
    this.current = undefined;
  }
}
