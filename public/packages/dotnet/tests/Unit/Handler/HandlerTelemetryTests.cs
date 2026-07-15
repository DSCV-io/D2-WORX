// -----------------------------------------------------------------------
// <copyright file="HandlerTelemetryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using AwesomeAssertions;
using DcsvIo.D2.Handler;
using Xunit;

// Same serialization as BaseHandlerTests — both subscribe to the static
// HandlerTelemetry telemetry surfaces.
[Collection("HandlerTelemetrySerial")]
public sealed class HandlerTelemetryTests
{
    [Fact]
    public void SourceName_MatchesAssemblyIdentifier()
    {
        HandlerTelemetry.SourceName.Should().Be("DcsvIo.D2.Handler");
    }

    [Fact]
    public void ActivitySource_HasMatchingName()
    {
        HandlerTelemetry.SR_ActivitySource.Name.Should().Be(HandlerTelemetry.SourceName);
    }

    [Fact]
    public void Meter_HasMatchingName()
    {
        HandlerTelemetry.SR_Meter.Name.Should().Be(HandlerTelemetry.SourceName);
    }

    // ----------------------------------------------------------------------
    // Instrument identity — name + unit + description. These ARE the public
    // contract — dashboards / alerts key on these strings. A single typo is
    // a silent prod incident; document each in a dedicated test.
    // ----------------------------------------------------------------------

    [Fact]
    public void Invoked_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.SR_Invoked.Name.Should().Be("d2.handler.invoked");
        HandlerTelemetry.SR_Invoked.Unit.Should().Be("{calls}");
        HandlerTelemetry.SR_Invoked.Description.Should().Be("Handler invocations attempted.");
    }

    [Fact]
    public void Succeeded_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.SR_Succeeded.Name.Should().Be("d2.handler.succeeded");
        HandlerTelemetry.SR_Succeeded.Unit.Should().Be("{calls}");
        HandlerTelemetry.SR_Succeeded.Description.Should()
            .Be("Handler invocations that returned a successful D2Result.");
    }

    [Fact]
    public void Failed_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.SR_Failed.Name.Should().Be("d2.handler.failed");
        HandlerTelemetry.SR_Failed.Unit.Should().Be("{calls}");
        HandlerTelemetry.SR_Failed.Description.Should()
            .Be("Handler invocations that returned a failed D2Result or threw.");
    }

    [Fact]
    public void Duration_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.SR_Duration.Name.Should().Be("d2.handler.duration");
        HandlerTelemetry.SR_Duration.Unit.Should().Be("ms");
        HandlerTelemetry.SR_Duration.Description.Should()
            .Be("Handler invocation wall-clock duration in milliseconds.");
    }

    // ----------------------------------------------------------------------
    // Listener wiring — verify each instrument is observable from the
    // standard System.Diagnostics primitives, not just from our own code.
    // ----------------------------------------------------------------------

    [Fact]
    public void ActivityListener_ReceivesActivitiesStartedOnSource()
    {
        using var collector = new TestActivityCollector();

        // ReSharper disable once ExplicitCallerInfoArgument — explicit name is the test contract
        using (var activity = HandlerTelemetry.SR_ActivitySource.StartActivity("TestActivity"))
        {
            activity.Should().NotBeNull();
        }

        collector.Activities.Should().NotBeEmpty();
        collector.Last!.OperationName.Should().Be("TestActivity");
    }

    [Fact]
    public void MeterListener_ReceivesCounterIncrement()
    {
        using var collector = new TestMetricCollector();

        HandlerTelemetry.SR_Invoked.Add(1);

        collector.CountFor("d2.handler.invoked").Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void MeterListener_ReceivesHistogramRecord()
    {
        using var collector = new TestMetricCollector();

        HandlerTelemetry.SR_Duration.Record(42.0);

        collector.ValuesFor("d2.handler.duration").Should().Contain(42.0);
    }

    [Fact]
    public void Instruments_AreSingletons_NotPerCallInstances()
    {
        // Adversarial: per OTel guidance every static field MUST be a single
        // instance reused for the lifetime of the process — re-creating
        // counters at runtime breaks any subscribed MeterListener. Verify
        // identity (not just equality) of each instrument.
        var a = HandlerTelemetry.SR_Invoked;
        var b = HandlerTelemetry.SR_Invoked;

        a.Should().BeSameAs(b);
        HandlerTelemetry.SR_Succeeded.Should().BeSameAs(HandlerTelemetry.SR_Succeeded);
        HandlerTelemetry.SR_Failed.Should().BeSameAs(HandlerTelemetry.SR_Failed);
        HandlerTelemetry.SR_Duration.Should().BeSameAs(HandlerTelemetry.SR_Duration);
        HandlerTelemetry.SR_ActivitySource.Should().BeSameAs(HandlerTelemetry.SR_ActivitySource);
        HandlerTelemetry.SR_Meter.Should().BeSameAs(HandlerTelemetry.SR_Meter);
    }
}
