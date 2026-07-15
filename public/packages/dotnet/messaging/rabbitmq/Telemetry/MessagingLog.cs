// -----------------------------------------------------------------------
// <copyright file="MessagingLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Telemetry;

using Microsoft.Extensions.Logging;

/// <summary><c>LoggerMessage</c> delegates for the publish/consume paths.</summary>
internal static partial class MessagingLog
{
    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Debug,
        Message = "Publish to {Exchange}/{RoutingKey} (msgType={MessageType}, "
            + "attempt={Attempt}/{MaxAttempts}, kid={Kid}).")]
    public static partial void PublishAttempt(
        ILogger logger,
        string exchange,
        string routingKey,
        string messageType,
        int attempt,
        int maxAttempts,
        string? kid);

    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Information,
        Message = "Published to {Exchange}/{RoutingKey} (msgType={MessageType}, "
            + "attempts={Attempts}, durationMs={DurationMs:F1}).")]
    public static partial void PublishSucceeded(
        ILogger logger,
        string exchange,
        string routingKey,
        string messageType,
        int attempts,
        double durationMs);

    [LoggerMessage(
        EventId = 102,
        Level = LogLevel.Warning,
        Message = "Publish to {Exchange}/{RoutingKey} failed (transient) on attempt "
            + "{Attempt}/{MaxAttempts} (exType={ExType}); retrying.")]
    public static partial void PublishTransientFailure(
        ILogger logger,
        string exchange,
        string routingKey,
        int attempt,
        int maxAttempts,
        string exType);

    [LoggerMessage(
        EventId = 103,
        Level = LogLevel.Error,
        Message = "Publish to {Exchange}/{RoutingKey} failed terminally after "
            + "{Attempts} attempt(s) (msgType={MessageType}, exType={ExType}); "
            + "returning ServiceUnavailable.")]
    public static partial void PublishTerminalFailure(
        ILogger logger,
        string exchange,
        string routingKey,
        string messageType,
        int attempts,
        string exType);

    [LoggerMessage(
        EventId = 104,
        Level = LogLevel.Warning,
        Message = "Publish confirm wait timed out after {TimeoutMs:F0}ms on "
            + "{Exchange}/{RoutingKey}; treating as transient.")]
    public static partial void PublishConfirmTimeout(
        ILogger logger, double timeoutMs, string exchange, string routingKey);
}
