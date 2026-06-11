// -----------------------------------------------------------------------
// <copyright file="KeyCustodianAppServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Linq;
using D2.Edge.KeyCustodian.App.Application;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration tests for <see cref="KeyCustodianAppServiceCollectionExtensions"/>:
/// the 7 handlers and the policy provider are all registered with the right
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
        services.Should().Contain(d => d.ServiceType == typeof(IGetJwksHandler));
        services.Should().Contain(d => d.ServiceType == typeof(IGetRotationPlanHandler));
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
}
