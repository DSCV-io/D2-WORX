// -----------------------------------------------------------------------
// <copyright file="RequestOriginEdgeAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.Host;

using System.Threading.Tasks;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Http;
using D2.Shared.Context.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Edge-HTTP residual: forged Origin header is ignored for authority (local
/// establishment only via public <c>UseD2RequestOriginEdge</c>). Unestablished
/// deny lives in domain authority rules — not in NoOp middleware (see ledger
/// N/A for WithoutMiddleware_UnestablishedDeny).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests.")]
[Trait("Category", "Unit")]
public sealed class RequestOriginEdgeAuthorityTests
{
    [Fact]
    public async Task RequestOriginEdge_ForgedOriginHeader_IgnoredForAuthority()
    {
        // Authority-grade Origin is recomputed from local unforgeable facts —
        // a client-supplied header must never become RequestOrigin authority.
        using var host = await BuildHostAsync();
        var client = host.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Add("X-D2-Request-Origin", "CrossProcessHop");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "1.2.3.4");
        client.DefaultRequestHeaders.Add("X-D2-Origin", "System");

        var body = await client.GetStringAsync("/");

        body.Should().Be(
            "EdgeInbound:1:edge",
            "forged headers must not override Edge-inbound establishment");
    }

    private static async Task<IHost> BuildHostAsync()
    {
        // Mirrors Shared RequestOriginEdgeInboundMiddlewareTests TestServer path
        // (public UseD2RequestOriginEdge) with an authenticated context so
        // establishment runs; forged headers ride on the client.
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

                        app.Use(async (http, next) =>
                        {
                            // Establish request context (auth middleware's job in prod).
                            var ctx = new MutableRequestContext { IsAuthenticated = true };
                            http.Items[D2HttpContextItems.REQUEST_CONTEXT] = ctx;
                            await next();
                        });

                        app.UseD2RequestOriginEdge();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/", (HttpContext http) =>
                            {
                                var ctx = (IRequestContext)http.Items[
                                    D2HttpContextItems.REQUEST_CONTEXT]!;
                                return Results.Text(
                                    $"{ctx.Origin}:{ctx.CallPath.Count}:{ctx.CallPath[0].Id}");
                            });
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }
}
