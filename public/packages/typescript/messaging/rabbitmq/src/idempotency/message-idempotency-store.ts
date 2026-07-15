// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { type D2Result, ok, serviceUnavailable } from "@d2/result";
import { falsey } from "@d2/utilities";

/**
 * Dedup store for the consumer's idempotency window. Twin of the .NET
 * `IMessageIdempotencyStore` — an at-least-once consumer marks a processed
 * `message-id` and skips a redelivery of the same id.
 *
 * The read path (`hasSeen`) and write path (`markSeen`) have DELIBERATELY
 * different failure postures in the consumer pipeline (see the runtime's
 * idempotency contract): a read-path `ServiceUnavailable` fails OPEN (process
 * anyway), a write-path `ServiceUnavailable` fails CLOSED (NACK to DLQ) so the
 * dedup window is never silently left unguarded.
 */
export interface IMessageIdempotencyStore {
  /**
   * Reports whether `messageId` has already been marked seen. A store outage
   * returns `ServiceUnavailable` so the caller can fail-open.
   */
  hasSeen(messageId: string): Promise<D2Result<boolean>>;

  /**
   * Records `messageId` as seen (with a bounded TTL). A store outage returns
   * `ServiceUnavailable` so the caller can fail-closed.
   */
  markSeen(messageId: string): Promise<D2Result>;
}

const _DEFAULT_TTL_MS = 24 * 60 * 60 * 1000;
const _DEFAULT_MAX_ENTRIES = 100_000;

/**
 * In-process {@link IMessageIdempotencyStore} with a 24-hour TTL. Suitable for
 * single-instance deployments and tests; a multi-instance deployment supplies
 * a distributed-cache-backed store instead (the Redis-backed impl is a later
 * consumer's concern — this port is its replace-trigger). Never returns
 * `ServiceUnavailable` (an in-memory map does not have a store outage), but the
 * port models it for the distributed impls.
 *
 * Eviction (twin of the .NET `CacheIdempotencyStore`, which delegates to the
 * distributed cache's native TTL so it "doesn't grow unbounded"): this
 * in-memory fallback prunes ITSELF. Every {@link markSeen} sweeps out entries
 * whose TTL has elapsed BEFORE inserting — so a stream of processed-but-never-
 * redelivered ids can't linger to the process restart — and a hard entry cap
 * bounds the live (unexpired) set against a within-window burst. Because every
 * live entry shares the same TTL, the `Map`'s insertion order equals its
 * ascending-expiry order, which makes the front-to-back prune and the
 * oldest-first cap eviction correct with no per-entry timestamp scan.
 */
export class InMemoryMessageIdempotencyStore implements IMessageIdempotencyStore {
  private readonly seen = new Map<string, number>();
  private readonly ttlMs: number;
  private readonly maxEntries: number;
  private readonly now: () => number;

  /**
   * @param ttlMs Time-to-live for a mark, in milliseconds (default 24h).
   * @param now Injectable clock (epoch ms) for deterministic tests.
   * @param maxEntries Hard cap on retained (unexpired) marks (default 100k).
   */
  constructor(
    ttlMs: number = _DEFAULT_TTL_MS,
    now: () => number = Date.now,
    maxEntries: number = _DEFAULT_MAX_ENTRIES,
  ) {
    this.ttlMs = ttlMs;
    this.now = now;
    this.maxEntries = maxEntries;
  }

  hasSeen(messageId: string): Promise<D2Result<boolean>> {
    if (falsey(messageId))
      return Promise.resolve(serviceUnavailable<boolean>());

    const expiry = this.seen.get(messageId);
    if (expiry === undefined) return Promise.resolve(ok(false));

    if (expiry <= this.now()) {
      this.seen.delete(messageId);
      return Promise.resolve(ok(false));
    }

    return Promise.resolve(ok(true));
  }

  markSeen(messageId: string): Promise<D2Result> {
    if (falsey(messageId)) return Promise.resolve(serviceUnavailable());

    this.pruneExpired();

    // Re-key so the entry lands at the tail — keeps the Map's insertion order
    // equal to ascending-expiry order (all live entries share one TTL), the
    // invariant the front-to-back prune and oldest-first cap eviction rely on.
    this.seen.delete(messageId);
    this.seen.set(messageId, this.now() + this.ttlMs);

    this.evictToCap();

    return Promise.resolve(ok());
  }

  /** Drops every entry whose TTL has elapsed (front-to-back, stops at the first live one). */
  private pruneExpired(): void {
    const now = this.now();
    for (const [id, expiry] of this.seen) {
      if (expiry > now) break; // ordered by expiry — first live entry ends the sweep

      this.seen.delete(id);
    }
  }

  /** Enforces the hard cap by evicting the oldest-expiring entries first. */
  private evictToCap(): void {
    while (this.seen.size > this.maxEntries) {
      const oldest = this.seen.keys().next().value as string;
      this.seen.delete(oldest);
    }
  }
}
