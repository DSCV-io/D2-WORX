// -----------------------------------------------------------------------
// <copyright file="TopologyHostedService.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Topology;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Drives <see cref="ITopologyDeclarer"/> on startup once the connection is
/// ready. Non-blocking — kicks off a background task that awaits
/// <see cref="ID2Connection.ReadyTask"/> so a slow / unavailable broker
/// doesn't stall host startup. Subscribers / publishers will surface their
/// own <c>ServiceUnavailable</c> failures while topology hasn't been
/// declared yet.
/// </summary>
[MustDisposeResource(false)]
internal sealed class TopologyHostedService : IHostedService, IAsyncDisposable
{
    private readonly ITopologyDeclarer r_declarer;
    private readonly ID2Connection r_connection;
    private readonly ILogger<TopologyHostedService> r_logger;
    private readonly CancellationTokenSource r_cts = new();
    private Task? _declareTask;

    /// <summary>Initializes the hosted service.</summary>
    /// <param name="declarer">Topology declarer.</param>
    /// <param name="connection">Connection wrapper.</param>
    /// <param name="logger">Logger.</param>
    [MustDisposeResource(false)]
    public TopologyHostedService(
        ITopologyDeclarer declarer,
        ID2Connection connection,
        ILogger<TopologyHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(declarer);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(logger);
        r_declarer = declarer;
        r_connection = connection;
        r_logger = logger;
    }

    /// <summary>Gets the background declare task — internal observable
    /// for the M7 fault-log test (lets tests await its completion).</summary>
    internal Task? DeclareTaskForTesting => _declareTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Spin off a background task so StartAsync returns immediately.
        // Awaiting ReadyTask blocks until the connection lands; declaration
        // then runs on a dedicated channel.
        _declareTask = Task.Run(
            async () =>
            {
                await r_connection.ReadyTask.WaitAsync(r_cts.Token);
                await r_declarer.DeclareAsync(r_cts.Token);
            },
            r_cts.Token);

        // M7: a fire-and-forget Task.Run that throws would otherwise vanish
        // into TaskScheduler.UnobservedTaskException and the operator would
        // see "consumers don't get messages" with no log explaining why
        // (e.g. PRECONDITION_FAILED on a queue declared with mismatched
        // arguments). Surface the failure structured so it's actionable.
        _declareTask.ContinueWith(
            t => TopologyLog.DeclarationFailedFaultSink(r_logger, t.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await r_cts.CancelAsync();

        // Wait for the background task to wind down so any in-flight channel
        // ops get a chance to abort cleanly. Suppress cancellation —
        // shutdown.
        if (_declareTask is { } t)
        {
            try
            {
                await t.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown — declaration was canceled mid-flight.
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await r_cts.CancelAsync();
        r_cts.Dispose();
    }
}
