// -----------------------------------------------------------------------
// <copyright file="OutboundTelemetryTagsGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Outbound;

using AwesomeAssertions;
using D2.Shared.Auth.Outbound.Telemetry;
using Xunit;

/// <summary>
/// Per-public-VALUE pin for every constant emitted into
/// <see cref="OutboundTelemetryTags"/> by the telemetry-tags SrcGen.
/// </summary>
public sealed class OutboundTelemetryTagsGeneratedTests
{
    [Fact]
    public void ServiceIdentityFetches_TagAndOutcomeConstants_HavePinnedValues()
    {
        OutboundTelemetryTags.ServiceIdentityFetches.TAG_OUTCOME.Should().Be("outcome");
        OutboundTelemetryTags.ServiceIdentityFetches.Outcome.CACHE_HIT
            .Should().Be("cache_hit");
        OutboundTelemetryTags.ServiceIdentityFetches.Outcome.CACHE_HIT_AFTER_SINGLEFLIGHT
            .Should().Be("cache_hit_after_singleflight");
        OutboundTelemetryTags.ServiceIdentityFetches.Outcome.FETCH_SUCCESS
            .Should().Be("fetch_success");
        OutboundTelemetryTags.ServiceIdentityFetches.Outcome.FETCH_FAILURE
            .Should().Be("fetch_failure");
        OutboundTelemetryTags.ServiceIdentityFetches.Outcome.HTTP_FAILURE
            .Should().Be("http_failure");
        OutboundTelemetryTags.ServiceIdentityFetches.Outcome.DISCOVERY_FAILURE
            .Should().Be("discovery_failure");
    }

    [Fact]
    public void TokenExchangeRequests_TagAndOutcomeConstants_HavePinnedValues()
    {
        OutboundTelemetryTags.TokenExchangeRequests.TAG_OUTCOME.Should().Be("outcome");
        OutboundTelemetryTags.TokenExchangeRequests.Outcome.CACHE_HIT
            .Should().Be("cache_hit");
        OutboundTelemetryTags.TokenExchangeRequests.Outcome.CACHE_HIT_AFTER_SINGLEFLIGHT
            .Should().Be("cache_hit_after_singleflight");
        OutboundTelemetryTags.TokenExchangeRequests.Outcome.FETCH_SUCCESS
            .Should().Be("fetch_success");
        OutboundTelemetryTags.TokenExchangeRequests.Outcome.FETCH_FAILURE
            .Should().Be("fetch_failure");
        OutboundTelemetryTags.TokenExchangeRequests.Outcome.HTTP_FAILURE
            .Should().Be("http_failure");
        OutboundTelemetryTags.TokenExchangeRequests.Outcome.DISCOVERY_FAILURE
            .Should().Be("discovery_failure");
    }
}
