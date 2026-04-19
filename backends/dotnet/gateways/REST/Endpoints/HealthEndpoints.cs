// -----------------------------------------------------------------------
// <copyright file="HealthEndpoints.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST.Endpoints;

using System.Diagnostics;
using System.Text.Json;
using D2.Gateways.Protos.Realtime.V1;
using D2.Services.Protos.Auth.V1;
using D2.Services.Protos.Common.V1;
using D2.Services.Protos.Comms.V1;
using D2.Services.Protos.Files.V1;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Interfaces.Caching.Distributed.Handlers.R;
using D2.Shared.Utilities.Extensions;
using D2.Shared.Utilities.Serialization;
using Grpc.Core;
using Serilog;

/// <summary>
/// Defines the aggregated health check endpoint that fans out to all services.
/// </summary>
public static class HealthEndpoints
{
    // gRPC client registrations live in their owning per-service endpoint files
    // (CommsEndpoints.AddCommsGrpcClient, AuthEndpoints.AddAuthGrpcClient,
    // FilesEndpoints.AddFilesGrpcClient, SignalREndpoints.AddSignalRGrpcClient,
    // GeoEndpoints.AddGeoGrpcClient). HealthEndpoints just consumes the clients
    // — server-side CheckHealth RPCs are exempt from API key auth, so the same
    // clients work for health probes and business endpoints alike.

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
        /// Maps the aggregated health check endpoint.
        /// </summary>
        ///
        /// <returns>
        /// The updated endpoint route builder.
        /// </returns>
        public IEndpointRouteBuilder MapHealthEndpointsV1()
        {
            erb.MapGet("/api/health", CheckAllHealthAsync)
                .AllowAnonymous()
                .WithName("AggregatedHealthCheck")
                .WithSummary("Returns aggregated health status of all D2-WORX services.");

            return erb;
        }
    }

    private static readonly JsonSerializerOptions sr_jsonOptions = SerializerOptions.SR_WebIgnoreNull;

    /// <summary>
    /// Handles the GET /api/health endpoint.
    /// Fans out to all services in parallel and aggregates the results.
    /// </summary>
    private static async Task<IResult> CheckAllHealthAsync(
        GeoService.GeoServiceClient geoClient,
        CommsService.CommsServiceClient commsClient,
        AuthService.AuthServiceClient authClient,
        FilesService.FilesServiceClient filesClient,
        RealtimeGateway.RealtimeGatewayClient signalrClient,
        IRead.IPingHandler cachePingHandler,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var services = new Dictionary<string, object>();

        // Fan out to all services + gateway cache in parallel.
        var geoTask = CheckGrpcServiceAsync(geoClient, "geo", ct);
        var commsTask = CheckGrpcServiceAsync(commsClient, "comms", ct);
        var authTask = CheckGrpcServiceAsync(authClient, "auth", ct);
        var filesTask = CheckGrpcServiceAsync(filesClient, "files", ct);
        var signalrTask = CheckGrpcServiceAsync(signalrClient, "signalr", ct);
        var cacheTask = CheckGatewayCacheAsync(cachePingHandler, ct);

        await Task.WhenAll(geoTask, authTask, commsTask, filesTask, signalrTask, cacheTask);
        sw.Stop();

        // Gateway's own health.
        var cacheResult = await cacheTask;
        services["gateway"] = new
        {
            status = cacheResult.Status,
            latencyMs = cacheResult.LatencyMs,
            components = new Dictionary<string, object>
            {
                ["cache"] = new
                {
                    status = cacheResult.Status,
                    latencyMs = cacheResult.LatencyMs,
                    error = cacheResult.Error,
                },
            },
        };

        services["geo"] = await geoTask;
        services["auth"] = await authTask;
        services["comms"] = await commsTask;
        services["files"] = await filesTask;
        services["signalr"] = await signalrTask;

        var allHealthy = cacheResult.Status == "healthy"
                         && HasHealthyStatus(await geoTask)
                         && HasHealthyStatus(await authTask)
                         && HasHealthyStatus(await commsTask)
                         && HasHealthyStatus(await filesTask)
                         && HasHealthyStatus(await signalrTask);

        var overallStatus = allHealthy ? "healthy" : "degraded";

        Log.Information(
            "Health check completed: {Status} (geo={GeoStatus}, auth={AuthStatus}, comms={CommsStatus}, files={FilesStatus}, signalr={SignalRStatus}, cache={CacheStatus}) in {ElapsedMs}ms",
            overallStatus,
            GetStatus(await geoTask),
            GetStatus(await authTask),
            GetStatus(await commsTask),
            GetStatus(await filesTask),
            GetStatus(await signalrTask),
            cacheResult.Status,
            sw.ElapsedMilliseconds);

        var response = new
        {
            status = overallStatus,
            timestamp = DateTime.UtcNow.ToString("o"),
            services,
        };

        var statusCode = overallStatus == "healthy" ? 200 : 503;
        return Results.Json(response, sr_jsonOptions, statusCode: statusCode);
    }

    /// <summary>
    /// Calls the Geo gRPC service's CheckHealth RPC and returns the mapped response.
    /// </summary>
    private static async Task<object> CheckGrpcServiceAsync(
        GeoService.GeoServiceClient client,
        string serviceName,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.CheckHealthAsync(
                new CheckHealthRequest(),
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: ct);
            sw.Stop();

            return MapGrpcHealthResponse(response, sw.ElapsedMilliseconds);
        }
        catch (RpcException ex)
        {
            sw.Stop();
            Log.Warning(ex, "gRPC health check to {ServiceName} failed: {StatusCode}", serviceName, ex.StatusCode);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = $"gRPC {ex.StatusCode}: {ex.Status.Detail}" },
                },
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Warning(ex, "Health check to {ServiceName} failed", serviceName);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = "Service check failed" },
                },
            };
        }
    }

    /// <summary>
    /// Calls the Comms gRPC service's CheckHealth RPC and returns the mapped response.
    /// </summary>
    private static async Task<object> CheckGrpcServiceAsync(
        CommsService.CommsServiceClient client,
        string serviceName,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.CheckHealthAsync(
                new CheckHealthRequest(),
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: ct);
            sw.Stop();

            return MapGrpcHealthResponse(response, sw.ElapsedMilliseconds);
        }
        catch (RpcException ex)
        {
            sw.Stop();
            Log.Warning(ex, "gRPC health check to {ServiceName} failed: {StatusCode}", serviceName, ex.StatusCode);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = $"gRPC {ex.StatusCode}: {ex.Status.Detail}" },
                },
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Warning(ex, "Health check to {ServiceName} failed", serviceName);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = "Service check failed" },
                },
            };
        }
    }

    /// <summary>
    /// Calls the Auth gRPC service's CheckHealth RPC and returns the mapped response.
    /// </summary>
    private static async Task<object> CheckGrpcServiceAsync(
        AuthService.AuthServiceClient client,
        string serviceName,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.CheckHealthAsync(
                new CheckHealthRequest(),
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: ct);
            sw.Stop();

            return MapGrpcHealthResponse(response, sw.ElapsedMilliseconds);
        }
        catch (RpcException ex)
        {
            sw.Stop();
            Log.Warning(ex, "gRPC health check to {ServiceName} failed: {StatusCode}", serviceName, ex.StatusCode);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = $"gRPC {ex.StatusCode}: {ex.Status.Detail}" },
                },
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Warning(ex, "Health check to {ServiceName} failed", serviceName);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = "Service check failed" },
                },
            };
        }
    }

    /// <summary>
    /// Calls the Files gRPC service's CheckHealth RPC and returns the mapped response.
    /// </summary>
    private static async Task<object> CheckGrpcServiceAsync(
        FilesService.FilesServiceClient client,
        string serviceName,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.CheckHealthAsync(
                new CheckHealthRequest(),
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: ct);
            sw.Stop();

            return MapGrpcHealthResponse(response, sw.ElapsedMilliseconds);
        }
        catch (RpcException ex)
        {
            sw.Stop();
            Log.Warning(ex, "gRPC health check to {ServiceName} failed: {StatusCode}", serviceName, ex.StatusCode);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = $"gRPC {ex.StatusCode}: {ex.Status.Detail}" },
                },
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Warning(ex, "Health check to {ServiceName} failed", serviceName);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = "Service check failed" },
                },
            };
        }
    }

    /// <summary>
    /// Calls the SignalR gRPC gateway's CheckHealth RPC and returns the mapped response.
    /// </summary>
    private static async Task<object> CheckGrpcServiceAsync(
        RealtimeGateway.RealtimeGatewayClient client,
        string serviceName,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.CheckHealthAsync(
                new CheckHealthRequest(),
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: ct);
            sw.Stop();

            return MapGrpcHealthResponse(response, sw.ElapsedMilliseconds);
        }
        catch (RpcException ex)
        {
            sw.Stop();
            Log.Warning(ex, "gRPC health check to {ServiceName} failed: {StatusCode}", serviceName, ex.StatusCode);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = $"gRPC {ex.StatusCode}: {ex.Status.Detail}" },
                },
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Warning(ex, "Health check to {ServiceName} failed", serviceName);
            return new
            {
                status = "unhealthy",
                latencyMs = sw.ElapsedMilliseconds,
                components = new Dictionary<string, object>
                {
                    ["error"] = new { status = "unhealthy", error = "Service check failed" },
                },
            };
        }
    }

    /// <summary>
    /// Maps a gRPC CheckHealthResponse to the gateway's anonymous response shape.
    /// </summary>
    private static object MapGrpcHealthResponse(CheckHealthResponse response, long latencyMs)
    {
        var components = new Dictionary<string, object>();
        foreach (var (key, comp) in response.Components)
        {
            components[key] = new
            {
                status = comp.Status,
                latencyMs = comp.LatencyMs,
                error = comp.Error.Falsey() ? null : comp.Error,
            };
        }

        return new
        {
            status = response.Status,
            latencyMs,
            components,
        };
    }

    /// <summary>
    /// Checks the gateway's own cache health via the ping handler.
    /// </summary>
    private static async Task<(string Status, long LatencyMs, string? Error)> CheckGatewayCacheAsync(
        IRead.IPingHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new IRead.PingInput(), ct);
        if (result.Data is not null)
        {
            return (
                result.Data.Healthy ? "healthy" : "unhealthy",
                result.Data.LatencyMs,
                result.Data.Error);
        }

        return ("unhealthy", 0, "Ping handler returned no data");
    }

    /// <summary>
    /// Extracts the status string from an anonymous service result object.
    /// </summary>
    private static string GetStatus(object serviceResult)
    {
        var json = JsonSerializer.Serialize(serviceResult, sr_jsonOptions);
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        return element.TryGetProperty("status", out var s) ? s.GetString() ?? "unknown" : "unknown";
    }

    /// <summary>
    /// Checks whether a service response object has a "healthy" status.
    /// </summary>
    private static bool HasHealthyStatus(object serviceResult) =>
        GetStatus(serviceResult) == "healthy";
}
