// -----------------------------------------------------------------------
// <copyright file="MapD2EdgeEndpointsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.Host;

using System.Net;
using D2.Edge.Api.Composition;
using D2.Edge.Api.Grpc.KeyCustodian;
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
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Map surface pins: health + well-known + six KC gRPC Maps with
/// <c>Scopes.Internal.Kc.*</c>; no free-string <c>RequireAnyScope("</c> in Edge.Api Map code.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MapD2EdgeEndpointsTests
{
    public static TheoryData<string> ProductionKcGrpcServiceFileNames { get; } = new()
    {
        "KeyCustodianSignerService.g.cs",
        "KeyCustodianKeyringService.g.cs",
        "KeyCustodianCertificateAuthorityService.g.cs",
        "KeyCustodianCaCertificateService.g.cs",
        "KeyCustodianSealPublicKeyService.g.cs",
        "KeyCustodianOwnSealPrivateKeyService.g.cs",
    };

    public static TheoryData<string> ProductionKcTransportMapperFileNames { get; } = new()
    {
        "SignTransportMappers.g.cs",
        "GetKeyringTransportMappers.g.cs",
        "IssueLeafTransportMappers.g.cs",
        "GetCaCertificateTransportMappers.g.cs",
        "GetOrLazyProvisionSealPublicKeyTransportMappers.g.cs",
        "GetOrLazyProvisionOwnSealPrivateKeyTransportMappers.g.cs",
    };

    [Fact]
    public void MapD2EdgeEndpoints_NullEndpoints_Throws()
    {
        IEndpointRouteBuilder endpoints = null!;
        var act = () => endpoints.MapD2EdgeEndpoints();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EdgeApi_MapCode_HasNoFreeStringRequireAnyScope()
    {
        var edgeApiRoot = EdgeHostTestKit.ResolveEdgeApiSourceRoot();
        Directory.Exists(edgeApiRoot)
            .Should().BeTrue($"Edge.Api source root at {edgeApiRoot}");

        var binSeg = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        var objSeg = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";

        var offenders = Directory
            .EnumerateFiles(edgeApiRoot, "*.cs", SearchOption.AllDirectories)
            .Where(p =>
                !p.Contains(objSeg, StringComparison.Ordinal)
                && !p.Contains(binSeg, StringComparison.Ordinal))
            .SelectMany(p =>
                File.ReadAllLines(p).Select((line, i) => (p, i: i + 1, line)))
            .Where(x =>
                x.line.Contains("RequireAnyScope(\"", StringComparison.Ordinal)
                || x.line.Contains("RequireAllScopes(\"", StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "Map code must use Scopes.* constants, not free-string scope literals");
    }

    [Fact]
    public void ProductionWellKnownTypes_LiveInEdgeApiAssembly()
    {
        typeof(GetJwksRouteRegistration).Assembly.GetName().Name
            .Should().Be("D2.Edge.Api");

        typeof(GetOidcConfigurationRouteRegistration).Assembly.GetName().Name
            .Should().Be("D2.Edge.Api");
    }

    [Fact]
    public void ProductionKcGrpcServiceTypes_LiveInEdgeApiAssembly()
    {
        typeof(KeyCustodianSignerService).Assembly.GetName().Name
            .Should().Be("D2.Edge.Api");

        typeof(KeyCustodianKeyringService).Assembly.GetName().Name
            .Should().Be("D2.Edge.Api");

        typeof(KeyCustodianCertificateAuthorityService).Assembly.GetName().Name
            .Should().Be("D2.Edge.Api");

        typeof(KeyCustodianCaCertificateService).Assembly.GetName().Name
            .Should().Be("D2.Edge.Api");

        typeof(KeyCustodianSealPublicKeyService).Assembly.GetName().Name
            .Should().Be("D2.Edge.Api");

        typeof(KeyCustodianOwnSealPrivateKeyService).Assembly.GetName().Name
            .Should().Be("D2.Edge.Api");
    }

    [Theory]
    [MemberData(nameof(ProductionKcGrpcServiceFileNames))]
    public void ProductionKcGrpcServices_AreNotDualHomedUnderTestsGenerated(
        string serviceFileName)
    {
        var testsRoot = EdgeHostTestKit.ResolveEdgeTestsSourceRoot();

        var dualHome = Path.Combine(
            testsRoot,
            "Unit",
            "KeyCustodian",
            "TypeSpecGrpc",
            "Generated",
            serviceFileName);

        File.Exists(dualHome)
            .Should().BeFalse(
                "production thin services live only under Edge.Api/Grpc/KeyCustodian");
    }

    [Theory]
    [MemberData(nameof(ProductionKcTransportMapperFileNames))]
    public void ProductionKcTransportMappers_AreNotDualHomedUnderTestsGenerated(
        string mapperFileName)
    {
        var testsRoot = EdgeHostTestKit.ResolveEdgeTestsSourceRoot();

        var dualHome = Path.Combine(
            testsRoot,
            "Unit",
            "KeyCustodian",
            "TypeSpecGrpc",
            "Generated",
            mapperFileName);

        File.Exists(dualHome)
            .Should().BeFalse(
                "production transport mappers live only under Edge.Api/Mappers/KeyCustodian");
    }

    [Fact]
    public async Task MapD2EdgeEndpoints_WellKnownJwks_EmptyStore_Returns503()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var options = KcAppTestKit.BuildOptions();
        options.IssuerBaseUrl = "https://d2-edge:8443";

        using var host = await BuildMapHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/.well-known/jwks.json");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task MapD2EdgeEndpoints_Oidc_ReturnsMappedRoute()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var options = KcAppTestKit.BuildOptions();
        options.IssuerBaseUrl = "https://d2-edge:8443";

        using var host = await BuildMapHostAsync(db, options);
        var client = host.GetTestServer().CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public void MapD2EdgeEndpoints_Source_MapsWellKnownAndAllSixKcGrpc()
    {
        var path = EdgeHostTestKit.ResolveEdgeApiSourceFile(
            "Composition", "EdgeEndpointRouteBuilderExtensions.cs");

        File.Exists(path).Should().BeTrue();
        var source = File.ReadAllText(path);

        source.Should().Contain("MapGetJwksRoute");
        source.Should().Contain("MapGetOidcConfigurationRoute");
        source.Should().Contain("MapD2DefaultEndpoints");

        source.Should().Contain("MapGrpcService<KeyCustodianSignerService>");
        source.Should().Contain("MapGrpcService<KeyCustodianKeyringService>");
        source.Should().Contain("MapGrpcService<KeyCustodianCertificateAuthorityService>");
        source.Should().Contain("MapGrpcService<KeyCustodianCaCertificateService>");
        source.Should().Contain("MapGrpcService<KeyCustodianSealPublicKeyService>");
        source.Should().Contain("MapGrpcService<KeyCustodianOwnSealPrivateKeyService>");

        source.Should().Contain("Scopes.Internal.Kc.Sign");
        source.Should().Contain("Scopes.Internal.Kc.Keyring");
        source.Should().Contain("Scopes.Internal.Kc.Issue");
        source.Should().Contain("Scopes.Internal.Kc.Cacert");
        source.Should().Contain("Scopes.Internal.Kc.Seal.Encrypt");
        source.Should().Contain("Scopes.Internal.Kc.Seal.Open");

        source.Should().NotContain("Step 3");
        source.Should().NotContain("Step 2");
        source.Should().NotContain("Step 4");
    }

    [Fact]
    public void EdgeApiTests_Csproj_HasNoProductionKeyCustodianProtobufIncludes()
    {
        var testsRoot = EdgeHostTestKit.ResolveEdgeTestsSourceRoot();
        var csproj = Path.Combine(testsRoot, "D2.Edge.Tests.csproj");
        File.Exists(csproj).Should().BeTrue();

        var text = File.ReadAllText(csproj);
        text.Should().NotContain("key_custodian_signer_sign.g.proto");

        text.Should().NotContain(
            "key_custodian_certificate_authority_issue_workload_certificate.g.proto");

        text.Should().NotContain(
            "key_custodian_ca_certificate_get_ca_certificate.g.proto");

        text.Should().NotContain("key_custodian_keyring_get_keyring.g.proto");
        text.Should().NotContain("key_custodian_seal_public_key");
        text.Should().NotContain("key_custodian_own_seal_private_key");
    }

    private static async Task<IHost> BuildMapHostAsync(
        KeyCustodianTestDbContext db, KeyCustodianOptions options)
    {
        // Minimal TestServer host mapping production MapD2EdgeEndpoints (not only
        // MapGet* bypass). Avoids full AddD2EdgeHost (Redis/RMQ/hosted refresh).
        // AddGrpc is required once Map registers MapGrpcService×6.
        Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", "true");

        return await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddHealthChecks();
                        services.AddGrpc();
                        services.AddD2Handler();
                        services.AddScoped<IRequestContext, MutableRequestContext>();

                        services.AddSingleton<IKeyCustodianDbContext>(db);
                        services.AddSingleton(Options.Create(options));
                        services.AddTransient<IGetJwksHandler, GetJwksHandler>();

                        services.AddTransient<
                            IGetOidcConfigurationHandler,
                            GetOidcConfigurationHandler>();

                        services.AddTransient<ISignHandler, SignHandler>();

                        services.AddKeyedSingleton(
                            KeyCustodianRootKey.ROOT_SERVICE_KEY,
                            KcAppTestKit.BuildTestRootCrypto());

                        services.AddSingleton<ISigningDomainAuthorityPolicy>(
                            new OptionsSigningDomainAuthorityPolicy(
                                Options.Create(new SigningDomainAuthorityOptions())));

                        services.AddTransient<IGetKeyringHandler, GetKeyringHandler>();

                        services.AddSingleton<IKeyringDomainAuthorityPolicy>(
                            new OptionsKeyringDomainAuthorityPolicy(
                                Options.Create(new KeyringDomainAuthorityOptions())));

                        services.AddD2CaLeafSigningCapability();

                        services.AddSingleton<IClock>(
                            new TestClock(KcAppTestKit.SR_BaseInstant));

                        services.AddSingleton(KcAppTestKit.NullClassifier());

                        services.AddTransient<
                            IIssueWorkloadCertificateHandler,
                            IssueWorkloadCertificateHandler>();

                        services.AddTransient<IIssueLeafHandler, IssueLeafHandler>();

                        services.AddTransient<
                            IGetCaCertificateHandler,
                            GetCaCertificateHandler>();

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
                            // Production Map (health/metrics + well-known + six KC gRPC).
                            endpoints.MapD2EdgeEndpoints();
                        });
                    });
            })
            .StartAsync();
    }
}
