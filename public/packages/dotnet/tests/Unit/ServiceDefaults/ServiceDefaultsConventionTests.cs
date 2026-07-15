// -----------------------------------------------------------------------
// <copyright file="ServiceDefaultsConventionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.ServiceDefaults;

using AwesomeAssertions;
using D2.Shared.ServiceDefaults;
using Xunit;

/// <summary>
/// Convention pins for <see cref="D2.Shared.ServiceDefaults"/> — sealed-by-default
/// for non-static public types, static-by-convention for the extension classes
/// + the empty constants placeholder.
/// </summary>
public sealed class ServiceDefaultsConventionTests
{
    [Fact]
    public void D2ServiceDefaultsOptions_IsSealed()
    {
        typeof(D2ServiceDefaultsOptions).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void D2ServiceDefaultsConstants_IsStaticAbstractSealed()
    {
        var t = typeof(D2ServiceDefaultsConstants);

        // Static class compiles to abstract + sealed in the IL.
        t.IsAbstract.Should().BeTrue();
        t.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void ServiceDefaultsServiceCollectionExtensions_IsStaticAbstractSealed()
    {
        var t = typeof(ServiceDefaultsServiceCollectionExtensions);
        t.IsAbstract.Should().BeTrue();
        t.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void WebApplicationServiceDefaultsExtensions_IsStaticAbstractSealed()
    {
        var t = typeof(WebApplicationServiceDefaultsExtensions);
        t.IsAbstract.Should().BeTrue();
        t.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Assembly_HasNoNonStaticPublicClassesOtherThanOptions()
    {
        var assembly = typeof(D2ServiceDefaultsOptions).Assembly;

        var nonStaticPublicClasses = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsPublic: true })
            .Where(t => !(t.IsAbstract && t.IsSealed)) // exclude static classes
            .Where(t => t != typeof(D2ServiceDefaultsOptions))
            .Select(t => t.FullName)
            .ToList();

        nonStaticPublicClasses.Should().BeEmpty(
            "the aggregator owns ZERO logic — every public surface is either "
            + "a static extension class, the constants placeholder, or the "
            + "sealed options class");
    }
}
