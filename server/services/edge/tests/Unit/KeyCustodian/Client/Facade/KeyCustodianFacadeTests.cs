// -----------------------------------------------------------------------
// <copyright file="KeyCustodianFacadeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Facade;

using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Facade;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionOwnSealPrivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
using D2.Edge.KeyCustodian.Client.CaCertificate;
using D2.Edge.KeyCustodian.Client.Facade;
using D2.Edge.KeyCustodian.Client.Issuance;
using D2.Edge.KeyCustodian.Client.Jwks;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Edge.KeyCustodian.Client.OidcConfiguration;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Edge.KeyCustodian.Client.Signing;
using D2.Shared.Handler.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Tests for the generated <see cref="IKeyCustodianApi"/> façade layer.
/// Covers DI resolution, delegation, and adversarial failure propagation.
/// </summary>
public sealed class KeyCustodianFacadeTests
{
    // -------------------------------------------------------------------------
    // DI registration tests
    // -------------------------------------------------------------------------

    [Fact]
    public void AddD2KeyCustodianApp_RegistersKeyCustodianApi()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        services.Should().Contain(d => d.ServiceType == typeof(IKeyCustodianApi));
    }

    [Fact]
    public void AddD2KeyCustodianApp_FacadeIsRegisteredAsTransient()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        var descriptor = services.Single(d => d.ServiceType == typeof(IKeyCustodianApi));

        // Transient — façade injects transient handlers that capture scoped DbContext;
        // a Singleton façade would be a captive-dependency bug.
        descriptor.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddD2KeyCustodianApp_FacadeImplementationIsKeyCustodianApi()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        var descriptor = services.Single(d => d.ServiceType == typeof(IKeyCustodianApi));
        descriptor.ImplementationType.Should().Be<KeyCustodianApi>();
    }

    [Fact]
    public void AddD2KeyCustodianClient_ResolvesIKeyCustodianApi()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianClient();
        services.AddTransient<IGetJwksHandler>(
            _ => new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))));
        services.AddTransient<IGetOidcConfigurationHandler>(
            _ => new StubGetOidcConfigurationHandler(
                D2Result<GetOidcConfigurationOutput?>.Ok(SampleOidc())));
        services.AddTransient<ISignHandler>(_ => SignStub());
        services.AddTransient<IGetKeyringHandler>(_ => KeyringStub());
        services.AddTransient<IIssueLeafHandler>(_ => IssueLeafStub());
        services.AddTransient<IGetCaCertificateHandler>(_ => CaCertStub());
        services.AddTransient<IGetOrLazyProvisionSealPublicKeyHandler>(_ => SealPubStub());
        services.AddTransient<IGetOrLazyProvisionOwnSealPrivateKeyHandler>(_ => SealPrivStub());

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IKeyCustodianApi>()
            .Should().BeOfType<KeyCustodianApi>();
    }

    [Fact]
    public void AddD2KeyCustodianApp_ResolvesIKeyCustodianApi_ThroughFullEntryPoint()
    {
        // Proves AddD2KeyCustodianApp() wires the façade end-to-end.
        // AddD2KeyCustodianApp() calls AddD2KeyCustodianClient() internally — this
        // test exercises the full registration chain from the public entry point.
        // A stub overrides the concrete IGetJwksHandler registration so the façade
        // can be resolved without requiring the Infra seams (DbContext, root crypto).
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        // Stubs override the concrete handlers registered by AddD2KeyCustodianApp().
        // DI resolves the last registration, so these replace the concrete impls for
        // this test (the real GetOidcConfigurationHandler needs IOptions, which the
        // stub sidesteps; the façade resolution is what's under test).
        services.AddTransient<IGetJwksHandler>(
            _ => new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))));
        services.AddTransient<IGetOidcConfigurationHandler>(
            _ => new StubGetOidcConfigurationHandler(
                D2Result<GetOidcConfigurationOutput?>.Ok(SampleOidc())));
        services.AddTransient<ISignHandler>(_ => SignStub());
        services.AddTransient<IGetKeyringHandler>(_ => KeyringStub());
        services.AddTransient<IIssueLeafHandler>(_ => IssueLeafStub());
        services.AddTransient<IGetCaCertificateHandler>(_ => CaCertStub());
        services.AddTransient<IGetOrLazyProvisionSealPublicKeyHandler>(_ => SealPubStub());
        services.AddTransient<IGetOrLazyProvisionOwnSealPrivateKeyHandler>(_ => SealPrivStub());

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IKeyCustodianApi>()
            .Should().BeOfType<KeyCustodianApi>();
    }

    // -------------------------------------------------------------------------
    // Delegation tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetJwksAsync_DelegatesTo_GetJwksHandler()
    {
        var jwk = new Jwk("kid-001", "modulus", "AQAB", "RSA", "sig", "RS256");
        var expected = D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([jwk]));
        var stub = new StubGetJwksHandler(expected);
        var facade = new KeyCustodianApi(
            stub,
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetJwksAsync(new GetJwksInput());

        stub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetJwksAsync_PassesCancellationToken_ToHandler()
    {
        var stub = new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([])));
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(
            stub,
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        await facade.GetJwksAsync(new GetJwksInput(), cts.Token);

        stub.LastCancellationToken.Should().Be(cts.Token);
    }

    // -------------------------------------------------------------------------
    // Adversarial: failure propagation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetJwksAsync_HandlerReturnsServiceUnavailable_FacadeSurfacesSameFailure()
    {
        // Ensures the façade does not swallow failures — result identity must be preserved.
        var failure = D2Result<GetJwksOutput?>.ServiceUnavailable();
        var stub = new StubGetJwksHandler(failure);
        var facade = new KeyCustodianApi(
            stub,
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetJwksAsync(new GetJwksInput());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        result.Should().Be(failure);
    }

    [Fact]
    public async Task GetJwksAsync_HandlerReturnsCanceled_FacadeSurfacesCanceled()
    {
        var canceled = D2Result<GetJwksOutput?>.Canceled();
        var stub = new StubGetJwksHandler(canceled);
        var facade = new KeyCustodianApi(
            stub,
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetJwksAsync(new GetJwksInput());

        result.Success.Should().BeFalse();
        result.Should().Be(canceled);
    }

    // -------------------------------------------------------------------------
    // OIDC discovery delegation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOidcConfigurationAsync_DelegatesTo_GetOidcConfigurationHandler()
    {
        var expected = D2Result<GetOidcConfigurationOutput?>.Ok(SampleOidc());
        var oidcStub = new StubGetOidcConfigurationHandler(expected);
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            oidcStub,
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetOidcConfigurationAsync(new GetOidcConfigurationInput());

        oidcStub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetOidcConfigurationAsync_PassesCancellationToken_ToHandler()
    {
        var oidcStub = new StubGetOidcConfigurationHandler(
            D2Result<GetOidcConfigurationOutput?>.Ok(SampleOidc()));
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            oidcStub,
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        await facade.GetOidcConfigurationAsync(new GetOidcConfigurationInput(), cts.Token);

        oidcStub.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task GetOidcConfigurationAsync_HandlerFails_FacadeSurfacesSameFailure()
    {
        var failure = D2Result<GetOidcConfigurationOutput?>.ServiceUnavailable();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            new StubGetOidcConfigurationHandler(failure),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetOidcConfigurationAsync(new GetOidcConfigurationInput());

        result.Success.Should().BeFalse();
        result.Should().Be(failure);
    }

    // -------------------------------------------------------------------------
    // Structural tests — interface and impl shape
    // -------------------------------------------------------------------------

    [Fact]
    public void IKeyCustodianApi_IsInClientFacadeNamespace()
    {
        typeof(IKeyCustodianApi).Namespace
            .Should().Be("D2.Edge.KeyCustodian.Client.Facade");
    }

    [Fact]
    public void KeyCustodianApi_IsInAppFacadeNamespace()
    {
        typeof(KeyCustodianApi).Namespace
            .Should().Be("D2.Edge.KeyCustodian.App.Application.Facade");
    }

    [Fact]
    public void KeyCustodianApi_IsSealed()
    {
        typeof(KeyCustodianApi).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void IKeyCustodianApi_HasGetJwksAsyncMethod()
    {
        typeof(IKeyCustodianApi).GetMethod("GetJwksAsync")
            .Should().NotBeNull();
    }

    [Fact]
    public void IKeyCustodianApi_HasGetOidcConfigurationAsyncMethod()
    {
        typeof(IKeyCustodianApi).GetMethod("GetOidcConfigurationAsync")
            .Should().NotBeNull();
    }

    [Fact]
    public void IKeyCustodianApi_HasSignAsyncMethod()
    {
        typeof(IKeyCustodianApi).GetMethod("SignAsync")
            .Should().NotBeNull();
    }

    [Fact]
    public void IKeyCustodianApi_HasGetKeyringAsyncMethod()
    {
        typeof(IKeyCustodianApi).GetMethod("GetKeyringAsync")
            .Should().NotBeNull();
    }

    [Fact]
    public void IKeyCustodianApi_HasIssueLeafAsyncMethod()
    {
        typeof(IKeyCustodianApi).GetMethod("IssueLeafAsync")
            .Should().NotBeNull();
    }

    [Fact]
    public void IKeyCustodianApi_HasGetCaCertificateAsyncMethod()
    {
        typeof(IKeyCustodianApi).GetMethod("GetCaCertificateAsync")
            .Should().NotBeNull();
    }

    [Fact]
    public void IKeyCustodianApi_HasGetOrLazyProvisionSealPublicKeyAsyncMethod()
    {
        typeof(IKeyCustodianApi).GetMethod("GetOrLazyProvisionSealPublicKeyAsync")
            .Should().NotBeNull();
    }

    [Fact]
    public void IKeyCustodianApi_HasGetOrLazyProvisionOwnSealPrivateKeyAsyncMethod()
    {
        typeof(IKeyCustodianApi).GetMethod("GetOrLazyProvisionOwnSealPrivateKeyAsync")
            .Should().NotBeNull();
    }

    // -------------------------------------------------------------------------
    // IssueLeaf + GetCaCertificate delegation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IssueLeafAsync_DelegatesTo_IssueLeafHandler()
    {
        var expected = D2Result<IssueLeafOutput?>.Ok(new IssueLeafOutput(
            [0x01], [0x02], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1)));
        var issueStub = new StubIssueLeafHandler(expected);
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            issueStub,
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.IssueLeafAsync(new IssueLeafInput([0x30]));

        issueStub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task IssueLeafAsync_HandlerFails_FacadeSurfacesSameFailure()
    {
        var failure = KeyCustodianFailures<IssueLeafOutput?>.IssuanceNotAuthorized();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            new StubIssueLeafHandler(failure),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.IssueLeafAsync(new IssueLeafInput([0x30]));

        result.Success.Should().BeFalse();
        result.Should().Be(failure);
    }

    [Fact]
    public async Task GetCaCertificateAsync_DelegatesTo_GetCaCertificateHandler()
    {
        var expected = D2Result<GetCaCertificateOutput?>.Ok(
            new GetCaCertificateOutput([0x01], [0x02]));
        var caCertStub = new StubGetCaCertificateHandler(expected);
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            caCertStub,
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetCaCertificateAsync(new GetCaCertificateInput());

        caCertStub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetCaCertificateAsync_HandlerFails_FacadeSurfacesSameFailure()
    {
        var failure = KeyCustodianFailures<GetCaCertificateOutput?>.NoActiveIssuingCa();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            new StubGetCaCertificateHandler(failure),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetCaCertificateAsync(new GetCaCertificateInput());

        result.Success.Should().BeFalse();
        result.Should().Be(failure);
    }

    // -------------------------------------------------------------------------
    // Sign delegation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SignAsync_DelegatesTo_SignHandler()
    {
        var expected = D2Result<SignOutput?>.Ok(new SignOutput("c2ln", "kid-001"));
        var signStub = new StubSignHandler(expected);
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            signStub,
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.SignAsync(new SignInput("audit", [0x01]));

        signStub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task SignAsync_PassesCancellationToken_ToHandler()
    {
        var signStub = new StubSignHandler(
            D2Result<SignOutput?>.Ok(new SignOutput("c2ln", "kid-001")));
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            signStub,
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        await facade.SignAsync(new SignInput("audit", [0x01]), cts.Token);

        signStub.LastCancellationToken.Should().Be(cts.Token);
    }

    // -------------------------------------------------------------------------
    // GetKeyring delegation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetKeyringAsync_DelegatesTo_GetKeyringHandler()
    {
        var expected = D2Result<GetKeyringOutput?>.Ok(
            new GetKeyringOutput(
                "kid-001",
                [new KeyringEntry("kid-001", new byte[32])],
                "d2/audit"u8.ToArray()));
        var keyringStub = new StubGetKeyringHandler(expected);
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            keyringStub,
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetKeyringAsync(new GetKeyringInput("audit"));

        keyringStub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetKeyringAsync_PassesCancellationToken_ToHandler()
    {
        var keyringStub = KeyringStub();
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            keyringStub,
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        await facade.GetKeyringAsync(new GetKeyringInput("audit"), cts.Token);

        keyringStub.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task GetKeyringAsync_HandlerFails_FacadeSurfacesSameFailure()
    {
        var failure = KeyCustodianFailures<GetKeyringOutput?>.KeyringKeyUnavailable();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            new StubGetKeyringHandler(failure),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            SealPrivStub());

        var result = await facade.GetKeyringAsync(new GetKeyringInput("audit"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
        result.Should().Be(failure);
    }

    // -------------------------------------------------------------------------
    // Seal delegation (getOrLazyProvisionSealPublicKey / getOrLazyProvisionOwnSealPrivateKey)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKeyAsync_DelegatesTo_GetOrLazyProvisionSealPublicKeyHandler()
    {
        var expected = D2Result<GetOrLazyProvisionSealPublicKeyOutput?>.Ok(
            new GetOrLazyProvisionSealPublicKeyOutput("kid-001", [new SealPublicEntry("kid-001", new byte[65])]));
        var sealStub = new StubGetOrLazyProvisionSealPublicKeyHandler(expected);
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            sealStub,
            SealPrivStub());

        var result = await facade.GetOrLazyProvisionSealPublicKeyAsync(new GetOrLazyProvisionSealPublicKeyInput("audit"));

        sealStub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKeyAsync_PassesCancellationToken_ToHandler()
    {
        var sealStub = SealPubStub();
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            sealStub,
            SealPrivStub());

        await facade.GetOrLazyProvisionSealPublicKeyAsync(new GetOrLazyProvisionSealPublicKeyInput("audit"), cts.Token);

        sealStub.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task GetOrLazyProvisionSealPublicKeyAsync_HandlerFails_FacadeSurfacesSameFailure()
    {
        var failure = KeyCustodianFailures<GetOrLazyProvisionSealPublicKeyOutput?>.SealKeyUnavailable();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            new StubGetOrLazyProvisionSealPublicKeyHandler(failure),
            SealPrivStub());

        var result = await facade.GetOrLazyProvisionSealPublicKeyAsync(new GetOrLazyProvisionSealPublicKeyInput("audit"));

        result.Success.Should().BeFalse();
        result.Should().Be(failure);
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKeyAsync_DelegatesTo_GetOrLazyProvisionOwnSealPrivateKeyHandler()
    {
        var expected = D2Result<GetOrLazyProvisionOwnSealPrivateKeyOutput?>.Ok(
            new GetOrLazyProvisionOwnSealPrivateKeyOutput(
                "kid-001", [new SealPrivateEntry("kid-001", new byte[121])]));
        var sealStub = new StubGetOrLazyProvisionOwnSealPrivateKeyHandler(expected);
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            sealStub);

        var result = await facade.GetOrLazyProvisionOwnSealPrivateKeyAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

        sealStub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKeyAsync_PassesCancellationToken_ToHandler()
    {
        var sealStub = SealPrivStub();
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            sealStub);

        await facade.GetOrLazyProvisionOwnSealPrivateKeyAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput(), cts.Token);

        sealStub.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task GetOrLazyProvisionOwnSealPrivateKeyAsync_HandlerFails_FacadeSurfacesSameFailure()
    {
        var failure = KeyCustodianFailures<GetOrLazyProvisionOwnSealPrivateKeyOutput?>.SealNotAuthorized();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            OidcStub(),
            SignStub(),
            KeyringStub(),
            IssueLeafStub(),
            CaCertStub(),
            SealPubStub(),
            new StubGetOrLazyProvisionOwnSealPrivateKeyHandler(failure));

        var result = await facade.GetOrLazyProvisionOwnSealPrivateKeyAsync(new GetOrLazyProvisionOwnSealPrivateKeyInput());

        result.Success.Should().BeFalse();
        result.Should().Be(failure);
    }

    // -------------------------------------------------------------------------
    // Test helpers + stub handlers (test doubles for the façade's dependencies)
    // -------------------------------------------------------------------------

    private static GetOidcConfigurationOutput SampleOidc() =>
        new(
            "https://edge.internal",
            "https://edge.internal/.well-known/jwks.json",
            ["RS256"],
            ["none"],
            ["public"]);

    private static StubGetOidcConfigurationHandler OidcStub() =>
        new(D2Result<GetOidcConfigurationOutput?>.Ok(SampleOidc()));

    private static StubSignHandler SignStub() =>
        new(D2Result<SignOutput?>.Ok(new SignOutput("c2ln", "kid")));

    private static StubGetKeyringHandler KeyringStub() =>
        new(D2Result<GetKeyringOutput?>.Ok(
            new GetKeyringOutput(
                "kid", [new KeyringEntry("kid", new byte[32])], "d2/audit"u8.ToArray())));

    private static StubIssueLeafHandler IssueLeafStub() =>
        new(D2Result<IssueLeafOutput?>.Ok(new IssueLeafOutput(
            [0x01], [0x02], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1))));

    private static StubGetCaCertificateHandler CaCertStub() =>
        new(D2Result<GetCaCertificateOutput?>.Ok(new GetCaCertificateOutput([0x01], [0x02])));

    private static StubGetOrLazyProvisionSealPublicKeyHandler SealPubStub() =>
        new(D2Result<GetOrLazyProvisionSealPublicKeyOutput?>.Ok(
            new GetOrLazyProvisionSealPublicKeyOutput("kid", [new SealPublicEntry("kid", new byte[65])])));

    private static StubGetOrLazyProvisionOwnSealPrivateKeyHandler SealPrivStub() =>
        new(D2Result<GetOrLazyProvisionOwnSealPrivateKeyOutput?>.Ok(
            new GetOrLazyProvisionOwnSealPrivateKeyOutput("kid", [new SealPrivateEntry("kid", new byte[121])])));

    private sealed class StubGetJwksHandler(D2Result<GetJwksOutput?> result) : IGetJwksHandler
    {
        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<D2Result<GetJwksOutput?>> HandleAsync(
            GetJwksInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            CallCount++;
            LastCancellationToken = ct;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubGetOidcConfigurationHandler(
        D2Result<GetOidcConfigurationOutput?> result) : IGetOidcConfigurationHandler
    {
        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<D2Result<GetOidcConfigurationOutput?>> HandleAsync(
            GetOidcConfigurationInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            CallCount++;
            LastCancellationToken = ct;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubSignHandler(D2Result<SignOutput?> result) : ISignHandler
    {
        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<D2Result<SignOutput?>> HandleAsync(
            SignInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            CallCount++;
            LastCancellationToken = ct;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubGetKeyringHandler(D2Result<GetKeyringOutput?> result)
        : IGetKeyringHandler
    {
        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<D2Result<GetKeyringOutput?>> HandleAsync(
            GetKeyringInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            CallCount++;
            LastCancellationToken = ct;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubIssueLeafHandler(D2Result<IssueLeafOutput?> result)
        : IIssueLeafHandler
    {
        public int CallCount { get; private set; }

        public ValueTask<D2Result<IssueLeafOutput?>> HandleAsync(
            IssueLeafInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubGetCaCertificateHandler(D2Result<GetCaCertificateOutput?> result)
        : IGetCaCertificateHandler
    {
        public int CallCount { get; private set; }

        public ValueTask<D2Result<GetCaCertificateOutput?>> HandleAsync(
            GetCaCertificateInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubGetOrLazyProvisionSealPublicKeyHandler(D2Result<GetOrLazyProvisionSealPublicKeyOutput?> result)
        : IGetOrLazyProvisionSealPublicKeyHandler
    {
        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<D2Result<GetOrLazyProvisionSealPublicKeyOutput?>> HandleAsync(
            GetOrLazyProvisionSealPublicKeyInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            CallCount++;
            LastCancellationToken = ct;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubGetOrLazyProvisionOwnSealPrivateKeyHandler(
        D2Result<GetOrLazyProvisionOwnSealPrivateKeyOutput?> result) : IGetOrLazyProvisionOwnSealPrivateKeyHandler
    {
        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<D2Result<GetOrLazyProvisionOwnSealPrivateKeyOutput?>> HandleAsync(
            GetOrLazyProvisionOwnSealPrivateKeyInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            CallCount++;
            LastCancellationToken = ct;
            return ValueTask.FromResult(result);
        }
    }
}
