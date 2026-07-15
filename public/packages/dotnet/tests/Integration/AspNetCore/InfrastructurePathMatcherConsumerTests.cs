// -----------------------------------------------------------------------
// <copyright file="InfrastructurePathMatcherConsumerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.AspNetCore;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using Microsoft.AspNetCore.Http;
using Xunit;

/// <summary>
/// Regression tests pinning the consumer-swap behavior of
/// <see cref="InfrastructurePathMatcher"/>: per-lib internal duplicates
/// collapsed into the canonical public matcher;
/// <c>DcsvIo.D2.Logging</c> + <c>DcsvIo.D2.Telemetry</c> consume the
/// same matcher.
/// </summary>
/// <remarks>
/// These tests pin the wire contract that the path set
/// (<see cref="D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS"/>)
/// stays aligned across the three consumers — the bypass middleware
/// (this lib), the Logging request-logging level callback, and the
/// Telemetry AspNetCore-instrumentation Filter callback. A divergence
/// would surface as one consumer treating <c>/health</c> as
/// infrastructure while another doesn't — exactly the cross-lib drift the
/// consolidation eliminates.
/// </remarks>
public sealed class InfrastructurePathMatcherConsumerTests
{
    [Fact]
    public void DefaultPathSet_AlignedAcrossConsumers()
    {
        // The default infrastructure-path list lives in
        // D2AspNetCoreConstants and is what every consumer's default
        // points at. Confirms the centralization didn't accidentally
        // diverge a per-lib literal.
        D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS
            .Should().Equal("/health", "/alive", "/metrics", "/.well-known");
    }

    [Fact]
    public void LoggingDefault_PointsToCanonicalConstants()
    {
        // DcsvIo.D2.Logging.D2LoggingOptions.InfrastructurePaths defaults
        // to the same list (verified via Logging's own existing tests).
        // Cross-confirm by checking both lists are sequence-equal.
        var loggingDefault = new DcsvIo.D2.Logging.D2LoggingOptions().InfrastructurePaths;
        loggingDefault.Should().Equal(D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS);
    }

    [Fact]
    public void TelemetryDefault_PointsToCanonicalConstants()
    {
        var telemetryDefault = new DcsvIo.D2.Telemetry.D2TelemetryOptions()
            .InstrumentationExcludedPaths;
        telemetryDefault.Should().Equal(D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS);
    }

    [Fact]
    public void PostConsolidation_HealthPathStillMatches()
    {
        // Direct pin of the public matcher's behavior — fail-fast if a
        // consumer-swap somehow regresses the matcher's segment-boundary
        // semantics.
        InfrastructurePathMatcher.IsInfrastructurePath(
            new PathString("/health"),
            D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS)
            .Should().BeTrue();
    }
}
