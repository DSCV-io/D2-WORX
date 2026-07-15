// -----------------------------------------------------------------------
// <copyright file="AuthEndpointGuardServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Startup;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration extension for the deny-by-default auth endpoint guard.
/// </summary>
public static class AuthEndpointGuardServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the <see cref="AuthEndpointGuardStartupFilter"/> startup
        /// filter, which fails host startup when any mapped
        /// <see cref="Microsoft.AspNetCore.Routing.RouteEndpoint"/> lacks a
        /// declared auth intent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The guard is registered as a transient <see cref="IStartupFilter"/>.
        /// <c>IStartupFilter.Configure(next)</c> runs during HTTP-pipeline
        /// construction in <c>GenericWebHostService.StartAsync</c>, AFTER the
        /// <c>WebApplication</c>'s <c>DataSources</c> are merged into the routing
        /// composite and BEFORE any request is served. This is the correct
        /// lifecycle point for endpoint-presence validation in both the
        /// generic-host + <c>UseEndpoints</c> model AND the production
        /// <c>WebApplication</c> model.
        /// </para>
        /// <para>
        /// Idempotent — uses <c>TryAddEnumerable</c> so a double call does NOT
        /// register a second instance of the guard.
        /// </para>
        /// <para>
        /// The guard is opt-out via
        /// <c>D2ServiceDefaultsOptions.SkipAuthEndpointGuard = true</c>
        /// (wired in the service-defaults aggregator). Callers that invoke this
        /// extension directly unconditionally register the guard with no
        /// opt-out path — use the service-defaults aggregator for the normal
        /// host path.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddD2AuthEndpointGuard()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddEnumerable(
                ServiceDescriptor.Transient<IStartupFilter, AuthEndpointGuardStartupFilter>());

            return services;
        }
    }
}
