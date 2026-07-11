// -----------------------------------------------------------------------
// <copyright file="AuditEndpointRouteBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Api.Composition;

using D2.Audit.Api.Grpc;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.ServiceDefaults;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// Audit endpoint mapping — <see cref="MapD2AuditEndpoints"/>.
/// </summary>
public static class AuditEndpointRouteBuilderExtensions
{
    /// <param name="endpoints">The endpoint route builder.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps Audit default health/metrics endpoints and the scoped
        /// <c>AuditPing</c> gRPC service (<see cref="Scopes.Internal.Audit.Ping"/>).
        /// No public REST product surface — public HTTP for PingAudit lives on
        /// Edge via the generated bridge. Health stays JWT-free via
        /// <c>MapD2DefaultEndpoints</c> only.
        /// </summary>
        /// <returns>The same <paramref name="endpoints"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        public IEndpointRouteBuilder MapD2AuditEndpoints()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.MapD2DefaultEndpoints();

            // Multiproc S2S business op — dual-factor JWT scope + mTLS.
            // TypeSpec @d2RequireAnyScope drives Edge bridge; host Map required.
            endpoints.MapGrpcService<AuditPingService>()
                .RequireAnyScope(Scopes.Internal.Audit.Ping);

            return endpoints;
        }
    }
}
