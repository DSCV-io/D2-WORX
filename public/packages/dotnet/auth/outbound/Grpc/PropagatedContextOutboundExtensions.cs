// -----------------------------------------------------------------------
// <copyright file="PropagatedContextOutboundExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Outbound.Grpc;

using System;
using global::Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Host + per-channel surface for the outbound <c>x-d2-context</c> propagation factor —
/// the <see cref="PropagatedContextClientInterceptor"/> that writes the propagation
/// subset + accumulated call-path on every outbound gRPC RPC.
/// </summary>
/// <remarks>
/// The generated gRPC-client DI extension auto-chains <c>.AddD2PropagatedContext()</c>
/// alongside <c>.AddD2ForwardedJwt().AddD2WorkloadCertificate()</c> on every internal
/// client, so a host never calls the per-channel extension directly. The
/// <see cref="DcsvIo.D2.Auth.Abstractions.IAmbientRequestScopeAccessor"/> the interceptor
/// reads is registered by the
/// inbound transport (<c>AddD2AuthHttp()</c> / <c>AddD2AuthGrpc()</c>) — a forwarding
/// host is by definition an inbound host — so this surface registers neither it nor any
/// configuration; it only registers the interceptor type itself.
/// </remarks>
public static class PropagatedContextOutboundExtensions
{
    /// <param name="builder">The gRPC client builder being configured.</param>
    extension(IHttpClientBuilder builder)
    {
        /// <summary>
        /// Attaches the <see cref="PropagatedContextClientInterceptor"/> to the gRPC
        /// channel under construction (channel-scoped — one interceptor per channel; it
        /// re-derives the current request's scope per RPC). Safe-by-default: a channel
        /// that does not call this writes no propagation header.
        /// </summary>
        /// <returns>The same <paramref name="builder"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
        public IHttpClientBuilder AddD2PropagatedContext()
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.AddInterceptor<PropagatedContextClientInterceptor>(
                InterceptorScope.Channel);
        }
    }

    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the <see cref="PropagatedContextClientInterceptor"/> (singleton) so
        /// the per-channel <c>.AddD2PropagatedContext()</c> can resolve it. Idempotent.
        /// The <see cref="DcsvIo.D2.Auth.Abstractions.IAmbientRequestScopeAccessor"/> the
        /// interceptor depends on is
        /// supplied by the inbound transport, mirroring <c>AddD2ForwardedJwtOutbound()</c>.
        /// </summary>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddD2PropagatedContextOutbound()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<PropagatedContextClientInterceptor>();

            return services;
        }
    }
}
