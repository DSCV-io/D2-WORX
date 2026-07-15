// -----------------------------------------------------------------------
// <copyright file="TopologyLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Topology;

using Microsoft.Extensions.Logging;

/// <summary><c>LoggerMessage</c> delegates for topology declaration.</summary>
internal static partial class TopologyLog
{
    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Information,
        Message = "Declaring messaging topology: {SubscriberCount} subscriber(s).")]
    public static partial void DeclarationStarted(ILogger logger, int subscriberCount);

    [LoggerMessage(
        EventId = 201,
        Level = LogLevel.Information,
        Message = "Topology declaration complete in {DurationMs:F0}ms.")]
    public static partial void DeclarationCompleted(ILogger logger, double durationMs);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Information,
        Message = "Declared exchange={Exchange} type={Type} queue={Queue} pattern={Pattern} "
            + "(DLX={Dlx}, DLQ={Dlq}).")]
    public static partial void SubscriberTopologyDeclared(
        ILogger logger,
        string exchange,
        string type,
        string queue,
        string pattern,
        string dlx,
        string dlq);

    [LoggerMessage(
        EventId = 203,
        Level = LogLevel.Information,
        Message = "Declared {TierCount} retry tier(s) for queue={Queue} (TTLs={Ttls}).")]
    public static partial void RetryTopologyDeclared(
        ILogger logger, int tierCount, string queue, string ttls);

    [LoggerMessage(
        EventId = 204,
        Level = LogLevel.Error,
        Message = "Topology declaration failed ({ExceptionType}, queue={Queue}); "
            + "host startup will fail. Likely causes: broker permissions, "
            + "conflicting pre-existing declarations.")]
    public static partial void DeclarationFailed(
        ILogger logger, string exceptionType, string queue);

    [LoggerMessage(
        EventId = 206,
        Level = LogLevel.Error,
        Message = "Topology declaration cycle faulted in background; "
            + "host startup will fail.")]
    public static partial void DeclarationFailedFaultSink(
        ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 205,
        Level = LogLevel.Information,
        Message = "Topology hosted service: no subscribers registered; nothing to declare.")]
    public static partial void NoSubscribersToDeclare(ILogger logger);
}
