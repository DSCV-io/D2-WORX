// -----------------------------------------------------------------------
// <copyright file="SimpleCounterListener.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthOutbound.Fixtures;

using System.Diagnostics.Metrics;
using DcsvIo.D2.Auth.Outbound.Telemetry;

/// <summary>
/// Captures the running total of an untagged counter on the outbound meter.
/// Tests assert <see cref="Total"/> &gt; 0 to verify the counter increments.
/// </summary>
internal sealed class SimpleCounterListener : IDisposable
{
    private readonly MeterListener r_listener = new();
    private long _total;

    /// <summary>Initializes the listener for a named counter on the outbound meter.</summary>
    /// <param name="instrumentName">The instrument name.</param>
    public SimpleCounterListener(string instrumentName)
    {
        r_listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == OutboundTelemetry.METER_NAME &&
                instrument.Name == instrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        r_listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            Interlocked.Add(ref _total, value));

        r_listener.Start();
    }

    /// <summary>Gets the running total of all observed measurements.</summary>
    public long Total => Interlocked.Read(ref _total);

    /// <inheritdoc/>
    public void Dispose() => r_listener.Dispose();
}
