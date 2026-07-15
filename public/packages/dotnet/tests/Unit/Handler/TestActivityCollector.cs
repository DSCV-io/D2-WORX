// -----------------------------------------------------------------------
// <copyright file="TestActivityCollector.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DcsvIo.D2.Handler;

/// <summary>
/// Captures every <see cref="Activity"/> emitted by the
/// <see cref="HandlerTelemetry.SR_ActivitySource"/> during the lifetime of the
/// collector. Disposes the listener on <see cref="Dispose"/>.
/// </summary>
internal sealed class TestActivityCollector : IDisposable
{
    private readonly ActivityListener r_listener;

    public TestActivityCollector()
    {
        r_listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == HandlerTelemetry.SourceName,
            Sample = (ref _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => Activities.Add(a),
        };
        ActivitySource.AddActivityListener(r_listener);
    }

    public List<Activity> Activities { get; } = [];

    public Activity? Last => Activities.LastOrDefault();

    public void Dispose() => r_listener.Dispose();
}
