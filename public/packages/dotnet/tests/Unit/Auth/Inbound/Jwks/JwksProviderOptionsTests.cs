// -----------------------------------------------------------------------
// <copyright file="JwksProviderOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Jwks;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Jwks;
using Xunit;

public sealed class JwksProviderOptionsTests
{
    [Fact]
    public void Defaults_AreSensible()
    {
        var options = new JwksProviderOptions();

        options.RefreshCooldown.Should().Be(TimeSpan.FromSeconds(30));
        options.HttpRequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.CircuitBreakerFailureThreshold.Should().Be(5);
        options.CircuitBreakerCooldown.Should().Be(TimeSpan.FromSeconds(30));
        options.BackplaneChannelKey.Should().Be("d2.security.key-rotated:jwks");
    }

    [Fact]
    public void ParameterizedCtor_PartialOverrides_RetainOtherDefaults()
    {
        var options = new JwksProviderOptions(
            refreshCooldown: TimeSpan.FromMinutes(2),
            backplaneChannelKey: "custom.channel");

        options.RefreshCooldown.Should().Be(TimeSpan.FromMinutes(2));
        options.HttpRequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.CircuitBreakerFailureThreshold.Should().Be(5);
        options.CircuitBreakerCooldown.Should().Be(TimeSpan.FromSeconds(30));
        options.BackplaneChannelKey.Should().Be("custom.channel");
    }

    [Fact]
    public void ParameterizedCtor_OverridesCircuitBreakerKnobs()
    {
        var options = new JwksProviderOptions(
            circuitBreakerFailureThreshold: 10,
            circuitBreakerCooldown: TimeSpan.FromSeconds(120));

        options.CircuitBreakerFailureThreshold.Should().Be(10);
        options.CircuitBreakerCooldown.Should().Be(TimeSpan.FromSeconds(120));
        options.RefreshCooldown.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void WithExpression_OverridesCooldown()
    {
        var baseline = new JwksProviderOptions();
        var overridden = baseline with { RefreshCooldown = TimeSpan.FromMinutes(5) };

        overridden.RefreshCooldown.Should().Be(TimeSpan.FromMinutes(5));
        baseline.RefreshCooldown.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void WithExpression_OverridesHttpRequestTimeout()
    {
        var baseline = new JwksProviderOptions();
        var overridden = baseline with { HttpRequestTimeout = TimeSpan.FromSeconds(2) };

        overridden.HttpRequestTimeout.Should().Be(TimeSpan.FromSeconds(2));
        baseline.HttpRequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Equality_SameValues_ProducesEqualOptions()
    {
        var a = new JwksProviderOptions();
        var b = new JwksProviderOptions();

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentChannelKey_NotEqual()
    {
        var a = new JwksProviderOptions();
        var b = a with { BackplaneChannelKey = "different" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentTimeout_NotEqual()
    {
        var a = new JwksProviderOptions();
        var b = a with { HttpRequestTimeout = TimeSpan.FromSeconds(99) };

        a.Should().NotBe(b);
    }
}
