// -----------------------------------------------------------------------
// <copyright file="AuthGrpcServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Grpc;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Auth.Grpc.Ambient;
using DcsvIo.D2.Auth.Grpc.Interceptors;
using DcsvIo.D2.Auth.Validation;
using DcsvIo.D2.Context.Abstractions;
using global::Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration entry point for the gRPC-transport binding of the inbound
/// auth runtime. Companion to <c>DcsvIo.D2.Auth.AddD2Auth</c> — that registers
/// the validator + liveness tracker; this registers the
/// <see cref="JwtAuthInterceptor"/> + dual-path scoped
/// <see cref="IRequestContext"/> resolver (same Items||Mutable contract as
/// <c>AddD2AuthHttp</c>).
/// </summary>
public static class AuthGrpcServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the gRPC-transport auth surface: the
        /// <see cref="JwtAuthInterceptor"/> (singleton), a dual-path scoped
        /// <see cref="IRequestContext"/> resolver (HTTP Items when established,
        /// else scoped <see cref="MutableRequestContext"/>), and the configuration
        /// that attaches the interceptor to the host's
        /// <see cref="GrpcServiceOptions"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// REQUIRES <c>services.AddD2Auth(...)</c> to have been called upstream.
        /// Verified via fail-fast presence check on <see cref="JwtValidator"/>;
        /// missing → <see cref="InvalidOperationException"/> with a clear
        /// remediation message.
        /// </para>
        /// <para>
        /// The dual-path <see cref="IRequestContext"/> resolver matches
        /// <c>AddD2AuthHttp()</c> (identical body, same
        /// <see cref="HttpContext.Items"/> slot). Hosts that call BOTH get
        /// correct resolution under either transport. The resolver REPLACES any
        /// prior plain Mutable-only registration from
        /// <c>AddD2SystemWorkPlane()</c> so inbound gRPC still prefers the
        /// interceptor-populated Items slot while System workers (no
        /// <see cref="HttpContext"/>) fall through to the scope's
        /// <see cref="MutableRequestContext"/> after
        /// <c>ISystemWorkScopeFactory.BeginAsync</c>. The interceptor ALSO writes
        /// the context to <see cref="global::Grpc.Core.ServerCallContext.UserState"/>
        /// for the gRPC-specific hot-path accessor
        /// <c>ServerCallContext.GetD2RequestContext()</c>.
        /// </para>
        /// <para>
        /// Additive on top of the host's own <c>services.AddGrpc(...)</c> call
        /// — does NOT call <c>AddGrpc</c> itself. Hosts configure their own gRPC
        /// settings (<c>MaxReceiveMessageSize</c>, etc.); this extension wires
        /// the auth interceptor into the existing
        /// <see cref="GrpcServiceOptions.Interceptors"/> collection via
        /// <c>services.Configure&lt;GrpcServiceOptions&gt;(...)</c>.
        /// </para>
        /// <para>
        /// Idempotent for interceptor / ambient seams; the dual-path
        /// <see cref="IRequestContext"/> registration is replace-on-each-call
        /// (same lambda under dual-transport hosts).
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <c>services.AddD2Auth(...)</c> was not called upstream.
        /// </exception>
        public IServiceCollection AddD2AuthGrpc()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(services);

            // Fail-fast precondition check — surface a friendly error rather than
            // letting the interceptor fail at first call with a confusing
            // "unable to resolve JwtValidator" message.
            var hasValidator = false;
            for (var i = 0; i < services.Count; i++)
            {
                if (services[i].ServiceType == typeof(JwtValidator))
                {
                    hasValidator = true;
                    break;
                }
            }

            if (!hasValidator)
            {
                throw new InvalidOperationException(
                    "AddD2AuthGrpc() requires AddD2Auth(...) to be called first. "
                        + "Order them as: services.AddD2Auth(opts => { ... }).AddD2AuthGrpc();");
            }

            services.AddHttpContextAccessor();

            // Singleton lifetime for the interceptor itself: the type is stateless
            // (all deps are singletons); per-call resolution would be wasteful DI
            // work. TryAdd so multiple AddD2AuthGrpc() calls are idempotent.
            services.TryAddSingleton<JwtAuthInterceptor>();

            // Wire the interceptor into the GrpcServiceOptions.Interceptors
            // collection via Configure (which appends to the existing collection
            // every time the configured options instance is built — hence the
            // duplicate-add guard inside the lambda).
            services.Configure<GrpcServiceOptions>(o =>
            {
                for (var i = 0; i < o.Interceptors.Count; i++)
                {
                    if (o.Interceptors[i].Type == typeof(JwtAuthInterceptor))
                        return;
                }

                o.Interceptors.Add<JwtAuthInterceptor>();
            });

            // Ensure Mutable is present for dual-path fall-through. TryAdd:
            // AddD2SystemWorkPlane / AddD2AuthHttp may already have registered it.
            services.TryAddScoped<MutableRequestContext>();

            // Dual-path IRequestContext — identical contract to AddD2AuthHttp.
            // Replace any prior registration (SystemWorkPlane plain default or
            // sibling HTTP dual-path) so System workers never hit a throw-only path.
            services.RemoveAll<IRequestContext>();
            services.AddScoped<IRequestContext>(static sp => ResolveDualPathRequestContext(sp));

            // Request-scoped forwarded-JWT holder — identical registration to
            // AddD2AuthHttp() (deliberate parity; a parity test pins both register
            // the same impl type). JwtAuthInterceptor populates it after
            // successful validation, alongside its IRequestContext dual-write; the
            // outbound forwarding credential reads it. TryAdd keeps it idempotent.
            services.TryAddScoped<IForwardedJwtAccessor, MutableForwardedJwtAccessor>();

            // Ambient-scope adapter (the read-back door, symmetric to the holder
            // write side above): the outbound forwarding credential resolves the
            // current gRPC call's scope through the framework-free
            // IAmbientRequestScopeAccessor port, and this gRPC transport supplies the
            // IHttpContextAccessor-backed adapter (the per-call HttpContext is set by
            // Grpc.AspNetCore.Server on the same AsyncLocal seam the HTTP pipeline
            // uses). Singleton — stateless (per-request state flows through the
            // AsyncLocal-backed accessor). Registered here so a gRPC-inbound
            // forwarding host gets it automatically, mirroring AddD2AuthHttp(). On a
            // dual-transport host (HTTP + gRPC on one Kestrel) TryAdd is first-wins,
            // harmless: this adapter and the HTTP sibling read the same door.
            services.TryAddSingleton<
                IAmbientRequestScopeAccessor,
                GrpcHttpContextAmbientRequestScopeAccessor>();

            return services;
        }
    }

    /// <summary>
    /// Shared HTTP/gRPC dual-path resolver body: established context from
    /// <see cref="HttpContext.Items"/> when present; otherwise the scope's
    /// <see cref="MutableRequestContext"/>. Kept as a private twin of the HTTP
    /// transport's internal helper (the two transport csprojs are siblings with
    /// no inter-project dependency).
    /// </summary>
    /// <param name="sp">The request (or System work) scope's service provider.</param>
    /// <returns>The established Items context, or the scope's Mutable fall-through.</returns>
    private static IRequestContext ResolveDualPathRequestContext(IServiceProvider sp)
    {
        var accessor = sp.GetService<IHttpContextAccessor>();
        var http = accessor?.HttpContext;

        if (http is not null
            && http.Items.TryGetValue(D2HttpContextItems.REQUEST_CONTEXT, out var raw)
            && raw is IRequestContext established)
        {
            return established;
        }

        return sp.GetRequiredService<MutableRequestContext>();
    }
}
