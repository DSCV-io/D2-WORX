// -----------------------------------------------------------------------
// <copyright file="AuthEndpointGuardServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Startup;

using AwesomeAssertions;
using D2.Shared.Auth.Startup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Unit tests for <see cref="AuthEndpointGuardServiceCollectionExtensions"/> —
/// verifies DI registration shape (startup filter present, idempotent on
/// double-call, null-arg guard).
/// </summary>
public sealed class AuthEndpointGuardServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2AuthEndpointGuard_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2AuthEndpointGuard();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2AuthEndpointGuard_ReturnsSameServicesForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddD2AuthEndpointGuard();

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2AuthEndpointGuard_RegistersOneStartupFilter()
    {
        var services = new ServiceCollection();

        services.AddD2AuthEndpointGuard();

        // The guard must be registered as an IStartupFilter (not IHostedService).
        // IStartupFilter runs during HTTP-pipeline construction — after the
        // WebApplication's DataSources are merged — which is the correct
        // lifecycle for endpoint-presence validation.
        var registrations = services
            .Where(d =>
                d.ServiceType == typeof(IStartupFilter) &&
                d.ImplementationType == typeof(AuthEndpointGuardStartupFilter))
            .ToList();

        registrations.Should().HaveCount(1);
    }

    [Fact]
    public void AddD2AuthEndpointGuard_CalledTwice_DoesNotDoubleRegister()
    {
        // Idempotency contract: TryAddEnumerable prevents duplicate entries.
        var services = new ServiceCollection();

        services.AddD2AuthEndpointGuard();
        services.AddD2AuthEndpointGuard();

        var registrations = services
            .Where(d =>
                d.ServiceType == typeof(IStartupFilter) &&
                d.ImplementationType == typeof(AuthEndpointGuardStartupFilter))
            .ToList();

        registrations.Should().HaveCount(
            1,
            "TryAddEnumerable must prevent double-registration on repeated calls");
    }

    [Fact]
    public void AddD2AuthEndpointGuard_RegisteredAsTransient()
    {
        // IStartupFilter instances are resolved once per pipeline build;
        // transient lifetime is the conventional registration for startup filters.
        var services = new ServiceCollection();

        services.AddD2AuthEndpointGuard();

        var descriptor = services.Single(d =>
            d.ServiceType == typeof(IStartupFilter) &&
            d.ImplementationType == typeof(AuthEndpointGuardStartupFilter));

        descriptor.Lifetime.Should().Be(
            ServiceLifetime.Transient,
            "startup filters are conventionally registered as transient");
    }
}
