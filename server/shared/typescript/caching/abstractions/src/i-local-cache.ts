// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { ICacheAtomic } from "./i-cache-atomic.js";
import type { ICacheBasic } from "./i-cache-basic.js";

/**
 * Per-process in-memory cache. Atomic primitives operate at process
 * scope — they do not coordinate across instances. There is no
 * broadcast surface because nothing outside this process can observe
 * local cache state.
 *
 * The interface name documents the cache scope at the dependency site.
 * Reading `ILocalCache` at an inject site tells you, without looking
 * at registration, that this cache lives in process memory and dies
 * with the process.
 *
 * Composes {@link ICacheBasic} + {@link ICacheAtomic} only.
 */
export interface ILocalCache extends ICacheBasic, ICacheAtomic {}
