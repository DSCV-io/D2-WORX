// -----------------------------------------------------------------------
// <copyright file="RouteFacadeDelegationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute;

using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using D2.Edge.Tests.TypeSpecDto.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Http;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Result;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Delegation tests for the TypeSpec-emitted <c>SignRouteRegistration</c>.
/// Exercises the MAP-ii 2xx-status-mapped shape:
///   - success (2xx/3xx status) maps to <c>Results.Json(result.Data, statusCode: status)</c>
///     — e.g. Ok 200, Created 201, SomeFound 206, preserving the real status code
///   - failure (status &gt;= 400) maps to <c>Results.Json(pd, statusCode, contentType=application/problem+json)</c>
///
/// Verifies that the route lambda correctly threads the input through to the
/// façade and returns the right HTTP response shape for both paths.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestJwtBuilder lifetime is bounded by individual tests.")]
public sealed class RouteFacadeDelegationTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "keycustodian";
    private const string _SIGN_SCOPE = "self.write";

    // ── Success path ──────────────────────────────────────────────────────

    [Fact]
    public async Task SignRoute_Success_Returns200WithSignatureBody()
    {
        const string expectedSig = "abc123==";
        using var jwt = new TestJwtBuilder();
        var fake = new FakeKeyCustodianSignerFacade(
            D2Result<SignOutput?>.Ok(new SignOutput(expectedSig)));
        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SIGN_SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "delegation-test-1");

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "key-001", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(expectedSig);
    }

    [Fact]
    public async Task SignRoute_Success_FacadeReceivedCorrectInput()
    {
        // Assert that the route lambda correctly plumbs the deserialized input
        // through to the façade without mutation.
        const string kid = "expected-kid";
        using var jwt = new TestJwtBuilder();
        var fake = new FakeKeyCustodianSignerFacade(
            D2Result<SignOutput?>.Ok(new SignOutput("sig==")));
        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SIGN_SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "delegation-test-2");

        await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new SignInput(kid, Array.Empty<byte>()));

        fake.SignCallCount.Should().Be(1);
        fake.LastSignInput.Should().NotBeNull();
        fake.LastSignInput!.Kid.Should().Be(kid);
    }

    // ── Status-fidelity paths — MAP-ii emits the real 2xx status code ────

    [Fact]
    public async Task SignRoute_Created_Returns201WithBody()
    {
        // Proves Created (201) survives MAP-ii as HTTP 201 — the old Results.Ok
        // branch would have returned 200 instead of 201.
        const string expectedSig = "created-sig==";
        using var jwt = new TestJwtBuilder();
        var fake = new FakeKeyCustodianSignerFacade(
            D2Result<SignOutput?>.Created(new SignOutput(expectedSig)));
        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SIGN_SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "delegation-test-created");

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "key-001", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(expectedSig);
    }

    [Fact]
    public async Task SignRoute_SomeFound_Returns206WithBody()
    {
        // Pins the latent bug D3: a SomeFound result has Success==false AND
        // StatusCode==206. The old if (result.Success) branch would have routed
        // this to ToProblemDetails, which throws on a 2xx status code. This test
        // MUST fail without the status-mapped emitter change (status < 400 branch).
        const string expectedSig = "some-found-sig==";
        using var jwt = new TestJwtBuilder();
        var fake = new FakeKeyCustodianSignerFacade(
            D2Result<SignOutput?>.SomeFound(new SignOutput(expectedSig)));
        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SIGN_SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "delegation-test-somefound");

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "key-001", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(expectedSig);
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("application/problem+json");
    }

    // ── Failure path — MAP-ii: failure → problem-details JSON ────────────

    [Fact]
    public async Task SignRoute_ServiceUnavailable_Returns503ProblemJson()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeKeyCustodianSignerFacade(D2Result<SignOutput?>.ServiceUnavailable());
        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SIGN_SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "delegation-test-3");

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "k1", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task SignRoute_NotFound_Returns404ProblemJson()
    {
        using var jwt = new TestJwtBuilder();
        var fake = new FakeKeyCustodianSignerFacade(D2Result<SignOutput?>.NotFound());
        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SIGN_SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "delegation-test-4");

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "k1", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task SignRoute_ValidationFailed_Returns400ProblemJson()
    {
        // D2Result<T>.ValidationFailed() maps to HTTP 400 (BadRequest) — the
        // project's semantic: input validation failure is a client error (RFC 7807
        // recommends 400 for constraint violations; 422 is used for semantic errors
        // beyond syntactic validation, which D2Result uses a different factory for).
        using var jwt = new TestJwtBuilder();
        var fake = new FakeKeyCustodianSignerFacade(D2Result<SignOutput?>.ValidationFailed());
        using var host = await BuildHostAsync(jwt, fake);
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = _SIGN_SCOPE });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "delegation-test-5");

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "k1", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // ── Host builder ───────────────────────────────────────────────────────

    private static async Task<IHost> BuildHostAsync(
        TestJwtBuilder jwtBuilder,
        FakeKeyCustodianSignerFacade fake)
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

                        // Swap network-touching JWKS provider for the in-memory fake.
                        // JwtValidator is registered via TryAddSingleton (no explicit factory)
                        // and resolves IJwksProvider lazily — swapping the public seam is
                        // sufficient; no internal JwtValidator construction needed.
                        services.RemoveAll<IJwksProvider>();
                        services.AddSingleton<IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));

                        services.RemoveAll<D2.Shared.Auth.Abstractions.Sessions.ISessionLivenessTracker>();
                        services.AddSingleton<D2.Shared.Auth.Abstractions.Sessions.ISessionLivenessTracker>(
                            new FakeSessionLivenessTracker());

                        services.AddSingleton<IKeyCustodianSignerFacade>(fake);

                        // The Sign route gate requires D2GeneratedIdempotencyStore in DI;
                        // delegation tests supply a no-op store — idempotency semantics
                        // are tested separately in RouteIdempotencyGateTests.
                        services.AddSingleton<D2GeneratedIdempotencyStore>(
                            new FakeIdempotencyStore());
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Auth();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapSignRoute();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }
}
