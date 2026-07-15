// -----------------------------------------------------------------------
// <copyright file="HealthEndpointsRouteBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// ASP.NET Core endpoint-routing extension that maps the canonical D²
/// <c>/health</c> + <c>/alive</c> health-check endpoints.
/// </summary>
public static class HealthEndpointsRouteBuilderExtensions
{
    /// <param name="endpoints">The endpoint route builder.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps two health-check endpoints:
        /// <see cref="D2AspNetCoreConstants.HEALTH_ENDPOINT_PATH"/>
        /// (<c>/health</c>) returns the full health-check status (every
        /// registered check, regardless of tag) and
        /// <see cref="D2AspNetCoreConstants.ALIVE_ENDPOINT_PATH"/>
        /// (<c>/alive</c>) returns only checks tagged
        /// <see cref="D2AspNetCoreConstants.LIVE_HEALTH_TAG"/>
        /// (<c>"live"</c>) — the kubernetes-conventional liveness split.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Per-service infrastructure layers add their own checks via
        /// <c>services.AddHealthChecks().AddDbContextCheck&lt;...&gt;()</c>
        /// / <c>.AddRedis(...)</c> / etc. — those auto-flow into the
        /// <c>/health</c> endpoint. Checks tagged
        /// <see cref="D2AspNetCoreConstants.LIVE_HEALTH_TAG"/> additionally
        /// participate in the <c>/alive</c> endpoint.
        /// </para>
        /// <para>
        /// Calling this method multiple times on the same
        /// <see cref="IEndpointRouteBuilder"/> raises an exception per
        /// the underlying ASP.NET Core endpoint-routing convention
        /// (duplicate route patterns). Per-pipeline registration SHOULD
        /// happen exactly once at composition root.
        /// </para>
        /// </remarks>
        /// <returns>
        /// The same <paramref name="endpoints"/> for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        public IEndpointRouteBuilder MapD2HealthEndpoints()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.MapHealthChecks(D2AspNetCoreConstants.HEALTH_ENDPOINT_PATH);

            endpoints.MapHealthChecks(
                D2AspNetCoreConstants.ALIVE_ENDPOINT_PATH,
                new HealthCheckOptions
                {
                    Predicate = r => r.Tags.Contains(
                        D2AspNetCoreConstants.LIVE_HEALTH_TAG),
                });

            return endpoints;
        }
    }
}
