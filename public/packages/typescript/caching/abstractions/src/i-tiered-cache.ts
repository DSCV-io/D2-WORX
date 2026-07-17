// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { ICacheAtomic } from "./i-cache-atomic.js";
import type { ICacheBasic } from "./i-cache-basic.js";
import type { ICacheBroadcast } from "./i-cache-broadcast.js";

/**
 * Composed L1 (in-process) + L2 (remote) cache. Reads check L1 first,
 * fall through to L2 on miss, populate L1 with whatever L2 returned.
 * Writes go L2-first — L1 only writes if L2 succeeded — so partial-write
 * states are impossible and the result is binary (success or the L2
 * failure bubbled up). Atomic primitives route through L2 (the source
 * of truth) and invalidate / refresh L1 as a side effect.
 *
 * Composes {@link ICacheBasic} + {@link ICacheAtomic} +
 * {@link ICacheBroadcast}. **Does NOT compose {@link ICacheSet}** —
 * set primitives are cluster-only and tiered composition would
 * silently hide their cluster-wide nature. Callers needing
 * SADD/SCARD inject {@link IDistributedCache} directly.
 *
 * **Marker vs {@link IDistributedCache}:** shared building blocks
 * (Basic + Atomic + Broadcast) are method-identical. The marker name
 * carries behavioral intent: reading `ITieredCache` tells you reads
 * served from L1 are sub-microsecond, L1 stays cluster-coherent via
 * the broadcast backplane, and per-tier expirations are kept in
 * lockstep so L1 never outlives L2.
 *
 * Use this for read-heavy entity data where freshness within a few
 * seconds is acceptable. Prefer {@link IDistributedCache} when every
 * read must hit the canonical store.
 */
export interface ITieredCache
  extends ICacheBasic, ICacheAtomic, ICacheBroadcast {}
