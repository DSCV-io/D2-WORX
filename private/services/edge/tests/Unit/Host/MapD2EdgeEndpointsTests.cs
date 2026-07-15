// -----------------------------------------------------------------------
// <copyright file="MapD2EdgeEndpointsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.Host;

using System.Net;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Private.Audit.Client;
using DcsvIo.D2.Private.Audit.Client.Ping;
using DcsvIo.D2.Private.Edge.Api.Composition;
using DcsvIo.D2.Private.Edge.Api.Grpc.KeyCustodian;
using DcsvIo.D2.Private.Edge.Api.Routes.KeyCustodian;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Facade;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionOwnSealPrivateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Issuance;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Vault;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Facade;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Result;
using Microsoft.AspNetCore.Builder;
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
            .Should().Be("DcsvIo.D2.Private.Edge.Api");

        typeof(GetOidcConfigurationRouteRegistration).Assembly.GetName().Name
            .Should().Be("DcsvIo.D2.Private.Edge.Api");
    }

    [Fact]
    public void ProductionKcGrpcServiceTypes_LiveInEdgeApiAssembly()
    {
        typeof(KeyCustodianSignerService).Assembly.GetName().Name
            .Should().Be("DcsvIo.D2.Private.Edge.Api");

        typeof(KeyCustodianKeyringService).Assembly.GetName().Name
            .Should().Be("DcsvIo.D2.Private.Edge.Api");

        typeof(KeyCustodianCertificateAuthorityService).Assembly.GetName().Name
            .Should().Be("DcsvIo.D2.Private.Edge.Api");

        typeof(KeyCustodianCaCertificateService).Assembly.GetName().Name
            .Should().Be("DcsvIo.D2.Private.Edge.Api");

        typeof(KeyCustodianSealPublicKeyService).Assembly.GetName().Name
            .Should().Be("DcsvIo.D2.Private.Edge.Api");

        typeof(KeyCustodianOwnSealPrivateKeyService).Assembly.GetName().Name
            .Should().Be("DcsvIo.D2.Private.Edge.Api");
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

        source.Should().Contain("ProductScopes.Internal.Kc.Sign");
        source.Should().Contain("ProductScopes.Internal.Kc.Keyring");
        source.Should().Contain("ProductScopes.Internal.Kc.Issue");
        source.Should().Contain("ProductScopes.Internal.Kc.Cacert");
        source.Should().Contain("ProductScopes.Internal.Kc.Seal.Encrypt");
        source.Should().Contain("ProductScopes.Internal.Kc.Seal.Open");
        source.Should().Contain("MapAllAuditBridges()");

        // KC gRPC structural isolation to mTLS port via MapWhen.
        source.Should().Contain("MapWhen");
        source.Should().Contain("EdgeHttpsRolePolicies.MTLS_HTTPS_PORT");
        source.Should().Contain("Connection.LocalPort");

        source.Should().NotContain("Step 3");
        source.Should().NotContain("Step 2");
        source.Should().NotContain("Step 4");
    }

    [Fact]
    public void MapD2EdgeEndpoints_Source_KcGrpcMapsAreInsideMtlsMapWhenBranch()
    {
        var path = EdgeHostTestKit.ResolveEdgeApiSourceFile(
            "Composition", "EdgeEndpointRouteBuilderExtensions.cs");
        var source = File.ReadAllText(path);

        // Structural isolation: MapWhen on mTLS port is the only call path into
        // MapKeyCustodianGrpcServices (where MapGrpcServiceÃ—6 live). Public Map
        // body calls MapKeyCustodianGrpcMtlsOnly â†’ MapWhen â†’ MapKeyCustodianGrpcServices.
        source.Should().Contain("app.MapWhen(");
        source.Should().Contain("MapKeyCustodianGrpcMtlsOnly");
        source.Should().Contain("MapKeyCustodianGrpcServices(e)");

        var helperMethodIdx = source.IndexOf(
            "private static void MapKeyCustodianGrpcServices(", StringComparison.Ordinal);
        helperMethodIdx.Should().BeGreaterThanOrEqualTo(0);

        var firstKcMapIdx = source.IndexOf(
            "MapGrpcService<KeyCustodianSignerService>", StringComparison.Ordinal);
        firstKcMapIdx.Should().BeGreaterThan(
            helperMethodIdx,
            "KC MapGrpcService registrations live only in MapKeyCustodianGrpcServices");

        // Public Map body must not MapGrpcService before the mTLS helper call.
        var publicMapIdx = source.IndexOf(
            "public IEndpointRouteBuilder MapD2EdgeEndpoints()", StringComparison.Ordinal);
        var mtlsOnlyCallIdx = source.IndexOf(
            "MapKeyCustodianGrpcMtlsOnly(endpoints)", StringComparison.Ordinal);
        publicMapIdx.Should().BeGreaterThanOrEqualTo(0);
        mtlsOnlyCallIdx.Should().BeGreaterThan(publicMapIdx);

        var between = source[publicMapIdx..mtlsOnlyCallIdx];
        between.Should().NotContain(
            "MapGrpcService<",
            "public Map must not register gRPC before mTLS isolation helper");
    }

    [Fact]
    public void EdgeApiTests_Csproj_HasNoProductionKeyCustodianProtobufIncludes()
    {
        var testsRoot = EdgeHostTestKit.ResolveEdgeTestsSourceRoot();
        var csproj = Path.Combine(testsRoot, "DcsvIo.D2.Private.Edge.Tests.csproj");
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
        // Minimal WebApplication + TestServer mapping production MapD2EdgeEndpoints
        // (not only MapGet* bypass). Avoids full AddD2EdgeHost (Redis/RMQ/hosted
        // refresh). WebApplication is required so MapWhen mTLS isolation works
        // (IApplicationBuilder + IEndpointRouteBuilder). AddGrpc is required once
        // Map registers MapGrpcServiceÃ—6 on the mTLS branch.
        Environment.SetEnvironmentVariable("OTEL_SDK_DISABLED", "true");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.AddHealthChecks();
        builder.Services.AddGrpc();
        builder.Services.AddD2Handler();
        builder.Services.AddScoped<IRequestContext, MutableRequestContext>();

        builder.Services.AddSingleton<IKeyCustodianDbContext>(db);
        builder.Services.AddSingleton(Options.Create(options));
        builder.Services.AddTransient<IGetJwksHandler, GetJwksHandler>();

        builder.Services.AddTransient<
            IGetOidcConfigurationHandler,
            GetOidcConfigurationHandler>();

        builder.Services.AddTransient<ISignHandler, SignHandler>();

        builder.Services.AddKeyedSingleton(
            KeyCustodianRootKey.ROOT_SERVICE_KEY,
            KcAppTestKit.BuildTestRootCrypto());

        builder.Services.AddSingleton<ISigningDomainAuthorityPolicy>(
            new OptionsSigningDomainAuthorityPolicy(
                Options.Create(new SigningDomainAuthorityOptions())));

        builder.Services.AddTransient<IGetKeyringHandler, GetKeyringHandler>();

        builder.Services.AddSingleton<IKeyringDomainAuthorityPolicy>(
            new OptionsKeyringDomainAuthorityPolicy(
                Options.Create(new KeyringDomainAuthorityOptions())));

        builder.Services.AddD2CaLeafSigningCapability();

        builder.Services.AddSingleton<IClock>(
            new TestClock(KcAppTestKit.SR_BaseInstant));

        builder.Services.AddSingleton(KcAppTestKit.NullClassifier());

        builder.Services.AddTransient<
            IIssueWorkloadCertificateHandler,
            IssueWorkloadCertificateHandler>();

        builder.Services.AddTransient<IIssueLeafHandler, IssueLeafHandler>();

        builder.Services.AddTransient<
            IGetCaCertificateHandler,
            GetCaCertificateHandler>();

        builder.Services.AddSingleton<
            IRotationPolicyProvider,
            OptionsRotationPolicyProvider>();

        builder.Services.AddTransient<
            IGetOrLazyProvisionSealPublicKeyHandler,
            GetOrLazyProvisionSealPublicKeyHandler>();

        builder.Services.AddTransient<
            IGetOrLazyProvisionOwnSealPrivateKeyHandler,
            GetOrLazyProvisionOwnSealPrivateKeyHandler>();

        builder.Services.AddTransient<IKeyCustodianApi, KeyCustodianApi>();

        // MapAllAuditBridges requires IAuditGrpcClient DI (else ASP.NET
        // treats the client param as a body on GET and fails Map).
        // Â§1.32: stub returns typed ServiceUnavailable; replace-trigger
        // is live AddD2AuditGrpcClients on the Edge host.
        builder.Services.AddSingleton<IAuditGrpcClient, MapHostStubAuditGrpcClient>();

        var app = builder.Build();
        app.UseRouting();

        // Production Map (health/metrics + well-known + mTLS-only six KC gRPC + Audit bridges).
        app.MapD2EdgeEndpoints();

        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Minimal Â§1.32 double for Map host composition â€” asserts the PingAudit
    /// bridge can resolve <see cref="IAuditGrpcClient"/> without a live channel.
    /// </summary>
    private sealed class MapHostStubAuditGrpcClient : IAuditGrpcClient
    {
        public ValueTask<D2Result<PingAuditOutput?>> PingAuditAsync(
            PingAuditInput input,
            ResilientPipeline<string, PingAuditOutput?>? pipelineOverride = null,
            CancellationToken ct = default) =>
            ValueTask.FromResult(
                D2Result<PingAuditOutput?>.ServiceUnavailable());
    }
}
