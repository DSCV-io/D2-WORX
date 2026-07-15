// -----------------------------------------------------------------------
// <copyright file="RabbitMqConnectionLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using Microsoft.Extensions.Logging;

/// <summary><c>LoggerMessage</c> delegates for the connection layer.</summary>
internal static partial class RabbitMqConnectionLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "RabbitMQ connection opened to {Host}:{Port} (attempt {Attempt}).")]
    public static partial void ConnectionOpened(
        ILogger logger, string host, int port, int attempt);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "RabbitMQ reconnect attempt {Attempt} failed (exType={ExType}); "
            + "next try in {Delay}. Publishers return ServiceUnavailable, "
            + "consumers idle until reconnect succeeds.")]
    public static partial void ReconnectAttemptFailed(
        ILogger logger, int attempt, TimeSpan delay, string exType);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "RabbitMQ connection close raised an error (exType={ExType}); "
            + "ignoring on shutdown.")]
    public static partial void ConnectionCloseFailed(ILogger logger, string exType);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "RabbitMQ ConnectionStartupHostedService starting "
            + "background reconnect loop (host stays up while connection "
            + "establishes).")]
    public static partial void StartupOpening(ILogger logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "RabbitMQ connection has been degraded for {Down}. "
            + "RabbitMQ.Client automatic recovery should restore it; if it "
            + "stays down, restart this replica to force reconnection. "
            + "Publishers return ServiceUnavailable; consumers idle.")]
    public static partial void ConnectionDegraded(ILogger logger, TimeSpan down);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "RabbitMQ connection recovered after {Down} of degradation.")]
    public static partial void ConnectionRecovered(ILogger logger, TimeSpan down);
}
