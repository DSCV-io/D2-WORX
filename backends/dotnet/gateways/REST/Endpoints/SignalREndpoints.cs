// -----------------------------------------------------------------------
// <copyright file="SignalREndpoints.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

using D2.Gateways.Protos.Realtime.V1;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Owns the <see cref="RealtimeGateway.RealtimeGatewayClient"/> registration
/// plus any REST endpoints that proxy to the SignalR realtime gateway. No
/// business endpoints today — the client is consumed by
/// <see cref="HealthEndpoints"/> for the CheckHealth probe and is the home
/// for future SignalR-backed gateway routes.
/// </summary>
public static class SignalREndpoints
{
    /// <summary>
    /// Extension methods for the service collection.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds a gRPC client for the SignalR realtime gateway. Reads
        /// <c>SIGNALR_GRPC_ADDRESS</c> (bare <c>host:port</c>). No API key is
        /// configured today — health-only consumer is server-side exempt. When a
        /// non-health endpoint is added, attach <c>GATEWAY_SIGNALR_GRPC_API_KEY</c>
        /// here following the pattern in <see cref="CommsEndpoints"/>.
        /// </summary>
        /// <returns>The updated service collection.</returns>
        public IServiceCollection AddSignalRGrpcClient()
        {
            var signalrAddress = Environment.GetEnvironmentVariable("SIGNALR_GRPC_ADDRESS");
            if (signalrAddress.Falsey())
            {
                throw new ArgumentException(
                    "SignalR gRPC service address not configured. Missing 'SIGNALR_GRPC_ADDRESS' environment variable.");
            }

            services.AddGrpcClient<RealtimeGateway.RealtimeGatewayClient>(o =>
            {
                o.Address = new Uri($"http://{signalrAddress}");
            });

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
        /// Maps the REST endpoints backed by the SignalR realtime gateway.
        /// Currently a no-op — added preemptively so future SignalR-backed
        /// routes have an obvious home alongside the gRPC client registration.
        /// </summary>
        /// <returns>The updated endpoint route builder.</returns>
        public IEndpointRouteBuilder MapSignalREndpointsV1()
        {
            // No business endpoints yet.
            return erb;
        }
    }
}
