// -----------------------------------------------------------------------
// <copyright file="ServiceDefaultsTestHostBuilder.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults.Infrastructure;

using System.Collections.Generic;
using DcsvIo.D2.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Static helper that builds a fully wired AspNetCore test host driving
/// the D² ServiceDefaults aggregator end-to-end through
/// <c>Microsoft.AspNetCore.TestHost</c>. Mirrors the
/// <c>AspNetCoreTestHostBuilder</c> + <c>TelemetryTestHostBuilder</c>
/// pattern. Auth wiring is OPT-OUT by default
/// (<see cref="D2ServiceDefaultsOptions.SkipAuthAutoWiring"/> = <c>true</c>)
/// so tests don't need to register <c>ITieredCache</c> +
/// <c>JwtValidator</c>'s transitive dependency closure. Tests that
/// exercise the auth-wired path supply a configure callback populating
/// <see cref="D2ServiceDefaultsOptions.AuthConfigure"/> and provide the
/// required <c>ITieredCache</c> via the extras-services hook.
/// </summary>
internal static class ServiceDefaultsTestHostBuilder
{
    /// <summary>
    /// Builds and starts an in-process AspNetCore host with
    /// <c>AddD2ServiceDefaults</c> + <c>UseD2DefaultPipeline</c> +
    /// <c>MapD2DefaultEndpoints</c> wired plus a default <c>GET /probe</c>
    /// endpoint that returns <c>"ok"</c>. <c>D2_CORS_ORIGINS:0</c> is
    /// pre-populated (in-memory IConfiguration form) to satisfy the CORS
    /// validator's fail-closed gate.
    /// </summary>
    /// <param name="configureOptions">
    /// Optional callback to mutate the aggregator's
    /// <see cref="D2ServiceDefaultsOptions"/> (e.g. to opt out of
    /// LocalCache, opt INTO auth wiring, set per-component pass-through
    /// configurations). When null, defaults to
    /// <c>opts =&gt; opts.SkipAuthAutoWiring = true</c>.
    /// </param>
    /// <param name="extraServices">
    /// Optional services-collection mutator invoked AFTER
    /// <c>AddD2ServiceDefaults</c>.
    /// </param>
    /// <param name="extraConfigure">
    /// Optional middleware-pipeline mutator invoked AFTER
    /// <c>UseD2DefaultPipeline</c>.
    /// </param>
    /// <param name="extraEndpoints">
    /// Optional endpoint-registration mutator invoked alongside
    /// <c>MapD2DefaultEndpoints</c> + the default <c>/probe</c> endpoint.
    /// </param>
    /// <param name="extraConfiguration">
    /// Optional in-memory configuration to layer beneath the host's
    /// configuration pipeline.
    /// </param>
    /// <returns>The started <see cref="IHost"/>; caller disposes.</returns>
    internal static async Task<IHost> BuildAsync(
        Action<D2ServiceDefaultsOptions>? configureOptions = null,
        Action<IServiceCollection>? extraServices = null,
        Action<IApplicationBuilder>? extraConfigure = null,
        Action<IEndpointRouteBuilder>? extraEndpoints = null,
        IDictionary<string, string?>? extraConfiguration = null)
    {
        var configDict = new Dictionary<string, string?>
        {
            // Satisfy AddD2Cors's fail-closed validator with a single
            // canonical origin. In-memory IConfiguration uses ":" path
            // separator (env vars use "__"); the production wire-up
            // accepts D2_CORS_ORIGINS__0 / __1 / ... env vars which
            // .NET translates to "D2_CORS_ORIGINS:0" / ":1" / ... .
            ["D2_CORS_ORIGINS:0"] = "https://example.com",

            // Satisfy AddD2Logging's required ServiceName / Environment
            // when no IHostEnvironment ApplicationName is registered (the
            // generic HostBuilder ApplicationName is "testhost" or
            // similar — adequate fallback, but pin a value for
            // determinism).
            ["OTEL_SERVICE_NAME"] = "service-defaults-tests",
        };

        if (extraConfiguration is { Count: > 0 })
        {
            foreach (var (key, value) in extraConfiguration)
                configDict[key] = value;
        }

        var hostBuilder = new HostBuilder()
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddInMemoryCollection(configDict);
            })
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices((ctx, services) =>
                    {
                        services.AddRouting();

                        services.AddD2ServiceDefaults(
                            ctx.Configuration,
                            configureOptions ?? (opts =>
                                opts.SkipAuthAutoWiring = true));

                        extraServices?.Invoke(services);
                    })
                    .Configure(app =>
                    {
                        app.UseD2DefaultPipeline();

                        extraConfigure?.Invoke(app);

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapD2DefaultEndpoints();
                            endpoints.MapGet("/probe", () => Results.Text("ok"));
                            extraEndpoints?.Invoke(endpoints);
                        });
                    });
            });

        var host = await hostBuilder.StartAsync();
        return host;
    }
}
