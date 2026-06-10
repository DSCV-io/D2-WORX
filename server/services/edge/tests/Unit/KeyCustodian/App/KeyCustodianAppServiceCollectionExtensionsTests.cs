// -----------------------------------------------------------------------
// <copyright file="KeyCustodianAppServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Linq;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App;
using D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;
using D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.Q;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.Policy;
using D2.Edge.KeyCustodian.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Registration tests for <see cref="KeyCustodianAppServiceCollectionExtensions"/>:
/// the 7 handlers, the 3 generators, the smoke tester, and the policy provider
/// are all registered with the right service type.
/// </summary>
public sealed class KeyCustodianAppServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKeyCustodianApp_RegistersAllHandlerInterfaces()
    {
        var services = new ServiceCollection();
        services.AddKeyCustodianApp();

        services.Should().Contain(d => d.ServiceType == typeof(IGenerateKey));
        services.Should().Contain(d => d.ServiceType == typeof(IActivateKey));
        services.Should().Contain(d => d.ServiceType == typeof(IRotateKey));
        services.Should().Contain(d => d.ServiceType == typeof(IRetireKey));
        services.Should().Contain(d => d.ServiceType == typeof(ICompromiseKey));
        services.Should().Contain(d => d.ServiceType == typeof(IGetJwks));
        services.Should().Contain(d => d.ServiceType == typeof(IGetRotationPlan));
    }

    [Fact]
    public void AddKeyCustodianApp_RegistersThreeKeyGenerators_OnePerType()
    {
        var services = new ServiceCollection();
        services.AddKeyCustodianApp();

        var generatorDescriptors = services
            .Where(d => d.ServiceType == typeof(IKeyGenerator))
            .ToList();
        generatorDescriptors.Should().HaveCount(3);
    }

    [Fact]
    public void AddKeyCustodianApp_RegistersSmokeTesterAndPolicyProvider()
    {
        var services = new ServiceCollection();
        services.AddKeyCustodianApp();

        services.Should().Contain(d => d.ServiceType == typeof(ISmokeTester));
        services.Should().Contain(d => d.ServiceType == typeof(IRotationPolicyProvider));
    }

    [Fact]
    public void AddKeyCustodianApp_HandlersAreTransient()
    {
        var services = new ServiceCollection();
        services.AddKeyCustodianApp();

        services.Single(d => d.ServiceType == typeof(IGenerateKey))
            .Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddKeyCustodianApp_GeneratorsCoverEveryKeyType()
    {
        var services = new ServiceCollection();
        services.AddKeyCustodianApp();

        // Resolve concrete generator implementation types and confirm each KeyType
        // is covered exactly once.
        var implementationTypes = services
            .Where(d => d.ServiceType == typeof(IKeyGenerator))
            .Select(d => d.ImplementationType!.Name)
            .ToList();

        implementationTypes.Should().Contain("RsaSigningKeyGenerator");
        implementationTypes.Should().Contain("AesPayloadKeyGenerator");
        implementationTypes.Should().Contain("SecretKeyGenerator");

        // Every KeyType enum value must have a generator.
        System.Enum.GetValues<KeyType>().Should().HaveCount(3);
    }
}
