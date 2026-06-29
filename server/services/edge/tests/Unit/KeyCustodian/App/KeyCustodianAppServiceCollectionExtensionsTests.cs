// -----------------------------------------------------------------------
// <copyright file="KeyCustodianAppServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetOidcConfiguration;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Clients;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Registration tests for <see cref="KeyCustodianAppServiceCollectionExtensions"/>:
/// all 10 handlers and the policy provider are registered with the correct
/// service type and lifetime. Key generation + smoke testing are pure domain
/// rules with no DI, so there are no generator / smoke-tester registrations.
/// </summary>
public sealed class KeyCustodianAppServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2KeyCustodianApp_RegistersAllHandlerInterfaces()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        services.Should().Contain(d => d.ServiceType == typeof(IGenerateKeyHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IActivateKeyHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IRotateKeyHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IRetireKeyHandler));
        services.Should().Contain(d => d.ServiceType == typeof(ICompromiseKeyHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IRunDueRotationsHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IIssueWorkloadCertificateHandler));
        services.Should().Contain(d => d.ServiceType == typeof(ISeedCertificateAuthorityHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetJwksHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetOidcConfigurationHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetRotationPlanHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IKeyCustodianApi));
        services.Should().Contain(d => d.ServiceType == typeof(ISigningDomainAuthorityPolicy));
    }

    [Fact]
    public void AddD2KeyCustodianApp_RegistersPolicyProvider()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        services.Should().Contain(d => d.ServiceType == typeof(IRotationPolicyProvider));
    }

    [Fact]
    public void AddD2KeyCustodianApp_HandlersAreTransient()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        services.Single(d => d.ServiceType == typeof(IGenerateKeyHandler))
            .Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddD2KeyCustodianApp_ResolvesEveryHandlerAndPolicyProvider_FromBuiltProvider()
    {
        // Arrange: register the App layer + all Infra-owned seams the handlers need.
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();
        services.AddLogging();
        services.AddD2Handler();

        // IRequestContext — required by HandlerContext<T> (open-generic registration
        // above wires HandlerContext<T> via DI; each handler's ctor takes it).
        services.AddSingleton<IRequestContext>(_ => new MutableRequestContext());

        // IKeyCustodianDbContext — Infra-owned; provide the test-owned in-memory impl.
        services.AddSingleton<IKeyCustodianDbContext>(
            _ => KeyCustodianTestDbContext.CreateEmpty());

        // Keyed root IPayloadCrypto — handlers inject via
        // [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)].
        services.AddKeyedSingleton<IPayloadCrypto>(
            KeyCustodianRootKey.ROOT_SERVICE_KEY,
            (_, _) => KcAppTestKit.BuildTestRootCrypto());

        // IKeyRotationAnnouncer — Infra-owned; RotateKeyHandler + CompromiseKeyHandler.
        services.AddSingleton<IKeyRotationAnnouncer>(_ => new RecordingAnnouncer());

        // IDbExceptionClassifier — Infra-owned; all BaseRepoHandler-derived handlers.
        services.AddSingleton(KcAppTestKit.NullClassifier());

        // IClock — Infra-owned; most handlers use GetCurrentInstant().
        services.AddSingleton<IClock>(new TestClock(KcAppTestKit.SR_BaseInstant));

        // IOptions<KeyCustodianOptions> — policy provider + Generate/Compromise handlers.
        services.AddSingleton(KcAppTestKit.BuildOptionsAccessor());

        // ICaProvider — Infra-owned; SeedCertificateAuthorityHandler.
        services.AddSingleton<ICaProvider>(_ => new StubCaProvider());

        // IOptions<SigningDomainAuthorityOptions> — App-owned; OptionsSigningDomainAuthorityPolicy.
        services.AddSingleton(Options.Create(new SigningDomainAuthorityOptions()));

        using var sp = services.BuildServiceProvider();

        // Act + Assert: resolve every registered handler interface and the policy
        // provider; assert non-null and correct concrete type.
        sp.GetRequiredService<IGenerateKeyHandler>()
            .Should().BeOfType<GenerateKeyHandler>();
        sp.GetRequiredService<IActivateKeyHandler>()
            .Should().BeOfType<ActivateKeyHandler>();
        sp.GetRequiredService<IRotateKeyHandler>()
            .Should().BeOfType<RotateKeyHandler>();
        sp.GetRequiredService<IRetireKeyHandler>()
            .Should().BeOfType<RetireKeyHandler>();
        sp.GetRequiredService<ICompromiseKeyHandler>()
            .Should().BeOfType<CompromiseKeyHandler>();
        sp.GetRequiredService<IRunDueRotationsHandler>()
            .Should().BeOfType<RunDueRotationsHandler>();
        sp.GetRequiredService<IIssueWorkloadCertificateHandler>()
            .Should().BeOfType<IssueWorkloadCertificateHandler>();
        sp.GetRequiredService<ISeedCertificateAuthorityHandler>()
            .Should().BeOfType<SeedCertificateAuthorityHandler>();
        sp.GetRequiredService<IGetJwksHandler>()
            .Should().BeOfType<GetJwksHandler>();
        sp.GetRequiredService<IGetRotationPlanHandler>()
            .Should().BeOfType<GetRotationPlanHandler>();
        sp.GetRequiredService<IRotationPolicyProvider>()
            .Should().BeOfType<OptionsRotationPolicyProvider>();
        sp.GetRequiredService<ISigningDomainAuthorityPolicy>()
            .Should().BeOfType<OptionsSigningDomainAuthorityPolicy>(
                "AddD2KeyCustodianApp registers the singleton policy provider");
        sp.GetRequiredService<IGetOidcConfigurationHandler>()
            .Should().BeOfType<GetOidcConfigurationHandler>(
                "AddD2KeyCustodianApp registers the OIDC-discovery query handler");
        sp.GetRequiredService<IKeyCustodianApi>()
            .Should().NotBeNull();
    }

    // Minimal ICaProvider stub for DI-resolution verification only. Never invoked
    // in this test — the resolution test only proves the DI graph resolves cleanly,
    // not that the handler executes correctly.
    private sealed class StubCaProvider : ICaProvider
    {
        public D2Result<LoadedCaMaterial> GetSeedCaMaterial() =>
            D2Result<LoadedCaMaterial>.ServiceUnavailable();
    }
}
