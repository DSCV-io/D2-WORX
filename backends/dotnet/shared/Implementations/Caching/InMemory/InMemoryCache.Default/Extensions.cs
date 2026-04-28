// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.InMemoryCache.Default;

using D2.Shared.InMemoryCache.Default.Handlers.D;
using D2.Shared.InMemoryCache.Default.Handlers.R;
using D2.Shared.InMemoryCache.Default.Handlers.U;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.D;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.R;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.U;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable AccessToStaticMemberViaDerivedType

/// <summary>
/// Extension methods for adding default in-memory caching services.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds default in-memory caching services to the service collection.
    /// </summary>
    ///
    /// <param name="services">
    /// The service collection to add the services to.
    /// </param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds default in-memory caching services to the service collection.
        /// </summary>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddDefaultMemoryCaching(int maxEntries = 10_000)
        {
            services.AddMemoryCache(options => options.SizeLimit = maxEntries);

            // The handlers for in-memory cache operations.
            services.AddTransient(typeof(IRead.IGetHandler<>), typeof(Get<>));
            services.AddTransient(typeof(IUpdate.ISetHandler<>), typeof(Set<>));
            services.AddTransient<IDelete.IRemoveHandler, Remove>();
            services.AddTransient(typeof(IRead.IGetManyHandler<>), typeof(GetMany<>));
            services.AddTransient(typeof(IUpdate.ISetManyHandler<>), typeof(SetMany<>));

            return services;
        }
    }
}
