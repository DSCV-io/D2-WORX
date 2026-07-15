// -----------------------------------------------------------------------
// <copyright file="EdgePipelineAuthGateTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.Host;

using System.Text.Json;
using System.Threading.Tasks;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Http;
using DcsvIo.D2.Auth.Http.Endpoints;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Host-layer residual: gated HTTP route without bearer is denied (401) via real
/// <c>UseD2Auth</c> middleware. Full pipeline order is pinned by
/// <c>UseD2EdgePipelineOrderTests</c> (source-order). Matrix name
/// <c>UseD2EdgePipeline_UnauthenticatedGatedRoute_IsDenied</c> aliases to
/// <see cref="UseD2Auth_UnauthenticatedGatedRoute_IsDenied"/> ΓÇö auth gate of the
/// pipeline, not a re-composition of the full UseD2EdgePipeline graph.
/// Host = pipeline/DI only ΓÇö not TypeSpecGrpc/WellKnown.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests.")]
[Trait("Category", "Unit")]
public sealed class EdgePipelineAuthGateTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "d2.internal";

    [Fact]
    public async Task UseD2Auth_UnauthenticatedGatedRoute_IsDenied()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/gated", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task UseD2Auth_AuthenticatedGatedRoute_IsAdmitted()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt);
        var client = host.GetTestClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object>
            {
                ["scope"] = ProductScopes.Internal.Audit.Ping,
            });
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(new Uri("/gated", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("admitted");
    }

    private static async Task<IHost> BuildHostAsync(TestJwtBuilder jwtBuilder)
    {
        // Same public-API harness pattern as GrpcKeyringScopeEnforcementTests:
        // AddD2Auth + replace public IJwksProvider / ISessionLivenessTracker only
        // (JwtValidator is internal â€” never re-register from Edge.Tests).
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddD2LocalCache();
                        services.AddSingleton<ITieredCache, FakeTieredCacheStub>();
                        services.AddD2Auth(opts =>
                        {
                            opts.Issuer = new Uri(_ISSUER);
                            opts.Audience = _AUDIENCE;
                        });

                        services.RemoveAll<IJwksProvider>();
                        services.AddSingleton<IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));

                        services.RemoveAll<ISessionLivenessTracker>();
                        services.AddSingleton<ISessionLivenessTracker>(
                            new FakeSessionLivenessTracker());

                        services.AddD2AuthHttp();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Auth();
                        app.UseEndpoints(endpoints =>
                        {
                            // Production gated Map pattern â€” Scopes.* constant.
                            endpoints.MapGet("/gated", () => "admitted")
                                .RequireAnyScope(ProductScopes.Internal.Audit.Ping);
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }
}
