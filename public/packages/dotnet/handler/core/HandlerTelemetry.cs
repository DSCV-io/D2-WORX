// -----------------------------------------------------------------------
// <copyright file="HandlerTelemetry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Static OpenTelemetry primitives shared by every <c>BaseHandler</c>
/// invocation. One <see cref="System.Diagnostics.ActivitySource"/> for spans;
/// one <see cref="Meter"/> with four instruments (invoked / succeeded / failed
/// counters and a duration histogram). Static per OTel official guidance —
/// avoids generic-static-field issues that surface when each closed
/// <c>BaseHandler&lt;TSelf, ...&gt;</c> would otherwise hold its own.
/// </summary>
/// <remarks>
/// Tests assert metric / span emission via
/// <c>System.Diagnostics.MeterListener</c> +
/// <c>System.Diagnostics.ActivityListener</c> rather than mocking the
/// telemetry surface.
/// </remarks>
public static class HandlerTelemetry
{
    /// <summary>OTel source name — matches the assembly's well-known identifier.</summary>
    public const string SourceName = "DcsvIo.D2.Handler";

    /// <summary>
    /// Static <see cref="System.Diagnostics.ActivitySource"/> for handler spans.
    /// </summary>
    public static readonly ActivitySource SR_ActivitySource = new(SourceName);

    /// <summary>Static <see cref="Meter"/> for handler metrics.</summary>
    public static readonly Meter SR_Meter = new(SourceName);

    /// <summary>Counter — incremented at every <c>HandleAsync</c> entry.</summary>
    public static readonly Counter<long> SR_Invoked =
        SR_Meter.CreateCounter<long>(
            "d2.handler.invoked",
            unit: "{calls}",
            description: "Handler invocations attempted.");

    /// <summary>
    /// Counter — incremented when <c>HandleAsync</c> returns a successful
    /// result.
    /// </summary>
    public static readonly Counter<long> SR_Succeeded =
        SR_Meter.CreateCounter<long>(
            "d2.handler.succeeded",
            unit: "{calls}",
            description: "Handler invocations that returned a successful D2Result.");

    /// <summary>
    /// Counter — incremented when <c>HandleAsync</c> returns a failure result
    /// OR throws.
    /// </summary>
    public static readonly Counter<long> SR_Failed =
        SR_Meter.CreateCounter<long>(
            "d2.handler.failed",
            unit: "{calls}",
            description: "Handler invocations that returned a failed D2Result or threw.");

    /// <summary>
    /// Histogram — records each handler's wall-clock duration in milliseconds.
    /// </summary>
    public static readonly Histogram<double> SR_Duration =
        SR_Meter.CreateHistogram<double>(
            "d2.handler.duration",
            unit: "ms",
            description: "Handler invocation wall-clock duration in milliseconds.");
}
