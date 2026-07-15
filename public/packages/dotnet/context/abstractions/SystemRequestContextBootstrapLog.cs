// -----------------------------------------------------------------------
// <copyright file="SystemRequestContextBootstrapLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="LoggerMessage"/>-compiled log delegates for the in-host system-worker
/// establishment boundary (<see cref="SystemRequestContextBootstrap"/>). Compiled once
/// at type-load (no allocations / format-string parsing at the call site) per CA1848.
/// </summary>
/// <remarks>
/// <strong>PII discipline</strong>: NO delegate accepts an <see cref="Exception"/>
/// and the only string parameter is a non-PII workload service label (the host's own
/// service id); the established call-path is summarized by its entry COUNT, never its
/// contents.
/// </remarks>
// §5.6 carve-out: [LoggerMessage] partial methods cannot be declared inside a
// C# 14 extension(...) block (compiler-syntactic restriction), so the block form
// is categorically inapplicable here; instance-extension style is correct.
internal static partial class SystemRequestContextBootstrapLog
{
    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Debug,
        Message = "System context established at {HostServiceId}; call-path now has "
                + "{HopCount} entries.")]
    public static partial void SystemContextEstablished(
        this ILogger logger,
        int hopCount,
        string hostServiceId);
}
