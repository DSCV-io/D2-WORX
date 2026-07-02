// -----------------------------------------------------------------------
// <copyright file="KeyCustodianFacadeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Clients;

using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;
using D2.Edge.KeyCustodian.Clients;
using D2.Shared.Handler.Abstractions;
using D2.Shared.Result;
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
    public void AddD2KeyCustodianClients_ResolvesIKeyCustodianApi()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianClients();
        services.AddTransient<IGetJwksHandler>(
            _ => new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))));
        services.AddTransient<IGetOidcConfigurationHandler>(
            _ => new StubGetOidcConfigurationHandler(
                D2Result<GetOidcConfigurationOutput?>.Ok(SampleOidc())));
        services.AddTransient<ISignHandler>(_ => SignStub());
        services.AddTransient<IGetKeyringHandler>(_ => KeyringStub());

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IKeyCustodianApi>()
            .Should().BeOfType<KeyCustodianApi>();
    }

    [Fact]
    public void AddD2KeyCustodianApp_ResolvesIKeyCustodianApi_ThroughFullEntryPoint()
    {
        // Proves AddD2KeyCustodianApp() wires the façade end-to-end.
        // AddD2KeyCustodianApp() calls AddD2KeyCustodianClients() internally — this
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
        var facade = new KeyCustodianApi(stub, OidcStub(), SignStub(), KeyringStub());

        var result = await facade.GetJwksAsync(new GetJwksInput());

        stub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetJwksAsync_PassesCancellationToken_ToHandler()
    {
        var stub = new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([])));
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(stub, OidcStub(), SignStub(), KeyringStub());

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
        var facade = new KeyCustodianApi(stub, OidcStub(), SignStub(), KeyringStub());

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
        var facade = new KeyCustodianApi(stub, OidcStub(), SignStub(), KeyringStub());

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
            KeyringStub());

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
            KeyringStub());

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
            KeyringStub());

        var result = await facade.GetOidcConfigurationAsync(new GetOidcConfigurationInput());

        result.Success.Should().BeFalse();
        result.Should().Be(failure);
    }

    // -------------------------------------------------------------------------
    // Structural tests — interface and impl shape
    // -------------------------------------------------------------------------

    [Fact]
    public void IKeyCustodianApi_IsInClientsNamespace()
    {
        typeof(IKeyCustodianApi).Namespace
            .Should().Be("D2.Edge.KeyCustodian.Clients");
    }

    [Fact]
    public void KeyCustodianApi_IsInAppNamespace()
    {
        typeof(KeyCustodianApi).Namespace
            .Should().Be("D2.Edge.KeyCustodian.App.Application");
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
            KeyringStub());

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
            KeyringStub());

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
            keyringStub);

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
            keyringStub);

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
            new StubGetKeyringHandler(failure));

        var result = await facade.GetKeyringAsync(new GetKeyringInput("audit"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
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
            new GetKeyringOutput("kid", [new KeyringEntry("kid", new byte[32])], "d2/audit"u8.ToArray())));

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
}
