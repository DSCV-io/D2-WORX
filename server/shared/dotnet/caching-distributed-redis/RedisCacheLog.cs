// -----------------------------------------------------------------------
// <copyright file="RedisCacheLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Caching.Distributed.Redis;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for the Redis cache impl.
/// </summary>
internal static partial class RedisCacheLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Redis op {Operation} failed for key {KeyOrCount}.")]
    public static partial void RedisOpFailed(
        ILogger logger,
        Exception ex,
        string operation,
        string keyOrCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Backplane handler threw on key {Key}; isolating so other handlers continue.")]
    public static partial void BackplaneHandlerThrew(ILogger logger, Exception ex, string key);
}
