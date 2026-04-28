// -----------------------------------------------------------------------
// <copyright file="FilesEndpoints.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

using D2.Services.Protos.Files.V1;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Owns the <see cref="FilesService.FilesServiceClient"/> registration plus any
/// REST endpoints backed by the Files gRPC service. No business endpoints yet
/// — the client is consumed today by <see cref="HealthEndpoints"/> for the
/// CheckHealth probe and is the home for future files-backed gateway routes.
/// </summary>
public static class FilesEndpoints
{
    /// <summary>
    /// Extension methods for the service collection.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds a gRPC client for the Files main service. Reads
        /// <c>FILES_GRPC_ADDRESS</c> (bare <c>host:port</c>) and
        /// <c>GATEWAY_FILES_GRPC_API_KEY</c>. The API key is attached to every
        /// outbound call — health-exempt RPCs ignore it; protected RPCs require it.
        /// </summary>
        /// <returns>The updated service collection.</returns>
        public IServiceCollection AddFilesGrpcClient()
        {
            var filesAddress = Environment.GetEnvironmentVariable("FILES_GRPC_ADDRESS");
            if (filesAddress.Falsey())
            {
                throw new ArgumentException(
                    "Files gRPC service address not configured. Missing 'FILES_GRPC_ADDRESS' environment variable.");
            }

            var apiKey = Environment.GetEnvironmentVariable("GATEWAY_FILES_GRPC_API_KEY");

            services.AddGrpcClient<FilesService.FilesServiceClient>(o =>
            {
                o.Address = new Uri($"http://{filesAddress}");
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
        /// Maps the REST endpoints backed by the Files main service.
        /// Currently a no-op — added preemptively so future Files-backed routes
        /// have an obvious home alongside the gRPC client registration above.
        /// </summary>
        /// <returns>The updated endpoint route builder.</returns>
        public IEndpointRouteBuilder MapFilesEndpointsV1()
        {
            // No business endpoints yet.
            return erb;
        }
    }
}
