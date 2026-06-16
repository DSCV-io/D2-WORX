// -----------------------------------------------------------------------
// <copyright file="RoutePolicyEnforcementTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute;

using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using D2.Edge.Tests.TypeSpecDto.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated;
using D2.Edge.Tests.TypeSpecRoute.Generated.Facade;
using D2.Edge.Tests.Unit.KeyCustodian.TypeSpecRoute.Fixtures;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Errors;
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
/// TestServer auth-matrix enforcement tests for the TypeSpec-emitted
/// <c>SignRouteRegistration</c> and <c>AllScopesRouteRegistration</c>.
///
/// Drives real HTTP requests through <c>JwtAuthMiddleware</c> + the emitted route
/// fluents (<c>RequireAnyScope</c> / <c>RequireAllScopes</c>) with real RS256-signed
/// JWTs from <see cref="TestJwtBuilder"/> backed by <see cref="FakeJwksProvider"/>.
/// Validates the three emitted scope policies (any/all/harmless) and both
/// rejection paths (missing bearer, insufficient scope).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestJwtBuilder lifetime is bounded by individual tests.")]
public sealed class RoutePolicyEnforcementTests
{
    private const string _ISSUER = "https://edge.internal";
    private const string _AUDIENCE = "keycustodian";

    // ── RequireAnyScope — sign route ──────────────────────────────────────

    [Fact]
    public async Task SignRoute_BearerWithRequiredScope_Returns200()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, D2Result<SignOutput?>.Ok(new SignOutput("sig==")));
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "self.write" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "policy-test-1");

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "k1", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignRoute_BearerWithWrongScope_Returns401ScopeInsufficient()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, D2Result<SignOutput?>.Ok(new SignOutput("sig==")));
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "other.scope" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "k1", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task SignRoute_NoBearer_Returns401BearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, D2Result<SignOutput?>.Ok(new SignOutput("sig==")));
        var client = host.GetTestServer().CreateClient();

        var response = await client.PostAsync(
            "/internal/v1/kc/sign",
            JsonContent.Create(new { kid = "k1", payload = string.Empty }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task SignRoute_BearerWithNoScopes_Returns401ScopeInsufficient()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, D2Result<SignOutput?>.Ok(new SignOutput("sig==")));
        var client = host.GetTestServer().CreateClient();

        // No "scope" claim in token.
        var token = jwt.MintToken(_ISSUER, _AUDIENCE);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "k1", payload = string.Empty });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    // ── RequireAllScopes — allScopes route ────────────────────────────────

    [Fact]
    public async Task AllScopesRoute_BearerWithBothScopes_Returns200()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, D2Result<SignOutput?>.Ok(new SignOutput("sig==")));
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "self.read self.write" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // GET route — input is bound from query string via [AsParameters].
        // Payload is byte[] and not needed for scope-enforcement proof; kid=k1 satisfies model binding.
        var response = await client.GetAsync(
            "/internal/v1/kc/all-scopes?kid=k1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AllScopesRoute_BearerMissingOneScope_Returns401ScopeInsufficient()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, D2Result<SignOutput?>.Ok(new SignOutput("sig==")));
        var client = host.GetTestServer().CreateClient();

        // Only self.read — self.write missing.
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "self.read" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(
            "/internal/v1/kc/all-scopes?kid=k1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public async Task AllScopesRoute_NoBearer_Returns401BearerMissing()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, D2Result<SignOutput?>.Ok(new SignOutput("sig==")));
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync(
            "/internal/v1/kc/all-scopes?kid=k1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var code = await ReadErrorCodeAsync(response);
        code.Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    // ── Facade failure → problem-details ──────────────────────────────────

    [Fact]
    public async Task SignRoute_FacadeReturnsFailure_ReturnsProblemJson()
    {
        using var jwt = new TestJwtBuilder();
        using var host = await BuildHostAsync(jwt, D2Result<SignOutput?>.ServiceUnavailable());
        var client = host.GetTestServer().CreateClient();
        var token = jwt.MintToken(
            _ISSUER,
            _AUDIENCE,
            extraClaims: new Dictionary<string, object> { ["scope"] = "self.write" });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", "policy-test-2");

        var response = await client.PostAsJsonAsync(
            "/internal/v1/kc/sign",
            new { kid = "k1", payload = string.Empty });

        // MAP-ii: failure → Results.Json(pd, ...) — not 200
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // ── Host builder ───────────────────────────────────────────────────────

    private static async Task<IHost> BuildHostAsync(
        TestJwtBuilder jwtBuilder,
        D2Result<SignOutput?> facadeResult)
    {
        var fake = new FakeKeyCustodianSignerFacade(facadeResult);

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

                        // Swap session liveness tracker for the always-alive fake.
                        services.RemoveAll<D2.Shared.Auth.Abstractions.Sessions.ISessionLivenessTracker>();
                        services.AddSingleton<D2.Shared.Auth.Abstractions.Sessions.ISessionLivenessTracker>(
                            new FakeSessionLivenessTracker());

                        // Register the façade fake for DI resolution in the route lambdas.
                        services.AddSingleton<IKeyCustodianSignerFacade>(fake);

                        // The Sign route gate requires D2GeneratedIdempotencyStore in DI;
                        // policy-enforcement tests supply a no-op store — idempotency semantics
                        // are tested separately in RouteIdempotencyGateTests.
                        services.AddSingleton<D2GeneratedIdempotencyStore>(
                            new FakeIdempotencyStore());
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();

                        // JwtAuthMiddleware — AFTER UseRouting, BEFORE UseEndpoints.
                        app.UseD2Auth();

                        app.UseEndpoints(endpoints =>
                        {
                            // TypeSpec-emitted route registrations via extension methods.
                            endpoints.MapSignRoute();
                            endpoints.MapAllScopesRoute();
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
                D2.Shared.ProblemDetails.D2ProblemDetailsKeys.EXTENSION_ERROR_CODE, out var el))
            return el.GetString();
        return null;
    }
}
