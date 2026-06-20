// -----------------------------------------------------------------------
// <copyright file="AuthGrpcServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc;

using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Grpc.Interceptors;
using D2.Shared.Auth.Validation;
using D2.Shared.Context.Abstractions;
using global::Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration entry point for the gRPC-transport binding of the inbound
/// auth runtime. Companion to <c>D2.Shared.Auth.AddD2Auth</c> — that registers
/// the validator + liveness tracker; this registers the
/// <see cref="JwtAuthInterceptor"/> + a scoped <see cref="IRequestContext"/>
/// resolver that reads from <see cref="HttpContext.Items"/> (the gRPC
/// interceptor writes the validated context to the same shared slot the HTTP
/// middleware uses, so the resolver lambda is identical across both
/// transports).
/// </summary>
public static class AuthGrpcServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the gRPC-transport auth surface: the
        /// <see cref="JwtAuthInterceptor"/> (singleton), a scoped
        /// <see cref="IRequestContext"/> resolver that reads from
        /// <see cref="HttpContext.Items"/> at
        /// <see cref="D2HttpContextItems.REQUEST_CONTEXT"/>, and the configuration
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
        /// The scoped <see cref="IRequestContext"/> resolver is REGISTRATION-
        /// ORDER-INSENSITIVE: the sibling extension <c>AddD2AuthHttp()</c>
        /// registers an identical lambda reading from the SAME
        /// <see cref="HttpContext.Items"/> slot. Hosts that call BOTH extensions
        /// (dual-transport service: HTTP endpoints + gRPC services on the same
        /// Kestrel host) get correct resolution under either transport because
        /// both the HTTP middleware and the gRPC interceptor write the validated
        /// <see cref="IRequestContext"/> to that slot. <c>TryAddScoped</c> means
        /// first-wins is harmless — the lambdas behave identically given the same
        /// <see cref="HttpContext"/> state. The interceptor ALSO writes the
        /// context to <see cref="global::Grpc.Core.ServerCallContext.UserState"/>
        /// for the gRPC-specific hot-path accessor
        /// <c>ServerCallContext.GetD2RequestContext()</c> used by gRPC service
        /// code that already has a <see cref="global::Grpc.Core.ServerCallContext"/>
        /// in hand and wants to skip the <see cref="IHttpContextAccessor"/>
        /// allocation cost.
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
        /// Idempotent — multiple <c>AddD2AuthGrpc()</c> calls do not double-
        /// register the interceptor (defensive presence check).
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

            // Cross-transport scoped IRequestContext resolver: reads from
            // HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]. The gRPC
            // interceptor writes to that slot on successful auth (alongside its
            // ServerCallContext.UserState write); the sibling HTTP middleware
            // writes the same slot for dual-transport hosts. The lambda body is
            // IDENTICAL to the one registered by AddD2AuthHttp() — the parity
            // guarantees TryAddScoped first-wins is harmless regardless of
            // registration order. Failing fast is strictly better than returning
            // null — downstream code would null-ref later with an unhelpful
            // stack trace.
            services.TryAddScoped<IRequestContext>(static sp =>
            {
                var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext
                    ?? throw new InvalidOperationException(
                        "IRequestContext was resolved without an active HttpContext. "
                            + "Ensure the resolution site runs inside an AspNetCore "
                            + "request (UseD2Auth() for HTTP; AddD2AuthGrpc() for gRPC) "
                            + "and that an HttpContext is on the execution context.");

                return http.Items.TryGetValue(D2HttpContextItems.REQUEST_CONTEXT, out var raw)
                    && raw is IRequestContext ctx
                    ? ctx
                    : throw new InvalidOperationException(
                        "IRequestContext was resolved before the auth pipeline ran. "
                            + "Ensure UseD2Auth() (for HTTP) or AddD2AuthGrpc()'s "
                            + "interceptor (for gRPC) has run before resolving "
                            + "IRequestContext.");
            });

            // Request-scoped forwarded-JWT holder — identical registration to
            // AddD2AuthHttp() (deliberate parity; a parity test pins both register
            // the same impl type). JwtAuthInterceptor populates it after
            // successful validation, alongside its IRequestContext dual-write; the
            // outbound forwarding credential reads it. TryAdd keeps it idempotent.
            services.TryAddScoped<IForwardedJwtAccessor, MutableForwardedJwtAccessor>();

            return services;
        }
    }
}
