// -----------------------------------------------------------------------
// <copyright file="DependencyInjectionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using AwesomeAssertions;
using DcsvIo.D2.Validation;
using DcsvIo.D2.Validation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

/// <summary>
/// Verifies <c>AddValidation()</c> registers all three default validator
/// implementations as singletons, registrations are idempotent (TryAdd
/// semantics), and consumer-side overrides via <c>Replace</c> work.
/// </summary>
public sealed class DependencyInjectionTests
{
    // -------------------------------------------------------------------------
    // IEmailValidator
    // -------------------------------------------------------------------------

    [Fact]
    public void AddValidation_RegistersDefaultEmailValidator_AsSingleton()
    {
        var services = new ServiceCollection();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IEmailValidator>();

        validator.Should().BeOfType<DefaultEmailValidator>();
    }

    [Fact]
    public void AddValidation_EmailValidator_SingletonLifetime_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var v1 = provider.GetRequiredService<IEmailValidator>();
        var v2 = provider.GetRequiredService<IEmailValidator>();

        ReferenceEquals(v1, v2).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // IPhoneValidator
    // -------------------------------------------------------------------------

    [Fact]
    public void AddValidation_RegistersDefaultPhoneValidator_AsSingleton()
    {
        var services = new ServiceCollection();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IPhoneValidator>();

        validator.Should().BeOfType<DefaultPhoneValidator>();
    }

    [Fact]
    public void AddValidation_PhoneValidator_SingletonLifetime_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var v1 = provider.GetRequiredService<IPhoneValidator>();
        var v2 = provider.GetRequiredService<IPhoneValidator>();

        ReferenceEquals(v1, v2).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // IPostalCodeValidator
    // -------------------------------------------------------------------------

    [Fact]
    public void AddValidation_RegistersDefaultPostalCodeValidator_AsSingleton()
    {
        var services = new ServiceCollection();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IPostalCodeValidator>();

        validator.Should().BeOfType<DefaultPostalCodeValidator>();
    }

    [Fact]
    public void AddValidation_PostalCodeValidator_SingletonLifetime_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var v1 = provider.GetRequiredService<IPostalCodeValidator>();
        var v2 = provider.GetRequiredService<IPostalCodeValidator>();

        ReferenceEquals(v1, v2).Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Idempotency — calling twice does not double-register (TryAdd semantics)
    // -------------------------------------------------------------------------

    [Fact]
    public void AddValidation_CalledTwice_DoesNotDuplicateEmailValidatorRegistration()
    {
        var services = new ServiceCollection();
        services.AddValidation();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetServices<IEmailValidator>().ToList();

        resolved.Should().HaveCount(1);
    }

    [Fact]
    public void AddValidation_CalledTwice_DoesNotDuplicatePhoneValidatorRegistration()
    {
        var services = new ServiceCollection();
        services.AddValidation();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetServices<IPhoneValidator>().ToList();

        resolved.Should().HaveCount(1);
    }

    [Fact]
    public void AddValidation_CalledTwice_DoesNotDuplicatePostalCodeValidatorRegistration()
    {
        var services = new ServiceCollection();
        services.AddValidation();
        services.AddValidation();

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetServices<IPostalCodeValidator>().ToList();

        resolved.Should().HaveCount(1);
    }

    // -------------------------------------------------------------------------
    // Consumer-side overrides (Replace after AddValidation)
    // -------------------------------------------------------------------------

    [Fact]
    public void AddValidation_ConsumerOverrideEmail_TakesPrecedence()
    {
        var services = new ServiceCollection();
        services.AddValidation();
        services.Replace(ServiceDescriptor.Singleton<IEmailValidator, StubEmailValidator>());

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IEmailValidator>();

        validator.Should().BeOfType<StubEmailValidator>();
    }

    [Fact]
    public void AddValidation_ConsumerOverridePhone_TakesPrecedence()
    {
        var services = new ServiceCollection();
        services.AddValidation();
        services.Replace(ServiceDescriptor.Singleton<IPhoneValidator, StubPhoneValidator>());

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IPhoneValidator>();

        validator.Should().BeOfType<StubPhoneValidator>();
    }

    [Fact]
    public void AddValidation_ConsumerOverridePostalCode_TakesPrecedence()
    {
        var services = new ServiceCollection();
        services.AddValidation();
        services.Replace(
            ServiceDescriptor.Singleton<IPostalCodeValidator, StubPostalCodeValidator>());

        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IPostalCodeValidator>();

        validator.Should().BeOfType<StubPostalCodeValidator>();
    }

    // -------------------------------------------------------------------------
    // Stub implementations (test-internal; not exercise-worthy on their own)
    // -------------------------------------------------------------------------

    private sealed class StubEmailValidator : IEmailValidator
    {
        public DcsvIo.D2.Result.D2Result<string> Validate(string? email) =>
            DcsvIo.D2.Result.D2Result<string>.Ok(email ?? string.Empty);
    }

    private sealed class StubPhoneValidator : IPhoneValidator
    {
        public DcsvIo.D2.Result.D2Result<string> Validate(
            string? phone,
            DcsvIo.D2.Geo.Abstractions.CountryCode? defaultRegion = null) =>
                DcsvIo.D2.Result.D2Result<string>.Ok(phone ?? string.Empty);
    }

    private sealed class StubPostalCodeValidator : IPostalCodeValidator
    {
        public DcsvIo.D2.Result.D2Result<string> Validate(
            string? postalCode,
            DcsvIo.D2.Geo.Abstractions.CountryCode? countryCode = null) =>
                DcsvIo.D2.Result.D2Result<string>.Ok(postalCode ?? string.Empty);
    }
}
