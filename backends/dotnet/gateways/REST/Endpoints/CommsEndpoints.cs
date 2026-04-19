// -----------------------------------------------------------------------
// <copyright file="CommsEndpoints.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

using D2.Services.Protos.Comms.V1;
using D2.Shared.Auth.Default;
using D2.Shared.Handler;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Defines REST endpoints backed by the Comms gRPC service, plus the
/// <see cref="CommsService.CommsServiceClient"/> registration. Mirrors
/// the per-service convention used by <see cref="GeoEndpoints"/>.
/// </summary>
public static class CommsEndpoints
{
    private const string AUTH_USER_CONTEXT_KEY = "auth_user";

    /// <summary>
    /// Extension methods for the service collection.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds a gRPC client for the Comms main service to the service collection.
        /// Reads <c>COMMS_GRPC_ADDRESS</c> (bare <c>host:port</c>) and
        /// <c>GATEWAY_COMMS_GRPC_API_KEY</c>. The API key is attached to every
        /// outbound call — health-exempt RPCs ignore it; protected RPCs require it.
        /// </summary>
        /// <returns>The updated service collection.</returns>
        public IServiceCollection AddCommsGrpcClient()
        {
            var commsAddress = Environment.GetEnvironmentVariable("COMMS_GRPC_ADDRESS");
            if (commsAddress.Falsey())
            {
                throw new ArgumentException(
                    "Comms gRPC service address not configured. Missing 'COMMS_GRPC_ADDRESS' environment variable.");
            }

            var apiKey = Environment.GetEnvironmentVariable("GATEWAY_COMMS_GRPC_API_KEY");

            services.AddGrpcClient<CommsService.CommsServiceClient>(o =>
            {
                o.Address = new Uri($"http://{commsAddress}");
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
        /// Maps the REST endpoints backed by the Comms service.
        /// </summary>
        /// <returns>The updated endpoint route builder.</returns>
        public IEndpointRouteBuilder MapCommsEndpointsV1()
        {
            var notificationPrefs = erb.MapGroup("/api/v1/notification-preferences")
                .RequireAuthorization();

            notificationPrefs.MapGet(string.Empty, GetMyPreferencesAsync)
                .WithName("GetMyNotificationPreferences")
                .WithSummary("Returns the current user's channel preferences (or defaults).");

            notificationPrefs.MapPut(string.Empty, SetMyPreferencesAsync)
                .WithName("SetMyNotificationPreferences")
                .WithSummary("Updates the current user's channel preferences.");

            return erb;
        }
    }

    private static async Task<IResult> GetMyPreferencesAsync(
        CommsService.CommsServiceClient commsClient,
        IHandlerContext handlerContext,
        CancellationToken ct)
    {
        if (handlerContext.Request.UserId is not Guid userId)
        {
            return Results.Unauthorized();
        }

        var response = await commsClient.GetUserChannelPreferenceAsync(
            new GetUserChannelPreferenceRequest
            {
                ContextKey = AUTH_USER_CONTEXT_KEY,
                RelatedEntityId = userId.ToString(),
            },
            cancellationToken: ct);

        return response.Result.ToHttpResult(response.Data);
    }

    private static async Task<IResult> SetMyPreferencesAsync(
        SetMyPreferencesRequest body,
        CommsService.CommsServiceClient commsClient,
        IHandlerContext handlerContext,
        CancellationToken ct)
    {
        if (handlerContext.Request.UserId is not Guid userId)
        {
            return Results.Unauthorized();
        }

        var req = new SetUserChannelPreferenceRequest
        {
            ContextKey = AUTH_USER_CONTEXT_KEY,
            RelatedEntityId = userId.ToString(),
        };
        if (body.EmailEnabled is bool e)
        {
            req.EmailEnabled = e;
        }

        if (body.SmsEnabled is bool s)
        {
            req.SmsEnabled = s;
        }

        var response = await commsClient.SetUserChannelPreferenceAsync(req, cancellationToken: ct);

        return response.Result.ToHttpResult(response.Data);
    }

    /// <summary>Body for PUT /api/v1/notification-preferences — both fields optional (only provided values are written).</summary>
    public sealed record SetMyPreferencesRequest(bool? EmailEnabled, bool? SmsEnabled);
}
