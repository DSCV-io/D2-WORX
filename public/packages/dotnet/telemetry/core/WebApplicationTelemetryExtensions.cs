// -----------------------------------------------------------------------
// <copyright file="WebApplicationTelemetryExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry;

using DcsvIo.D2.Telemetry.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

/// <summary>
/// ASP.NET Core endpoint-routing extensions for
/// <see cref="DcsvIo.D2.Telemetry"/> — maps the in-process Prometheus
/// scraping endpoint at
/// <see cref="D2TelemetryConstants.PROMETHEUS_ENDPOINT_PATH"/> with an
/// IP-allow-list endpoint filter that restricts access to loopback +
/// RFC 1918 private addresses (operator-only access; no public
/// throttling needed since the surface is private).
/// </summary>
public static class WebApplicationTelemetryExtensions
{
    /// <param name="endpoints">The endpoint route builder.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the <c>/metrics</c> route via OpenTelemetry's
        /// <c>MapPrometheusScrapingEndpoint</c> AND attaches an endpoint
        /// filter that returns <c>403 Forbidden</c> for any request whose
        /// connection-remote IP is neither loopback nor RFC 1918 private.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When
        /// <see cref="D2TelemetryConstants.OTEL_SDK_DISABLED_ENV_VAR"/>
        /// is set to <c>"true"</c> (case-insensitive), this method
        /// short-circuits — no <c>/metrics</c> route is mapped — symmetric
        /// with the
        /// <see cref="TelemetryServiceCollectionExtensions.AddD2Telemetry"/>
        /// no-op behavior. Consumers MUST tolerate the absence of the
        /// route under the kill-switch condition.
        /// </para>
        /// <para>
        /// The IP allow-list deliberately matches the connection-remote
        /// IP, not any forwarded-for header. Hosts behind reverse proxies
        /// expose the proxy IP at the connection layer; the proxy IS the
        /// loopback / RFC 1918 source for in-cluster scraping. Public
        /// scrapers must not reach this endpoint by design.
        /// </para>
        /// </remarks>
        /// <returns>
        /// The same <paramref name="endpoints"/> for fluent chaining.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="endpoints"/> is null.
        /// </exception>
        public IEndpointRouteBuilder MapD2PrometheusEndpoint()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            if (OtelSdkDisabledGate.IsDisabled())
                return endpoints;

            endpoints.MapPrometheusScrapingEndpoint()
                .AddEndpointFilter(static async (context, next) =>
                {
                    var httpContext = context.HttpContext;
                    var remoteIp = httpContext.Connection.RemoteIpAddress;

                    if (InternalIpFilter.IsAllowed(remoteIp))
                        return await next(context);

                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Results.Text("Forbidden");
                });

            return endpoints;
        }
    }
}
