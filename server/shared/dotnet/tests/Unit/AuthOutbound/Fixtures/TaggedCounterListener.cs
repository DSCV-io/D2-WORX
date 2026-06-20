// -----------------------------------------------------------------------
// <copyright file="TaggedCounterListener.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.Fixtures;

using System.Diagnostics.Metrics;
using D2.Shared.Auth.Outbound.Telemetry;

/// <summary>
/// Captures emitted counter measurements with their <c>outcome</c> tag value
/// for a named counter under the <see cref="OutboundTelemetry.METER_NAME"/>
/// meter. Tests assert on the captured measurements to verify telemetry
/// emission on each code path.
/// </summary>
internal sealed class TaggedCounterListener : IDisposable
{
    private readonly MeterListener r_listener = new();
    private readonly List<(string Outcome, long Value)> r_measurements = [];

    /// <summary>Initializes the listener for a named counter on the outbound meter.</summary>
    /// <param name="instrumentName">
    /// The instrument name (e.g. <c>d2.auth.outbound.token_exchange.requests</c>).
    /// </param>
    public TaggedCounterListener(string instrumentName)
    {
        r_listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OutboundTelemetry.METER_NAME &&
                instrument.Name == instrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        r_listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var outcome = "<none>";
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome" && tag.Value is string s)
                {
                    outcome = s;
                    break;
                }
            }

            lock (r_measurements)
                r_measurements.Add((outcome, value));
        });

        r_listener.Start();
    }

    /// <summary>Returns a snapshot of measurements captured so far.</summary>
    /// <returns>A copy of the captured (outcome, value) tuples.</returns>
    public List<(string Outcome, long Value)> Snapshot()
    {
        lock (r_measurements)
            return [.. r_measurements];
    }

    /// <inheritdoc/>
    public void Dispose() => r_listener.Dispose();
}
