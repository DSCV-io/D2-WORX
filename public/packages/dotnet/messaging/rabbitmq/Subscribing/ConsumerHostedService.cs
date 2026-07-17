// -----------------------------------------------------------------------
// <copyright file="ConsumerHostedService.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Owns one <see cref="SubscriberChannel"/> per registered subscriber.
/// Awaits connection ready, then opens all channels in parallel; on shutdown
/// closes them in parallel. If no subscribers are registered, this hosted
/// service is a no-op (publisher-only services don't pay any cost).
/// </summary>
[MustDisposeResource(false)]
internal sealed class ConsumerHostedService : IHostedService, IAsyncDisposable
{
    private readonly ID2Connection r_connection;
    private readonly SubscriberRegistry r_registry;
    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly HandlerDispatcherFactory r_dispatcherFactory;
    private readonly ITopologyDeclarer r_topology;
    private readonly ILoggerFactory r_loggerFactory;
    private readonly ILogger<ConsumerHostedService> r_logger;
    private readonly List<SubscriberChannel> r_channels = [];
    private readonly CancellationTokenSource r_cts = new();
    private readonly TaskCompletionSource r_readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Task? _startTask;

    /// <summary>Initializes the hosted service.</summary>
    /// <param name="connection">Connection wrapper.</param>
    /// <param name="registry">Subscriber registry.</param>
    /// <param name="scopeFactory">Per-message DI scope factory.</param>
    /// <param name="dispatcherFactory">Closed-generic dispatcher per subscriber.</param>
    /// <param name="topology">Topology declarer — invoked synchronously
    /// before <c>BasicConsume</c> so consumers don't race the queue
    /// existence check.</param>
    /// <param name="loggerFactory">Logger factory (per-channel logger).</param>
    /// <param name="logger">Hosted-service logger.</param>
    [MustDisposeResource(false)]
    public ConsumerHostedService(
        ID2Connection connection,
        SubscriberRegistry registry,
        IServiceScopeFactory scopeFactory,
        HandlerDispatcherFactory dispatcherFactory,
        ITopologyDeclarer topology,
        ILoggerFactory loggerFactory,
        ILogger<ConsumerHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(dispatcherFactory);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);
        r_connection = connection;
        r_registry = registry;
        r_scopeFactory = scopeFactory;
        r_dispatcherFactory = dispatcherFactory;
        r_topology = topology;
        r_loggerFactory = loggerFactory;
        r_logger = logger;
    }

    /// <summary>Gets a task that completes when every registered subscriber
    /// channel has finished <c>BasicConsume</c> (or when the registry is
    /// empty). Lets publishers wait for the consumer side to be ready
    /// before sending — crucial for integration tests on a fresh broker
    /// where the queue might not exist yet at the moment of the first
    /// publish.</summary>
    public Task ReadyTask => r_readyTcs.Task;

    /// <summary>Gets the background start task — internal observable
    /// for the O1 fault-log test (lets tests await its completion).</summary>
    internal Task? StartTaskForTesting => _startTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (r_registry.All.Count == 0)
        {
            // Pre-warm dispatchers (no-op when registry empty); tests rely on
            // the absence of a hang here.
            r_readyTcs.TrySetResult();
            return Task.CompletedTask;
        }

        // Spin off — connection availability is async; don't block host startup.
        _startTask = Task.Run(StartChannelsAsync, r_cts.Token);

        // Mirror TopologyHostedService's fault-log pattern: a faulted
        // background start would otherwise vanish into
        // TaskScheduler.UnobservedTaskException unless something explicitly
        // awaits ReadyTask. Surface it structured.
        _startTask.ContinueWith(
            t => SubscriberLog.HostStartupFaulted(r_logger, t.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await r_cts.CancelAsync();

        if (_startTask is { } st)
        {
            try
            {
                await st.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        // Close channels in parallel — independent operations.
        var dispose = r_channels.Select(ch => ch.DisposeAsync().AsTask()).ToArray();
        await Task.WhenAll(dispose);
        r_channels.Clear();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // L3: when the host runs StopAsync first (the standard lifecycle),
        // r_cts is already canceled + disposed. Re-canceling a disposed CTS
        // throws ObjectDisposedException and the disposal cascade swallows
        // it noisily. Short-circuit here so the second invocation is a
        // clean no-op.
        if (r_cts.IsCancellationRequested) return;

        await r_cts.CancelAsync();
        r_cts.Dispose();
    }

    private async Task StartChannelsAsync()
    {
        try
        {
            // Block on first connection ready. If the broker is already up,
            // ReadyTask is already-completed; otherwise we wait gracefully.
            await r_connection.ReadyTask.WaitAsync(r_cts.Token);

            // Declare topology BEFORE BasicConsume — declaration is idempotent,
            // so re-running it here is harmless if TopologyHostedService got
            // there first, and ensures every queue/exchange/binding exists
            // before a consumer tries to attach.
            await r_topology.DeclareAsync(r_cts.Token);

            foreach (var reg in r_registry.All)
            {
                r_cts.Token.ThrowIfCancellationRequested();
                var ch = new SubscriberChannel(
                    r_connection,
                    r_scopeFactory,
                    r_dispatcherFactory,
                    reg,
                    r_loggerFactory.CreateLogger<SubscriberChannel>());
                await ch.StartAsync(r_cts.Token);
                r_channels.Add(ch);
            }

            SubscriberLog.HostStarted(r_logger, r_channels.Count);
            r_readyTcs.TrySetResult();
        }
        catch (Exception ex)
        {
            r_readyTcs.TrySetException(ex);
            throw;
        }
    }
}
