// -----------------------------------------------------------------------
// <copyright file="AddD2EdgeHostDiIsolationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.Host;

using D2.Edge.Api.Composition;
using D2.Edge.Api.Kestrel;
using D2.Edge.Api.Outbound;
using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.KeyCustodian.Client.Facade;
using D2.Edge.KeyCustodian.Client.Signing;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Shared.AspNetCore.Mtls;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Abstractions.Sessions;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Caching;
using D2.Shared.Caching.Distributed.Redis;
using D2.Shared.Caching.Tiered;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Connection;
using D2.Shared.Utilities.Configuration;
using D2.Shared.WorkloadIdentity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// DI isolation for <see cref="EdgeHostServiceCollectionExtensions.AddD2EdgeHost"/>:
/// load-bearing seams resolve; JWT minter is structurally absent; Redis/PG forms
/// are parsed; missing required config fails loud. Does not start hosted services
/// (outbound leaf refresh issues at host start).
/// </summary>
[Trait("Category", "Unit")]
public sealed class AddD2EdgeHostDiIsolationTests : IDisposable
{
    private readonly EdgeHostTestKit r_kit = new();

    public void Dispose() => r_kit.Dispose();

    [Fact]
    public void AddD2EdgeHost_ResolvesLoadBearingSeams_WithoutStartingHostedServices()
    {
        using var sp = BuildProvider();

        sp.GetRequiredService<IWorkloadCertificateIssuer>()
            .Should().BeOfType<PoCCsrSigningWorkloadCertificateIssuer>();

        sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value.Enabled
            .Should().BeTrue();

        sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value.AllowedWorkloads
            .Should().ContainSingle().Which.Should().Be("audit");

        sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value.TrustAnchorsProvider
            .Should().NotBeNull();

        var serviceId = sp.GetRequiredService<IOptions<D2WorkloadIdentityOptions>>()
            .Value.ServiceId;

        serviceId.Should().Be(EdgeHostIdentity.SERVICE_ID);
        serviceId.Should().Be("edge");

        var spiffe = SpiffeWorkloadIdentity.Create(EdgeHostIdentity.SERVICE_ID);
        spiffe.Success.Should().BeTrue();
        spiffe.Data!.Uri.Should().Be("spiffe://d2.internal/workload/edge");

        WorkloadIdentity.FromTrusted(EdgeHostIdentity.SERVICE_ID).Uri
            .Should().Be(spiffe.Data.Uri);

        sp.GetRequiredService<IOptions<AuthOptions>>().Value.Issuer
            .Should().Be(new Uri(EdgeHostTestKit.DEFAULT_ISSUER));

        sp.GetRequiredService<IOptions<AuthOptions>>().Value.Audience
            .Should().Be(WellKnownAudiences.D2_INTERNAL_AUDIENCE);

        sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value.ConnectionString
            .Should().Be(
                ConnectionStringHelper.ParseRedisUri(EdgeHostTestKit.REDIS_URL));

        sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value.ConnectionString
            .Should().NotStartWith("redis://");

        sp.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value.ConnectionString
            .Should().Be(
                ConnectionStringHelper.ParsePostgresUri(
                    EdgeHostTestKit.KC_DATABASE_URL));

        sp.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value.ConnectionString
            .Should().NotStartWith("postgresql://");

        sp.GetRequiredService<IOptions<RabbitMqConnectionOptions>>().Value.ConnectionUri
            .Should().Be(EdgeHostTestKit.RABBITMQ_URL);

        // Façade needs request-scoped IRequestContext — descriptor presence only.
        var descriptors = new ServiceCollection();
        descriptors.AddD2EdgeHost(r_kit.BuildConfiguration());
        descriptors.Any(d => d.ServiceType == typeof(IKeyCustodianApi)).Should().BeTrue();

        // CA caps — scoped GetRequiredService (DbContext-scoped graph).
        using (var scope = sp.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ICaLeafSigningCapability>()
                .Should().NotBeNull();

            scope.ServiceProvider.GetRequiredService<ICaRootSigningCapability>()
                .Should().NotBeNull();
        }

        // ITieredCache is singleton over Redis IConnectionMultiplexer (Connect on
        // first resolve). Pure isolation pins registration + ImplementationType
        // without Connect — first-use / host-start needs a live REDIS_URL.
        var tiered = descriptors.Single(d => d.ServiceType == typeof(ITieredCache));
        tiered.ImplementationType.Should().Be<DefaultTieredCache>();
        tiered.Lifetime.Should().Be(ServiceLifetime.Singleton);

        // Outbound hosted refresh is registered (IssueAsync at host start) — do not
        // GetServices<IHostedService>() (constructs RMQ/Redis hosted services).
        descriptors.Any(d =>
                d.ServiceType == typeof(IHostedService)
                && (d.ImplementationType?.Name.Contains(
                        "WorkloadLeafRefresh", StringComparison.Ordinal) == true
                    || d.ImplementationFactory is not null
                    || d.ImplementationInstance?.GetType().Name.Contains(
                        "WorkloadLeafRefresh", StringComparison.Ordinal) == true))
            .Should().BeTrue("WorkloadLeafRefreshHostedService must be registered");

        // Three-bind Kestrel configure is registered (descriptor — avoid binding).
        descriptors.Any(d =>
                d.ServiceType == typeof(IConfigureOptions<KestrelServerOptions>)
                && d.ImplementationType == typeof(EdgeHttpsRoleKestrelConfigure))
            .Should().BeTrue();
    }

    [Fact]
    public void AddD2EdgeHost_JwtSigningCapability_IsStructurallyAbsent()
    {
        using var sp = BuildProvider();

        sp.GetService<IJwtSigningCapability>().Should().BeNull();

        // Descriptor presence must also be empty (not merely unresolved).
        var descriptors = new ServiceCollection();
        descriptors.AddD2EdgeHost(r_kit.BuildConfiguration());

        descriptors.Any(d => d.ServiceType == typeof(IJwtSigningCapability))
            .Should().BeFalse();
    }

    [Fact]
    public void AddD2EdgeHost_SeamInventory_RegistersAuthCacheOriginAndMessaging()
    {
        // Test bar B residual seams — descriptor + pure resolve where safe;
        // Redis/RMQ first-use documented (no Connect in isolation).
        var descriptors = new ServiceCollection();
        descriptors.AddD2EdgeHost(r_kit.BuildConfiguration());

        descriptors.Any(d => d.ServiceType == typeof(IMessageBus))
            .Should().BeTrue("RMQ IMessageBus is registered; Connect is first-use");

        descriptors.Any(d => d.ServiceType == typeof(ISessionLivenessTracker))
            .Should().BeTrue("AuthConfigure ON registers session liveness");

        descriptors.Any(d => d.ServiceType == typeof(IJwksProvider))
            .Should().BeTrue("AuthConfigure ON registers JWKS provider");

        // JwtValidator is internal to D2.Shared.Auth — pin by type name (not public).
        descriptors.Any(d =>
                d.ServiceType.Name is "JwtValidator" or "ClaimsToContextMapper"
                || d.ImplementationType?.Name is "JwtValidator" or "ClaimsToContextMapper")
            .Should().BeTrue("AuthConfigure ON registers JWT validation types");

        descriptors.Any(d => d.ServiceType == typeof(IDistributedCache))
            .Should().BeTrue("Redis distributed cache registration present");

        // Pure resolve of public auth options + JWKS (no Redis Connect).
        using var sp = BuildProvider();
        sp.GetRequiredService<IJwksProvider>().Should().NotBeNull();
        sp.GetRequiredService<IOptions<AuthOptions>>().Value.Issuer.Should().NotBeNull();

        // RequestOrigin Edge + Grpc establishment ServiceId pin.
        sp.GetRequiredService<IOptions<D2WorkloadIdentityOptions>>().Value.ServiceId
            .Should().Be(EdgeHostIdentity.SERVICE_ID);

        // Source pin: composition registers both establishment extensions.
        var originPath = EdgeHostTestKit.ResolveEdgeApiSourceFile(
            "Composition", "EdgeHostServiceCollectionExtensions.cs");
        var originSource = File.ReadAllText(originPath);
        originSource.Should().Contain("AddD2RequestOriginEdge");
        originSource.Should().Contain("AddD2RequestOriginGrpc");
    }

    [Fact]
    public void AddD2EdgeHost_OutboundDualFactor_IsRegistered()
    {
        // AuditBridge no-cert hop Evidence = outbound dual-factor DI + channel https
        // (not MutualTlsSigner inbound harness).
        var descriptors = new ServiceCollection();
        descriptors.AddD2EdgeHost(r_kit.BuildConfiguration());

        descriptors.Any(d =>
                d.ServiceType == typeof(IWorkloadCertificateIssuer)
                || d.ImplementationType == typeof(PoCCsrSigningWorkloadCertificateIssuer))
            .Should().BeTrue();

        // Source pin: composition calls both outbound dual-factor extensions.
        var path = EdgeHostTestKit.ResolveEdgeApiSourceFile(
            "Composition", "EdgeHostServiceCollectionExtensions.cs");
        File.Exists(path).Should().BeTrue();
        var source = File.ReadAllText(path);

        source.Should().Contain("AddD2WorkloadCertificateOutbound()");
        source.Should().Contain("AddD2ForwardedJwtOutbound()");
    }

    [Fact]
    public void AddD2EdgeHost_AuthIssuer_IsNotMtlsPort()
    {
        using var sp = BuildProvider();

        var issuer = sp.GetRequiredService<IOptions<AuthOptions>>().Value.Issuer!;

        issuer.Port.Should().Be(EdgeHttpsRolePolicies.IssuerHttpsPort);
        issuer.Port.Should().NotBe(EdgeHttpsRolePolicies.MtlsHttpsPort);
        issuer.Scheme.Should().Be("https");
    }

    [Fact]
    public void AddD2EdgeHost_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();
        var act = () => services.AddD2EdgeHost(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2EdgeHost_MissingKeyCustodianDatabaseUrl_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?> { ["KEYCUSTODIAN_DATABASE_URL"] = null });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*KEYCUSTODIAN_DATABASE_URL*");
    }

    [Fact]
    public void AddD2EdgeHost_MissingRedisUrl_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?> { ["REDIS_URL"] = null });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*REDIS_URL*");
    }

    [Fact]
    public void AddD2EdgeHost_MissingRabbitMqUrl_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?> { ["RABBITMQ_URL"] = null });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RABBITMQ_URL*");
    }

    [Fact]
    public void AddD2EdgeHost_MissingIssuerBaseUrl_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["KEYCUSTODIAN_APP:IssuerBaseUrl"] = null,
                ["KEYCUSTODIAN_APP:ISSUERBASEURL"] = null,
            });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IssuerBaseUrl*");
    }

    [Fact]
    public void AddD2EdgeHost_HttpIssuer_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["KEYCUSTODIAN_APP:IssuerBaseUrl"] = "http://d2-edge:8443",
            });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*https*");
    }

    [Fact]
    public void AddD2EdgeHost_MtlsPortIssuer_ThrowsAtRegistration()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["KEYCUSTODIAN_APP:IssuerBaseUrl"] = "https://d2-edge:9443",
            });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*9443*");
    }

    [Fact]
    public void AddD2EdgeHost_BlankRedisUrl_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?> { ["REDIS_URL"] = "   " });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*REDIS_URL*");
    }

    [Fact]
    public void AddD2EdgeHost_BlankRabbitMqUrl_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?> { ["RABBITMQ_URL"] = "   " });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*RABBITMQ_URL*");
    }

    [Fact]
    public void AddD2EdgeHost_BlankDatabaseUrl_Throws()
    {
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration(
            new Dictionary<string, string?> { ["KEYCUSTODIAN_DATABASE_URL"] = "   " });

        var act = () => services.AddD2EdgeHost(config);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*KEYCUSTODIAN_DATABASE_URL*");
    }

    [Fact]
    public void AddD2EdgeHost_DoubleRegister_DoesNotThrowAtRegistration()
    {
        // Double-register is ServiceCollection-legal (duplicate descriptors);
        // isolation pins that registration itself does not throw.
        var services = new ServiceCollection();
        var config = r_kit.BuildConfiguration();

        services.AddD2EdgeHost(config);
        var act = () => services.AddD2EdgeHost(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void ParsePostgresUri_OnHostConfig_ProducesAdoNetForm()
    {
        var parsed = ConnectionStringHelper.ParsePostgresUri(
            EdgeHostTestKit.KC_DATABASE_URL);

        parsed.Should().Contain("Host=localhost");
        parsed.Should().Contain("Database=d2-keycustodian");
        parsed.Should().NotStartWith("postgresql://");
    }

    [Fact]
    public void ParseRedisUri_OnHostConfig_ProducesStackExchangeForm()
    {
        var parsed = ConnectionStringHelper.ParseRedisUri(EdgeHostTestKit.REDIS_URL);

        parsed.Should().Be("localhost:6379");
        parsed.Should().NotStartWith("redis://");
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddD2EdgeHost(r_kit.BuildConfiguration());

        return services.BuildServiceProvider();
    }
}
