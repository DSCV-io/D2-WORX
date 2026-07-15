// -----------------------------------------------------------------------
// <copyright file="RedisCacheOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Distributed.Redis;

/// <summary>
/// Configuration knobs for the Redis-backed distributed cache and the
/// Redis-backed invalidation backplane.
/// </summary>
public sealed class RedisCacheOptions
{
    /// <summary>
    /// Gets or sets the StackExchange.Redis configuration string. Required.
    /// Format: <c>host:port[,host:port],password=...,ssl=true</c>. Sentinel
    /// and Cluster topologies are inferred from the connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default TTL applied to cache entries written
    /// without an explicit expiration. Default 1 hour.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the optional key prefix automatically prepended to
    /// every cache key. Useful when multiple services share a Redis
    /// instance and want namespace isolation. Default is empty.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the channel name used by the invalidation backplane.
    /// All instances using this channel name share the same invalidation
    /// stream. Default <c>"d2:cache:invalidations"</c>.
    /// </summary>
    public string InvalidationChannel { get; set; } = "d2:cache:invalidations";

    /// <summary>
    /// Gets or sets per-command timeout. Maps to
    /// <see cref="StackExchange.Redis.ConfigurationOptions.SyncTimeout"/>
    /// and <see cref="StackExchange.Redis.ConfigurationOptions.AsyncTimeout"/>.
    /// Default 2 seconds.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets connect timeout. Default 5 seconds.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets connect retry count for the initial connection.
    /// Default 3.
    /// </summary>
    public int ConnectRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether StackExchange.Redis should
    /// abort on a failed initial connection. Default <c>false</c> —
    /// graceful degradation (cache calls fail with
    /// <c>ServiceUnavailable</c> until Redis comes back online).
    /// </summary>
    public bool AbortOnConnectFail { get; set; }
}
