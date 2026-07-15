// -----------------------------------------------------------------------
// <copyright file="CacheContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Distributed;

using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Distributed.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

/// <summary>
/// Per-test cache context — owns its own connection multiplexer + fresh
/// key prefix. Disposable so each test gets clean state.
/// </summary>
internal sealed class CacheContext : IAsyncDisposable
{
    private readonly ConnectionMultiplexer r_redis;

    internal CacheContext(
        string connectionString,
        string keyPrefix,
        ICacheInvalidationBackplane? backplane = null)
    {
        r_redis = ConnectionMultiplexer.Connect(connectionString);
        var opts = Options.Create(new RedisCacheOptions
        {
            ConnectionString = connectionString,
            KeyPrefix = keyPrefix,
            DefaultExpiration = TimeSpan.FromMinutes(5),
        });
        Cache = new RedisDistributedCache(
            r_redis,
            opts,
            new JsonCacheSerializer(),
            NullLogger<RedisDistributedCache>.Instance,
            backplane);
        Backplane = backplane;
    }

    /// <summary>Gets the cache instance.</summary>
    internal RedisDistributedCache Cache { get; }

    /// <summary>Gets the backplane (if provided).</summary>
    internal ICacheInvalidationBackplane? Backplane { get; }

    public async ValueTask DisposeAsync()
    {
        await r_redis.DisposeAsync();
    }
}
