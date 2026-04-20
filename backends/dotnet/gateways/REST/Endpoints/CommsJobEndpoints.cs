// -----------------------------------------------------------------------
// <copyright file="CommsJobEndpoints.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

using D2.Services.Protos.Common.V1;
using D2.Services.Protos.Comms.V1;
using D2.Shared.ServiceKey.Default;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Defines the Comms scheduled job REST endpoints.
/// Each endpoint proxies to the Comms gRPC CommsJobService.
/// </summary>
public static class CommsJobEndpoints
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
        /// Adds a gRPC client for the Comms job service to the service collection.
        /// Reads <c>COMMS_GRPC_ADDRESS</c> (bare <c>host:port</c>) and <c>GATEWAY_COMMS_GRPC_API_KEY</c>.
        /// </summary>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddCommsJobsGrpcClient()
        {
            var commsGrpcAddress = Environment.GetEnvironmentVariable("COMMS_GRPC_ADDRESS");
            if (commsGrpcAddress.Falsey())
            {
                throw new ArgumentException(
                    "Comms gRPC service address not configured. Missing 'COMMS_GRPC_ADDRESS' environment variable.");
            }

            var apiKey = Environment.GetEnvironmentVariable("GATEWAY_COMMS_GRPC_API_KEY");
            if (apiKey.Falsey())
            {
                throw new ArgumentException(
                    "Comms gRPC API key not configured. Missing 'GATEWAY_COMMS_GRPC_API_KEY' environment variable.");
            }

            services.AddGrpcClient<CommsJobService.CommsJobServiceClient>(o =>
            {
                o.Address = new Uri($"http://{commsGrpcAddress}");
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
        /// Maps the Comms scheduled job REST API endpoints.
        /// All endpoints require a service key (Dkron/internal callers only).
        /// </summary>
        ///
        /// <returns>
        /// The updated endpoint route builder.
        /// </returns>
        public IEndpointRouteBuilder MapCommsJobEndpointsV1()
        {
            var group = erb.MapGroup("/api/v1/comms/jobs");

            group.MapPost("/purge-deleted-messages", PurgeDeletedMessagesAsync)
                .RequireServiceKey()
                .WithName("PurgeDeletedMessages")
                .WithSummary("Purges soft-deleted messages beyond retention period.");

            group.MapPost("/purge-delivery-history", PurgeDeliveryHistoryAsync)
                .RequireServiceKey()
                .WithName("PurgeDeliveryHistory")
                .WithSummary("Purges old delivery request and attempt history.");

            return erb;
        }
    }

    private static async Task<IResult> PurgeDeletedMessagesAsync(
        CommsJobService.CommsJobServiceClient client,
        CancellationToken ct)
    {
        var response = await client.PurgeDeletedMessagesAsync(
            new TriggerJobRequest(),
            cancellationToken: ct);

        return response.Result.ToHttpResult(response.Data);
    }

    private static async Task<IResult> PurgeDeliveryHistoryAsync(
        CommsJobService.CommsJobServiceClient client,
        CancellationToken ct)
    {
        var response = await client.PurgeDeliveryHistoryAsync(
            new TriggerJobRequest(),
            cancellationToken: ct);

        return response.Result.ToHttpResult(response.Data);
    }
}
