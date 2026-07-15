// -----------------------------------------------------------------------
// <copyright file="TieredCacheServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Tiered;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration helpers for <see cref="DefaultTieredCache"/>.
/// </summary>
public static class TieredCacheServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="DefaultTieredCache"/> as the singleton
        /// <see cref="ITieredCache"/>. Requires <see cref="ILocalCache"/>
        /// and <see cref="IDistributedCache"/> to be registered first.
        /// <see cref="ICacheInvalidationBackplane"/> is optional — if
        /// registered, the tiered cache subscribes for cluster-wide L1
        /// invalidation; if not, broadcast methods throw and L1 stays
        /// stale until TTL.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2TieredCache()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<ITieredCache, DefaultTieredCache>();
            return services;
        }
    }
}
