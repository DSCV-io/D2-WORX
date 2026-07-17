// -----------------------------------------------------------------------
// <copyright file="LocalCacheServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Local.Default;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration helpers for <see cref="DefaultLocalCache"/>.
/// </summary>
public static class LocalCacheServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="DefaultLocalCache"/> as a singleton
        /// implementation of <see cref="ILocalCache"/>.
        /// </summary>
        /// <param name="configure">Optional configuration delegate.</param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2LocalCache(Action<LocalCacheOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (configure is not null)
                services.Configure(configure);
            else
                services.AddOptions<LocalCacheOptions>();

            services.TryAddSingleton<ILocalCache, DefaultLocalCache>();
            return services;
        }
    }
}
