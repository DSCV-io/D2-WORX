// -----------------------------------------------------------------------
// <copyright file="AuthHttpServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Http;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Auth.Http.Ambient;
using DcsvIo.D2.Auth.Validation;
using DcsvIo.D2.Context.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration entry point for the HTTP-transport binding of the inbound
/// auth runtime. Companion to <c>DcsvIo.D2.Auth.AddD2Auth</c> — that registers
/// the validator + liveness tracker; this registers the
/// <see cref="IHttpContextAccessor"/> + dual-path scoped
/// <see cref="IRequestContext"/> resolver that downstream handlers inject.
/// </summary>
public static class AuthHttpServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the HTTP-transport auth surface: the
        /// <see cref="IHttpContextAccessor"/>, a scoped dual-path
        /// <see cref="IRequestContext"/> resolver (HTTP Items when established,
        /// else scoped <see cref="MutableRequestContext"/>), and the forwarded-JWT
        /// ambient seams.
        /// </summary>
        /// <remarks>
        /// <para>
        /// REQUIRES <c>services.AddD2Auth(...)</c> to have been called upstream.
        /// Verified via fail-fast presence check on <see cref="JwtValidator"/>;
        /// missing → <see cref="InvalidOperationException"/> with a clear
        /// remediation message.
        /// </para>
        /// <para>
        /// The dual-path <see cref="IRequestContext"/> resolver is shared with
        /// <c>AddD2AuthGrpc()</c> (identical lambda, same
        /// <see cref="HttpContext.Items"/> slot). Hosts that call BOTH get
        /// correct resolution under either transport. The resolver REPLACES any
        /// prior plain Mutable-only registration from
        /// <c>AddD2SystemWorkPlane()</c> so inbound HTTP still prefers the
        /// middleware-populated Items slot while System workers (no
        /// <see cref="HttpContext"/>) fall through to the scope's
        /// <see cref="MutableRequestContext"/> after
        /// <c>ISystemWorkScopeFactory.BeginAsync</c>.
        /// </para>
        /// <para>
        /// Pre-auth / missing-slot HTTP resolution returns an Unestablished
        /// <see cref="MutableRequestContext"/> — authority rules fail-closed on
        /// type-zero origin; this is intentional (no throw-only path that would
        /// also break hosted System workers on the same host).
        /// </para>
        /// <para>
        /// Idempotent for accessor / ambient seams via <c>TryAdd*</c>; the
        /// dual-path <see cref="IRequestContext"/> registration is replace-on-
        /// each-call (same lambda under dual-transport hosts).
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <c>services.AddD2Auth(...)</c> was not called upstream.
        /// </exception>
        public IServiceCollection AddD2AuthHttp()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(services);

            // Fail-fast precondition check — surface a friendly error rather than
            // letting the middleware fail at first request with a confusing
            // "unable to resolve JwtValidator from a scoped service" message.
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
                    "AddD2AuthHttp() requires AddD2Auth(...) to be called first. "
                        + "Order them as: services.AddD2Auth(opts => { ... }).AddD2AuthHttp();");
            }

            services.AddHttpContextAccessor();

            // Ensure Mutable is present for the dual-path fall-through (System
            // workers + pre-auth HTTP). TryAdd: AddD2SystemWorkPlane may already
            // have registered it.
            services.TryAddScoped<MutableRequestContext>();

            // Dual-path IRequestContext: prefer middleware/interceptor Items slot;
            // else scoped Mutable (Unestablished until established). Replace any
            // prior registration (including SystemWorkPlane's plain Mutable default
            // and a prior dual-path from the sibling gRPC extension) so the unified
            // resolver always wins on auth-wired hosts.
            services.RemoveAll<IRequestContext>();
            services.AddScoped<IRequestContext>(static sp => ResolveDualPathRequestContext(sp));

            // Request-scoped forwarded-JWT holder — structurally isolated from
            // IRequestContext (a different type with a different registration,
            // never projected by the request-context enricher). JwtAuthMiddleware
            // populates it after successful validation; the outbound forwarding
            // credential reads it. TryAdd keeps it idempotent and harmless under
            // dual-transport hosts (the gRPC extension registers the same holder).
            services.TryAddScoped<IForwardedJwtAccessor, MutableForwardedJwtAccessor>();

            // Ambient-scope adapter (the read-back door, symmetric to the holder
            // write side above): the outbound forwarding credential resolves the
            // current request's scope through the framework-free
            // IAmbientRequestScopeAccessor port, and this HTTP transport supplies
            // the IHttpContextAccessor-backed adapter. Singleton — it is stateless
            // (per-request state flows through the AsyncLocal-backed accessor).
            // Registered here so a forwarding host (HTTP-inbound by definition in
            // the current architecture) gets it automatically; keeping the adapter
            // in this framework-referencing lib leaves DcsvIo.D2.Auth.Outbound
            // free of any AspNetCore framework reference.
            services.TryAddSingleton<
                IAmbientRequestScopeAccessor,
                HttpContextAmbientRequestScopeAccessor>();

            return services;
        }
    }

    /// <summary>
    /// Shared HTTP/gRPC dual-path resolver body: established context from
    /// <see cref="HttpContext.Items"/> when present; otherwise the scope's
    /// <see cref="MutableRequestContext"/>.
    /// </summary>
    /// <param name="sp">The request (or System work) scope's service provider.</param>
    /// <returns>The established Items context, or the scope's Mutable fall-through.</returns>
    internal static IRequestContext ResolveDualPathRequestContext(IServiceProvider sp)
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
