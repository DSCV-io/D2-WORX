// -----------------------------------------------------------------------
// <copyright file="AuthHttpServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http;

using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Validation;
using D2.Shared.Context.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration entry point for the HTTP-transport binding of the inbound
/// auth runtime. Companion to <c>D2.Shared.Auth.AddD2Auth</c> — that registers
/// the validator + liveness tracker; this registers the
/// <see cref="IHttpContextAccessor"/> + scoped <see cref="IRequestContext"/>
/// adapter that downstream handlers / services constructor-inject.
/// </summary>
public static class AuthHttpServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the HTTP-transport auth surface: the
        /// <see cref="IHttpContextAccessor"/> and a scoped <see cref="IRequestContext"/>
        /// resolver that reads from
        /// <see cref="HttpContext.Items"/> at
        /// <see cref="D2HttpContextItems.REQUEST_CONTEXT"/> (populated by
        /// <c>JwtAuthMiddleware</c> earlier in the request pipeline).
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
        /// ORDER-INSENSITIVE: the sibling extension <c>AddD2AuthGrpc()</c>
        /// registers an identical lambda reading from the SAME
        /// <see cref="HttpContext.Items"/> slot. Hosts that call BOTH extensions
        /// (dual-transport service: HTTP endpoints + gRPC services on the same
        /// Kestrel host) get correct resolution under either transport because
        /// both the HTTP middleware and the gRPC interceptor write the validated
        /// <see cref="IRequestContext"/> to that slot. <c>TryAddScoped</c> means
        /// first-wins is harmless — the lambdas behave identically given the same
        /// <see cref="HttpContext"/> state.
        /// </para>
        /// <para>
        /// Idempotent — safe to call from multiple composition roots that may
        /// each defensively register the HTTP transport surface.
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

            // Cross-transport scoped IRequestContext resolver: reads from
            // HttpContext.Items[D2HttpContextItems.REQUEST_CONTEXT]. The HTTP
            // middleware writes to that slot on successful auth; the sibling
            // gRPC interceptor writes the same slot for dual-transport hosts.
            // Failing fast is strictly better than returning null — downstream
            // code would null-ref later with an unhelpful stack trace.
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

            // Request-scoped forwarded-JWT holder — structurally isolated from
            // IRequestContext (a different type with a different registration,
            // never projected by the request-context enricher). JwtAuthMiddleware
            // populates it after successful validation; the outbound forwarding
            // credential reads it. TryAdd keeps it idempotent and harmless under
            // dual-transport hosts (the gRPC extension registers the same holder).
            services.TryAddScoped<IForwardedJwtAccessor, MutableForwardedJwtAccessor>();

            return services;
        }
    }
}
