// -----------------------------------------------------------------------
// <copyright file="RequestOriginGrpcServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc;

using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc.Interceptors;
using D2.Shared.Time;
using D2.Shared.Utilities.Extensions;
using global::Grpc.AspNetCore.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration entry point for the cross-process establishment boundary — the
/// <see cref="RequestOriginCrossProcessInterceptor"/> that derives
/// <see cref="RequestOrigin.CrossProcessHop"/> + the peer caller from the validated
/// mutual-TLS certificate and applies/extends the propagated call-path on each inbound
/// gRPC call.
/// </summary>
public static class RequestOriginGrpcServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the <see cref="RequestOriginCrossProcessInterceptor"/> and attaches
        /// it to the host's <see cref="GrpcServiceOptions.Interceptors"/> collection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Order after <c>AddD2AuthGrpc()</c></strong>: call this AFTER
        /// <c>AddD2AuthGrpc()</c> so the establishment interceptor is appended to the
        /// interceptor pipeline AFTER <c>JwtAuthInterceptor</c> — the auth interceptor
        /// validates and populates the scoped request-context, then this interceptor
        /// enriches that same context with the cross-process origin, peer caller, and
        /// appended call-path. (Interceptors run inbound in registration order, so the
        /// auth interceptor's establishment of the identity precedes this enrichment.)
        /// </para>
        /// <para>
        /// Binds <see cref="D2WorkloadIdentityOptions"/> with a required-<c>ServiceId</c>
        /// validation that runs at host startup, so a missing self-identity surfaces during
        /// composition rather than on the first call. <see cref="IClock"/> is registered
        /// as <see cref="SystemClock"/> when the host has not already bound one
        /// (<c>TryAdd</c>). Idempotent — repeat calls do not double-register the interceptor.
        /// </para>
        /// </remarks>
        /// <param name="configure">
        /// Optional configuration delegate for <see cref="D2WorkloadIdentityOptions"/>
        /// (sets the host's own <c>ServiceId</c>).
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddD2RequestOriginGrpc(
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
            services.TryAddSingleton<RequestOriginCrossProcessInterceptor>();

            services.Configure<GrpcServiceOptions>(o =>
            {
                for (var i = 0; i < o.Interceptors.Count; i++)
                {
                    if (o.Interceptors[i].Type == typeof(RequestOriginCrossProcessInterceptor))
                        return;
                }

                o.Interceptors.Add<RequestOriginCrossProcessInterceptor>();
            });

            return services;
        }
    }
}
