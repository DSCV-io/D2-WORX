// -----------------------------------------------------------------------
// <copyright file="ITieredCache.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

/// <summary>
/// Composed L1 (in-process) + L2 (remote) cache. Reads check L1 first,
/// fall through to L2 on miss, populate L1 with whatever L2 returned.
/// Writes go L2-first — L1 only writes if L2 succeeded — so partial-write
/// states are impossible and the result is binary (success or the L2
/// failure bubbled up). Atomic primitives route through L2 (the source
/// of truth) and invalidate / refresh L1 as a side effect.
/// </summary>
/// <remarks>
/// <para>
/// Structurally identical to <see cref="IDistributedCache"/> — both
/// compose the same building blocks. The marker name carries behavioral
/// intent at the dependency site: reading <c>ITieredCache cache</c>
/// tells you reads served from L1 are sub-microsecond, L1 stays
/// cluster-coherent via the broadcast backplane, and per-tier expirations
/// are kept in lockstep so L1 never outlives L2.
/// </para>
/// <para>
/// Use this for read-heavy entity data where freshness within a few
/// seconds is acceptable. Prefer <see cref="IDistributedCache"/> when
/// every read must hit the canonical store (rate-limit counters,
/// distributed locks, ephemeral one-off lookups).
/// </para>
/// </remarks>
public interface ITieredCache : ICacheBasic, ICacheAtomic, ICacheBroadcast
{
}
