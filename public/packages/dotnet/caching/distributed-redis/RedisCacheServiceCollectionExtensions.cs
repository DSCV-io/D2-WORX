// -----------------------------------------------------------------------
// <copyright file="RedisCacheServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Distributed.Redis;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

/// <summary>
/// DI registration helpers for the Redis-backed distributed cache and
/// invalidation backplane.
/// </summary>
public static class RedisCacheServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="RedisDistributedCache"/> as the singleton
        /// <see cref="IDistributedCache"/>. The Redis connection multiplexer
        /// is registered as a singleton too (created from the configured
        /// connection string). The default <see cref="ICacheSerializer"/>
        /// is <see cref="JsonCacheSerializer"/>; callers may override by
        /// registering a different <c>ICacheSerializer</c> before this call.
        /// </summary>
        /// <param name="configure">
        /// Configuration delegate; <c>ConnectionString</c> is required.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2DistributedCacheRedis(Action<RedisCacheOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.Configure(configure);
            services.TryAddSingleton<ICacheSerializer, JsonCacheSerializer>();
            services.TryAddSingleton<IConnectionMultiplexer>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
                if (opts.ConnectionString.Falsey())
                {
                    throw new InvalidOperationException(
                        "RedisCacheOptions.ConnectionString is required.");
                }

                var configurationOptions = ConfigurationOptions.Parse(opts.ConnectionString);
                configurationOptions.AbortOnConnectFail = opts.AbortOnConnectFail;
                configurationOptions.SyncTimeout = (int)opts.CommandTimeout.TotalMilliseconds;
                configurationOptions.AsyncTimeout = (int)opts.CommandTimeout.TotalMilliseconds;
                configurationOptions.ConnectTimeout = (int)opts.ConnectTimeout.TotalMilliseconds;
                configurationOptions.ConnectRetry = opts.ConnectRetries;
                return ConnectionMultiplexer.Connect(configurationOptions);
            });
            services.TryAddSingleton<IDistributedCache, RedisDistributedCache>();
            return services;
        }

        /// <summary>
        /// Registers <see cref="RedisCacheInvalidationBackplane"/> as the
        /// singleton <see cref="ICacheInvalidationBackplane"/>. Requires
        /// <see cref="AddD2DistributedCacheRedis"/> (or some other
        /// registration of <see cref="IConnectionMultiplexer"/>) to be
        /// called first.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2RedisCacheInvalidationBackplane()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<
                ICacheInvalidationBackplane,
                RedisCacheInvalidationBackplane>();
            return services;
        }
    }
}
