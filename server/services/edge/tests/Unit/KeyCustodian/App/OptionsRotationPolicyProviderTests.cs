// -----------------------------------------------------------------------
// <copyright file="OptionsRotationPolicyProviderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Implementations.Policy;
using D2.Edge.KeyCustodian.App.Options;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;

/// <summary>
/// Tests for <see cref="OptionsRotationPolicyProvider"/>: default policy, per-
/// domain override precedence, and invalid-config → flagged failure (no throw).
/// </summary>
public sealed class OptionsRotationPolicyProviderTests
{
    [Fact]
    public void ForDomain_NoOverride_UsesDefaultPolicy()
    {
        var options = KcAppTestKit.BuildOptions();
        var provider = new OptionsRotationPolicyProvider(Options.Create(options));

        var result = provider.ForDomain(KeyDomain.Cookie);

        result.Success.Should().BeTrue();
        result.Data!.Cadence.Should().Be(Duration.FromHours(4));
        result.Data!.Grace.Should().Be(Duration.FromHours(2));
        result.Data!.SmokeSoak.Should().Be(Duration.FromHours(1));
    }

    [Fact]
    public void ForDomain_WithOverride_UsesDomainPolicy()
    {
        var options = KcAppTestKit.BuildOptions();
        options.Policies[KeyDomain.JWKS_SIGNING] = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromDays(30),
            Grace = TimeSpan.FromDays(2),
            SmokeSoak = TimeSpan.FromHours(6),
        };
        var provider = new OptionsRotationPolicyProvider(Options.Create(options));

        var result = provider.ForDomain(KeyDomain.JwksSigning);

        result.Success.Should().BeTrue();
        result.Data!.Cadence.Should().Be(Duration.FromDays(30));
    }

    [Fact]
    public void ForDomain_OtherDomain_StillUsesDefaultWhenOverrideExistsElsewhere()
    {
        var options = KcAppTestKit.BuildOptions();
        options.Policies[KeyDomain.JWKS_SIGNING] = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromDays(30),
            Grace = TimeSpan.FromDays(2),
            SmokeSoak = TimeSpan.FromHours(6),
        };
        var provider = new OptionsRotationPolicyProvider(Options.Create(options));

        provider.ForDomain(KeyDomain.Cookie).Data!.Cadence.Should().Be(Duration.FromHours(4));
    }

    [Fact]
    public void ForDomain_InvalidPolicy_NonPositiveDuration_ReturnsInvalidRotationPolicy()
    {
        var options = KcAppTestKit.BuildOptions();
        options.Default = new RotationPolicyOptions
        {
            Cadence = TimeSpan.Zero,
            Grace = TimeSpan.FromHours(1),
            SmokeSoak = TimeSpan.FromHours(1),
        };
        var provider = new OptionsRotationPolicyProvider(Options.Create(options));

        var result = provider.ForDomain(KeyDomain.Cookie);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_INVALID_ROTATION_POLICY");
    }

    [Fact]
    public void ForDomain_InvalidPolicy_CadenceShorterThanGracePlusSoak_ReturnsInvalidRotationPolicy()
    {
        var options = KcAppTestKit.BuildOptions();
        options.Default = new RotationPolicyOptions
        {
            Cadence = TimeSpan.FromHours(2),
            Grace = TimeSpan.FromHours(2),
            SmokeSoak = TimeSpan.FromHours(2),
        };
        var provider = new OptionsRotationPolicyProvider(Options.Create(options));

        provider.ForDomain(KeyDomain.Cookie).ErrorCode
            .Should().Be("KEYCUSTODIAN_INVALID_ROTATION_POLICY");
    }
}
