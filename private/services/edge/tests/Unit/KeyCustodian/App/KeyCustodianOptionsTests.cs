// -----------------------------------------------------------------------
// <copyright file="KeyCustodianOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

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
    // IssuerBaseUrl rejection — two layers:
    //   1. The ValidateDataAnnotations startup path (Validator.TryValidateObject):
    //      [Required] (AllowEmptyStrings=false, so its rule is Trim().Length != 0)
    //      rejects null, empty, AND whitespace; [MinLength(1)] also rejects empty.
    //   2. IValidatableObject.Validate()'s explicit Falsey() defense-in-depth guard,
    //      which TryValidateObject short-circuits past once layer 1 fails, so it is
    //      pinned by calling Validate() directly.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \t ")]
    public void TryValidateObject_BlankIssuerBaseUrl_FailsValidation(string? issuerBaseUrl)
    {
        // Pins the startup-path contract: ValidateOnStart uses ValidateDataAnnotations,
        // which runs Validator.TryValidateObject. [Required]'s Trim() rule rejects null,
        // empty, and whitespace alike. Relaxing [Required] to AllowEmptyStrings = true (or
        // dropping it) would let a blank issuer boot — this assertion would then fail.
        var options = new KeyCustodianOptions
        {
            IssuerBaseUrl = issuerBaseUrl!,
            Default = ValidDefaultPolicy(),
        };

        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        valid.Should().BeFalse(
            because: "a null, empty, or whitespace IssuerBaseUrl must fail validation");
        results.Should().Contain(
            r => r.MemberNames.Contains(nameof(KeyCustodianOptions.IssuerBaseUrl)),
            because: "the validation error must be attributed to IssuerBaseUrl");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Validate_BlankIssuerBaseUrl_YieldsIssuerBaseUrlResult(string? issuerBaseUrl)
    {
        // Pins the Falsey() defense-in-depth guard itself. TryValidateObject short-circuits
        // Validate() once [Required] rejects the value at property level, so a direct call
        // is the only way to reach the guard. Deleting the Falsey() block makes Validate()
        // yield no IssuerBaseUrl result and this assertion fails — a genuine
        // fails-without-the-guard regression pin.
        var options = new KeyCustodianOptions
        {
            IssuerBaseUrl = issuerBaseUrl!,
            Default = ValidDefaultPolicy(),
        };

        var results = options.Validate(new ValidationContext(options)).ToList();

        results.Should().Contain(
            r => r.MemberNames.Contains(nameof(KeyCustodianOptions.IssuerBaseUrl)),
            because: "the Falsey() guard must flag a blank IssuerBaseUrl on a direct Validate call");
    }

    [Fact]
    public void TryValidateObject_ValidIssuerBaseUrl_Passes()
    {
        // Positive case — a well-formed issuer with a valid default policy must pass, so the
        // blank-issuer guards above cannot regress into rejecting legitimate configuration.
        var options = new KeyCustodianOptions
        {
            IssuerBaseUrl = "https://edge.internal",
            Default = ValidDefaultPolicy(),
        };

        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            options, new ValidationContext(options), results, validateAllProperties: true);

        valid.Should().BeTrue(
            because: "a well-formed IssuerBaseUrl with a valid default policy is valid");
        results.Should().BeEmpty();
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

    // A valid default rotation policy so the Default-policy recursion in
    // KeyCustodianOptions.Validate() produces no noise results — leaving IssuerBaseUrl
    // as the only field under test in the blank-issuer cases.
    private static RotationPolicyOptions ValidDefaultPolicy() => new()
    {
        Cadence = TimeSpan.FromDays(30),
        Grace = TimeSpan.FromDays(2),
        SmokeSoak = TimeSpan.FromDays(1),
    };
}
