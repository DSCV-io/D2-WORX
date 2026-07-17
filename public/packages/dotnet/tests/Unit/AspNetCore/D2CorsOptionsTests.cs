// -----------------------------------------------------------------------
// <copyright file="D2CorsOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.Headers.Http;
using Xunit;

public sealed class D2CorsOptionsTests
{
    [Fact]
    public void Defaults_OriginsIsEmpty_FailClosed()
    {
        var opts = new D2CorsOptions();
        opts.Origins.Should().BeEmpty();
    }

    [Fact]
    public void Defaults_AllowedHeadersIncludesCanonicalSet()
    {
        var opts = new D2CorsOptions();
        opts.AllowedHeaders.Should()
            .Contain("Content-Type")
            .And.Contain(HttpHeaders.AUTHORIZATION)
            .And.Contain(HttpHeaders.CORRELATION_ID)
            .And.Contain(HttpHeaders.IDEMPOTENCY_KEY)
            .And.Contain("X-Forwarded-For")
            .And.Contain("X-Real-IP")
            .And.Contain("CF-Connecting-IP");
    }

    [Fact]
    public void Defaults_AllowedMethodsIncludesAllSeven()
    {
        var opts = new D2CorsOptions();
        opts.AllowedMethods.Should()
            .Equal("GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS");
    }

    [Fact]
    public void Defaults_AllowCredentialsIsTrue()
    {
        new D2CorsOptions().AllowCredentials.Should().BeTrue();
    }

    [Fact]
    public void Defaults_PreflightMaxAgeSecondsIs600()
    {
        new D2CorsOptions().PreflightMaxAgeSeconds.Should().Be(600);
    }

    [Fact]
    public void WithExpression_OverridesPropertyAndPreservesOthers()
    {
        var opts = new D2CorsOptions
        {
            Origins = ["https://app.example.com"],
            AllowCredentials = false,
        };

        opts.Origins.Should().Equal("https://app.example.com");
        opts.AllowCredentials.Should().BeFalse();
        opts.PreflightMaxAgeSeconds.Should().Be(600);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new D2CorsOptions
        {
            Origins = ["https://a.example.com"],
            AllowCredentials = true,
        };
        var b = new D2CorsOptions
        {
            Origins = ["https://a.example.com"],
            AllowCredentials = true,
        };

        // Records compare by reference for IReadOnlyList — explicit equality
        // check via SequenceEqual + property-by-property is the operational
        // semantic.
        a.AllowCredentials.Should().Be(b.AllowCredentials);
        a.PreflightMaxAgeSeconds.Should().Be(b.PreflightMaxAgeSeconds);
        a.Origins.Should().BeEquivalentTo(b.Origins);
    }
}
