// -----------------------------------------------------------------------
// <copyright file="AuditEndpointRouteBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Api.Composition;

using D2.Audit.Api.Grpc;
using D2.Audit.Api.Kestrel;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
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
        /// Maps Audit default health/metrics endpoints on all binds, and the
        /// scoped <c>AuditPing</c> gRPC service
        /// (<see cref="Scopes.Internal.Audit.Ping"/>) <strong>only on the mTLS
        /// listen</strong> (<see cref="AuditHttpsRolePolicies.MTLS_HTTPS_PORT"/>).
        /// No public REST product surface — public HTTP for PingAudit lives on
        /// Edge via the generated bridge. Health stays JWT-free via
        /// <c>MapD2DefaultEndpoints</c> only.
        /// </summary>
        /// <returns>The same <paramref name="endpoints"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="endpoints"/> is not also an
        /// <see cref="IApplicationBuilder"/> (required for mTLS
        /// <c>MapWhen</c> port isolation of product gRPC).
        /// </exception>
        public IEndpointRouteBuilder MapD2AuditEndpoints()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(endpoints);

            // Cleartext :8080 (and any non-mTLS reach of the main pipeline):
            // infrastructure health/metrics only — no product gRPC Maps.
            endpoints.MapD2DefaultEndpoints();

            // Structural isolation: all product gRPC behind mTLS only.
            MapAuditGrpcMtlsOnly(endpoints);

            return endpoints;
        }
    }

    /// <summary>
    /// Registers Audit product gRPC services with their scope constants.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder for the mTLS branch.</param>
    private static void MapAuditGrpcServices(IEndpointRouteBuilder endpoints)
    {
        // Multiproc S2S business op — dual-factor JWT scope + mTLS.
        // TypeSpec @d2RequireAnyScope drives Edge bridge; host Map required.
        endpoints.MapGrpcService<AuditPingService>()
            .RequireAnyScope(Scopes.Internal.Audit.Ping);
    }

    /// <summary>
    /// Isolates Audit gRPC to <see cref="AuditHttpsRolePolicies.MTLS_HTTPS_PORT"/>
    /// via <c>MapWhen</c> on <see cref="IApplicationBuilder"/>.
    /// </summary>
    /// <param name="endpoints">
    /// The host endpoint route builder (must be <see cref="IApplicationBuilder"/>).
    /// </param>
    private static void MapAuditGrpcMtlsOnly(IEndpointRouteBuilder endpoints)
    {
        if (endpoints is not IApplicationBuilder app)
        {
            throw new InvalidOperationException(
                "MapD2AuditEndpoints requires an IApplicationBuilder (WebApplication) "
                    + "so Audit gRPC can MapWhen-isolate to the mTLS port "
                    + $"({AuditHttpsRolePolicies.MTLS_HTTPS_PORT}). Nested "
                    + "UseEndpoints(e => e.MapD2AuditEndpoints()) is not supported — "
                    + "call MapD2AuditEndpoints on the WebApplication / IApplicationBuilder.");
        }

        app.MapWhen(
            static ctx => ctx.Connection.LocalPort == AuditHttpsRolePolicies.MTLS_HTTPS_PORT,
            static branch =>
            {
                branch.UseRouting();

                branch.UseEndpoints(static e => MapAuditGrpcServices(e));
            });
    }
}
