// -----------------------------------------------------------------------
// <copyright file="BHASW.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Static class containing the activity source and metrics for the base handler.
/// </summary>
/// <remarks>
/// "BHASW" stands for "Base Handler Activity Source Wrapper".
/// </remarks>
internal static class BHASW
{
    /// <summary>
    /// The activity source for tracing handler operations.
    /// </summary>
    internal static readonly ActivitySource SR_ActivitySource
        = new("D2.Shared.Handler");

    /// <summary>
    /// The meter for handler metrics.
    /// </summary>
    internal static readonly Meter SR_Meter = new("D2.Shared.Handler");

    /// <summary>
    /// Histogram tracking handler execution duration in milliseconds.
    /// </summary>
    internal static readonly Histogram<double> SR_Duration =
        SR_Meter.CreateHistogram<double>("d2.handler.duration", "ms", "Handler execution duration");

    /// <summary>
    /// Counter tracking total handler invocations.
    /// </summary>
    internal static readonly Counter<long> SR_Invocations =
        SR_Meter.CreateCounter<long>("d2.handler.invocations", description: "Total handler invocations");

    /// <summary>
    /// Counter tracking total handler failures (non-success results).
    /// </summary>
    internal static readonly Counter<long> SR_Failures =
        SR_Meter.CreateCounter<long>("d2.handler.failures", description: "Total handler failures");

    /// <summary>
    /// Counter tracking total unhandled exceptions in handlers.
    /// </summary>
    internal static readonly Counter<long> SR_Exceptions =
        SR_Meter.CreateCounter<long>("d2.handler.exceptions", description: "Total unhandled exceptions");
}
