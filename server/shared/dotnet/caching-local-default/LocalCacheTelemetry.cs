// -----------------------------------------------------------------------
// <copyright file="LocalCacheTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Caching.Local.Default;

using System.Diagnostics.Metrics;

/// <summary>
/// Static OTel <see cref="Meter"/> for the default local-cache impl.
/// Per-call observability is intentionally minimal — no spans, no per-op
/// logs — because the cache work itself is ~60ns and per-call instrumentation
/// would dominate it. Aggregate counters give us the signal that matters
/// (hit rate, miss rate, eviction count) without per-op overhead.
/// </summary>
internal static class LocalCacheTelemetry
{
    /// <summary>OTel meter for the default local cache.</summary>
    internal static readonly Meter SR_Meter = new("D2.Shared.Caching.Local", "1.0.0");

    /// <summary>Counter incremented on every cache hit.</summary>
    internal static readonly Counter<long> SR_Hits =
        SR_Meter.CreateCounter<long>("d2.cache.local.hits", description: "Local cache hits.");

    /// <summary>Counter incremented on every cache miss.</summary>
    internal static readonly Counter<long> SR_Misses =
        SR_Meter.CreateCounter<long>("d2.cache.local.misses", description: "Local cache misses.");

    /// <summary>Counter incremented on every cache write.</summary>
    internal static readonly Counter<long> SR_Sets =
        SR_Meter.CreateCounter<long>("d2.cache.local.sets", description: "Local cache writes.");

    /// <summary>Counter incremented on every explicit removal.</summary>
    internal static readonly Counter<long> SR_Removes = SR_Meter.CreateCounter<long>(
        "d2.cache.local.removes",
        description: "Local cache removals (explicit).");

    /// <summary>Counter incremented on every implicit eviction (capacity / TTL).</summary>
    internal static readonly Counter<long> SR_Evictions = SR_Meter.CreateCounter<long>(
        "d2.cache.local.evictions",
        description: "Entries evicted by capacity / expiration.");
}
