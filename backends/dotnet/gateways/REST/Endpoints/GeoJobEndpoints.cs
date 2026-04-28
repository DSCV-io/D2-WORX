// -----------------------------------------------------------------------
// <copyright file="GeoJobEndpoints.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

using D2.Services.Protos.Common.V1;
using D2.Services.Protos.Geo.V1;
using D2.Shared.ServiceKey.Default;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Defines the Geo scheduled job REST endpoints.
/// Each endpoint proxies to the Geo gRPC GeoJobService.
/// </summary>
public static class GeoJobEndpoints
{
    /// <summary>
    /// Extension methods for the service collection.
    /// </summary>
    ///
    /// <param name="services">
    /// The service collection to extend.
    /// </param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds a gRPC client for the Geo job service to the service collection.
        /// Reads <c>GEO_GRPC_ADDRESS</c> (bare <c>host:port</c>) and <c>GATEWAY_GEO_GRPC_API_KEY</c>.
        /// </summary>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddGeoJobsGrpcClient()
        {
            var geoAddress = Environment.GetEnvironmentVariable("GEO_GRPC_ADDRESS");
            if (geoAddress.Falsey())
            {
                throw new ArgumentException(
                    "Geo service address not configured. Missing 'GEO_GRPC_ADDRESS' environment variable.");
            }

            var apiKey = Environment.GetEnvironmentVariable("GATEWAY_GEO_GRPC_API_KEY");
            if (apiKey.Falsey())
            {
                throw new ArgumentException(
                    "Geo gRPC API key not configured. Missing 'GATEWAY_GEO_GRPC_API_KEY' environment variable.");
            }

            services.AddGrpcClient<GeoJobService.GeoJobServiceClient>(o =>
            {
                o.Address = new Uri($"http://{geoAddress}");
            })
            .AddCallCredentials((_, metadata) =>
            {
                metadata.Add("x-api-key", apiKey!);
                return Task.CompletedTask;
            })
            .ConfigureChannel(o => o.UnsafeUseInsecureChannelCallCredentials = true);

            return services;
        }
    }

    /// <summary>
    /// Extension methods for the endpoint route builder.
    /// </summary>
    ///
    /// <param name="erb">
    /// The endpoint route builder to extend.
    /// </param>
    extension(IEndpointRouteBuilder erb)
    {
        /// <summary>
        /// Maps the Geo scheduled job REST API endpoints.
        /// All endpoints require a service key (Dkron/internal callers only).
        /// </summary>
        ///
        /// <returns>
        /// The updated endpoint route builder.
        /// </returns>
        public IEndpointRouteBuilder MapGeoJobEndpointsV1()
        {
            var group = erb.MapGroup("/api/v1/geo/jobs");

            group.MapPost("/purge-stale-whois", PurgeStaleWhoIsAsync)
                .RequireServiceKey()
                .WithName("PurgeStaleWhoIs")
                .WithSummary("Purges WhoIs records older than the retention period.");

            group.MapPost("/cleanup-orphaned-locations", CleanupOrphanedLocationsAsync)
                .RequireServiceKey()
                .WithName("CleanupOrphanedLocations")
                .WithSummary("Cleans up locations with no contact or WhoIs references.");

            return erb;
        }
    }

    private static async Task<IResult> PurgeStaleWhoIsAsync(
        GeoJobService.GeoJobServiceClient client,
        CancellationToken ct)
    {
        var response = await client.PurgeStaleWhoIsAsync(
            new TriggerJobRequest(),
            cancellationToken: ct);

        return response.Result.ToHttpResult(response.Data);
    }

    private static async Task<IResult> CleanupOrphanedLocationsAsync(
        GeoJobService.GeoJobServiceClient client,
        CancellationToken ct)
    {
        var response = await client.CleanupOrphanedLocationsAsync(
            new TriggerJobRequest(),
            cancellationToken: ct);

        return response.Result.ToHttpResult(response.Data);
    }
}
