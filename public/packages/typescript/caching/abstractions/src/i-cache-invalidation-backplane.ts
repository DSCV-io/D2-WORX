// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { D2Result } from "@d2/result";

/**
 * Pub/sub backplane for cross-instance cache invalidation. Publishers
 * fire when a caller uses one of the `*AndBroadcast*` variants on
 * {@link ITieredCache} or {@link IDistributedCache}, or when any code
 * calls {@link publishInvalidation} directly. Subscribers (typically
 * tiered caches in other instances) receive the key and act on it —
 * usually by dropping the named entry from their L1.
 *
 * Extends {@link AsyncDisposable}. Runtime: Node ≥ 20 (global
 * `Symbol.asyncDispose`). Package `lib` includes `ESNext.Disposable`
 * so the types resolve.
 *
 * **Contract (twin of .NET `ICacheInvalidationBackplane` remarks):**
 *
 * 1. **Everyone acts** — no sender-ID filter; the publisher receives
 *    its own messages.
 * 2. **At-most-once** delivery (missed message → next read hits L2).
 * 3. **Dispose unsubscribes** — disposing a subscription **removes that
 *    handler from fan-out** and **stops further invalidation key
 *    delivery** to it (primary .NET Subscribe law). Signal abort alone
 *    is **not** sufficient — unsubscribe is the delivery-stop guarantee.
 * 4. **Signal-on-dispose** — the handler's `AbortSignal` is aborted when
 *    the **subscription** is disposed (accompanies unsubscribe; does not
 *    replace it).
 * 5. **Handler isolation** — one handler throw must not break delivery
 *    to other handlers.
 * 6. **Multi-sub independence** — each `subscribe` returns its own
 *    disposable; subscriptions are independent.
 * 7. **Dispose idempotency** — disposing a subscription (or the
 *    backplane) more than once is safe (no throw).
 * 8. **Backplane dispose cascade** — disposing the backplane tears down
 *    shared provider resources, unsubscribes remaining handlers, and
 *    cancels in-flight handler work.
 *
 * Provider-agnostic — Redis pub/sub is the default implementation, but
 * the same interface can wrap Postgres LISTEN/NOTIFY, an in-process
 * channel for tests, etc.
 */
export interface ICacheInvalidationBackplane extends AsyncDisposable {
  /**
   * Subscribes to receive invalidation messages. The handler is invoked
   * once per received key (including keys this instance published).
   * Implementations isolate errors per handler — one handler throwing
   * does not affect delivery to other handlers.
   *
   * @param handler - Callback invoked for every received invalidation
   *   key. Receives the key and an optional `AbortSignal` tied to
   *   subscription lifetime (aborted when the returned subscription is
   *   disposed).
   * @returns Disposable subscription. Dispose to **unsubscribe** (stop
   *   further key delivery to this handler); typically held as a field
   *   on the subscriber and disposed in the subscriber's own disposal
   *   so the subscription lifetime matches the subscriber.
   */
  subscribe(
    handler: (key: string, signal?: AbortSignal) => void | Promise<void>,
  ): AsyncDisposable;

  /**
   * Publishes an invalidation message for a single key. Every
   * subscriber (across every connected instance, including this one)
   * receives the key.
   *
   * @returns `ok`; failure on backplane error.
   */
  publishInvalidation(key: string, signal?: AbortSignal): Promise<D2Result>;

  /**
   * Bulk-publish counterpart of {@link publishInvalidation}.
   * Implementations may pipeline the publishes for fewer round-trips.
   *
   * @returns `ok`; failure on backplane error.
   */
  publishInvalidationMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result>;
}
