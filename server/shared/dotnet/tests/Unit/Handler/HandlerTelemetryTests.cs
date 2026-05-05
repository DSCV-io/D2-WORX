// -----------------------------------------------------------------------
// <copyright file="HandlerTelemetryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Handler;

using AwesomeAssertions;
using D2.Shared.Handler;
using Xunit;

// Same serialization as BaseHandlerTests — both subscribe to the static
// HandlerTelemetry telemetry surfaces.
[Collection("HandlerTelemetrySerial")]
public sealed class HandlerTelemetryTests
{
    [Fact]
    public void SourceName_MatchesAssemblyIdentifier()
    {
        HandlerTelemetry.SourceName.Should().Be("D2.Shared.Handler");
    }

    [Fact]
    public void ActivitySource_HasMatchingName()
    {
        HandlerTelemetry.ActivitySource.Name.Should().Be(HandlerTelemetry.SourceName);
    }

    [Fact]
    public void Meter_HasMatchingName()
    {
        HandlerTelemetry.Meter.Name.Should().Be(HandlerTelemetry.SourceName);
    }

    // ----------------------------------------------------------------------
    // Instrument identity — name + unit + description. These ARE the public
    // contract — dashboards / alerts key on these strings. A single typo is
    // a silent prod incident; document each in a dedicated test.
    // ----------------------------------------------------------------------

    [Fact]
    public void Invoked_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.Invoked.Name.Should().Be("d2.handler.invoked");
        HandlerTelemetry.Invoked.Unit.Should().Be("{calls}");
        HandlerTelemetry.Invoked.Description.Should().Be("Handler invocations attempted.");
    }

    [Fact]
    public void Succeeded_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.Succeeded.Name.Should().Be("d2.handler.succeeded");
        HandlerTelemetry.Succeeded.Unit.Should().Be("{calls}");
        HandlerTelemetry.Succeeded.Description.Should()
            .Be("Handler invocations that returned a successful D2Result.");
    }

    [Fact]
    public void Failed_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.Failed.Name.Should().Be("d2.handler.failed");
        HandlerTelemetry.Failed.Unit.Should().Be("{calls}");
        HandlerTelemetry.Failed.Description.Should()
            .Be("Handler invocations that returned a failed D2Result or threw.");
    }

    [Fact]
    public void Duration_NameAndUnitAndDescription_Match()
    {
        HandlerTelemetry.Duration.Name.Should().Be("d2.handler.duration");
        HandlerTelemetry.Duration.Unit.Should().Be("ms");
        HandlerTelemetry.Duration.Description.Should()
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
        using (var activity = HandlerTelemetry.ActivitySource.StartActivity("TestActivity"))
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

        HandlerTelemetry.Invoked.Add(1);

        collector.CountFor("d2.handler.invoked").Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void MeterListener_ReceivesHistogramRecord()
    {
        using var collector = new TestMetricCollector();

        HandlerTelemetry.Duration.Record(42.0);

        collector.ValuesFor("d2.handler.duration").Should().Contain(42.0);
    }

    [Fact]
    public void Instruments_AreSingletons_NotPerCallInstances()
    {
        // Adversarial: per OTel guidance every static field MUST be a single
        // instance reused for the lifetime of the process — re-creating
        // counters at runtime breaks any subscribed MeterListener. Verify
        // identity (not just equality) of each instrument.
        var a = HandlerTelemetry.Invoked;
        var b = HandlerTelemetry.Invoked;

        a.Should().BeSameAs(b);
        HandlerTelemetry.Succeeded.Should().BeSameAs(HandlerTelemetry.Succeeded);
        HandlerTelemetry.Failed.Should().BeSameAs(HandlerTelemetry.Failed);
        HandlerTelemetry.Duration.Should().BeSameAs(HandlerTelemetry.Duration);
        HandlerTelemetry.ActivitySource.Should().BeSameAs(HandlerTelemetry.ActivitySource);
        HandlerTelemetry.Meter.Should().BeSameAs(HandlerTelemetry.Meter);
    }
}
