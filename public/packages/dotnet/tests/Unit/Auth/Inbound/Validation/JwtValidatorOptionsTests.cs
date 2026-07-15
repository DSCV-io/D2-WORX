// -----------------------------------------------------------------------
// <copyright file="JwtValidatorOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Validation;

using AwesomeAssertions;
using D2.Shared.Auth.Validation;
using Xunit;

public sealed class JwtValidatorOptionsTests
{
    [Fact]
    public void Defaults_AreSensibleAndPinned()
    {
        var options = new JwtValidatorOptions();

        // Pin literal default values per §1.18 — RS256-only, both
        // require-flags true. Changing any of these is a security-posture
        // change that must be a deliberate code edit + test edit, not a silent
        // default flip.
        options.RequireSessionIdClaim.Should().BeTrue();
        options.RequireExpirationTime.Should().BeTrue();
        options.ValidAlgorithms.Should().ContainSingle().Which.Should().Be("RS256");
    }

    [Fact]
    public void ParameterizedCtor_OverridesRequireSessionIdClaim()
    {
        var options = new JwtValidatorOptions(requireSessionIdClaim: false);

        options.RequireSessionIdClaim.Should().BeFalse();
        options.RequireExpirationTime.Should().BeTrue();
        options.ValidAlgorithms.Should().ContainSingle().Which.Should().Be("RS256");
    }

    [Fact]
    public void ParameterizedCtor_OverridesRequireExpirationTime()
    {
        var options = new JwtValidatorOptions(requireExpirationTime: false);

        options.RequireExpirationTime.Should().BeFalse();
        options.RequireSessionIdClaim.Should().BeTrue();
        options.ValidAlgorithms.Should().ContainSingle().Which.Should().Be("RS256");
    }

    [Fact]
    public void ParameterizedCtor_OverridesValidAlgorithms()
    {
        var options = new JwtValidatorOptions(
            validAlgorithms: new[] { "RS256", "RS384" });

        options.ValidAlgorithms.Should().BeEquivalentTo(new[] { "RS256", "RS384" });
        options.RequireSessionIdClaim.Should().BeTrue();
        options.RequireExpirationTime.Should().BeTrue();
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "RedundantArgumentDefaultValue",
        Justification = "Pinning the all-nulls contract is the intent of this test.")]
    public void ParameterlessCtor_AndAllNullCtor_YieldEqualValues()
    {
        var defaults = new JwtValidatorOptions();
        var allNulls = new JwtValidatorOptions(null, null, null);

        // Record equality across both instances pins the contract that omitting
        // a parameter is identical to passing null.
        defaults.RequireSessionIdClaim.Should().Be(allNulls.RequireSessionIdClaim);
        defaults.RequireExpirationTime.Should().Be(allNulls.RequireExpirationTime);
        defaults.ValidAlgorithms.Should().BeEquivalentTo(allNulls.ValidAlgorithms);
    }

    [Fact]
    public void WithExpression_OverridesRequireSessionIdClaim()
    {
        var baseline = new JwtValidatorOptions();
        var overridden = baseline with { RequireSessionIdClaim = false };

        overridden.RequireSessionIdClaim.Should().BeFalse();
        baseline.RequireSessionIdClaim.Should().BeTrue();
    }

    [Fact]
    public void WithExpression_OverridesValidAlgorithms()
    {
        var baseline = new JwtValidatorOptions();
        var overridden = baseline with { ValidAlgorithms = new[] { "ES256" } };

        overridden.ValidAlgorithms.Should().ContainSingle().Which.Should().Be("ES256");
        baseline.ValidAlgorithms.Should().ContainSingle().Which.Should().Be("RS256");
    }

    [Fact]
    public void Equality_SameValues_ProducesEqualOptions()
    {
        var a = new JwtValidatorOptions();
        var b = new JwtValidatorOptions();

        // Records compare structurally — same property values → equal.
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentRequireSessionIdClaim_NotEqual()
    {
        var a = new JwtValidatorOptions();
        var b = a with { RequireSessionIdClaim = false };

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentRequireExpirationTime_NotEqual()
    {
        var a = new JwtValidatorOptions();
        var b = a with { RequireExpirationTime = false };

        a.Should().NotBe(b);
    }
}
