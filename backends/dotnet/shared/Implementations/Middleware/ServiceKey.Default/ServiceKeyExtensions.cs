// -----------------------------------------------------------------------
// <copyright file="ServiceKeyExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceKey.Default;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for service-to-service API key authentication.
/// </summary>
public static class ServiceKeyExtensions
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers service key authentication options from configuration.
        /// </summary>
        ///
        /// <param name="configuration">
        /// The application configuration.
        /// </param>
        /// <param name="sectionName">
        /// The configuration section name. Defaults to "GATEWAY_SERVICEKEY".
        /// </param>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddServiceKeyAuth(
            IConfiguration configuration,
            string sectionName = "GATEWAY_SERVICEKEY")
        {
            services.Configure<ServiceKeyOptions>(configuration.GetSection(sectionName));
            return services;
        }
    }

    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/>.
    /// </summary>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds service key detection middleware to the pipeline.
        /// Must run after request enrichment and before rate limiting.
        /// </summary>
        ///
        /// <returns>
        /// The application builder for chaining.
        /// </returns>
        public IApplicationBuilder UseServiceKeyDetection()
        {
            return app.UseMiddleware<ServiceKeyMiddleware>();
        }
    }

    /// <summary>
    /// Extension methods for <see cref="RouteHandlerBuilder"/>.
    /// </summary>
    extension(RouteHandlerBuilder builder)
    {
        /// <summary>
        /// Requires a valid service API key in the <c>X-Api-Key</c> header.
        /// Use this on endpoints that should only be callable by trusted backend
        /// services (e.g., SvelteKit server), not by end users directly.
        /// </summary>
        ///
        /// <returns>
        /// The route handler builder for chaining.
        /// </returns>
        public RouteHandlerBuilder RequireServiceKey()
        {
            return builder.AddEndpointFilter<ServiceKeyEndpointFilter>();
        }
    }
}
