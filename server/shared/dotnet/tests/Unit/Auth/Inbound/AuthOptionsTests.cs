// -----------------------------------------------------------------------
// <copyright file="AuthOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound;

using System;
using AwesomeAssertions;
using D2.Shared.Auth;
using Xunit;

public sealed class AuthOptionsTests
{
    [Fact]
    public void Construction_AllRequired_RoundTrips()
    {
        var options = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
        };

        options.Issuer.Should().Be(new Uri("https://edge.internal"));
        options.Audience.Should().Be("files");
        options.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ClockSkew_DefaultsTo30Seconds()
    {
        // Pin the default since the threshold matters for clock-drift
        // tolerance across services. A test failure here means the design
        // lock changed and downstream JwtValidator behavior shifts with it.
        var options = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "x",
        };

        options.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void WithExpression_OverridesClockSkew()
    {
        var baseline = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "x",
        };
        var overridden = baseline with { ClockSkew = TimeSpan.FromSeconds(5) };

        overridden.ClockSkew.Should().Be(TimeSpan.FromSeconds(5));
        baseline.ClockSkew.Should().Be(
            TimeSpan.FromSeconds(30),
            "with-expression must not mutate baseline");
    }

    [Fact]
    public void Equality_SameValues_ProducesEqualOptions()
    {
        var a = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
        };
        var b = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
        };

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentAudience_NotEqual()
    {
        var a = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
        };
        var b = a with { Audience = "courier" };

        a.Should().NotBe(b);
    }

    [Fact]
    public void Jwks_TrustedRootCertificatePath_DefaultsToNull()
    {
        var options = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
        };

        options.Jwks.TrustedRootCertificatePath.Should().BeNull(
            "public-CA deployments leave the path empty (system trust store only)");
    }

    [Fact]
    public void Jwks_WithExpression_OverridesTrustedRootCertificatePath()
    {
        var baseline = new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "x",
        };
        var overridden = baseline with
        {
            Jwks = baseline.Jwks with
            {
                TrustedRootCertificatePath = "/secrets/keycustodian/ca-root.crt",
            },
        };

        overridden.Jwks.TrustedRootCertificatePath
            .Should().Be("/secrets/keycustodian/ca-root.crt");
        baseline.Jwks.TrustedRootCertificatePath.Should().BeNull(
            "with-expression must not mutate baseline");
    }
}
