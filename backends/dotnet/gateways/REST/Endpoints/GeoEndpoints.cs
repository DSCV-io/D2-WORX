// -----------------------------------------------------------------------
// <copyright file="GeoEndpoints.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

using D2.Services.Protos.Geo.V1;
using D2.Shared.ServiceKey.Default;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Defines the Geo-service related API endpoints.
/// </summary>
public static class GeoEndpoints
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
        /// Adds a gRPC client for the Geo service to the service collection.
        /// Reads the <c>GEO_GRPC_ADDRESS</c> environment variable (bare <c>host:port</c>).
        /// </summary>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddGeoGrpcClient()
        {
            var geoAddress = Environment.GetEnvironmentVariable("GEO_GRPC_ADDRESS");
            if (geoAddress.Falsey())
            {
                throw new ArgumentException(
                    "Geo service address not configured. Missing 'GEO_GRPC_ADDRESS' environment variable.");
            }

            services.AddGrpcClient<GeoService.GeoServiceClient>(o =>
            {
                o.Address = new Uri($"http://{geoAddress}");
            });

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
        /// Maps the Geo-service related REST API endpoints.
        /// </summary>
        ///
        /// <returns>
        /// The updated endpoint route builder.
        /// </returns>
        public IEndpointRouteBuilder MapGeoEndpointsV1()
        {
            // Map Geo-service endpoints under /api/v1/geo.
            var group = erb.MapGroup("/api/v1/geo");

            // Public — geographic reference data is not sensitive.
            group.MapGet("/reference-data", GetReferenceDataAsync)
                .AllowAnonymous()
                .WithName("GetGeoReferenceData")
                .WithSummary("Returns full geographic reference data.")
                .WithDescription(
                    "Retrieves countries, subdivisions, currencies, languages, locales, and geopolitical entities.");

            // Service-only — triggered by SvelteKit or other backend services, not end users.
            group.MapPost("/reference-data/update", RequestReferenceDataUpdateAsync)
                .RequireServiceKey()
                .WithName("RequestGetGeoReferenceDataUpdate")
                .WithSummary("Requests that the geographic reference data be updated.")
                .WithDescription(
                    "Requests that the geographic reference data be updated. Returns the current version of geographic reference data.");

            // Return the updated endpoint route builder.
            return erb;
        }
    }

    /// <summary>
    /// Handles the GET /reference-data endpoint.
    /// </summary>
    ///
    /// <param name="geoClient">
    /// The Geo service gRPC client.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// An <see cref="IResult"/> representing the HTTP response.
    /// </returns>
    private static async Task<IResult> GetReferenceDataAsync(
        GeoService.GeoServiceClient geoClient,
        CancellationToken ct)
    {
        var response = await geoClient.GetReferenceDataAsync(
            new GetReferenceDataRequest(),
            cancellationToken: ct);

        return response.Result.ToHttpResult(response.Data);
    }

    /// <summary>
    /// Handles the POST /reference-data/update endpoint.
    /// </summary>
    ///
    /// <param name="geoClient">
    /// The Geo service gRPC client.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// An <see cref="IResult"/> representing the HTTP response.
    /// </returns>
    private static async Task<IResult> RequestReferenceDataUpdateAsync(
        GeoService.GeoServiceClient geoClient,
        CancellationToken ct)
    {
        var response = await geoClient.RequestReferenceDataUpdateAsync(
            new RequestReferenceDataUpdateRequest(),
            cancellationToken: ct);

        return response.Result.ToHttpResult(response.Data);
    }
}
