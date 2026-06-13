// -----------------------------------------------------------------------
// <copyright file="KeyCustodianInfraOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using System.ComponentModel.DataAnnotations;
using D2.Edge.KeyCustodian.Infra.Configuration;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Tests for <see cref="KeyCustodianInfraOptions"/>: the production-sane defaults,
/// the <c>[Range]</c> / <c>[Required]</c> validation rejections (including the
/// TimeSpan range), and an environment round-trip proving
/// <c>KEYCUSTODIAN_INFRA__*</c> binds.
/// </summary>
public sealed class KeyCustodianInfraOptionsTests
{
    [Fact]
    public void Defaults_AreProductionSane()
    {
        var options = new KeyCustodianInfraOptions();

        options.RotationCheckInterval.Should().Be(TimeSpan.FromMinutes(5));
        options.DbCommandTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void Validate_ValidOptions_Passes()
    {
        var options = new KeyCustodianInfraOptions
        {
            RootKeyPath = "/secrets/keycustodian",
            RotationCheckInterval = TimeSpan.FromMinutes(5),
            DbCommandTimeoutSeconds = 30,
        };

        TryValidate(options, out var results).Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyRootKeyPath_Fails(string rootKeyPath)
    {
        var options = ValidOptions();
        options.RootKeyPath = rootKeyPath;

        TryValidate(options, out _).Should().BeFalse();
    }

    [Fact]
    public void Validate_ZeroInterval_Fails()
    {
        var options = ValidOptions();
        options.RotationCheckInterval = TimeSpan.Zero;

        TryValidate(options, out _).Should().BeFalse();
    }

    [Fact]
    public void Validate_NegativeInterval_Fails()
    {
        var options = ValidOptions();
        options.RotationCheckInterval = TimeSpan.FromSeconds(-1);

        TryValidate(options, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_NonPositiveCommandTimeout_Fails(int seconds)
    {
        var options = ValidOptions();
        options.DbCommandTimeoutSeconds = seconds;

        TryValidate(options, out _).Should().BeFalse();
    }

    [Fact]
    public void Bind_FromEnvironmentStylePrefix_RoundTrips()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KEYCUSTODIAN_INFRA:RootKeyPath"] = "/var/keys",
                ["KEYCUSTODIAN_INFRA:RotationCheckInterval"] = "00:10:00",
                ["KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds"] = "45",
            })
            .Build();

        var options = new KeyCustodianInfraOptions();
        configuration.GetSection(KeyCustodianInfraOptions.SECTION).Bind(options);

        options.RootKeyPath.Should().Be("/var/keys");
        options.RotationCheckInterval.Should().Be(TimeSpan.FromMinutes(10));
        options.DbCommandTimeoutSeconds.Should().Be(45);
    }

    private static KeyCustodianInfraOptions ValidOptions() => new()
    {
        RootKeyPath = "/secrets/keycustodian",
        RotationCheckInterval = TimeSpan.FromMinutes(5),
        DbCommandTimeoutSeconds = 30,
    };

    private static bool TryValidate(
        KeyCustodianInfraOptions options, out List<ValidationResult> results)
    {
        results = [];
        var context = new ValidationContext(options);
        return Validator.TryValidateObject(options, context, results, validateAllProperties: true);
    }
}
