// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Resilience.Pipeline;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering
/// <see cref="ResilientPipeline{TKey, TValue}"/> instances configured via
/// the fluent <see cref="IResilientPipelineBuilder{TKey, TValue}"/> DSL.
/// </summary>
/// <remarks>
/// All registrations are KEYED. The library intentionally provides no
/// unkeyed registration path — two unkeyed registrations of the same
/// <c>(TKey, TValue)</c> shape would silently overwrite each other
/// (last-wins), and the resulting "phantom shadowing" footgun would
/// surface only at runtime when one consumer's resilience config
/// mysteriously affected another. Forcing keyed registration eliminates
/// that bug class entirely; consumers resolve via
/// <c>[FromKeyedServices(serviceKey)]</c>.
/// </remarks>
public static class ResilientPipelineServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers a <see cref="ResilientPipeline{TKey, TValue}"/> as a keyed
        /// singleton, configured by the supplied <paramref name="configure"/>
        /// callback. Layers are applied in the order the <c>Use*</c> methods are
        /// called (outer-first).
        /// </summary>
        /// <typeparam name="TKey">Per-call key type.</typeparam>
        /// <typeparam name="TValue">The value type produced by the operation.</typeparam>
        /// <param name="serviceKey">
        /// The DI key under which to register the pipeline. Consumers resolve
        /// via <c>[FromKeyedServices(serviceKey)]</c>.
        /// </param>
        /// <param name="configure">
        /// Callback receiving an <see cref="IResilientPipelineBuilder{TKey, TValue}"/>
        /// to compose the layer stack. Use the <c>UseSingleflight(serviceKey)</c>
        /// / <c>UseCircuitBreaker(serviceKey)</c> overloads inside to resolve
        /// keyed primitives explicitly.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddResilientPipeline<TKey, TValue>(
            object serviceKey,
            Action<IResilientPipelineBuilder<TKey, TValue>> configure)
            where TKey : notnull
        {
            services.AddKeyedSingleton<ResilientPipeline<TKey, TValue>>(serviceKey, (sp, _) =>
            {
                var builder = new ResilientPipelineBuilder<TKey, TValue>(sp);
                configure(builder);
                return builder.Build();
            });

            return services;
        }
    }
}
