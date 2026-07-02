// -----------------------------------------------------------------------
// <copyright file="OidcDiscoveryEndToEndTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.WellKnown;

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
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NodaTime;

/// <summary>
/// End-to-end proof that the generated well-known surface interoperates with the
/// .NET OIDC discovery stack — the exact
/// <see cref="ConfigurationManager{T}"/><c>&lt;OpenIdConnectConfiguration&gt;</c>
/// the shared <c>HttpJwksProvider</c> wraps. A real ConfigurationManager pointed
/// at the TestServer reads <c>/.well-known/openid-configuration</c>, extracts
/// <c>jwks_uri</c>, fetches the referenced JWKS, and surfaces both seeded signing
/// keys. This pins that the discovery doc's shape (snake_case keys + absolute
/// jwks_uri) is consumable by the canonical OIDC client.
/// </summary>
public sealed class OidcDiscoveryEndToEndTests
{
    // TestServer's default base address sentinel — used as the issuer so the
    // discovery doc's absolute jwks_uri resolves back through the TestServer
    // backchannel below.
    private const string _ISSUER = "http://localhost";

    [Fact]
    public async Task ConfigurationManager_DiscoversJwksUri_AndFetchesSigningKeys()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = KcAppTestKit.BuildOptions();
        options.IssuerBaseUrl = _ISSUER;
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
        var server = host.GetTestServer();

        // The real .NET OIDC discovery client: ConfigurationManager fetches
        // {issuer}/.well-known/openid-configuration, then the JWKS at jwks_uri.
        // The TestServer's HttpMessageHandler is the backchannel so the absolute
        // http://localhost/* URLs resolve to the in-memory host.
        var docRetriever = new HttpDocumentRetriever(server.CreateClient())
        {
            RequireHttps = false,
        };
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{_ISSUER}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            docRetriever);

        var config = await configManager.GetConfigurationAsync(CancellationToken.None);

        config.Issuer.Should().Be(_ISSUER);
        config.JwksUri.Should().Be($"{_ISSUER}/.well-known/jwks.json");
        config.IdTokenSigningAlgValuesSupported.Should().Contain("RS256");

        // ConfigurationManager populated SigningKeys by fetching the JWKS the
        // discovery doc advertised — both seeded kids must be present.
        config.SigningKeys.Select(k => k.KeyId)
            .Should().Contain([activeKid, retiringKid]);
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
                        services.AddD2Handler();
                        services.AddScoped<IRequestContext, MutableRequestContext>();
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
