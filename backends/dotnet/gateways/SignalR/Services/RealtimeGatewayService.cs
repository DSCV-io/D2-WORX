// -----------------------------------------------------------------------
// <copyright file="RealtimeGatewayService.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.SignalR.Services;

using D2.Gateways.Protos.Realtime.V1;
using D2.Gateways.SignalR.Hubs;
using D2.Gateways.SignalR.Interceptors;
using D2.Services.Protos.Common.V1;
using Grpc.Core;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// gRPC service implementation for the RealtimeGateway.
/// Routes push requests from internal services to SignalR Groups (channels).
/// </summary>
public partial class RealtimeGatewayService : RealtimeGateway.RealtimeGatewayBase
{
    private readonly IHubContext<AuthenticatedHub> r_hubContext;
    private readonly ILogger<RealtimeGatewayService> r_logger;
    private readonly StackExchange.Redis.IConnectionMultiplexer r_redis;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeGatewayService"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context for sending messages.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="redis">The Redis connection multiplexer for health checks.</param>
    public RealtimeGatewayService(
        IHubContext<AuthenticatedHub> hubContext,
        ILogger<RealtimeGatewayService> logger,
        StackExchange.Redis.IConnectionMultiplexer redis)
    {
        r_hubContext = hubContext;
        r_logger = logger;
        r_redis = redis;
    }

    /// <summary>
    /// Pushes an event to all connections subscribed to the specified channel.
    /// </summary>
    /// <param name="request">The push request with channel, event, and payload.</param>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>A push response indicating delivery status.</returns>
    [RequiresServiceKey]
    public override async Task<PushResponse> PushToChannel(
        PushToChannelRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Channel))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Channel is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Event))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Event is required."));
        }

        await r_hubContext.Clients
            .Group(request.Channel)
            .SendAsync("ReceiveEvent", request.Event, request.PayloadJson, context.CancellationToken);

        // Channel naming convention encodes target identity (e.g. "user:<id>",
        // "org:<id>"). Logged as a single field so correlation with auth /
        // SignalR connection logs is straightforward. Peer captures the
        // calling service for cross-service forensics.
        LogPushedEvent(r_logger, request.Event, request.Channel, context.Peer);

        return new PushResponse
        {
            Result = new D2ResultProto { Success = true, StatusCode = 200 },
            Delivered = true,
        };
    }

    /// <summary>
    /// Removes a specific connection from a channel group.
    /// Used when a service revokes access (e.g., user removed from thread).
    /// </summary>
    /// <param name="request">The removal request with channel and connection ID.</param>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>A push response confirming the removal.</returns>
    [RequiresServiceKey]
    public override async Task<PushResponse> RemoveFromChannel(
        RemoveFromChannelRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ConnectionId is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Channel))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Channel is required."));
        }

        await r_hubContext.Groups.RemoveFromGroupAsync(
            request.ConnectionId,
            request.Channel,
            context.CancellationToken);

        LogRemovedFromChannel(r_logger, request.ConnectionId, request.Channel, context.Peer);

        return new PushResponse
        {
            Result = new D2ResultProto { Success = true, StatusCode = 200 },
            Delivered = true,
        };
    }

    /// <summary>
    /// Health check — reports gateway status.
    /// </summary>
    /// <param name="request">The health check request.</param>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>The health check response.</returns>
    public override async Task<CheckHealthResponse> CheckHealth(
        CheckHealthRequest request,
        ServerCallContext context)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string cacheStatus;
        string? cacheError = null;

        try
        {
            var db = r_redis.GetDatabase();
            await db.PingAsync();
            sw.Stop();
            cacheStatus = "healthy";
        }
        catch (Exception ex)
        {
            sw.Stop();
            cacheStatus = "unhealthy";
            cacheError = ex.Message;
        }

        var cacheComponent = new ComponentHealth
        {
            Status = cacheStatus,
            LatencyMs = sw.ElapsedMilliseconds,
        };

        if (cacheError != null)
        {
            cacheComponent.Error = cacheError;
        }

        var response = new CheckHealthResponse
        {
            Status = cacheStatus == "healthy" ? "healthy" : "degraded",
            Timestamp = DateTime.UtcNow.ToString("o"),
            Components = { ["cache"] = cacheComponent },
        };

        return response;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pushed event {Event} to channel {Channel} from {Peer}")]
    private static partial void LogPushedEvent(ILogger logger, string @event, string channel, string peer);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed connection {ConnectionId} from channel {Channel} (request from {Peer})")]
    private static partial void LogRemovedFromChannel(ILogger logger, string connectionId, string channel, string peer);
}
