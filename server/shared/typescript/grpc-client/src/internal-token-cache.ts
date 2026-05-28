// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { InternalTokenSnapshot } from "./types.js";

/**
 * Skew applied when checking whether a cached token is still usable. We
 * treat a token as expired this many ms BEFORE its actual expiry so a
 * concurrent request never races the wall clock.
 */
const _DEFAULT_EXPIRY_SKEW_MS = 5_000;

/**
 * Single-slot cache for the BFF's KeyCustodian-minted internal token.
 *
 * Mirrors the .NET `ServiceIdentityCache`'s `Volatile.Write` semantics:
 * the JS event loop's single-threaded property serializes reads + writes,
 * so no atomic primitive is needed — assigning the snapshot in one
 * statement IS atomic from JS's perspective.
 */
export class InternalTokenCache {
  private current: InternalTokenSnapshot | undefined;
  private readonly skewMs: number;
  private readonly clock: () => number;

  constructor(opts: { skewMs?: number; clock?: () => number } = {}) {
    this.skewMs = opts.skewMs ?? _DEFAULT_EXPIRY_SKEW_MS;
    this.clock = opts.clock ?? Date.now;
  }

  /** Returns the cached snapshot when fresh; undefined when empty or expired. */
  tryGet(): InternalTokenSnapshot | undefined {
    const snapshot = this.current;
    if (snapshot === undefined) return undefined;
    if (snapshot.expiresAtMs - this.skewMs <= this.clock()) return undefined;
    return snapshot;
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
