// -----------------------------------------------------------------------
// <copyright file="ConsumerHostedServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing;

using AwesomeAssertions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Result;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Unit-level coverage for <see cref="ConsumerHostedService"/> — disposal
/// idempotency and background-fault logging. The runtime channel-lifecycle
/// behavior is exercised by the integration tests under
/// <c>Integration/Messaging/SubscriberChannelBehaviorIntegrationTests.cs</c>;
/// this file pins the host-process-only contracts.
/// </summary>
public sealed class ConsumerHostedServiceTests
{
    [Fact]
    public async Task DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Empty registry → StartAsync is a no-op; dispose path still runs.
        // The internal CancellationTokenSource is disposed exactly once;
        // a second DisposeAsync must NOT throw on the already-disposed CTS.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MessagingRabbitMq(
            configureConnection: o => o.ConnectionUri = "amqp://localhost");
        var sp = services.BuildServiceProvider();
        var hosted = sp.GetServices<IHostedService>()
            .OfType<ConsumerHostedService>()
            .Single();

        await hosted.StopAsync(CancellationToken.None);
        await hosted.DisposeAsync();

        var act2 = async () => await hosted.DisposeAsync();
        await act2.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_StartChannelsAsyncFaults_LogsHostStartupFaulted()
    {
        // ConsumerHostedService.StartAsync kicks off Task.Run(StartChannelsAsync).
        // A ContinueWith fires SubscriberLog.HostStartupFaulted on faulted
        // background tasks — without it, the failure vanishes into
        // TaskScheduler.UnobservedTaskException and consumers silently never
        // start. Drive a fault by stubbing ITopologyDeclarer to throw —
        // that throw bubbles up from StartChannelsAsync BEFORE any
        // SubscriberChannel is constructed, so we don't need an IChannel stub.
        var declarer = new ThrowingTopologyDeclarer(
            new InvalidOperationException("declarer-blew-up"));
        var conn = new ImmediatelyReadyConnection();
        var registry = new SubscriberRegistry([BuildSubscription("startfault-q")]);
        var logger = new CapturingLogger<ConsumerHostedService>();
        var sp = new ServiceCollection().BuildServiceProvider();

        var hosted = new ConsumerHostedService(
            conn,
            registry,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry),
            declarer,
            new NullLoggerFactory(),
            logger);

        await hosted.StartAsync(CancellationToken.None);

        var startTask = hosted.StartTaskForTesting!;
        try
        {
            await startTask;
        }
        catch
        {
            // Expected — declarer throws inside StartChannelsAsync.
        }

        await Task.Delay(50);

        logger.Records
            .Any(r => r.Level == LogLevel.Error && r.Exception is not null)
            .Should()
            .BeTrue("HostStartupFaulted must fire when StartChannelsAsync "
                + "throws — without it, the failure vanishes into "
                + "TaskScheduler.UnobservedTaskException and consumers "
                + "silently never start");

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

    private static SubscriberRegistration BuildSubscription(string queueName)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(GapMessage).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 1,
            Idempotency: false,
            TieredRetry: null);
        return new SubscriberRegistration(
            HandlerType: typeof(GapHandler),
            MessageType: typeof(GapMessage),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
    }

    /// <summary>Trivial type token for the registry stub.</summary>
    public sealed class GapMessage
    {
    }

    /// <summary>Companion handler for <see cref="GapMessage"/>.</summary>
    public sealed class GapHandler : BaseHandler<GapHandler, GapMessage, Unit>
    {
        /// <summary>Initializes the handler.</summary>
        /// <param name="context">DI-resolved handler context.</param>
        public GapHandler(HandlerContext<GapHandler> context)
            : base(context)
        {
        }

        /// <inheritdoc />
        protected override ValueTask<D2Result<Unit>> ExecuteAsync(
            GapMessage input, CancellationToken ct)
            => new(D2Result<Unit>.Ok(Unit.Value));
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
            => throw new NotImplementedException(
                "These tests fault at topology declaration BEFORE any "
                + "channel is created — if this throws, the test design "
                + "has drifted.");

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
                Records.Add(new LogRecord(logLevel, exception));
        }

        public sealed record LogRecord(LogLevel Level, Exception? Exception);
    }
}
