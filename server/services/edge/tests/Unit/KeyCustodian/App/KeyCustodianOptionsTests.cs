// -----------------------------------------------------------------------
// <copyright file="KeyCustodianOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Tests for <see cref="KeyCustodianOptions"/> and <see cref="RotationPolicyOptions"/>:
/// environment-variable binding round-trip (proves the KEYCUSTODIAN_APP__ section name
/// and IConfiguration hierarchy separator convention work end-to-end),
/// <see cref="RangeAttribute"/> regression tests for the three <see cref="TimeSpan"/>
/// properties (pinning the fix for the <c>[Required]</c>-on-struct no-op), and
/// <c>Policies</c> dictionary <c>OrdinalIgnoreCase</c> regression (pinning the fix
/// for env-var uppercase keys silently falling through to the default policy).
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
        string[] vars =
        [
            "KEYCUSTODIAN_APP__RSAKEYSIZEBITS",
            "KEYCUSTODIAN_APP__SECRETLENGTHBYTES",
            "KEYCUSTODIAN_APP__DEFAULT__CADENCE",
            "KEYCUSTODIAN_APP__DEFAULT__GRACE",
            "KEYCUSTODIAN_APP__DEFAULT__SMOKESOAK",
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__CADENCE",
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__GRACE",
            "KEYCUSTODIAN_APP__POLICIES__JWKS-SIGNING__SMOKESOAK",
        ];

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
            // The Policies dictionary uses StringComparer.OrdinalIgnoreCase, so we look up
            // case-insensitively against the actual keys to discover the produced casing.
            var policyKey = options.Policies.Keys
                .FirstOrDefault(k => k.Equals("JWKS-SIGNING", StringComparison.OrdinalIgnoreCase));
            policyKey.Should().NotBeNull(
                "env-variable segment JWKS-SIGNING must bind as a Policies dictionary entry");

            var signingPolicy = options.Policies[policyKey];
            signingPolicy.Cadence.Should().Be(TimeSpan.FromDays(7));
            signingPolicy.Grace.Should().Be(TimeSpan.FromDays(4));
            signingPolicy.SmokeSoak.Should().Be(TimeSpan.FromHours(2));
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

    // -----------------------------------------------------------------------
    // Policies dictionary OrdinalIgnoreCase regression
    // (pins the fix: env-var uppercase keys like "JWKS-SIGNING" must resolve
    // the same policy as the lowercase domain value "jwks-signing")
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("jwks-signing")]
    [InlineData("JWKS-SIGNING")]
    [InlineData("Jwks-Signing")]
    public void Policies_OrdinalIgnoreCase_SamePolicyForAllCasings(string key)
    {
        // Regression for the StringComparer.Ordinal bug: env vars inject uppercase
        // keys; domain lookups use lowercase; an Ordinal comparer silently falls
        // through to Default on any Windows deployment.
        var expected = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromDays(7),
            Grace = TimeSpan.FromDays(4),
            SmokeSoak = TimeSpan.FromHours(2),
        };

        var options = new KeyCustodianOptions();
        options.Policies[key] = expected;

        // Lookup with the lowercase domain value — must find the entry.
        options.Policies.TryGetValue("jwks-signing", out var found).Should().BeTrue(
            because: "OrdinalIgnoreCase comparer must resolve any casing to the same entry");
        found!.Cadence.Should().Be(expected.Cadence);
    }

    // -----------------------------------------------------------------------
    // IssuerBaseUrl whitespace-only guard (IValidatableObject.Validate regression)
    // [Required] rejects null; [MinLength(1)] rejects empty — but neither rejects
    // a whitespace-only value. The Falsey() guard in Validate() closes the gap:
    // "   " must fail validation rather than boot and serve issuer:"   ".
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \t ")]
    public void WhitespaceOnlyIssuerBaseUrl_FailsValidation(string whitespace)
    {
        // Arrange — valid rotation policy so the Default-policy recursion does
        // not produce noise findings; the only invalid field is IssuerBaseUrl.
        var options = new KeyCustodianOptions
        {
            IssuerBaseUrl = whitespace,
            Default = new RotationPolicyOptions
            {
                Cadence = TimeSpan.FromDays(30),
                Grace = TimeSpan.FromDays(2),
                SmokeSoak = TimeSpan.FromDays(1),
            },
        };

        var results = new List<ValidationResult>();

        // Act — TryValidateObject invokes IValidatableObject.Validate() (where the
        // Falsey() guard lives) in addition to checking data-annotation attributes.
        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        // Assert — validation must fail and name IssuerBaseUrl in the member list,
        // pinning the Falsey() guard: removing it would let whitespace through
        // [Required]+[MinLength(1)] and this assertion would fail.
        valid.Should().BeFalse(
            because: "a whitespace-only IssuerBaseUrl must fail IValidatableObject.Validate");
        results.Should().Contain(
            r => r.MemberNames.Contains(nameof(KeyCustodianOptions.IssuerBaseUrl)),
            because: "the validation error must be attributed to IssuerBaseUrl");
    }

    [Fact]
    public void Policies_OrdinalIgnoreCase_UppercaseKeyAndLowercaseLookupAreSameSlot()
    {
        // Adding the same key in two different casings must NOT create two entries
        // (would happen with Ordinal); with OrdinalIgnoreCase the second write overwrites.
        var options = new KeyCustodianOptions();
        options.Policies["JWKS-SIGNING"] = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromDays(7),
            Grace = TimeSpan.FromDays(4),
            SmokeSoak = TimeSpan.FromHours(2),
        };
        options.Policies["jwks-signing"] = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromDays(14),
            Grace = TimeSpan.FromDays(4),
            SmokeSoak = TimeSpan.FromHours(2),
        };

        options.Policies.Count.Should().Be(
            1,
            because: "OrdinalIgnoreCase treats both casings as the same key slot");
        options.Policies["JWKS-SIGNING"].Cadence.Should().Be(
            TimeSpan.FromDays(14),
            because: "the lowercase write should overwrite the uppercase entry");
    }
}
