// -----------------------------------------------------------------------
// <copyright file="KeyCustodianOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Tests for <see cref="KeyCustodianOptions"/> and <see cref="RotationPolicyOptions"/>:
/// environment-variable binding round-trip (proves the KEYCUSTODIAN_APP__ section name
/// and IConfiguration hierarchy separator convention work end-to-end) and
/// <see cref="RangeAttribute"/> regression tests for the three <see cref="TimeSpan"/>
/// properties (pinning the fix for the <c>[Required]</c>-on-struct no-op).
/// </summary>
public sealed class KeyCustodianOptionsTests
{
    // -----------------------------------------------------------------------
    // Env-variable binding round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public void Bind_EnvironmentVariables_BindsAllPropertiesIncludingDashedPolicyKey()
    {
        // Arrange — set every documented env var.
        var vars = new[]
        {
            "KEYCUSTODIAN_APP__RSAKEYSIZEBITS",
            "KEYCUSTODIAN_APP__SECRETLENGTHBYTES",
            "KEYCUSTODIAN_APP__DEFAULT__CADENCE",
            "KEYCUSTODIAN_APP__DEFAULT__GRACE",
            "KEYCUSTODIAN_APP__DEFAULT__SMOKESOAK",
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__CADENCE",
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__GRACE",
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__SMOKESOAK",
        };

        Environment.SetEnvironmentVariable("KEYCUSTODIAN_APP__RSAKEYSIZEBITS", "2048");
        Environment.SetEnvironmentVariable("KEYCUSTODIAN_APP__SECRETLENGTHBYTES", "64");
        Environment.SetEnvironmentVariable("KEYCUSTODIAN_APP__DEFAULT__CADENCE", "30.00:00:00");
        Environment.SetEnvironmentVariable("KEYCUSTODIAN_APP__DEFAULT__GRACE", "2.00:00:00");
        Environment.SetEnvironmentVariable("KEYCUSTODIAN_APP__DEFAULT__SMOKESOAK", "1.00:00:00");
        Environment.SetEnvironmentVariable(
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__CADENCE", "7.00:00:00");
        Environment.SetEnvironmentVariable(
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__GRACE", "4.00:00:00");
        Environment.SetEnvironmentVariable(
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__SMOKESOAK", "0.02:00:00");

        try
        {
            // Act
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var options = config.GetSection(KeyCustodianOptions.SECTION).Get<KeyCustodianOptions>();

            // Assert — scalar fields
            options.Should().NotBeNull();
            options.RsaKeySizeBits.Should().Be(2048);
            options.SecretLengthBytes.Should().Be(64);

            // Assert — Default policy
            options.Default.Cadence.Should().Be(TimeSpan.FromDays(30));
            options.Default.Grace.Should().Be(TimeSpan.FromDays(2));
            options.Default.SmokeSoak.Should().Be(TimeSpan.FromDays(1));

            // Assert — dashed Policies key.
            // IConfiguration's env-var provider preserves the segment as-is from
            // the environment variable name; on Windows env vars are upper-cased by
            // convention so the produced dictionary key is "JWKS-SIGNING".
            // The Policies dictionary uses StringComparer.Ordinal, so we look up
            // case-insensitively against the actual keys to discover the produced casing.
            var policy_key = options.Policies.Keys
                .FirstOrDefault(k => k.Equals("JWKS-SIGNING", StringComparison.OrdinalIgnoreCase));
            policy_key.Should().NotBeNull(
                "env-variable segment JWKS-SIGNING must bind as a Policies dictionary entry");

            var signing_policy = options.Policies[policy_key];
            signing_policy.Cadence.Should().Be(TimeSpan.FromDays(7));
            signing_policy.Grace.Should().Be(TimeSpan.FromDays(4));
            signing_policy.SmokeSoak.Should().Be(TimeSpan.FromHours(2));
        }
        finally
        {
            foreach (var v in vars)
                Environment.SetEnvironmentVariable(v, null);
        }
    }

    [Fact]
    public void Bind_EmptyConfigSection_ReturnsNullOrDefaultOptions()
    {
        // A section that exists but is empty: binder returns null because no values
        // are present. Pin this behavior so we notice if the runtime changes it.
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var options = config
            .GetSection("KEYCUSTODIAN_APP__NONEXISTENT_EMPTY_SECTION_9z7")
            .Get<KeyCustodianOptions>();

        // IConfiguration returns null for a section with no keys — this is the
        // documented behavior of Get<T>() on an empty section.
        options.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // [Range] regression — the [Required]-on-non-nullable-struct no-op fix
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)] // TimeSpan.Zero
    [InlineData(-1)] // negative (-1 second)
    public void Validate_ZeroCadence_FailsRangeValidation(int seconds)
    {
        var options = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromSeconds(seconds),
            Grace = TimeSpan.FromHours(1),
            SmokeSoak = TimeSpan.FromHours(1),
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        valid.Should().BeFalse();
        results.Should().Contain(
            r => r.MemberNames.Contains(nameof(RotationPolicyOptions.Cadence)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ZeroGrace_FailsRangeValidation(int seconds)
    {
        var options = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromHours(1),
            Grace = TimeSpan.FromSeconds(seconds),
            SmokeSoak = TimeSpan.FromHours(1),
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        valid.Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(RotationPolicyOptions.Grace)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_ZeroSmokeSoak_FailsRangeValidation(int seconds)
    {
        var options = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromHours(1),
            Grace = TimeSpan.FromHours(1),
            SmokeSoak = TimeSpan.FromSeconds(seconds),
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        valid.Should().BeFalse();
        results.Should().Contain(
            r => r.MemberNames.Contains(nameof(RotationPolicyOptions.SmokeSoak)));
    }

    [Fact]
    public void Validate_SaneValues_PassesRangeValidation()
    {
        var options = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromDays(30),
            Grace = TimeSpan.FromDays(2),
            SmokeSoak = TimeSpan.FromDays(1),
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        valid.Should().BeTrue();
        results.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MinimumOneSecond_PassesRangeValidation()
    {
        var options = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromSeconds(1),
            Grace = TimeSpan.FromSeconds(1),
            SmokeSoak = TimeSpan.FromSeconds(1),
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        valid.Should().BeTrue();
        results.Should().BeEmpty();
    }
}
