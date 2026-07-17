// -----------------------------------------------------------------------
// <copyright file="IDistributedCache.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

/// <summary>
/// Cluster-scoped cache backed by a remote store (e.g. Redis). Atomic
/// primitives coordinate cluster-wide. The
/// <see cref="ICacheBroadcast"/> surface coordinates with tiered
/// consumers in other instances so their L1 entries get busted in lockstep.
/// </summary>
/// <remarks>
/// <para>
/// Structurally identical to <see cref="ITieredCache"/> — both compose the
/// same building blocks. The marker name carries behavioral intent at the
/// dependency site: reading <c>IDistributedCache cache</c> tells you the
/// cache talks to the remote store on every read (no L1), so freshness
/// matters more than read speed for this consumer (e.g. rate-limit
/// counters, lock state, ephemeral session data).
/// </para>
/// </remarks>
public interface IDistributedCache : ICacheBasic, ICacheAtomic, ICacheBroadcast, ICacheSet
{
}
