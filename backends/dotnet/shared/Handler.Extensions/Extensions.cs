// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler.Extensions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering handler context services.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    ///
    /// <param name="services">
    /// The service collection to extend.
    /// </param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds handler context services to the service collection including
        /// <see cref="IRequestContext"/> and <see cref="IHandlerContext"/>.
        /// </summary>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        /// <remarks>
        /// <para>
        /// On the gateway, <see cref="IRequestContext"/> is populated by
        /// <c>RequestEnrichmentMiddleware</c> and set on <c>HttpContext.Features</c>.
        /// The factory checks for an existing instance before falling back to
        /// claim-based construction (used by non-gateway gRPC services).
        /// </para>
        /// </remarks>
        public IServiceCollection AddHandlerContext()
        {
            services.AddHttpContextAccessor();

            // Transient (not Scoped) so every resolution re-checks Features. The
            // gateway's MutableRequestContext is set into Features by
            // RequestEnrichmentMiddleware mid-pipeline — anything that resolves
            // IRequestContext earlier (e.g. structured logging enrichers) would
            // otherwise cache the claims-based fallback and miss the populated
            // identity once JwtFingerprintMiddleware runs.
            services.AddTransient<IRequestContext>(sp =>
            {
                var httpCtx = sp.GetService<IHttpContextAccessor>()?.HttpContext;

                // Gateway: MutableRequestContext already on Features (populated by middleware).
                var existing = httpCtx?.Features.Get<IRequestContext>();
                if (existing is not null)
                {
                    return existing;
                }

                // Non-gateway (e.g. Geo gRPC): build from JWT claims.
                return new RequestContext(sp.GetRequiredService<IHttpContextAccessor>());
            });

            services.AddTransient<IHandlerContext, HandlerContext>();

            return services;
        }
    }
}
