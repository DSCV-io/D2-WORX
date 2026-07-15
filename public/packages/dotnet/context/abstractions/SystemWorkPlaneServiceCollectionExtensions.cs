// -----------------------------------------------------------------------
// <copyright file="SystemWorkPlaneServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using DcsvIo.D2.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Platform registration for the System work plane —
/// <see cref="MutableRequestContext"/> / default scoped <see cref="IRequestContext"/>
/// + <see cref="ISystemWorkScopeFactory"/>. Hosted/background authority-bearing
/// work MUST enter through the factory; modules must not re-register
/// <see cref="IRequestContext"/>.
/// </summary>
public static class SystemWorkPlaneServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the platform System work plane:
        /// <list type="bullet">
        /// <item><description>
        /// Scoped <see cref="MutableRequestContext"/> (per-scope establishment
        /// target for System workers and other non-HTTP planes).
        /// </description></item>
        /// <item><description>
        /// Default scoped <see cref="IRequestContext"/> → the scope's
        /// <see cref="MutableRequestContext"/>. HTTP/gRPC hosts replace this
        /// with the dual-path resolver in <c>AddD2AuthHttp</c> /
        /// <c>AddD2AuthGrpc</c> (Items slot when established, else Mutable —
        /// pre-auth HTTP stays Unestablished and fail-closed in authority rules).
        /// </description></item>
        /// <item><description>
        /// Singleton <see cref="ISystemWorkScopeFactory"/> — the only sanctioned
        /// entry for hosted System-plane work.
        /// </description></item>
        /// <item><description>
        /// <see cref="IClock"/> as <see cref="SystemClock"/> when not already
        /// registered (<c>TryAdd</c>).
        /// </description></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call once from the host composition root (typically
        /// <c>AddD2ServiceDefaults</c>). Idempotent via <c>TryAdd*</c> for the
        /// factory / Mutable / clock; the default <see cref="IRequestContext"/>
        /// registration is also <c>TryAdd</c> so an HTTP dual-path registered
        /// earlier is preserved, while <c>AddD2AuthHttp</c> / <c>AddD2AuthGrpc</c>
        /// replace a plain Mutable default when they run after this extension.
        /// </para>
        /// <para>
        /// Requires the host to bind <c>D2WorkloadIdentityOptions.ServiceId</c>
        /// (establishment-boundary registration) before
        /// <see cref="ISystemWorkScopeFactory.BeginAsync"/> runs; an empty
        /// service id fails loud at begin time.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddD2SystemWorkPlane()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IClock, SystemClock>();
            services.TryAddScoped<MutableRequestContext>();

            // Platform default for System workers / messaging / non-HTTP hosts.
            // Auth.Http / Auth.Grpc replace this with the dual-path Items||Mutable
            // resolver when the host wires inbound HTTP or gRPC.
            services.TryAddScoped<IRequestContext>(
                static sp => sp.GetRequiredService<MutableRequestContext>());

            services.TryAddSingleton<ISystemWorkScopeFactory, SystemWorkScopeFactory>();

            return services;
        }
    }
}
