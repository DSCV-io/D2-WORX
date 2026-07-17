// -----------------------------------------------------------------------
// <copyright file="InfrastructureBypassMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Tests.Integration.AspNetCore.Infrastructure;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.AspNetCore.TestHost;
using Xunit;

public sealed class InfrastructureBypassMiddlewareTests
{
    [Fact]
    public async Task InfrastructurePath_TagsHttpContextItems()
    {
        // Tag-only mode (TagOnly=true) keeps the marker middleware running
        // so we can verify the flag.
        bool? capturedFlag = null;
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app =>
            {
                app.UseD2InfrastructureBypass(opts => opts.TagOnly = true);
                app.Use(async (ctx, next) =>
                {
                    capturedFlag =
                        ctx.Items[D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY]
                            as bool?;
                    await next(ctx);
                });
            },
            extraEndpoints: endpoints =>
            {
                endpoints.MapGet("/health", () => Results.Text("ok"));
            });

        var client = host.GetTestClient();
        await client.GetAsync("https://localhost/health");

        capturedFlag.Should().BeTrue();
    }

    [Fact]
    public async Task NonInfrastructurePath_TagsHttpContextItemsFalse()
    {
        bool? capturedFlag = null;
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app =>
            {
                app.UseD2InfrastructureBypass(opts => opts.TagOnly = true);
                app.Use(async (ctx, next) =>
                {
                    capturedFlag =
                        ctx.Items[D2AspNetCoreConstants.INFRASTRUCTURE_HTTP_CONTEXT_ITEM_KEY]
                            as bool?;
                    await next(ctx);
                });
            });

        var client = host.GetTestClient();
        await client.GetAsync("https://localhost/api/echo");

        capturedFlag.Should().BeFalse();
    }

    [Fact]
    public async Task ShortCircuitMode_InfrastructurePath_DoesNotRunDownstreamMiddleware()
    {
        // Default short-circuit: bypass middleware invokes the matched
        // endpoint directly, so the downstream marker NEVER runs.
        var downstreamRan = false;
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app =>
            {
                app.UseD2InfrastructureBypass();   // TagOnly=false default
                app.Use(async (ctx, next) =>
                {
                    downstreamRan = true;
                    await next(ctx);
                });
            },
            extraEndpoints: endpoints =>
            {
                endpoints.MapGet("/health", () => Results.Text("ok"));
            });

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/health");

        // Endpoint still served via the routing-matched delegate.
        response.IsSuccessStatusCode.Should().BeTrue();
        downstreamRan.Should().BeFalse();
    }

    [Fact]
    public async Task ShortCircuitMode_NonInfrastructurePath_RunsDownstreamMiddleware()
    {
        var downstreamRan = false;
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app =>
            {
                app.UseD2InfrastructureBypass();
                app.Use(async (ctx, next) =>
                {
                    downstreamRan = true;
                    await next(ctx);
                });
            });

        var client = host.GetTestClient();
        var response = await client.GetAsync("https://localhost/api/echo");

        response.IsSuccessStatusCode.Should().BeTrue();
        downstreamRan.Should().BeTrue();
    }

    [Fact]
    public async Task TagOnlyMode_InfrastructurePath_StillRunsDownstreamMiddleware()
    {
        var downstreamRan = false;
        using var host = await AspNetCoreTestHostBuilder.BuildAsync(
            extraConfigure: app =>
            {
                app.UseD2InfrastructureBypass(opts => opts.TagOnly = true);
                app.Use(async (ctx, next) =>
                {
                    downstreamRan = true;
                    await next(ctx);
                });
            },
            extraEndpoints: endpoints =>
            {
                endpoints.MapGet("/health", () => Results.Text("ok"));
            });

        var client = host.GetTestClient();
        await client.GetAsync("https://localhost/health");

        downstreamRan.Should().BeTrue();
    }
}
