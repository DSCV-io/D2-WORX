// -----------------------------------------------------------------------
// <copyright file="AuthEndpoints.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

using D2.Services.Protos.Auth.V1;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Owns the <see cref="AuthService.AuthServiceClient"/> registration plus any
/// REST endpoints backed by the Auth gRPC service. No business endpoints yet
/// — the client is consumed today by <see cref="HealthEndpoints"/> for the
/// CheckHealth probe and is the home for future auth-backed gateway routes.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Extension methods for the service collection.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds a gRPC client for the Auth main service. Reads
        /// <c>AUTH_GRPC_ADDRESS</c> (bare <c>host:port</c>) and
        /// <c>GATEWAY_AUTH_GRPC_API_KEY</c>. The API key is attached to every
        /// outbound call — health-exempt RPCs ignore it; protected RPCs require it.
        /// </summary>
        /// <returns>The updated service collection.</returns>
        public IServiceCollection AddAuthGrpcClient()
        {
            var authAddress = Environment.GetEnvironmentVariable("AUTH_GRPC_ADDRESS");
            if (authAddress.Falsey())
            {
                throw new ArgumentException(
                    "Auth gRPC service address not configured. Missing 'AUTH_GRPC_ADDRESS' environment variable.");
            }

            var apiKey = Environment.GetEnvironmentVariable("GATEWAY_AUTH_GRPC_API_KEY");

            services.AddGrpcClient<AuthService.AuthServiceClient>(o =>
            {
                o.Address = new Uri($"http://{authAddress}");
            })
            .AddCallCredentials((_, metadata) =>
            {
                if (!apiKey.Falsey())
                {
                    metadata.Add("x-api-key", apiKey!);
                }

                return Task.CompletedTask;
            })
            .ConfigureChannel(o => o.UnsafeUseInsecureChannelCallCredentials = true);

            return services;
        }
    }

    /// <summary>
    /// Extension methods for the endpoint route builder.
    /// </summary>
    /// <param name="erb">The endpoint route builder to extend.</param>
    extension(IEndpointRouteBuilder erb)
    {
        /// <summary>
        /// Maps the REST endpoints backed by the Auth main service.
        /// Currently a no-op — added preemptively so future Auth-backed routes
        /// have an obvious home alongside the gRPC client registration above.
        /// </summary>
        /// <returns>The updated endpoint route builder.</returns>
        public IEndpointRouteBuilder MapAuthEndpointsV1()
        {
            // No business endpoints yet.
            return erb;
        }
    }
}
