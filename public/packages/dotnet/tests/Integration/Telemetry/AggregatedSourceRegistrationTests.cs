// -----------------------------------------------------------------------
// <copyright file="AggregatedSourceRegistrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Internal;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using Xunit;

/// <summary>
/// Forces 100% coverage that EVERY shipped lib's <c>ActivitySource</c> /
/// <c>Meter</c> in <see cref="AggregatedTelemetrySources"/> is actually
/// wired through the OTel SDK by emitting a synthetic activity / counter
/// per name and asserting capture in the in-memory exporter.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class AggregatedSourceRegistrationTests
{
    public static IEnumerable<TheoryDataRow<string>> ActivitySourceNames =>
        AggregatedTelemetrySources.SR_ActivitySourceNames
            .Select(name => new TheoryDataRow<string>(name));

    public static IEnumerable<TheoryDataRow<string>> MeterNames =>
        AggregatedTelemetrySources.SR_MeterNames
            .Select(name => new TheoryDataRow<string>(name));

    [Theory]
    [MemberData(nameof(ActivitySourceNames))]
    public async Task EveryAggregatedActivitySource_EmitsAndIsCaptured(string sourceName)
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();

        using (var source = new ActivitySource(sourceName))
        {
            // Explicit name arg overrides the [CallerMemberName] default —
            // the test name itself ("EveryAggregated...") is meaningless
            // as a span name; "aggregation-pin" is the contract token.
            // ReSharper disable once ExplicitCallerInfoArgument
            using var activity = source.StartActivity("aggregation-pin");
            activity?.SetTag("test", "true");
        }

        await TelemetryTestHostBuilder.ForceFlushAsync(handle.Host);

        handle.Activities.Snapshot(sourceName).Should().NotBeEmpty(
            because: $"AggregatedTelemetrySources lists '{sourceName}' "
            + "as wired via AddSource — emitting through that source should "
            + "reach the exporter.");
    }

    [Theory]
    [MemberData(nameof(MeterNames))]
    public async Task EveryAggregatedMeter_EmitsAndIsCaptured(string meterName)
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();

        using (var meter = new Meter(meterName))
        {
            var counter = meter.CreateCounter<long>("aggregation_pin_counter");
            counter.Add(1);
        }

        await TelemetryTestHostBuilder.ForceFlushAsync(handle.Host);

        handle.Metrics.Snapshot(meterName).Should().NotBeEmpty(
            because: $"AggregatedTelemetrySources lists '{meterName}' "
            + "as wired via AddMeter — emitting through that meter should "
            + "reach the exporter.");
    }
}
