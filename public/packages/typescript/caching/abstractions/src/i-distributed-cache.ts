// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { ICacheAtomic } from "./i-cache-atomic.js";
import type { ICacheBasic } from "./i-cache-basic.js";
import type { ICacheBroadcast } from "./i-cache-broadcast.js";
import type { ICacheSet } from "./i-cache-set.js";

/**
 * Cluster-scoped cache backed by a remote store (e.g. Redis). Atomic
 * primitives coordinate cluster-wide. The {@link ICacheBroadcast}
 * surface coordinates with tiered consumers in other instances so
 * their L1 entries get busted in lockstep.
 *
 * Composes {@link ICacheBasic} + {@link ICacheAtomic} +
 * {@link ICacheBroadcast} + {@link ICacheSet}.
 *
 * **Marker vs {@link ITieredCache}:** shared building blocks
 * (Basic + Atomic + Broadcast) are method-identical. **`ICacheSet` is
 * only on `IDistributedCache`** (not full structural identity). The
 * marker name carries behavioral intent at the dependency site: reading
 * `IDistributedCache` tells you the cache talks to the remote store on
 * every read (no L1), so freshness matters more than read speed for
 * this consumer (e.g. rate-limit counters, lock state, ephemeral
 * session data).
 */
export interface IDistributedCache
  extends ICacheBasic, ICacheAtomic, ICacheBroadcast, ICacheSet {}
