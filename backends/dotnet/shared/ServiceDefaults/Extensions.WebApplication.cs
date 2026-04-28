// -----------------------------------------------------------------------
// <copyright file="Extensions.WebApplication.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceDefaults;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Serilog;

public static partial class Extensions
{
    /// <param name="app">
    /// Extension methods for the web application.
    /// </param>
    extension(WebApplication app)
    {
        /// <summary>
        /// Configures structured request logging for the web application.
        /// </summary>
        public void UseStructuredRequestLogging()
        {
            app.UseSerilogRequestLogging(options =>
            {
                options.GetLevel = (ctx, _, _) =>
                {
                    // Exclude infrastructure endpoints from logs
                    if (ctx.Request.Path.StartsWithSegments(_HEALTH_ENDPOINT_PATH) ||
                        ctx.Request.Path.StartsWithSegments(_ALIVE_ENDPOINT_PATH) ||
                        ctx.Request.Path.StartsWithSegments(_METRICS_ENDPOINT_PATH))
                    {
                        // This will log at Verbose level, which is typically not recorded.
                        return Serilog.Events.LogEventLevel.Verbose;
                    }

                    return Serilog.Events.LogEventLevel.Information;
                };

                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);

                    diagnosticContext.Set(
                        "UserAgent",
                        httpContext.Request.Headers.UserAgent.ToString());

                    diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);

                    if (httpContext.Request.Host.Value is not null)
                    {
                        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    }

                    if (httpContext.Connection.RemoteIpAddress is not null)
                    {
                        diagnosticContext.Set(
                            "RemoteIp",
                            httpContext.Connection.RemoteIpAddress.ToString());
                    }
                };
            });
        }

        /// <summary>
        /// Maps default health check endpoints for the web application.
        /// </summary>
        public void MapDefaultEndpoints()
        {
            app.MapHealthChecks(_HEALTH_ENDPOINT_PATH);

            app.MapHealthChecks(_ALIVE_ENDPOINT_PATH, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live"),
            });
        }

        /// <summary>
        /// Maps the Prometheus metrics endpoint with IP address restrictions.
        /// </summary>
        public void MapPrometheusEndpointWithIpRestriction()
        {
            // Skip Prometheus endpoint when OTel SDK is disabled (e.g., in E2E tests).
            if (string.Equals(
                    Environment.GetEnvironmentVariable("OTEL_SDK_DISABLED"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            app.MapPrometheusScrapingEndpoint()
                .AddEndpointFilter(async (context, next) =>
                {
                    var httpContext = context.HttpContext;
                    var remoteIp = httpContext.Connection.RemoteIpAddress;

                    if (IsAllowedIpForMetrics(remoteIp))
                    {
                        return await next(context);
                    }

                    httpContext.Response.StatusCode = 403;
                    return Results.Text("Forbidden");
                });
        }
    }
}
