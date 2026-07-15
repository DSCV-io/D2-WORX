// -----------------------------------------------------------------------
// <copyright file="InfrastructurePathMatcherTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Http;
using Xunit;

/// <summary>
/// Adversarial coverage of <see cref="InfrastructurePathMatcher"/> — the
/// canonical path matcher consumed by Logging, Telemetry, and
/// AspNetCore's bypass middleware. Every prefix; sub-path; segment-
/// boundary discipline; case insensitivity; defensive empty / null
/// guards; per-entry whitespace skip.
/// </summary>
public sealed class InfrastructurePathMatcherTests
{
    private static readonly IReadOnlyList<string> sr_defaultPaths =
        D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS;

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    [InlineData("/metrics")]
    [InlineData("/.well-known")]
    public void IsInfrastructurePath_CanonicalPath_ReturnsTrue(string path)
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString(path), sr_defaultPaths);

        match.Should().BeTrue();
    }

    [Fact]
    public void IsInfrastructurePath_SubPathUnderHealth_ReturnsTrue()
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health/db"), sr_defaultPaths);

        match.Should().BeTrue();
    }

    [Fact]
    public void IsInfrastructurePath_OidcDiscoveryUnderWellKnown_ReturnsTrue()
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/.well-known/openid-configuration"), sr_defaultPaths);

        match.Should().BeTrue();
    }

    [Fact]
    public void IsInfrastructurePath_ApiPath_ReturnsFalse()
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/api/foo"), sr_defaultPaths);

        match.Should().BeFalse();
    }

    [Fact]
    public void IsInfrastructurePath_HealthZ_NotMatched_BecauseHealthIsSegmentBoundary()
    {
        // /healthz must NOT match prefix /health — segment-boundary discipline
        // (PathString.StartsWithSegments respects segment boundaries).
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/healthz"), sr_defaultPaths);

        match.Should().BeFalse();
    }

    [Fact]
    public void IsInfrastructurePath_ApiHealth_NotMatched_BecauseHealthIsPrefixOnly()
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/api/health"), sr_defaultPaths);

        match.Should().BeFalse();
    }

    [Fact]
    public void IsInfrastructurePath_UpperCase_MatchesCaseInsensitively()
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/HEALTH"), sr_defaultPaths);

        match.Should().BeTrue();
    }

    [Fact]
    public void IsInfrastructurePath_EmptyPathString_ReturnsFalse()
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            PathString.Empty, sr_defaultPaths);

        match.Should().BeFalse();
    }

    [Fact]
    public void IsInfrastructurePath_NullPathsList_ReturnsFalse()
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health"), null);

        match.Should().BeFalse();
    }

    [Fact]
    public void IsInfrastructurePath_EmptyPathsList_ReturnsFalse()
    {
        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health"), Array.Empty<string>());

        match.Should().BeFalse();
    }

    [Fact]
    public void IsInfrastructurePath_CustomPathsList_OverridesDefaults()
    {
        var custom = new[] { "/internal" };

        var matchHealth = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health"), custom);
        var matchInternal = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/internal"), custom);

        matchHealth.Should().BeFalse();
        matchInternal.Should().BeTrue();
    }

    [Fact]
    public void IsInfrastructurePath_PathsListContainsWhitespaceEntry_SkipsAndContinues()
    {
        var paths = new[] { "   ", "/health" };

        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health"), paths);

        match.Should().BeTrue();
    }

    [Fact]
    public void IsInfrastructurePath_PathsListContainsEmptyEntry_SkipsAndContinues()
    {
        var paths = new[] { string.Empty, "/health" };

        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health"), paths);

        match.Should().BeTrue();
    }

    [Fact]
    public void IsInfrastructurePath_PathsListContainsNullEntry_SkipsAndContinues()
    {
        string?[] paths = [null, "/health"];

        var match = InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health"), paths!);

        match.Should().BeTrue();
    }
}
