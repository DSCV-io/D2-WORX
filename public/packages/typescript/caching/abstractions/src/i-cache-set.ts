// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { D2Result } from "@dcsv-io/d2-result";

/**
 * Distributed-set primitives — add-to-set, cardinality, membership.
 * Maps to Redis SADD / SCARD / SREM / SISMEMBER. Only on
 * {@link IDistributedCache}; per-process set-cardinality has no
 * realistic use case (it's a single-instance counter that can't
 * aggregate across the cluster), and tiered composition would silently
 * hide the cluster-wide nature of the operation.
 *
 * The motivating use case is rate-limiting "fingerprint too common"
 * detection: track distinct IPs ever observed for a given fingerprint,
 * compare cardinality against a threshold to decide whether the
 * fingerprint is shared widely enough to be unreliable. Other
 * plausible uses include distinct-user counts per feature flag,
 * active-session tracking by org, and any bounded-cardinality
 * "have I seen X for Y" check.
 *
 * **Counter width:** `number` (not `bigint`) is an intentional TS
 * ergonomic delta vs .NET `long`. Implementations must stay within
 * `Number.MAX_SAFE_INTEGER`.
 */
export interface ICacheSet {
  /**
   * Adds a member to the set at `key`. The set is created on first
   * add.
   *
   * **TTL-on-create-only:** optional `expirationMs` is applied **only
   * when the set is created**; subsequent adds on an existing set
   * **preserve the existing TTL**.
   *
   * @returns `ok(true)` if the member was newly added. `ok(false)` if
   *   the member already existed in the set. Failure on backing-store
   *   error.
   */
  setAdd(
    key: string,
    member: string,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;

  /**
   * Returns the cardinality (number of distinct members) of the set at
   * `key`. Absent set → `ok(0)`.
   *
   * @returns `ok(count)`. Failure on backing-store error.
   */
  setCardinality(key: string, signal?: AbortSignal): Promise<D2Result<number>>;

  /**
   * Removes a member from the set at `key`. **Idempotent** — succeeds
   * whether the member existed or not.
   *
   * @returns `ok(true)` if the member was present and removed.
   *   `ok(false)` if the member was absent. Failure on backing-store
   *   error.
   */
  setRemove(
    key: string,
    member: string,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;

  /**
   * Returns whether `member` is in the set at `key`.
   *
   * @returns `ok(true|false)`. Failure on backing-store error.
   */
  setContains(
    key: string,
    member: string,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>>;
}
