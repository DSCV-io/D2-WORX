// -----------------------------------------------------------------------
// <copyright file="ConnectionStartupHostedService.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Kicks off the singleton <see cref="ID2Connection"/>'s background
/// reconnect loop in <see cref="StartAsync"/>. Returns immediately so the
/// host comes up regardless of broker availability — publishers degrade to
/// <c>ServiceUnavailable</c> while disconnected; consumers idle until the
/// connection establishes for the first time, then RabbitMQ.Client's
/// automatic recovery handles subsequent in-flight reconnects.
/// </summary>
internal sealed class ConnectionStartupHostedService : IHostedService
{
    private readonly ID2Connection r_connection;
    private readonly ILogger<ConnectionStartupHostedService> r_logger;

    /// <summary>Initializes the hosted service.</summary>
    /// <param name="connection">Singleton connection wrapper.</param>
    /// <param name="logger">Logger.</param>
    public ConnectionStartupHostedService(
        ID2Connection connection,
        ILogger<ConnectionStartupHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(logger);
        r_connection = connection;
        r_logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        RabbitMqConnectionLog.StartupOpening(r_logger);
        r_connection.StartReconnectLoop();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
