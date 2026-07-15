// -----------------------------------------------------------------------
// <copyright file="RequestOriginGrpcServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
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
/// gRPC call — plus the platform fail-closed
/// <see cref="RequestOriginUnestablishedDenyInterceptor"/> that denies product gRPC
/// when Origin remains <see cref="RequestOrigin.Unestablished"/>.
/// </summary>
public static class RequestOriginGrpcServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the <see cref="RequestOriginCrossProcessInterceptor"/> and the
        /// <see cref="RequestOriginUnestablishedDenyInterceptor"/>, attaching both
        /// to the host's <see cref="GrpcServiceOptions.Interceptors"/> collection.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <strong>Order after <c>AddD2AuthGrpc()</c></strong>: call this AFTER
        /// <c>AddD2AuthGrpc()</c> so interceptors append AFTER
        /// <c>JwtAuthInterceptor</c>. Inbound order is then: JWT auth → Origin
        /// establish → Unestablished deny. (Interceptors run inbound in registration
        /// order.)
        /// </para>
        /// <para>
        /// Establish + deny are ONE registration path so every host that wires
        /// Origin gRPC gets fail-closed Unestablished deny by default (§9.42) —
        /// not a per-handler check.
        /// </para>
        /// <para>
        /// Binds <see cref="D2WorkloadIdentityOptions"/> with a required-<c>ServiceId</c>
        /// validation that runs at host startup, so a missing self-identity surfaces during
        /// composition rather than on the first call. <see cref="IClock"/> is registered
        /// as <see cref="SystemClock"/> when the host has not already bound one
        /// (<c>TryAdd</c>). Idempotent — repeat calls do not double-register either interceptor.
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
            services.TryAddSingleton<RequestOriginUnestablishedDenyInterceptor>();

            // Establish first, then deny — append order = inbound run order.
            services.Configure<GrpcServiceOptions>(o =>
            {
                var hasEstablish = false;
                var hasDeny = false;

                for (var i = 0; i < o.Interceptors.Count; i++)
                {
                    var type = o.Interceptors[i].Type;

                    if (type == typeof(RequestOriginCrossProcessInterceptor))
                        hasEstablish = true;

                    if (type == typeof(RequestOriginUnestablishedDenyInterceptor))
                        hasDeny = true;
                }

                if (!hasEstablish)
                    o.Interceptors.Add<RequestOriginCrossProcessInterceptor>();

                if (!hasDeny)
                    o.Interceptors.Add<RequestOriginUnestablishedDenyInterceptor>();
            });

            return services;
        }
    }
}
