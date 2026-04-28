// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.DistributedCache.Redis;

// ReSharper disable AccessToStaticMemberViaDerivedType
using D2.Shared.DistributedCache.Redis.Handlers.C;
using D2.Shared.DistributedCache.Redis.Handlers.D;
using D2.Shared.DistributedCache.Redis.Handlers.R;
using D2.Shared.DistributedCache.Redis.Handlers.U;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.C;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.D;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.R;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.U;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

/// <summary>
/// Extension methods for adding Redis distributed caching services.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds Redis distributed caching services to the service collection.
    /// </summary>
    ///
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Redis distributed caching services to the service collection.
        /// </summary>
        ///
        /// <param name="redisConnectionString">
        /// The Redis connection string.
        /// </param>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddRedisCaching(string redisConnectionString)
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(redisConnectionString);
                options.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(options);
            });

            // The handlers for distributed cache operations.
            services.AddTransient(typeof(IRead.IGetHandler<>), typeof(Get<>));
            services.AddTransient(typeof(IUpdate.ISetHandler<>), typeof(Set<>));
            services.AddTransient(typeof(ICreate.ISetNxHandler<>), typeof(SetNx<>));
            services.AddTransient<IDelete.IRemoveHandler, Remove>();
            services.AddTransient<IRead.IExistsHandler, Exists>();
            services.AddTransient<IRead.IGetTtlHandler, GetTtl>();
            services.AddTransient<IRead.IPingHandler, Handlers.Q.Ping>();
            services.AddTransient<IUpdate.IIncrementHandler, Increment>();
            services.AddTransient<ICreate.IAcquireLockHandler, AcquireLock>();
            services.AddTransient<IDelete.IReleaseLockHandler, Handlers.D.ReleaseLock>();

            return services;
        }
    }
}
