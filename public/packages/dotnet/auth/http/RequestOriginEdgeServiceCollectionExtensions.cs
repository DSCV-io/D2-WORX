// -----------------------------------------------------------------------
// <copyright file="RequestOriginEdgeServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http;

using System;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Time;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration entry point for the Edge-inbound establishment boundary — binds the
/// host's own <see cref="D2WorkloadIdentityOptions"/> and the <see cref="IClock"/> the
/// <c>RequestOriginEdgeInboundMiddleware</c> depends on. Pair with
/// <c>app.UseD2RequestOriginEdge()</c> in the request pipeline.
/// </summary>
public static class RequestOriginEdgeServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the services the Edge-inbound establishment middleware depends on:
        /// <see cref="D2WorkloadIdentityOptions"/> (with a required-<c>ServiceId</c>
        /// validation that runs at host startup) and <see cref="IClock"/> as
        /// <see cref="SystemClock"/> when the host has not already bound one
        /// (<c>TryAdd</c>).
        /// </summary>
        /// <param name="configure">
        /// Optional configuration delegate for <see cref="D2WorkloadIdentityOptions"/>
        /// (sets the host's own <c>ServiceId</c>).
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddD2RequestOriginEdge(
            Action<D2WorkloadIdentityOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var optionsBuilder = services.AddOptions<D2WorkloadIdentityOptions>();

            if (configure is not null)
                optionsBuilder.Configure(configure);

            optionsBuilder
                .Validate(
                    o => o.ServiceId.Truthy(),
                    "D2WorkloadIdentityOptions.ServiceId is required — set it to the host's "
                        + "own workload service id (the same value its mTLS leaf SAN encodes).")
                .ValidateOnStart();

            services.TryAddSingleton<IClock, SystemClock>();

            return services;
        }
    }
}
