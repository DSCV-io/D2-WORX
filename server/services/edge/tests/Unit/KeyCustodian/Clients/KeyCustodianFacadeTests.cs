// -----------------------------------------------------------------------
// <copyright file="KeyCustodianFacadeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Clients;

using D2.Edge.KeyCustodian.App.Application;
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
        var facade = new KeyCustodianApi(stub, OidcStub());

        var result = await facade.GetJwksAsync(new GetJwksInput());

        stub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetJwksAsync_PassesCancellationToken_ToHandler()
    {
        var stub = new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([])));
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(stub, OidcStub());

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
        var facade = new KeyCustodianApi(stub, OidcStub());

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
        var facade = new KeyCustodianApi(stub, OidcStub());

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
            oidcStub);

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
            oidcStub);

        await facade.GetOidcConfigurationAsync(new GetOidcConfigurationInput(), cts.Token);

        oidcStub.LastCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task GetOidcConfigurationAsync_HandlerFails_FacadeSurfacesSameFailure()
    {
        var failure = D2Result<GetOidcConfigurationOutput?>.ServiceUnavailable();
        var facade = new KeyCustodianApi(
            new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))),
            new StubGetOidcConfigurationHandler(failure));

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
}
