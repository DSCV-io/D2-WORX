// -----------------------------------------------------------------------
// <copyright file="WellKnownRouteTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.WellKnown;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
using D2.Edge.KeyCustodian.App.Application.Routes;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Clients;
using D2.Edge.Tests.Unit.KeyCustodian.App;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NodaTime;

/// <summary>
/// TestServer tests for the generated well-known routes
/// (<c>GetJwksRouteRegistration</c> / <c>GetOidcConfigurationRouteRegistration</c>).
/// Both are <c>@d2Harmless</c> — anonymous + reachable pre-auth. Proves the
/// emitted MapGet delegate plumbs the real KeyCustodian façade end-to-end:
/// the JWKS document from a seeded signing key, the OIDC document with the
/// canonical snake_case keys, and the empty-store 503 fail-secure path.
/// </summary>
public sealed class WellKnownRouteTests
{
    private const string _ISSUER = "https://edge.internal";

    // ── JWKS route ───────────────────────────────────────────────────────

    [Fact]
    public async Task JwksRoute_SeededSigningKeys_Returns200WithActiveFirstJwkSet()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created + Duration.FromHours(2));
        var retiringKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Retiring,
            created,
            activatedAt: created,
            retiringAt: created + Duration.FromHours(3));

        using var host = await BuildHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var jwks = await response.Content.ReadFromJsonAsync<GetJwksOutput>();
        jwks!.Keys.Select(k => k.Kid).Should().ContainInOrder(activeKid, retiringKid);
        jwks.Keys.Should().OnlyContain(
            k => k.Kty == "RSA" && k.Use == "sig" && k.Alg == "RS256");
    }

    [Fact]
    public async Task JwksRoute_EmptyStore_Returns503FailSecure()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var options = BuildOptions();

        using var host = await BuildHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task JwksRoute_NoAuthHeader_IsReachable()
    {
        // @d2Harmless → the route is anonymous; a request with no Authorization
        // header must reach the handler (not 401/403). An empty store yields 503,
        // which still proves the auth pipeline did not block the request.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var options = BuildOptions();

        using var host = await BuildHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    // ── OIDC discovery route ─────────────────────────────────────────────

    [Fact]
    public async Task OidcRoute_Returns200WithIssuerJwksUriAndRs256()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var options = BuildOptions();

        using var host = await BuildHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await response.Content.ReadFromJsonAsync<GetOidcConfigurationOutput>();
        doc!.Issuer.Should().Be(_ISSUER);
        doc.JwksUri.Should().Be($"{_ISSUER}/.well-known/jwks.json");
        doc.IdTokenSigningAlgValuesSupported.Should().Equal("RS256");
    }

    [Fact]
    public async Task OidcRoute_SerializesCanonicalSnakeCaseKeys()
    {
        // The wire keys must be the canonical OIDC snake_case names so strict OIDC
        // clients (and .NET's ConfigurationManager) parse them — the @encodedName
        // emitter ext is what makes this work over the live route.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var options = BuildOptions();

        using var host = await BuildHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        var json = await client.GetStringAsync("/.well-known/openid-configuration");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("jwks_uri", out _).Should().BeTrue();
        root.TryGetProperty("id_token_signing_alg_values_supported", out _).Should().BeTrue();
        root.TryGetProperty("response_types_supported", out _).Should().BeTrue();
        root.TryGetProperty("subject_types_supported", out _).Should().BeTrue();

        // camelCase C# names must NOT leak.
        root.TryGetProperty("jwksUri", out _).Should().BeFalse();
    }

    [Fact]
    public async Task OidcRoute_NoAuthHeader_IsReachable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var options = BuildOptions();

        using var host = await BuildHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── End-to-end discovery → JWKS chain ────────────────────────────────

    [Fact]
    public async Task DiscoveryThenJwks_OidcDocPointsAtJwks_AndBothResolveTogether()
    {
        // The discovery-then-JWKS chain: the OIDC discovery doc's jwks_uri points at the
        // JWKS route, and a client that reads the discovery doc then fetches the
        // referenced JWKS gets both seeded kids. This is the chain
        // ConfigurationManager<OpenIdConnectConfiguration> walks — see
        // OidcDiscoveryEndToEndTests for the real-ConfigurationManager proof.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = BuildOptions();
        var created = KcAppTestKit.SR_BaseInstant;
        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            crypto,
            options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created + Duration.FromHours(2));

        using var host = await BuildHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        // 1. Read the discovery doc.
        var discovery = await client.GetFromJsonAsync<GetOidcConfigurationOutput>(
            "/.well-known/openid-configuration");
        discovery!.JwksUri.Should().Be($"{_ISSUER}/.well-known/jwks.json");

        // 2. Fetch the JWKS at the path component of the advertised jwks_uri.
        var jwksPath = new Uri(discovery.JwksUri).AbsolutePath;
        var jwks = await client.GetFromJsonAsync<GetJwksOutput>(jwksPath);

        jwks!.Keys.Select(k => k.Kid).Should().Contain(activeKid);
    }

    // ── Host builder ─────────────────────────────────────────────────────

    private static KeyCustodianOptions BuildOptions()
    {
        var options = KcAppTestKit.BuildOptions();
        options.IssuerBaseUrl = _ISSUER;
        return options;
    }

    private static async Task<IHost> BuildHostAsync(
        KeyCustodianTestDbContext db, KeyCustodianOptions options)
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

                        // HandlerContext<> open-generic registration (BaseHandler
                        // cross-cutting context the real handlers take by ctor) +
                        // an IRequestContext it depends on (transport-supplied; a
                        // harmless anonymous route carries an empty request context).
                        services.AddD2Handler();
                        services.AddScoped<IRequestContext, MutableRequestContext>();

                        // Real façade + real handlers; the seeded in-memory DbContext
                        // backs GetJwks, the IssuerBaseUrl option backs GetOidc.
                        services.AddSingleton<IKeyCustodianDbContext>(db);
                        services.AddSingleton(Options.Create(options));
                        services.AddTransient<IGetJwksHandler, GetJwksHandler>();
                        services.AddTransient<
                            IGetOidcConfigurationHandler, GetOidcConfigurationHandler>();

                        // The generated façade ctor requires ISignHandler too; register it
                        // plus its dependencies (keyed root crypto + signing-domain policy)
                        // so IKeyCustodianApi resolves. The well-known routes never invoke
                        // sign — the façade is one transient that pulls in every handler.
                        services.AddTransient<ISignHandler, SignHandler>();
                        services.AddKeyedSingleton(
                            KeyCustodianRootKey.ROOT_SERVICE_KEY,
                            KcAppTestKit.BuildTestRootCrypto());
                        services.AddSingleton<ISigningDomainAuthorityPolicy>(
                            new OptionsSigningDomainAuthorityPolicy(
                                Options.Create(new SigningDomainAuthorityOptions())));

                        // The façade ctor also requires IGetKeyringHandler; register it plus
                        // its keyring-domain policy (deny-all) so IKeyCustodianApi resolves.
                        services.AddTransient<IGetKeyringHandler, GetKeyringHandler>();
                        services.AddSingleton<IKeyringDomainAuthorityPolicy>(
                            new OptionsKeyringDomainAuthorityPolicy(
                                Options.Create(new KeyringDomainAuthorityOptions())));

                        services.AddTransient<IKeyCustodianApi, KeyCustodianApi>();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGetJwksRoute();
                            endpoints.MapGetOidcConfigurationRoute();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }
}
