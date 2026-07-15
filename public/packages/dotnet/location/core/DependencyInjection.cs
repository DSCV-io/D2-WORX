// -----------------------------------------------------------------------
// <copyright file="DependencyInjection.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Location;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration entry point for <c>DcsvIo.D2.Location</c>.
/// </summary>
public static class DependencyInjection
{
    /// <param name="services">The DI service collection to register into.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="DefaultPostalCodeValidator"/> as the
        /// singleton <see cref="IPostalCodeValidator"/>. The default
        /// validator is stateless (regex is static readonly with JIT
        /// pre-warm), so the singleton lifetime is safe. Consumers
        /// requiring per-country strict validation register an override
        /// via <c>services.Replace(...)</c> AFTER calling this method.
        /// </summary>
        /// <returns>The same <c>services</c> instance for call chaining.</returns>
        public IServiceCollection AddD2Location()
        {
            services.TryAddSingleton<IPostalCodeValidator, DefaultPostalCodeValidator>();
            return services;
        }
    }
}
