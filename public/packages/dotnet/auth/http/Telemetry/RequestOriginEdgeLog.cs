// -----------------------------------------------------------------------
// <copyright file="RequestOriginEdgeLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Http.Telemetry;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="LoggerMessage"/>-compiled log delegates for the Edge-inbound
/// establishment middleware. Compiled once at type-load (no allocations / format-
/// string parsing at the call site) per CA1848.
/// </summary>
/// <remarks>
/// <strong>PII discipline</strong>: NO delegate accepts an <see cref="Exception"/>
/// and the only string parameter is a non-PII workload service label (e.g. <c>edge</c>);
/// the started call-path is summarized by its entry COUNT, never its contents.
/// </remarks>
// §5.6 carve-out: [LoggerMessage] partial methods cannot be declared inside a
// C# 14 extension(...) block (compiler-syntactic restriction), so the block form
// is categorically inapplicable here; instance-extension style is correct.
internal static partial class RequestOriginEdgeLog
{
    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Debug,
        Message = "Edge-inbound call-path started at {SelfServiceId}; call-path now has "
                + "{HopCount} entries.")]
    public static partial void CallPathStarted(
        this ILogger logger,
        int hopCount,
        string selfServiceId);
}
