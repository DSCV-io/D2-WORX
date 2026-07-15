// -----------------------------------------------------------------------
// <copyright file="AggregatedTelemetrySourcesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.Internal;

using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Internal;
using Xunit;

/// <summary>
/// Per-VALUE pinning for every aggregated source / meter name. The wire
/// values (literal strings <c>"DcsvIo.D2.Auth"</c> etc.) are the
/// long-lived contract operators query Tempo / Loki / Prometheus by
/// (e.g. <c>service="DcsvIo.D2.Auth"</c>); a value drift would silently
/// break those dashboards. Pinning here forces an explicit ack of any
/// rename.
/// </summary>
public sealed class AggregatedTelemetrySourcesTests
{
    [Fact]
    public void ActivitySourceNames_Count_IsFour()
    {
        AggregatedTelemetrySources.SR_ActivitySourceNames.Should().HaveCount(4);
    }

    [Theory]
    [InlineData("DcsvIo.D2.Handler")]
    [InlineData("DcsvIo.D2.Auth")]
    [InlineData("DcsvIo.D2.Auth.Outbound")]
    [InlineData("DcsvIo.D2.Messaging.RabbitMq")]
    public void ActivitySourceNames_ContainsExpectedLiteral(string expected)
    {
        AggregatedTelemetrySources.SR_ActivitySourceNames.Should().Contain(expected);
    }

    [Fact]
    public void ActivitySourceNames_NoDuplicates()
    {
        var names = AggregatedTelemetrySources.SR_ActivitySourceNames;

        names.Distinct(StringComparer.Ordinal).Count().Should().Be(names.Count);
    }

    [Fact]
    public void MeterNames_Count_IsSix()
    {
        AggregatedTelemetrySources.SR_MeterNames.Should().HaveCount(6);
    }

    [Theory]
    [InlineData("DcsvIo.D2.Handler")]
    [InlineData("DcsvIo.D2.Auth")]
    [InlineData("DcsvIo.D2.Auth.Outbound")]
    [InlineData("DcsvIo.D2.Messaging.RabbitMq")]
    [InlineData("DcsvIo.D2.Caching.Distributed.Redis")]
    [InlineData("DcsvIo.D2.Caching.Local")]
    public void MeterNames_ContainsExpectedLiteral(string expected)
    {
        AggregatedTelemetrySources.SR_MeterNames.Should().Contain(expected);
    }

    [Fact]
    public void MeterNames_NoDuplicates()
    {
        var names = AggregatedTelemetrySources.SR_MeterNames;

        names.Distinct(StringComparer.Ordinal).Count().Should().Be(names.Count);
    }

    [Fact]
    public void EveryActivitySourceName_AlsoAppearsInMeterNames()
    {
        // Current shape: every lib that publishes an ActivitySource also
        // publishes a Meter under the same name (the source-of-truth lib
        // class declares a single SOURCE_NAME constant used for both).
        // Cache libs add Meter-only entries on top — pinning the
        // ActivitySource ⊂ Meter relationship keeps the aggregation table
        // honest.
        var meters = new HashSet<string>(
            AggregatedTelemetrySources.SR_MeterNames,
            StringComparer.Ordinal);

        foreach (var sourceName in AggregatedTelemetrySources.SR_ActivitySourceNames)
            meters.Should().Contain(sourceName);
    }
}
