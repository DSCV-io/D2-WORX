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

        // Stub overrides the concrete GetJwksHandler registered by AddD2KeyCustodianApp().
        // DI resolves the last registration, so this replaces the concrete impl for this test.
        services.AddTransient<IGetJwksHandler>(
            _ => new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([]))));

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
        var facade = new KeyCustodianApi(stub);

        var result = await facade.GetJwksAsync(new GetJwksInput());

        stub.CallCount.Should().Be(1);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetJwksAsync_PassesCancellationToken_ToHandler()
    {
        var stub = new StubGetJwksHandler(D2Result<GetJwksOutput?>.Ok(new GetJwksOutput([])));
        using var cts = new CancellationTokenSource();
        var facade = new KeyCustodianApi(stub);

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
        var facade = new KeyCustodianApi(stub);

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
        var facade = new KeyCustodianApi(stub);

        var result = await facade.GetJwksAsync(new GetJwksInput());

        result.Success.Should().BeFalse();
        result.Should().Be(canceled);
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

    // -------------------------------------------------------------------------
    // Stub handler — test double for IGetJwksHandler
    // -------------------------------------------------------------------------

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
}
