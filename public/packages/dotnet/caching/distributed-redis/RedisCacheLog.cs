// -----------------------------------------------------------------------
// <copyright file="RedisCacheLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching.Distributed.Redis;

using Microsoft.Extensions.Logging;

/// <summary>
/// <c>LoggerMessage</c> delegates for the Redis cache impl. Per
/// <c>docs/dev/rules.md §3.1</c>, none of the delegates take
/// <see cref="Exception"/> directly — <c>ex.Message</c> on a
/// <c>RedisException</c> can carry connection-string fragments and
/// command details that leak into log sinks. Callers compute
/// <c>ex.GetType().Name</c> (and a non-PII operation context) before
/// invoking the delegate; the exception's structured details stay out
/// of the log scope.
/// </summary>
internal static partial class RedisCacheLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Redis op {Operation} failed ({ExceptionType}) for key {KeyOrCount}.")]
    public static partial void RedisOpFailed(
        ILogger logger,
        string operation,
        string exceptionType,
        string keyOrCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Backplane handler threw ({ExceptionType}) on key {Key}; "
            + "isolating so other handlers continue.")]
    public static partial void BackplaneHandlerThrew(
        ILogger logger,
        string exceptionType,
        string key);
}
