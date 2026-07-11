// -----------------------------------------------------------------------
// <copyright file="EdgeEndpointRouteBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Composition;

using D2.Edge.Api.Routes.KeyCustodian;
using D2.Shared.ServiceDefaults;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Edge endpoint mapping — <see cref="MapD2EdgeEndpoints"/>.
/// </summary>
public static class EdgeEndpointRouteBuilderExtensions
{
    /// <param name="endpoints">The endpoint route builder.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps Edge default health/metrics endpoints and production KeyCustodian
        /// well-known routes (JWKS + OIDC). KeyCustodian gRPC MapGrpcService bindings
        /// and the Audit bridge Map surface are out of scope for this host shell.
        /// </summary>
        /// <returns>The same <paramref name="endpoints"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        public IEndpointRouteBuilder MapD2EdgeEndpoints()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.MapD2DefaultEndpoints();

            // Production well-known (KC edge-module HTTP).
            endpoints.MapGetJwksRoute();
            endpoints.MapGetOidcConfigurationRoute();

            // KeyCustodian gRPC service maps and Audit bridge maps are out of scope.
            return endpoints;
        }
    }
}
