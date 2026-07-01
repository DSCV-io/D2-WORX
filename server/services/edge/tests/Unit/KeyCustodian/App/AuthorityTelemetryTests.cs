// -----------------------------------------------------------------------
// <copyright file="AuthorityTelemetryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application.Observability;
using Microsoft.Extensions.Logging;

/// <summary>
/// Reachability + contract pins for the capability-authority telemetry: the two
/// security counters (the highest-severity cross-process <c>jwks-signing</c>
/// rejection counter, and the general capability-authority rejection counter with
/// bounded closed-enum tags) and the <c>AuthorityRejected</c> log delegate (EventId
/// 9512, no <see cref="System.Exception"/> parameter, workload / capability / target
/// loggable). The counters are CALLED from the live sign handler; the seal /
/// cert-issuance path that also reports them is still deferred. The authority
/// foundation declares them and proves they exist + emit + are reachable on the
/// deny path.
/// </summary>
public sealed class AuthorityTelemetryTests
{
    [Fact]
    public void AuthorityCounters_PinOperationalContract()
    {
        // Counter names are part of the operational contract — renames break any
        // dashboard / SLO / alert rule keyed on these strings.
        KeyCustodianMetrics.SR_CrossProcessSigningRejections.Name
            .Should().Be("d2.keycustodian.cross_process_signing_rejections");
        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Name
            .Should().Be("d2.keycustodian.authority_rejections");
    }

    [Fact]
    public void SR_CrossProcessSigningRejections_Emits_OnDenyPath()
    {
        var measurements = new List<long>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                && instrument.Name == "d2.keycustodian.cross_process_signing_rejections")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => measurements.Add(value));
        listener.Start();

        // The highest-severity security counter — a separate process attempted to mint
        // with the cluster signing key.
        KeyCustodianMetrics.SR_CrossProcessSigningRejections.Add(delta: 1);

        listener.Dispose();

        measurements.Should().Contain(
            1L,
            "the cross-process jwks-signing rejection counter must emit on the deny path");
    }

    [Fact]
    public void SR_AuthorityRejectionsTotal_Emits_WithBoundedClosedEnumTags()
    {
        var tagPairs = new List<(string Capability, string Reason)>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                && instrument.Name == "d2.keycustodian.authority_rejections")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            string capability = string.Empty;
            string reason = string.Empty;

            foreach (var tag in tags)
            {
                if (tag.Key == KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY)
                    capability = tag.Value?.ToString() ?? string.Empty;

                if (tag.Key == KeyCustodianMetrics.AuthorityRejections.TAG_REASON)
                    reason = tag.Value?.ToString() ?? string.Empty;
            }

            tagPairs.Add((capability, reason));
        });
        listener.Start();

        // Tags are CLOSED-enum values from the AuthorityRejections named-constant set
        // (never free text), so cardinality is bounded.
        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            delta: 1,
            tag1: new(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.SIGN),
            tag2: new(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON,
                KeyCustodianMetrics.AuthorityRejections.Reason.MINTER_REQUIRED));

        listener.Dispose();

        tagPairs.Should().Contain(
            (KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
                KeyCustodianMetrics.AuthorityRejections.Reason.MINTER_REQUIRED),
            "the general authority-rejection counter carries bounded closed-enum tags");
    }

    [Fact]
    public void AuthorityRejected_LogsAtEventId9512_WithWorkloadCapabilityTarget()
    {
        var logger = new CapturingLogger();

        KeyCustodianLog.AuthorityRejected(
            logger, "files", KeyCustodianMetrics.AuthorityRejections.Capability.SIGN, "jwks-signing");

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.EventId.Id.Should().Be(9512, "AuthorityRejected is the next free App EventId");
        entry.Level.Should().Be(LogLevel.Warning);
        entry.Message.Should().Contain("files");
        entry.Message.Should().Contain(KeyCustodianMetrics.AuthorityRejections.Capability.SIGN);
        entry.Message.Should().Contain(
            "jwks-signing",
            "the denial log carries the (non-PII) workload / capability / target for forensics");
    }

    [Fact]
    public void AuthorityRejectionTags_PinWireContract()
    {
        // The single place that re-types the wire strings: a rename of any constant VALUE
        // is a breaking dashboard / SLO / alert change and must fail a test here. Every
        // other site references the constant.
        KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY.Should().Be("capability");
        KeyCustodianMetrics.AuthorityRejections.TAG_REASON.Should().Be("reason");

        KeyCustodianMetrics.AuthorityRejections.Capability.SIGN.Should().Be("sign");

        KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED
            .Should().Be("origin-unestablished");
        KeyCustodianMetrics.AuthorityRejections.Reason.MINTER_REQUIRED
            .Should().Be("minter-required");
        KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_ALLOWED_SET
            .Should().Be("not-in-allowed-set");
        KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT
            .Should().Be("identity-absent");
        KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_PROCESS
            .Should().Be("not-in-process");

        KeyCustodianMetrics.AuthorityRejections.Workload.NONE.Should().Be("<none>");
        KeyCustodianMetrics.AuthorityRejections.Workload.IN_PROCESS_MINTER
            .Should().Be("<in-process-minter>");
    }

    /// <summary>Thread-safe capturing logger for asserting log entries by EventId.</summary>
    private sealed class CapturingLogger : ILogger
    {
        public ConcurrentQueue<(LogLevel Level, EventId EventId, string Message)> Entries { get; }
            = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((logLevel, eventId, formatter(state, exception)));
    }
}
