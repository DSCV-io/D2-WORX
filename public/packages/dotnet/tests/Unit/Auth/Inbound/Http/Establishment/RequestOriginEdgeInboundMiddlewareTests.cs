// -----------------------------------------------------------------------
// <copyright file="RequestOriginEdgeInboundMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http.Establishment;

using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Http;
using D2.Shared.Auth.Http.Middleware;
using D2.Shared.Context.Abstractions;
using D2.Shared.Time;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;

/// <summary>
/// Unit + in-memory-TestServer matrix for the Edge-inbound establishment middleware: it
/// establishes <see cref="RequestOrigin.EdgeInbound"/>, a null caller (the external
/// client is not an internal workload), and STARTS the call-path with a single Edge
/// entry; and is a no-op when no request-context was populated upstream.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests, not the class.")]
[Trait("Category", "Unit")]
public sealed class RequestOriginEdgeInboundMiddlewareTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 6, 30, 12, 0, 0);

    // ---- Constructor null guards ----

    [Fact]
    public void Constructor_NullNext_Throws()
    {
        var act = () => new RequestOriginEdgeInboundMiddleware(
            null!,
            Options.Create(new D2WorkloadIdentityOptions { ServiceId = "edge" }),
            new TestClock(sr_now));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullWorkloadIdentity_Throws()
    {
        var act = () => new RequestOriginEdgeInboundMiddleware(
            _ => Task.CompletedTask,
            null!,
            new TestClock(sr_now));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullClock_Throws()
    {
        var act = () => new RequestOriginEdgeInboundMiddleware(
            _ => Task.CompletedTask,
            Options.Create(new D2WorkloadIdentityOptions { ServiceId = "edge" }),
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Invoke_WithEstablishedContext_StartsEdgeOriginAndCallPath()
    {
        var ctx = new MutableRequestContext { IsAuthenticated = true };
        var http = new DefaultHttpContext();
        http.Items[D2HttpContextItems.REQUEST_CONTEXT] = ctx;
        var nextRan = false;
        var middleware = Build(_ =>
        {
            nextRan = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(http);

        nextRan.Should().BeTrue();
        ctx.Origin.Should().Be(RequestOrigin.EdgeInbound);
        ctx.ImmediateCaller.Should().BeNull("the external client is not an internal workload");
        ctx.CallPath.Should().ContainSingle();
        ctx.CallPath[0].Id.Should().Be("edge");
        ctx.CallPath[0].Kind.Should().Be(CallPathKind.Edge);
        ctx.CallPath[0].Timestamp.Should().Be(sr_now.ToDateTimeOffset());
    }

    [Fact]
    public async Task Invoke_NoEstablishedContext_IsNoOpButCallsNext()
    {
        var http = new DefaultHttpContext();
        var nextRan = false;
        var middleware = Build(_ =>
        {
            nextRan = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(http);

        nextRan.Should().BeTrue("a request with no established context still flows through");
    }

    [Fact]
    public async Task TestServer_EdgeRequest_EstablishesEdgeOriginAndSingleCallPathEntry()
    {
        using var host = await BuildHostAsync();
        using var httpClient = host.GetTestServer().CreateClient();

        var body = await httpClient.GetStringAsync("/");

        body.Should().Be(
            "EdgeInbound:1:edge",
            "the TestServer request flows through UseD2RequestOriginEdge and starts the path");
    }

    private static RequestOriginEdgeInboundMiddleware Build(RequestDelegate next)
        => new(
            next,
            Options.Create(new D2WorkloadIdentityOptions { ServiceId = "edge" }),
            new TestClock(sr_now));

    private static async Task<IHost> BuildHostAsync()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddD2RequestOriginEdge(o => o.ServiceId = "edge");
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        // Simulate the auth middleware populating the request-context slot.
                        app.Use(async (httpContext, next) =>
                        {
                            httpContext.Items[D2HttpContextItems.REQUEST_CONTEXT] =
                                new MutableRequestContext { IsAuthenticated = true };

                            await next(httpContext);
                        });

                        app.UseD2RequestOriginEdge();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/", async httpContext =>
                            {
                                var ctx = (MutableRequestContext)httpContext
                                    .Items[D2HttpContextItems.REQUEST_CONTEXT]!;
                                var firstId = ctx.CallPath.Count > 0 ? ctx.CallPath[0].Id : "none";

                                await httpContext.Response.WriteAsync(
                                    $"{ctx.Origin}:{ctx.CallPath.Count}:{firstId}");
                            });
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }
}
