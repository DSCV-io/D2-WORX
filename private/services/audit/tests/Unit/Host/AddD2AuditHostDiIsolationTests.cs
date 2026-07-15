// -----------------------------------------------------------------------
// <copyright file="AddD2AuditHostDiIsolationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Audit.Tests.Unit.Host;

using DcsvIo.D2.AspNetCore.Mtls;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Caching.Tiered;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Private.Audit.Api.Composition;
using DcsvIo.D2.Private.Audit.Api.Kestrel;
using DcsvIo.D2.Private.Audit.Api.Mtls;
using DcsvIo.D2.Private.Audit.App.Application;
using DcsvIo.D2.Private.Audit.App.Application.Handlers.Queries.PingAudit;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing;
using DcsvIo.D2.Spiffe;
using DcsvIo.D2.Utilities.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// DI isolation for <see cref="AuditHostServiceCollectionExtensions.AddD2AuditHost"/>:
/// load-bearing seams resolve; JWT minter is structurally absent; Redis form
/// is parsed; missing required config fails loud.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AddD2AuditHostDiIsolationTests : IDisposable
{
    private readonly AuditHostTestKit r_kit = new();

    public void Dispose() => r_kit.Dispose();

    [Fact]
    public void AddD2AuditHost_ResolvesTieredCacheAndOriginInterceptor()
    {
        var descriptors = new ServiceCollection();
        descriptors.AddD2AuditHost(r_kit.BuildConfiguration());

        var tiered = descriptors.Single(d => d.ServiceType == typeof(ITieredCache));
        tiered.ImplementationType.Should().Be<DefaultTieredCache>();
        tiered.Lifetime.Should().Be(ServiceLifetime.Singleton);

        // Establishment ServiceId is registered via WorkloadIdentity options.
        using var sp = descriptors.BuildServiceProvider();
        var serviceId = sp.GetRequiredService<IOptions<D2WorkloadIdentityOptions>>()
            .Value.ServiceId;

        serviceId.Should().Be(AuditHostIdentity.SERVICE_ID);
        serviceId.Should().Be("audit");

        var spiffe = SpiffeWorkloadIdentity.Create(AuditHostIdentity.SERVICE_ID);
        spiffe.Success.Should().BeTrue();
        spiffe.Data!.Uri.Should().Be("spiffe://d2.internal/workload/audit");
    }

    [Fact]
    public void AddD2AuditHost_AuthConfigureOn_RegistersRedisTieredAndLiveness()
    {
        using var sp = BuildProvider();

        sp.GetRequiredService<IOptions<AuthOptions>>().Value.Issuer
            .Should().Be(new Uri(AuditHostTestKit.DEFAULT_ISSUER));

        sp.GetRequiredService<IOptions<AuthOptions>>().Value.Audience
            .Should().Be(WellKnownAudiences.D2_INTERNAL_AUDIENCE);

        sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value.ConnectionString
            .Should().Be(
                ConnectionStringHelper.ParseRedisUri(AuditHostTestKit.REDIS_URL));

        sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value.ConnectionString
            .Should().NotStartWith("redis://");

        var descriptors = new ServiceCollection();
        descriptors.AddD2AuditHost(r_kit.BuildConfiguration());
        descriptors.Any(d => d.ServiceType == typeof(ITieredCache)).Should().BeTrue();

        // Test bar B residual: session liveness + JWKS + JWT validation when auth on.
        descriptors.Any(d => d.ServiceType == typeof(ISessionLivenessTracker))
            .Should().BeTrue();
        descriptors.Any(d => d.ServiceType == typeof(IJwksProvider))
            .Should().BeTrue();

        // JwtValidator is internal to DcsvIo.D2.Auth — pin by type name (not public).
        descriptors.Any(d =>
                d.ServiceType.Name is "JwtValidator" or "ClaimsToContextMapper"
                || d.ImplementationType?.Name is "JwtValidator" or "ClaimsToContextMapper")
            .Should().BeTrue();

        descriptors.Any(d => d.ServiceType == typeof(IDistributedCache))
            .Should().BeTrue();

        // Pure resolve of public auth seams (no Redis Connect).
        var jwks = sp.GetRequiredService<IJwksProvider>();
        jwks.Should().NotBeNull();

        // Audit is a remote consumer — keeps HttpJwksProvider (not Edge in-process).
        jwks.GetType().Name.Should().Be("HttpJwksProvider");

        // OIDC client trusts the same public CA as mTLS TrustAnchors.
        sp.GetRequiredService<IOptions<AuthOptions>>().Value.Jwks.TrustedRootCertificatePath
            .Should().Be(r_kit.TrustAnchorPath);

        sp.GetRequiredService<IOptions<D2WorkloadIdentityOptions>>().Value.ServiceId
            .Should().Be(AuditHostIdentity.SERVICE_ID);
    }

    [Fact]
    public void AddD2AuditHost_AllowedWorkloads_ContainsEdge()
    {
        using var sp = BuildProvider();

        sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value.Enabled
            .Should().BeTrue();

        sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value.AllowedWorkloads
            .Should().ContainSingle().Which.Should().Be("edge");

        sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value.TrustAnchorsProvider
            .Should().NotBeNull();
    }

    [Fact]
    public void AddD2AuditHost_DoesNotRegisterIJwtSigningCapability()
    {
        using var sp = BuildProvider();

        // Structural deny — dual-pin mirrors Edge: GetService null + typeof absent.
        sp.GetService<IJwtSigningCapability>().Should().BeNull();

        var descriptors = new ServiceCollection();
        descriptors.AddD2AuditHost(r_kit.BuildConfiguration());

        descriptors.Any(d => d.ServiceType == typeof(IJwtSigningCapability))
            .Should().BeFalse();
    }

    [Fact]
    public void AddD2AuditHost_RegistersDualBindKestrelConfigure()
    {
        var descriptors = new ServiceCollection();
        descriptors.AddD2AuditHost(r_kit.BuildConfiguration());

        descriptors.Any(d =>
                d.ServiceType == typeof(IConfigureOptions<KestrelServerOptions>)
                && d.ImplementationType == typeof(AuditHttpsRoleKestrelConfigure))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2AuditHost_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddD2AuditHost(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2AuditHost_MissingRedisUrl_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?> { ["REDIS_URL"] = null });

        var act = () => services.AddD2AuditHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*REDIS_URL*");
    }

    [Fact]
    public void AddD2AuditHost_MissingIssuer_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["KEYCUSTODIAN_APP:IssuerBaseUrl"] = null,
                ["KEYCUSTODIAN_APP:ISSUERBASEURL"] = null,
            });

        var act = () => services.AddD2AuditHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IssuerBaseUrl*");
    }

    [Fact]
    public void AddD2AuditHost_IssuerMtlsPort_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["KEYCUSTODIAN_APP:IssuerBaseUrl"] = "https://d2-edge:9443",
            });

        var act = () => services.AddD2AuditHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*9443*");
    }

    [Fact]
    public void AddD2AuditHost_MissingTrustAnchor_ThrowsWhenProviderBuilt()
    {
        // MutualTlsConfigure may defer TrustAnchorsProvider construction until
        // options apply — pin the load path itself (same FromConfiguration used
        // by AddD2AuditHost) fails loud without a path.
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?>
            {
                [LoadPublicCaAnchors.TRUST_ANCHOR_PATH_KEY] = null,
            });

        var act = () => LoadPublicCaAnchors.FromConfiguration(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{LoadPublicCaAnchors.TRUST_ANCHOR_PATH_KEY}*");
    }

    [Fact]
    public void AddD2AuditApp_RegistersIPingAuditHandlerDescriptor()
    {
        var services = new ServiceCollection();
        services.AddD2AuditApp();

        services.Any(d => d.ServiceType == typeof(IPingAuditHandler))
            .Should().BeTrue();
        services.Single(d => d.ServiceType == typeof(IPingAuditHandler))
            .ImplementationType.Should().Be<PingAuditHandler>();
    }

    [Fact]
    public void AddD2AuditApp_ResolvesIPingAuditHandler()
    {
        // Descriptor presence ≠ resolvability (§1.3 / §1.31). Scaffold the
        // seams HandlerContext needs, then GetRequiredService the App seam.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2Handler();
        services.AddSingleton<IRequestContext>(_ => new MutableRequestContext());
        services.AddD2AuditApp();

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IPingAuditHandler>()
            .Should().BeOfType<PingAuditHandler>();
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddD2AuditHost(r_kit.BuildConfiguration());

        return services.BuildServiceProvider();
    }
}
