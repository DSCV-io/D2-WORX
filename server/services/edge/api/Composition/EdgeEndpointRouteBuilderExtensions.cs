// -----------------------------------------------------------------------
// <copyright file="EdgeEndpointRouteBuilderExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Composition;

using D2.Edge.Api.Grpc.KeyCustodian;
using D2.Edge.Api.Routes.KeyCustodian;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc.Endpoints;
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
        /// Maps Edge default health/metrics endpoints, production KeyCustodian
        /// well-known routes (JWKS + OIDC), and the six KeyCustodian gRPC services
        /// with <c>Scopes.Internal.Kc.*</c> scope constants. The Audit bridge Map
        /// surface is not registered here.
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

            // Production KeyCustodian gRPC (edge-module) — scopes from Scopes.g.cs only.
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

            return endpoints;
        }
    }
}
