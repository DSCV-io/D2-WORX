// -----------------------------------------------------------------------
// <copyright file="AddD2GeoDefaultTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.DI;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions.NameResolution;
using DcsvIo.D2.Geo.Default;
using DcsvIo.D2.Geo.Default.NameResolution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Coverage for the <see cref="DependencyInjection.AddD2GeoDefault"/>
/// composition root extension.
/// </summary>
public sealed class AddD2GeoDefaultTests
{
    // §1.2 category: State-lifecycle — DI composition.
    [Fact]
    public void AddD2GeoDefault_RegistersIGeoNameResolver()
    {
        var services = new ServiceCollection();

        services.AddD2GeoDefault();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IGeoNameResolver>();

        resolver.Should().NotBeNull();
        resolver.Should().BeOfType<DefaultGeoNameResolver>();
    }

    [Fact]
    public void AddD2GeoDefault_RegistersAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddD2GeoDefault();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IGeoNameResolver>();
        var second = provider.GetRequiredService<IGeoNameResolver>();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AddD2GeoDefault_ReturnsSameCollectionForChaining()
    {
        var services = new ServiceCollection();
        var returned = services.AddD2GeoDefault();
        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddD2GeoDefault_CalledTwice_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddD2GeoDefault();
        var act = () => services.AddD2GeoDefault();
        act.Should().NotThrow();

        // Subsequent registration may add a second descriptor; the resolved
        // service still satisfies the contract (last registration wins by
        // default for AddSingleton).
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IGeoNameResolver>().Should().NotBeNull();
    }
}
