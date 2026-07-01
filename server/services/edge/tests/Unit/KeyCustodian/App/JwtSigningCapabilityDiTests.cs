// -----------------------------------------------------------------------
// <copyright file="JwtSigningCapabilityDiTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Clients;
using D2.Shared.Context.Abstractions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI-isolation proof for the JWT-minter capability — the security property that makes
/// the cluster-signing root unreachable on the general surface: the capability is
/// registered ONLY by <c>AddD2JwtSigningCapability()</c> (the auth-module composition),
/// NEVER by the general <c>AddD2KeyCustodianApp()</c> / <c>AddD2KeyCustodianClients()</c>
/// registration. Possession of the resolved seam IS half the authority (the in-process
/// plane check is the other half).
/// </summary>
public sealed class JwtSigningCapabilityDiTests
{
    [Fact]
    public void GeneralAppComposition_DoesNotRegisterJwtSigningCapability()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianApp();

        services.Should().NotContain(
            d => d.ServiceType == typeof(IJwtSigningCapability),
            "the general composition can never reach the cluster-signing root");
    }

    [Fact]
    public void GeneralClientsComposition_DoesNotResolveJwtSigningCapability()
    {
        var services = new ServiceCollection();
        services.AddD2KeyCustodianClients();

        using var sp = services.BuildServiceProvider();

        sp.GetService<IJwtSigningCapability>().Should().BeNull(
            "the general client registration alone cannot resolve the minter capability");
    }

    [Fact]
    public void MinterComposition_ResolvesJwtSigningCapability()
    {
        var services = new ServiceCollection();
        services.AddD2JwtSigningCapability();

        // The seams the capability impl needs — registered by the minter (auth-module)
        // composition the capability ships inside.
        services.AddLogging();
        services.AddSingleton<IRequestContext>(_ => new MutableRequestContext());
        services.AddSingleton<IKeyCustodianDbContext>(_ => KeyCustodianTestDbContext.CreateEmpty());
        services.AddKeyedSingleton<IPayloadCrypto>(
            KeyCustodianRootKey.ROOT_SERVICE_KEY,
            (_, _) => KcAppTestKit.BuildTestRootCrypto());

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IJwtSigningCapability>()
            .Should().BeOfType<JwtSigningCapability>(
                "AddD2JwtSigningCapability grants the minter capability in its own composition");
    }

    [Fact]
    public void AddD2JwtSigningCapability_RegistersTransient()
    {
        var services = new ServiceCollection();
        services.AddD2JwtSigningCapability();

        services.Single(d => d.ServiceType == typeof(IJwtSigningCapability))
            .Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddD2JwtSigningCapability_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2JwtSigningCapability();

        act.Should().Throw<ArgumentNullException>();
    }
}
