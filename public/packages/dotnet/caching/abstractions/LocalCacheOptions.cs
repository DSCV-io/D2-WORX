// -----------------------------------------------------------------------
// <copyright file="LocalCacheOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

/// <summary>
/// Configuration knobs for an <see cref="ILocalCache"/> implementation.
/// Defaults are tuned for typical microservice workloads (100K entries,
/// 1-hour default TTL).
/// </summary>
/// <remarks>
/// Per-entry size accounting is intentionally NOT exposed — the default
/// implementation always counts entries as size 1 so <see cref="MaxEntries"/>
/// behaves as the literal entry-count cap most callers expect. Without
/// this discipline, <c>IMemoryCache</c>'s <c>SizeLimit</c> only triggers
/// eviction on system-wide memory pressure, making the configured ceiling
/// fictional.
/// </remarks>
public sealed class LocalCacheOptions
{
    /// <summary>
    /// Gets or sets the maximum number of entries before LRU-ish eviction
    /// kicks in. Default 100_000 — sized for production Edge / Files-class
    /// workloads where session-liveness, JWKS, token-exchange, and other
    /// per-process caches share a single <see cref="ILocalCache"/> singleton.
    /// At ~1 KB / entry that's ~100 MB process RSS worst-case. Per-service
    /// overrides remain possible via the <c>configure</c> delegate of
    /// <c>AddD2LocalCache</c>.
    /// </summary>
    public int MaxEntries { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the default TTL applied to entries written without an
    /// explicit expiration. Default 1 hour.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the optional key prefix automatically prepended to
    /// every cache key. Useful when multiple caches share a process and
    /// the caller wants namespace isolation (e.g. <c>"jwks:"</c>) without
    /// coordinating keys explicitly. Default is empty (no prefix).
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;
}
