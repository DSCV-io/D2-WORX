// -----------------------------------------------------------------------
// <copyright file="RabbitMqConnection.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

using DcsvIo.D2.Utilities.Extensions;
using global::RabbitMQ.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default <see cref="ID2Connection"/> impl. Holds the singleton
/// <see cref="IConnection"/> for the process. Opens lazily via a background
/// reconnect loop kicked off at host startup; retries forever with
/// exponential backoff so a temporarily-unreachable broker doesn't crash
/// the host.
/// </summary>
/// <remarks>
/// Once a connection is established, RabbitMQ.Client 7.x's
/// <c>AutomaticRecoveryEnabled = true</c> + <c>TopologyRecoveryEnabled =
/// true</c> handle in-flight reconnection (re-declaring topology,
/// re-attaching consumers) without our involvement. Our background loop
/// covers the gap before that first connection is established AND the
/// edge case where automatic recovery silently fails (we periodically
/// verify the connection is still healthy).
/// </remarks>
[MustDisposeResource(false)]
internal sealed class RabbitMqConnection : ID2Connection
{
    private readonly RabbitMqConnectionOptions r_options;
    private readonly ILogger<RabbitMqConnection> r_logger;
    private readonly TaskCompletionSource<bool> r_readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly object r_loopLock = new();
    private CancellationTokenSource? _shutdownCts;
    private Task? _reconnectLoop;
    private volatile IConnection? _connection;
    private bool _disposed;

    /// <summary>Initializes a new <see cref="RabbitMqConnection"/>.</summary>
    /// <param name="options">Connection-level options.</param>
    /// <param name="logger">Logger.</param>
    [MustDisposeResource(false)]
    public RabbitMqConnection(
        IOptions<RabbitMqConnectionOptions> options,
        ILogger<RabbitMqConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        r_options = options.Value;
        r_logger = logger;
    }

    /// <inheritdoc />
    public bool IsOpen => _connection is { IsOpen: true };

    /// <inheritdoc />
    public Task ReadyTask => r_readyTcs.Task;

    /// <inheritdoc />
    public void StartReconnectLoop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (r_options.ConnectionUri.Falsey())
        {
            throw new InvalidOperationException(
                "RabbitMqConnectionOptions.ConnectionUri is required.");
        }

        lock (r_loopLock)
        {
            if (_reconnectLoop is not null) return;

            _shutdownCts = new CancellationTokenSource();
            _reconnectLoop = Task.Run(() => ReconnectLoopAsync(_shutdownCts.Token));
        }
    }

    /// <inheritdoc />
    public ValueTask<IChannel> CreateChannelAsync(
        CreateChannelOptions? options = null,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var conn = _connection;

        if (conn is null || !conn.IsOpen) throw new BrokerUnavailableException();

        return new ValueTask<IChannel>(conn.CreateChannelAsync(options, ct));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        // Stop the reconnect loop first so it doesn't race with disposal.
        _shutdownCts?.Cancel();
        if (_reconnectLoop is not null)
        {
            try
            {
                await _reconnectLoop;
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }

        var conn = _connection;
        _connection = null;
        if (conn is not null)
        {
            try
            {
                // Host-shutdown path: don't propagate the original ct (it may
                // already be canceled). Use None to let the broker round-trip
                // close cleanly.
                await conn.CloseAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Same PII discipline as ReconnectAttemptFailed (L5): never
                // pass the Exception object — its Message can include the
                // ConnectionUri with embedded password.
                RabbitMqConnectionLog.ConnectionCloseFailed(
                    r_logger, ex.GetType().FullName ?? ex.GetType().Name);
            }

            await conn.DisposeAsync();
        }

        _shutdownCts?.Dispose();
    }

    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(r_options.ConnectionUri),
            ClientProvidedName = r_options.ClientProvidedName,
            ConsumerDispatchConcurrency = r_options.ConsumerDispatchConcurrency,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
        };

        // Stage 1 — open the FIRST connection. Retry forever with
        // exponential backoff. Once succeeded, transition to Stage 2.
        if (!await OpenInitialConnectionAsync(factory, ct)) return;

        // Stage 2 — observe only. RabbitMQ.Client owns reconnection from
        // here on (AutomaticRecoveryEnabled + TopologyRecoveryEnabled
        // re-declare topology + re-attach consumers without our help).
        // We never replace the IConnection mid-flight — doing so orphans
        // any consumers registered against the old instance, which is a
        // worse outcome than waiting for ops to restart the replica.
        await ObservationLoopAsync(ct);
    }

    private async Task<bool> OpenInitialConnectionAsync(
        ConnectionFactory factory, CancellationToken ct)
    {
        var attempt = 0;
        var delay = r_options.InitialReconnectDelay;

        while (!ct.IsCancellationRequested)
        {
            attempt++;
            try
            {
                var fresh = await factory.CreateConnectionAsync(ct);
                _connection = fresh;
                RabbitMqConnectionLog.ConnectionOpened(
                    r_logger, factory.HostName, factory.Port, attempt);
                r_readyTcs.TrySetResult(true);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                // L5: do NOT pass the exception object — its Message includes
                // the full ConnectionUri (with embedded password) when the
                // RabbitMQ.Client library can't open the socket. The
                // exception type alone is enough for ops triage; full
                // detail is available in the broker's own log if needed.
                RabbitMqConnectionLog.ReconnectAttemptFailed(
                    r_logger,
                    attempt,
                    delay,
                    ex.GetType().FullName ?? ex.GetType().Name);
                try
                {
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }

                // Exponential backoff capped at MaxReconnectDelay.
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(
                        delay.TotalMilliseconds * 2,
                        r_options.MaxReconnectDelay.TotalMilliseconds));
            }
        }

        return false;
    }

    private async Task ObservationLoopAsync(CancellationToken ct)
    {
        DateTimeOffset? degradedSince = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(r_options.HealthCheckInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (IsOpen)
            {
                if (degradedSince is not null)
                {
                    var down = DateTimeOffset.UtcNow - (DateTimeOffset)degradedSince;
                    RabbitMqConnectionLog.ConnectionRecovered(r_logger, down);
                    degradedSince = null;
                }
            }
            else
            {
                degradedSince ??= DateTimeOffset.UtcNow;
                var down = DateTimeOffset.UtcNow - (DateTimeOffset)degradedSince;
                RabbitMqConnectionLog.ConnectionDegraded(r_logger, down);
            }
        }
    }
}
