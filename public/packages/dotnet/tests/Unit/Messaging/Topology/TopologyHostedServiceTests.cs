// -----------------------------------------------------------------------
// <copyright file="TopologyHostedServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Topology;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using global::RabbitMQ.Client;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Coverage for <see cref="TopologyHostedService"/> background-fault
/// handling. <c>StartAsync</c> launches <c>Task.Run(DeclareAsync)</c>; a
/// <c>ContinueWith</c> fires <see cref="TopologyLog.DeclarationFailed"/>
/// on faulted background tasks — without it, a <c>PRECONDITION_FAILED</c>
/// on a queue-redeclare would vanish into
/// <c>TaskScheduler.UnobservedTaskException</c> and operators would see
/// "consumers don't get messages" with no log.
/// </summary>
public sealed class TopologyHostedServiceTests
{
    [Fact]
    public async Task StartAsync_DeclareFaults_LogsErrorViaContinueWith()
    {
        var declarer = new ThrowingTopologyDeclarer(
            new InvalidOperationException("PRECONDITION_FAILED simulated"));
        var conn = new ImmediatelyReadyConnection();
        var logger = new CapturingLogger<TopologyHostedService>();
        var hosted = new TopologyHostedService(declarer, conn, logger);

        await hosted.StartAsync(CancellationToken.None);

        // Wait for the background declare task + its ContinueWith to settle.
        var declareTask = hosted.DeclareTaskForTesting!;
        try
        {
            await declareTask;
        }
        catch
        {
            // Expected — declarer throws.
        }

        await Task.Delay(50);

        logger.Records
            .Any(r => r.Level == LogLevel.Error)
            .Should()
            .BeTrue("TopologyLog.DeclarationFailed must fire when the "
                + "background declare task faults");

        // StopAsync rethrows the faulted background task — swallow it.
        try
        {
            await hosted.StopAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected.
        }
    }

    private sealed class ImmediatelyReadyConnection : ID2Connection
    {
        public bool IsOpen => true;

        public Task ReadyTask { get; } = Task.CompletedTask;

        public void StartReconnectLoop()
        {
        }

        public ValueTask<IChannel> CreateChannelAsync(
            CreateChannelOptions? options = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask DisposeAsync() => default;
    }

    private sealed class ThrowingTopologyDeclarer : ITopologyDeclarer
    {
        private readonly Exception r_exception;

        public ThrowingTopologyDeclarer(Exception ex)
        {
            r_exception = ex;
        }

        public ValueTask DeclareAsync(CancellationToken ct)
            => throw r_exception;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Records)
                Records.Add(new LogRecord(logLevel));
        }

        public sealed record LogRecord(LogLevel Level);
    }
}
