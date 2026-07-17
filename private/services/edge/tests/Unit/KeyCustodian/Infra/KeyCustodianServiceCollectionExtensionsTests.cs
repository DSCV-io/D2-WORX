// -----------------------------------------------------------------------
// <copyright file="KeyCustodianServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Infra;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.CertificateAuthority;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Messaging;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Persistence;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Vault;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Messaging.RabbitMq;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Observability;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Persistence.Postgres;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Scheduling.Hosted;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Vault.File;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Composition tests for <see cref="KeyCustodianServiceCollectionExtensions"/>:
/// every registration resolves from a built provider (§1.3 — descriptor presence
/// is not resolution), the keyed root crypto resolves, the hosted-service
/// registration order puts the migrator before the rotation service, and the
/// options pipeline binds.
/// </summary>
public sealed class KeyCustodianServiceCollectionExtensionsTests : IDisposable
{
    private readonly string r_rootKeyDir;

    public KeyCustodianServiceCollectionExtensionsTests()
    {
        r_rootKeyDir = KcInfraTestKit.CreateRootKeyDir();
    }

    public void Dispose()
    {
        if (Directory.Exists(r_rootKeyDir))
            Directory.Delete(r_rootKeyDir, recursive: true);
    }

    [Fact]
    public void AddD2KeyCustodian_ResolvesEverySeam_FromBuiltProvider()
    {
        using var sp = BuildProvider();

        // Persistence seam.
        sp.GetRequiredService<KeyCustodianDbContext>().Should().NotBeNull();

        using (var scope = sp.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IKeyCustodianDbContext>()
                .Should().BeOfType<KeyCustodianDbContext>();
        }

        // Vault seam + keyed root crypto.
        sp.GetRequiredService<IRootKeyProvider>().Should().BeOfType<FileRootKeyProvider>();
        sp.GetRequiredService<ICaProvider>().Should().BeOfType<FileCaProvider>();
        sp.GetRequiredKeyedService<IPayloadCrypto>(KeyCustodianRootKey.ROOT_SERVICE_KEY)
            .Should().NotBeNull();

        // Messaging seam.
        sp.GetRequiredService<IKeyRotationAnnouncer>()
            .Should().BeOfType<RabbitMqKeyRotationAnnouncer>();

        // App handlers (chained AddD2KeyCustodianApp).
        using (var scope = sp.CreateScope())
        {
            var scoped = scope.ServiceProvider;
            scoped.GetRequiredService<IGenerateKeyHandler>().Should().NotBeNull();
            scoped.GetRequiredService<IActivateKeyHandler>().Should().NotBeNull();
            scoped.GetRequiredService<IRotateKeyHandler>().Should().NotBeNull();
            scoped.GetRequiredService<IRetireKeyHandler>().Should().NotBeNull();
            scoped.GetRequiredService<ICompromiseKeyHandler>().Should().NotBeNull();
            scoped.GetRequiredService<IRunDueRotationsHandler>().Should().NotBeNull();
            scoped.GetRequiredService<ISeedCertificateAuthorityHandler>().Should().NotBeNull();
            scoped.GetRequiredService<IGetJwksHandler>().Should().NotBeNull();
            scoped.GetRequiredService<IGetRotationPlanHandler>().Should().NotBeNull();
            scoped.GetRequiredService<IRotationPolicyProvider>().Should().NotBeNull();
        }
    }

    [Fact]
    public void AddD2KeyCustodian_KeyedRootKeyring_HasPrimaryKid()
    {
        using var sp = BuildProvider();

        var keyring = sp.GetRequiredKeyedService<PayloadCryptoKeyring>(
            KeyCustodianRootKey.ROOT_SERVICE_KEY);

        keyring.ActiveKid.Should().Be(RootKeyKids.PRIMARY_KID);
    }

    [Fact]
    public void AddD2KeyCustodian_RegistersMigratorThenSeederThenRotationService()
    {
        var services = NewServices();
        services.AddD2KeyCustodian(
            KcInfraTestKit.BuildConfiguration(r_rootKeyDir), KcInfraTestKit.FAKE_CONNECTION_STRING);

        // DI preserves registration order for IEnumerable<IHostedService>; resolving
        // the instances yields runtime types even for factory-registered services
        // (the migrator), unlike descriptor inspection. Order pins the StartAsync
        // sequence: migration applies, the CA is seeded, then rotation runs.
        using var sp = services.BuildServiceProvider();
        var hostedTypes = sp.GetServices<IHostedService>().Select(h => h.GetType()).ToList();

        var migratorIndex = hostedTypes.FindIndex(
            t => t == typeof(AdvisoryLockMigrator<KeyCustodianDbContext>));
        var seederIndex = hostedTypes.FindIndex(t => t == typeof(CaSeedingService));
        var rotationIndex = hostedTypes.FindIndex(t => t == typeof(KeyRotationService));

        migratorIndex.Should().BeGreaterThanOrEqualTo(0);
        seederIndex.Should().BeGreaterThanOrEqualTo(0);
        rotationIndex.Should().BeGreaterThanOrEqualTo(0);
        migratorIndex.Should().BeLessThan(
            seederIndex, "the migrator must start before the CA seeder");
        seederIndex.Should().BeLessThan(
            rotationIndex, "the CA seeder must start before the rotation service");
    }

    [Fact]
    public void AddD2KeyCustodian_RegistersAllThreeHostedServices()
    {
        var services = NewServices();
        services.AddD2KeyCustodian(
            KcInfraTestKit.BuildConfiguration(r_rootKeyDir), KcInfraTestKit.FAKE_CONNECTION_STRING);

        using var sp = services.BuildServiceProvider();
        var hosted = sp.GetServices<IHostedService>().ToList();

        hosted.Should().Contain(h => h is AdvisoryLockMigrator<KeyCustodianDbContext>);
        hosted.Should().Contain(h => h is CaSeedingService);
        hosted.Should().Contain(h => h is KeyRotationService);
    }

    [Fact]
    public void AddD2KeyCustodian_UnsetWorkloadIdentity_FailsOptionsValidation()
    {
        // Regression for the silent gap: the CA-seeding + key-rotation System workers inject
        // IOptions<D2WorkloadIdentityOptions>, so AddD2KeyCustodian must fail LOUD when the
        // host left ServiceId unset — not silently seed + rotate under an empty self-id.
        // Built WITHOUT NewServices() so no host ServiceId is configured.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2Handler();
        services.AddSingleton<IRequestContext>(_ => new MutableRequestContext());
        services.AddSingleton<IMessageBus, NoopMessageBus>();
        services.AddD2KeyCustodian(
            KcInfraTestKit.BuildConfiguration(r_rootKeyDir), KcInfraTestKit.FAKE_CONNECTION_STRING);

        using var sp = services.BuildServiceProvider();

        sp.Invoking(s => s.GetRequiredService<IOptions<D2WorkloadIdentityOptions>>().Value)
            .Should().Throw<OptionsValidationException>(
                because: "an unset host ServiceId must fail the KeyCustodian start gate");
    }

    [Fact]
    public void AddD2KeyCustodian_BindsInfraOptions_AndStampsConnectionString()
    {
        using var sp = BuildProvider();

        var infra = sp.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value;
        infra.RootKeyPath.Should().Be(r_rootKeyDir);
        infra.RotationCheckInterval.Should().Be(TimeSpan.FromMinutes(5));
        infra.ConnectionString.Should().Be(KcInfraTestKit.FAKE_CONNECTION_STRING);
    }

    [Fact]
    public void AddD2KeyCustodian_NullConfiguration_Throws()
    {
        var services = NewServices();

        var act = () => services.AddD2KeyCustodian(null!, KcInfraTestKit.FAKE_CONNECTION_STRING);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddD2KeyCustodian_BlankConnectionString_Throws(string connectionString)
    {
        var services = NewServices();

        var act = () => services.AddD2KeyCustodian(
            KcInfraTestKit.BuildConfiguration(r_rootKeyDir), connectionString);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddD2KeyCustodian_HealthCheckRegistrations_TaggedReady()
    {
        using var sp = BuildProvider();

        var registrations = sp
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations;

        registrations.Should().Contain(
            r => r.Name == "keycustodian-db"
                && r.Tags.Contains(KeyCustodianHealthTags.READY),
            "the DB connectivity check must carry the ready tag");
        registrations.Should().Contain(
            r => r.Name == "keycustodian"
                && r.Tags.Contains(KeyCustodianHealthTags.READY),
            "the KC readiness check must carry the ready tag");
    }

    [Fact]
    public void AddD2KeyCustodian_DoesNotRegisterIRequestContext_PlatformOwnsSystemWorkPlane()
    {
        // Modules must not re-register IRequestContext / MutableRequestContext —
        // the platform System work plane (AddD2SystemWorkPlane) owns those seams.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMessageBus, NoopMessageBus>();
        services.AddD2Handler();
        services.Configure<D2WorkloadIdentityOptions>(o => o.ServiceId = "key-custodian");
        services.AddD2KeyCustodian(
            KcInfraTestKit.BuildConfiguration(r_rootKeyDir), KcInfraTestKit.FAKE_CONNECTION_STRING);

        services.Should().NotContain(
            d => d.ServiceType == typeof(IRequestContext),
            because: "KC must not re-register IRequestContext — platform owns it");
        services.Should().NotContain(
            d => d.ServiceType == typeof(MutableRequestContext),
            because: "KC must not re-register MutableRequestContext — platform owns it");
        services.Should().NotContain(
            d => d.ServiceType == typeof(ISystemWorkScopeFactory),
            because: "KC must not re-register ISystemWorkScopeFactory — platform owns it");
    }

    [Fact]
    public async Task AddD2KeyCustodian_WithPlatformSystemWorkPlane_SystemWorkersResolveWithoutHttpContext()
    {
        // Composition regression: host wires AddD2SystemWorkPlane (+ dual-path-style
        // Mutable fall-through) then KC; BeginAsync + resolve IRequestContext must
        // not throw the historical "no HttpContext" multiproc-seed failure class.
        var services = NewServices();
        services.AddD2KeyCustodian(
            KcInfraTestKit.BuildConfiguration(r_rootKeyDir), KcInfraTestKit.FAKE_CONNECTION_STRING);
        services.AddD2CaRootSigningCapability();

        await using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<ISystemWorkScopeFactory>();

        await using var work = await factory.BeginAsync();
        var ctx = work.Services.GetRequiredService<IRequestContext>();

        ctx.Origin.Should().Be(RequestOrigin.System);
        ctx.ImmediateCaller.Should().Be("key-custodian");
    }

    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The handler infrastructure (HandlerContext<T> open generic + IRequestContext)
        // and System work plane are host responsibilities — Edge registers them.
        // The KC module assumes they are present.
        services.AddD2Handler();
        services.Configure<D2WorkloadIdentityOptions>(o => o.ServiceId = "key-custodian");
        services.AddD2SystemWorkPlane();

        // The host binds its own workload identity (ServiceId); AddD2KeyCustodian only
        // VALIDATES presence (fail-loud). Supply a valid id so the System-worker hosted
        // services construct + resolve under the presence gate. The unset-ServiceId reject
        // path is pinned by AddD2KeyCustodian_UnsetWorkloadIdentity_FailsOptionsValidation.

        services.AddSingleton<IMessageBus, NoopMessageBus>();
        return services;
    }

    private ServiceProvider BuildProvider()
    {
        var services = NewServices();
        services.AddD2KeyCustodian(
            KcInfraTestKit.BuildConfiguration(r_rootKeyDir), KcInfraTestKit.FAKE_CONNECTION_STRING);

        // The dedicated §9.44 root-signing capability — the composition-root opt-in the
        // System-worker host makes; the general Infra registration deliberately omits it,
        // so the four lifecycle-mutation handlers only resolve once it is added (the
        // isolation property is pinned in the App DI suite).
        services.AddD2CaRootSigningCapability();

        return services.BuildServiceProvider();
    }

    private sealed class NoopMessageBus : IMessageBus
    {
        public ValueTask<D2Result> PublishAsync<TMessage>(
            TMessage message, PublisherOptions? options = null, CancellationToken ct = default)
            where TMessage : class => ValueTask.FromResult(D2Result.Ok());

        public Task WaitForReadyAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
