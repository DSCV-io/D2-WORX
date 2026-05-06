// -----------------------------------------------------------------------
// <copyright file="RedisCacheTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Caching.Distributed.Redis;

using System.Diagnostics.Metrics;

/// <summary>
/// Static OTel <see cref="Meter"/> for the Redis distributed cache.
/// Aggregate counters; per-call instrumentation overhead is acceptable
/// here because Redis ops are network-bound (1-5ms) so a few ns of
/// counter increment is invisible.
/// </summary>
internal static class RedisCacheTelemetry
{
    /// <summary>OTel meter for the Redis distributed cache.</summary>
    internal static readonly Meter SR_Meter = new("D2.Shared.Caching.Distributed.Redis", "1.0.0");

    /// <summary>Counter incremented on every cache hit.</summary>
    internal static readonly Counter<long> SR_Hits =
        SR_Meter.CreateCounter<long>("d2.cache.redis.hits", description: "Redis cache hits.");

    /// <summary>Counter incremented on every cache miss.</summary>
    internal static readonly Counter<long> SR_Misses =
        SR_Meter.CreateCounter<long>("d2.cache.redis.misses", description: "Redis cache misses.");

    /// <summary>Counter incremented on every cache write.</summary>
    internal static readonly Counter<long> SR_Sets =
        SR_Meter.CreateCounter<long>("d2.cache.redis.sets", description: "Redis cache writes.");

    /// <summary>Counter incremented on every explicit removal.</summary>
    internal static readonly Counter<long> SR_Removes = SR_Meter.CreateCounter<long>(
        "d2.cache.redis.removes",
        description: "Redis cache removals.");

    /// <summary>Counter incremented on backplane publish.</summary>
    internal static readonly Counter<long> SR_Broadcasts = SR_Meter.CreateCounter<long>(
        "d2.cache.redis.broadcasts",
        description: "Invalidation messages published to backplane.");

    /// <summary>Counter incremented on RedisException.</summary>
    internal static readonly Counter<long> SR_Errors =
        SR_Meter.CreateCounter<long>("d2.cache.redis.errors", description: "Redis-side failures.");
}
