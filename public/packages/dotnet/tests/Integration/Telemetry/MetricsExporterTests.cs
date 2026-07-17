// -----------------------------------------------------------------------
// <copyright file="MetricsExporterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Telemetry;

using System.Diagnostics.Metrics;
using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Internal;
using DcsvIo.D2.Tests.Integration.Telemetry.Infrastructure;
using OpenTelemetry.Metrics;
using Xunit;

/// <summary>
/// End-to-end pin that the metrics pipeline routes a Counter increment
/// through the meter provider to the in-memory exporter. Specifically
/// exercises the Handler meter (one of the aggregated names) so the
/// AggregatedTelemetrySources wiring is implicitly re-walked.
/// </summary>
[Collection("LogLoggerStaticState")]
public sealed class MetricsExporterTests
{
    [Fact]
    public async Task CounterIncrementOnAggregatedMeter_IsCapturedByExporter()
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();

        var meterName = AggregatedTelemetrySources.SR_MeterNames[0];
        using (var meter = new Meter(meterName))
        {
            var counter = meter.CreateCounter<long>("metrics_pipeline_pin");
            counter.Add(1);
        }

        await TelemetryTestHostBuilder.ForceFlushAsync(handle.Host);

        handle.Metrics.Snapshot(meterName).Should().NotBeEmpty(
            because: "the metrics pipeline should route the synthetic "
            + "counter increment to the in-memory exporter.");
    }

    [Fact]
    public async Task ForceFlush_CompletesWithinTimeout_AndDeliversMetrics()
    {
        await using var handle = await TelemetryTestHostBuilder.BuildAsync();

        var meterName = AggregatedTelemetrySources.SR_MeterNames[0];
        using (var meter = new Meter(meterName))
        {
            var counter = meter.CreateCounter<long>("metrics_flush_pin");
            counter.Add(7);
        }

        var meterProvider = handle.Host.Services.GetService(typeof(MeterProvider)) as MeterProvider;
        meterProvider.Should().NotBeNull();

        // OTel's ForceFlush can return false when readers report
        // "no work to do" rather than failure — so the meaningful
        // assertion is that the call completes promptly AND that the
        // exporter received the metric.
        meterProvider.ForceFlush(timeoutMilliseconds: 5000);

        handle.Metrics.Snapshot(meterName).Should().NotBeEmpty(
            because: "ForceFlush should drain the pending counter "
            + "increment to the exporter.");
    }
}
