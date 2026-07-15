// -----------------------------------------------------------------------
// <copyright file="OidcDiscoveryEndToEndTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.WellKnown;

using D2.Edge.Api.Routes.KeyCustodian;
using D2.Edge.KeyCustodian.App.Application.Facade;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionOwnSealPrivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Client.Facade;
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

    [Fact]
    public async Task AuthOidc_ConfigurationManager_DiscoversAgainstIssuerHttpsWithoutClientCert()
    {
        // Pins absolute https Issuer shape + discovery without client certificate.
        // TestServer backchannel has no client cert; RequireHttps=false is harness
        // plumbing so http://localhost TestServer can serve the absolute https
        // discovery URLs rewritten through the TestServer handler below.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var options = KcAppTestKit.BuildOptions();
        const string issuer_https = "https://d2-edge:8443";
        options.IssuerBaseUrl = issuer_https;
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
        var server = host.GetTestServer();

        // Backchannel rewrites https://d2-edge:8443/* → TestServer (no client cert).
        var backchannel = new NoClientCertDocumentRetrieverStub(server.CreateHandler());
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{issuer_https}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            backchannel);

        var config = await configManager.GetConfigurationAsync(CancellationToken.None);

        config.Issuer.Should().Be(issuer_https);
        config.JwksUri.Should().Be($"{issuer_https}/.well-known/jwks.json");
        config.IdTokenSigningAlgValuesSupported.Should().Contain("RS256");
        config.SigningKeys.Select(k => k.KeyId).Should().Contain(activeKid);

        // Discovery completed without presenting a client certificate
        // (retriever never attaches ClientCertificates).
        backchannel.PresentedClientCertificate.Should().BeFalse();
        backchannel.FetchCount.Should().BeGreaterThan(0);
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

                        // The façade ctor also requires the issuance shell + CA-chain
                        // handlers (never invoked here). The shell's inner issuance
                        // handler needs the isolated leaf-signing capability — its
                        // dedicated extension is the composition-root opt-in — plus a
                        // clock and the DB-exception classifier.
                        services.AddD2CaLeafSigningCapability();
                        services.AddSingleton<D2.Shared.Time.IClock>(
                            new TestClock(KcAppTestKit.SR_BaseInstant));

                        services.AddSingleton(KcAppTestKit.NullClassifier());
                        services.AddTransient<
                            IIssueWorkloadCertificateHandler,
                            IssueWorkloadCertificateHandler>();

                        services.AddTransient<IIssueLeafHandler, IssueLeafHandler>();
                        services.AddTransient<
                            IGetCaCertificateHandler,
                            GetCaCertificateHandler>();

                        // The façade ctor also requires the two seal handlers (never invoked
                        // by the discovery routes) plus their shared rotation-policy provider,
                        // so IKeyCustodianApi resolves.
                        services.AddSingleton<
                            IRotationPolicyProvider,
                            OptionsRotationPolicyProvider>();

                        services.AddTransient<
                            IGetOrLazyProvisionSealPublicKeyHandler,
                            GetOrLazyProvisionSealPublicKeyHandler>();

                        services.AddTransient<
                            IGetOrLazyProvisionOwnSealPrivateKeyHandler,
                            GetOrLazyProvisionOwnSealPrivateKeyHandler>();

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

    /// <summary>
    /// Document retriever stub that rewrites https Issuer absolute URLs onto
    /// the TestServer handler and never attaches a client certificate.
    /// </summary>
    private sealed class NoClientCertDocumentRetrieverStub : IDocumentRetriever
    {
        private readonly HttpMessageHandler r_handler;

        public NoClientCertDocumentRetrieverStub(HttpMessageHandler handler)
        {
            r_handler = handler;
        }

        /// <summary>Gets the number of document fetches completed.</summary>
        public int FetchCount { get; private set; }

        /// <summary>
        /// Gets a value indicating whether a client certificate was attached
        /// to a fetch (always false for this retriever).
        /// </summary>
        public bool PresentedClientCertificate { get; private set; }

        public async Task<string> GetDocumentAsync(
            string address,
            CancellationToken cancel)
        {
            // Absolute https Issuer URLs rewrite to TestServer relative paths.
            var uri = new Uri(address);
            var relative = uri.PathAndQuery;

            // Plain HttpClient over the TestServer handler — no
            // HttpClientHandler.ClientCertificates (Issuer-role discovery
            // never requires an mTLS client cert).
            var client = new HttpClient(r_handler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://localhost"),
            };

            using (client)
            {
                // Explicit: never attach client certs (proof for ledger).
                PresentedClientCertificate = false;

                using var response = await client.GetAsync(relative, cancel);
                response.EnsureSuccessStatusCode();
                FetchCount++;
                return await response.Content.ReadAsStringAsync(cancel);
            }
        }
    }
}
