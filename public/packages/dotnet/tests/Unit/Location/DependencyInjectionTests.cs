// -----------------------------------------------------------------------
// <copyright file="DependencyInjectionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using AwesomeAssertions;
using DcsvIo.D2.Location;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

/// <summary>
/// Verifies <c>AddD2Location()</c> registers the default postal-code
/// validator as a singleton, the registration is idempotent, and
/// consumer-side overrides via <c>Replace</c> work.
/// </summary>
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddD2Location_RegistersDefaultPostalCodeValidator_AsSingleton()
    {
        var services = new ServiceCollection();
        services.AddD2Location();

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IPostalCodeValidator>();

        validator.Should().BeOfType<DefaultPostalCodeValidator>();
    }

    [Fact]
    public void AddD2Location_SingletonLifetime_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddD2Location();

        var provider = services.BuildServiceProvider();
        var v1 = provider.GetRequiredService<IPostalCodeValidator>();
        var v2 = provider.GetRequiredService<IPostalCodeValidator>();

        ReferenceEquals(v1, v2).Should().BeTrue();
    }

    [Fact]
    public void AddD2Location_CalledTwice_DoesNotDuplicateRegistration()
    {
        var services = new ServiceCollection();
        services.AddD2Location();
        services.AddD2Location();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetServices<IPostalCodeValidator>().ToList();

        resolved.Should().HaveCount(1);
    }

    [Fact]
    public void AddD2Location_ConsumerOverride_TakesPrecedence()
    {
        var services = new ServiceCollection();
        services.AddD2Location();
        services.Replace(ServiceDescriptor.Singleton<IPostalCodeValidator, CustomValidator>());

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IPostalCodeValidator>();

        validator.Should().BeOfType<CustomValidator>();
    }

    private sealed class CustomValidator : IPostalCodeValidator
    {
        public DcsvIo.D2.Result.D2Result<string> Validate(
            string? postalCode,
            DcsvIo.D2.Geo.Abstractions.CountryCode? countryCode = null) =>
                DcsvIo.D2.Result.D2Result<string>.Ok(postalCode ?? string.Empty);
    }
}
