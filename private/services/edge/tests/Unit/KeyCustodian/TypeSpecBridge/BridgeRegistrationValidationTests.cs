// -----------------------------------------------------------------------
// <copyright file="BridgeRegistrationValidationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge;

using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Http;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Fixtures;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecBridge.Generated;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using DcsvIo.D2.Result;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using ISessionLivenessTracker =
    DcsvIo.D2.Auth.Abstractions.Sessions.ISessionLivenessTracker;

/// <summary>
/// Compile+run validation for Edge HTTP→gRPC bridge Map* registrations
/// against real <c>DcsvIo.D2.Auth.Http</c> + <c>DcsvIo.D2.Result</c>.
/// Unbuilt collaborator <c>I{Module}GrpcClient</c> is a faithful
/// <see cref="FakeBridgeFixtureGrpcClient"/> (§1.32).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestJwtBuilder lifetime is bounded by individual tests.")]
public sealed class BridgeRegistrationValidationTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "keycustodian";

    [Fact]
    public async Task BridgeRoute_BearerWithRequiredScope_Returns200AndCallsClient()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeBridgeFixtureGrpcClient(
            D2Result<BridgeFixturePingOutput?>.Ok(new BridgeFixturePingOutput("pong")));

        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "self.read" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/internal/v1/fixtures/bridge-ping?Id=k1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fake.CallCount.Should().Be(1);
        fake.LastInput.Should().NotBeNull();
        fake.LastInput!.Id.Should().Be("k1");
    }

    [Fact]
    public async Task BridgeRoute_ClientServiceUnavailable_Returns503ProblemDetails()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeBridgeFixtureGrpcClient(
            D2Result<BridgeFixturePingOutput?>.ServiceUnavailable());

        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "self.read" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/internal/v1/fixtures/bridge-ping?Id=k1");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        fake.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task BridgeRoute_WrongScope_Returns401ScopeInsufficient()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeBridgeFixtureGrpcClient(
            D2Result<BridgeFixturePingOutput?>.Ok(new BridgeFixturePingOutput("pong")));

        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "other.scope" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/internal/v1/fixtures/bridge-ping?Id=k1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
        fake.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task BridgeRoute_NoBearer_Returns401BearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeBridgeFixtureGrpcClient(
            D2Result<BridgeFixturePingOutput?>.Ok(new BridgeFixturePingOutput("pong")));

        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/internal/v1/fixtures/bridge-ping?Id=k1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
        fake.CallCount.Should().Be(0);
    }

    private static async Task<IHost> BuildHostAsync(
        TestJwtBuilder jwtBuilder,
        FakeBridgeFixtureGrpcClient fake)
    {
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

                        // Faithful double of I{Module}GrpcClient / AddD2*GrpcClients seam.
                        services.AddSingleton<IBridgeFixtureGrpcClient>(fake);
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Auth();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPingBridgeFixtureBridge();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty(
                DcsvIo.D2.ProblemDetails.D2ProblemDetailsKeys.EXTENSION_ERROR_CODE, out var el))
            return el.GetString();
        return null;
    }
}
