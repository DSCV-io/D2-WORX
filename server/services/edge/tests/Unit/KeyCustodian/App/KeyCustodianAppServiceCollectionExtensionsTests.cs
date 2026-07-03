// -----------------------------------------------------------------------
// <copyright file="KeyCustodianAppServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetOidcConfiguration;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
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
/// every handler and policy provider is registered with the correct service type
/// and lifetime, the resolvability composition (the general registration + the
/// dedicated leaf-signing capability extension TOGETHER — mirroring the future
/// host composition root) resolves EVERY registered seam, and the isolation
/// composition (the general registration ALONE) CANNOT resolve the leaf-signing
/// capability — the issuance path is structurally absent from a host that does
/// not opt in.
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
        services.Should().Contain(d => d.ServiceType == typeof(IIssueLeafHandler));
        services.Should().Contain(d => d.ServiceType == typeof(ISeedCertificateAuthorityHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetJwksHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetOidcConfigurationHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetRotationPlanHandler));
        services.Should().Contain(d => d.ServiceType == typeof(ISignHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetKeyringHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetCaCertificateHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IKeyCustodianApi));
        services.Should().Contain(d => d.ServiceType == typeof(ISigningDomainAuthorityPolicy));
        services.Should().Contain(d => d.ServiceType == typeof(IKeyringDomainAuthorityPolicy));
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
    public void AddD2KeyCustodianApp_Alone_CannotResolveLeafSigningCapability()
    {
        // THE isolation composition: the general registration ALONE must not be
        // able to resolve the issuance leaf-signing capability — and therefore
        // cannot sign a workload leaf via the issuance path. The capability is
        // granted ONLY by its own dedicated extension from the composition root
        // that serves the issuance surface. Structural, not a branch guard.
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        services.Should().NotContain(
            d => d.ServiceType == typeof(ICaLeafSigningCapability),
            "the general registration never registers the leaf-signing capability");

        using var sp = services.BuildServiceProvider();

        sp.GetService<ICaLeafSigningCapability>().Should().BeNull(
            "a provider built from the general registration alone cannot resolve "
            + "the leaf-signing capability");
    }

    [Fact]
    public void AddD2KeyCustodianApp_ResolvesEveryHandlerAndPolicyProvider_FromBuiltProvider()
    {
        // THE resolvability composition: the general registration + the dedicated
        // leaf-signing capability extension TOGETHER — mirroring how the host
        // composition root that serves the issuance surface wires them. (The
        // dedicated extension has no production caller until the Edge host lands;
        // this composition is the test-side stand-in.)
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();
        services.AddD2CaLeafSigningCapability();
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

        // IOptions<KeyringDomainAuthorityOptions> — App-owned; OptionsKeyringDomainAuthorityPolicy.
        services.AddSingleton(Options.Create(new KeyringDomainAuthorityOptions()));

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
        sp.GetRequiredService<ISignHandler>()
            .Should().BeOfType<SignHandler>(
                "AddD2KeyCustodianApp registers the general sign query handler");
        sp.GetRequiredService<IGetKeyringHandler>()
            .Should().BeOfType<GetKeyringHandler>(
                "AddD2KeyCustodianApp registers the keyring-fetch query handler");
        sp.GetRequiredService<IRotationPolicyProvider>()
            .Should().BeOfType<OptionsRotationPolicyProvider>();
        sp.GetRequiredService<ISigningDomainAuthorityPolicy>()
            .Should().BeOfType<OptionsSigningDomainAuthorityPolicy>(
                "AddD2KeyCustodianApp registers the singleton policy provider");
        sp.GetRequiredService<IKeyringDomainAuthorityPolicy>()
            .Should().BeOfType<OptionsKeyringDomainAuthorityPolicy>(
                "AddD2KeyCustodianApp registers the singleton keyring policy provider");
        sp.GetRequiredService<IGetOidcConfigurationHandler>()
            .Should().BeOfType<GetOidcConfigurationHandler>(
                "AddD2KeyCustodianApp registers the OIDC-discovery query handler");
        sp.GetRequiredService<IGetCaCertificateHandler>()
            .Should().BeOfType<GetCaCertificateHandler>(
                "AddD2KeyCustodianApp registers the CA-chain query handler");
        sp.GetRequiredService<IIssueLeafHandler>()
            .Should().BeOfType<IssueLeafHandler>(
                "AddD2KeyCustodianApp registers the generated-op issuance shell");
        sp.GetRequiredService<ICaLeafSigningCapability>()
            .Should().BeOfType<CaLeafSigningCapability>(
                "the dedicated extension grants the leaf-signing capability");
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
