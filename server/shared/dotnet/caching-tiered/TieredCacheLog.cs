// -----------------------------------------------------------------------
// <copyright file="TieredCacheLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Caching.Tiered;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for the tiered cache.
/// </summary>
internal static partial class TieredCacheLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Tiered cache L1 invalidation handler failed for key {Key}.")]
    public static partial void L1InvalidationFailed(ILogger logger, Exception ex, string key);
}
