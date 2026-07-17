// -----------------------------------------------------------------------
// <copyright file="TieredCacheLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Tiered;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for the tiered cache. Per
/// <c>docs/dev/rules.md §3.1</c>, no delegate accepts <see cref="Exception"/> —
/// the call sites carry only result-shaped strings (error codes, key, etc.),
/// so passing Exception would be both unnecessary and a leak vector.
/// </summary>
internal static partial class TieredCacheLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Tiered cache L1 invalidation handler failed for key {Key} "
            + "(errorCode {ErrorCode}).")]
    public static partial void L1InvalidationFailed(
        ILogger logger,
        string key,
        string errorCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Tiered cache L1 {Operation} failed for key/scope {KeyOrCount} "
            + "(errorCode {ErrorCode}); L2 write succeeded — operation reported "
            + "as Ok and L1 will rebuild on next read.")]
    public static partial void L1WriteFailedAfterL2Success(
        ILogger logger,
        string operation,
        string keyOrCount,
        string errorCode);
}
