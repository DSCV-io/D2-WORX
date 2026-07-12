// -----------------------------------------------------------------------
// <copyright file="EdgeEndpointRouteBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Composition;

using D2.Edge.Api.Bridges.Audit;
using D2.Edge.Api.Grpc.KeyCustodian;
using D2.Edge.Api.Kestrel;
using D2.Edge.Api.Routes.KeyCustodian;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
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
        /// Maps Edge default health/metrics endpoints, production KeyCustodian
        /// well-known routes (JWKS + OIDC), the six KeyCustodian gRPC services
        /// with <c>Scopes.Internal.Kc.*</c> scope constants <strong>only on the
        /// mTLS listen</strong> (<see cref="EdgeHttpsRolePolicies.MTLS_HTTPS_PORT"/>),
        /// and the Audit standalone HTTP→gRPC bridges.
        /// </summary>
        /// <returns>The same <paramref name="endpoints"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <paramref name="endpoints"/> is not also an
        /// <see cref="IApplicationBuilder"/> (required for mTLS
        /// <c>MapWhen</c> port isolation of KC gRPC).
        /// </exception>
        public IEndpointRouteBuilder MapD2EdgeEndpoints()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(endpoints);

            // Public binds (:8080 + Issuer :8443 + any reach of main pipeline):
            // health/metrics, well-known discovery, Audit HTTP bridges.
            endpoints.MapD2DefaultEndpoints();
            endpoints.MapGetJwksRoute();
            endpoints.MapGetOidcConfigurationRoute();
            endpoints.MapAllAuditBridges();

            // Structural isolation: KC gRPC is NOT on the shared endpoint
            // table for all listens. MapWhen steals traffic on the mTLS port
            // only and registers the six services there — cleartext :8080 and
            // Issuer :8443 never match those Maps.
            MapKeyCustodianGrpcMtlsOnly(endpoints);

            return endpoints;
        }
    }

    /// <summary>
    /// Registers all six KeyCustodian gRPC services with their scope constants.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder for the mTLS branch.</param>
    private static void MapKeyCustodianGrpcServices(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<KeyCustodianSignerService>()
            .RequireAnyScope(Scopes.Internal.Kc.Sign);

        endpoints.MapGrpcService<KeyCustodianKeyringService>()
            .RequireAnyScope(Scopes.Internal.Kc.Keyring);

        endpoints.MapGrpcService<KeyCustodianCertificateAuthorityService>()
            .RequireAnyScope(Scopes.Internal.Kc.Issue);

        endpoints.MapGrpcService<KeyCustodianCaCertificateService>()
            .RequireAnyScope(Scopes.Internal.Kc.Cacert);

        endpoints.MapGrpcService<KeyCustodianSealPublicKeyService>()
            .RequireAnyScope(Scopes.Internal.Kc.Seal.Encrypt);

        endpoints.MapGrpcService<KeyCustodianOwnSealPrivateKeyService>()
            .RequireAnyScope(Scopes.Internal.Kc.Seal.Open);
    }

    /// <summary>
    /// Isolates KC gRPC to <see cref="EdgeHttpsRolePolicies.MTLS_HTTPS_PORT"/>
    /// via <c>MapWhen</c> on <see cref="IApplicationBuilder"/>.
    /// </summary>
    /// <param name="endpoints">
    /// The host endpoint route builder (must be <see cref="IApplicationBuilder"/>).
    /// </param>
    private static void MapKeyCustodianGrpcMtlsOnly(IEndpointRouteBuilder endpoints)
    {
        // WebApplication implements both IEndpointRouteBuilder and IApplicationBuilder.
        // Classic nested UseEndpoints(e => …) does not — production Program always
        // maps via WebApplication so MapWhen is available.
        if (endpoints is not IApplicationBuilder app)
        {
            throw new InvalidOperationException(
                "MapD2EdgeEndpoints requires an IApplicationBuilder (WebApplication) "
                    + "so KeyCustodian gRPC can MapWhen-isolate to the mTLS port "
                    + $"({EdgeHttpsRolePolicies.MTLS_HTTPS_PORT}). Nested "
                    + "UseEndpoints(e => e.MapD2EdgeEndpoints()) is not supported — "
                    + "call MapD2EdgeEndpoints on the WebApplication / IApplicationBuilder.");
        }

        app.MapWhen(
            static ctx => ctx.Connection.LocalPort == EdgeHttpsRolePolicies.MTLS_HTTPS_PORT,
            static branch =>
            {
                branch.UseRouting();

                branch.UseEndpoints(static e => MapKeyCustodianGrpcServices(e));
            });
    }
}
