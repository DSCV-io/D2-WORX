// -----------------------------------------------------------------------
// <copyright file="AspNetCoreTestHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;

using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Hosting;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.AspNetCore.Routing;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.Configuration;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Hosting;

/// <summary>
/// Static helper that builds a fully wired AspNetCore test host suitable for
/// driving the D² AspNetCore middleware stack end-to-end through
/// <c>Microsoft.AspNetCore.TestHost</c>. Mirrors the
/// <c>LoggingTestHostBuilder</c> shape — accepts optional services / app /
/// endpoints mutators so each test can compose its own pipeline shape.
/// </summary>
internal static class AspNetCoreTestHostBuilder
{
    /// <summary>
    /// Builds and starts an in-process AspNetCore host with optional
    /// services / app / endpoints mutators. Default registers a
    /// <c>GET /api/echo</c> endpoint returning <c>"ok"</c>; tests typically
    /// register additional endpoints via <paramref name="extraEndpoints"/>
    /// (e.g. <c>/health</c>, <c>/throw</c>).
    /// </summary>
    /// <param name="extraServices">
    /// Optional services-collection mutator invoked after default
    /// registrations.
    /// </param>
    /// <param name="extraConfigure">
    /// Optional middleware-pipeline mutator invoked after the default
    /// pipeline (UseRouting + UseEndpoints).
    /// </param>
    /// <param name="extraEndpoints">
    /// Optional endpoint-registration mutator invoked when the default
    /// endpoints are mapped.
    /// </param>
    /// <param name="configuration">
    /// Optional in-memory configuration to layer beneath the host's
    /// configuration pipeline.
    /// </param>
    /// <returns>
    /// The started <see cref="IHost"/> — caller disposes via
    /// <c>using</c> declaration.
    /// </returns>
    internal static async Task<IHost> BuildAsync(
        Action<IServiceCollection>? extraServices = null,
        Action<IApplicationBuilder>? extraConfigure = null,
        Action<IEndpointRouteBuilder>? extraEndpoints = null,
        IDictionary<string, string?>? configuration = null)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                if (configuration is { Count: > 0 })
                    cfg.AddInMemoryCollection(configuration);
            })
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        extraServices?.Invoke(services);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        extraConfigure?.Invoke(app);

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/api/echo", () => Results.Text("ok"));
                            extraEndpoints?.Invoke(endpoints);
                        });
                    });
            });

        var host = await hostBuilder.StartAsync();
        return host;
    }
}
