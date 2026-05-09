// -----------------------------------------------------------------------
// <copyright file="Phase8GapClosureTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging;

using AwesomeAssertions;
using D2.Shared.Handler;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Connection;
using D2.Shared.Messaging.RabbitMq.Idempotency;
using D2.Shared.Messaging.RabbitMq.Subscribing;
using D2.Shared.Messaging.RabbitMq.Topology;
using D2.Shared.Result;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Closes the unit-testable Phase-7 fix gaps that don't need a real
/// broker: M7 topology fault log, O1 ConsumerHostedService fault log,
/// and the IdempotencyStartupCheck operator-provided-store bypass.
/// (F1 republish race + M8 idle TTL eviction need a real
/// <see cref="IChannel"/>; those land in
/// <c>Integration/Messaging/Phase8GapClosureIntegrationTests.cs</c>.)
/// </summary>
public sealed class Phase8GapClosureTests
{
    [Fact]
    public async Task M7_TopologyDeclarationFault_LogsErrorViaContinueWith()
    {
        // M7 verification: TopologyHostedService.StartAsync kicks off
        // Task.Run(DeclareAsync). The Phase-3 fix added a ContinueWith
        // that fires TopologyLog.DeclarationFailed on faulted background
        // tasks — without it, a PRECONDITION_FAILED on a queue-redeclare
        // would vanish into TaskScheduler.UnobservedTaskException and
        // operators would see "consumers don't get messages" with no log.
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

        // StopAsync rethrows the faulted background task — swallow it,
        // we already verified the log fired.
        try
        {
            await hosted.StopAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected.
        }
    }

    [Fact]
    public async Task O1_ConsumerHostedService_StartFault_LogsHostStartupFaulted()
    {
        // O1 verification: ConsumerHostedService.StartAsync kicks off
        // Task.Run(StartChannelsAsync). The Phase-7 fix added a
        // ContinueWith that fires SubscriberLog.HostStartupFaulted on
        // faulted background tasks. Drive a fault by stubbing
        // ITopologyDeclarer to throw — that throw bubbles up from
        // StartChannelsAsync BEFORE any SubscriberChannel is constructed,
        // so we don't need an IChannel stub.
        var declarer = new ThrowingTopologyDeclarer(
            new InvalidOperationException("declarer-blew-up"));
        var conn = new ImmediatelyReadyConnection();
        var registry = new SubscriberRegistry([BuildSubscription("o1-q")]);
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

        // StopAsync rethrows the faulted background task — swallow it,
        // we already verified the log fired.
        try
        {
            await hosted.StopAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // Expected.
        }
    }

    [Fact]
    public async Task IdempotencyStartupCheck_CustomStoreRegistered_BypassesCacheRequirement()
    {
        // Phase-7 fix: an operator-provided IMessageIdempotencyStore
        // (e.g. integration test fakes) satisfies the startup check even
        // when IDistributedCache is missing. Before the fix, GetService
        // would throw on the default CacheIdempotencyStore's missing
        // dependency before reaching the bypass logic.
        var registry = new SubscriberRegistry([
            BuildSubscription("idem-q", idempotency: true),
        ]);
        var services = new ServiceCollection();
        services.AddSingleton<IMessageIdempotencyStore>(new FakeIdemStore());
        var sp = services.BuildServiceProvider();
        var check = new IdempotencyStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);
        await act.Should().NotThrowAsync(
            "operator-provided IMessageIdempotencyStore must satisfy the "
            + "startup check even when IDistributedCache is missing");
    }

    [Fact]
    public async Task IdempotencyStartupCheck_NoCustomStore_NoCache_StillFails()
    {
        // Inverse of the above: with neither operator store nor cache,
        // the check still hard-fails. Pin it so a refactor doesn't
        // soften the guard.
        var registry = new SubscriberRegistry([
            BuildSubscription("idem-q", idempotency: true),
        ]);
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new IdempotencyStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static SubscriberRegistration BuildSubscription(
        string queueName, bool idempotency = false)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(GapMessage).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 1,
            Idempotency: idempotency,
            TieredRetry: null);
        return new SubscriberRegistration(
            HandlerType: typeof(GapHandler),
            MessageType: typeof(GapMessage),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
    }

    /// <summary>Trivial message + handler used purely as type tokens —
    /// the stubs never invoke ExecuteAsync in these tests.</summary>
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

    private sealed class FakeIdemStore : IMessageIdempotencyStore
    {
        public ValueTask<D2Result<bool>> HasSeenAsync(
            string messageId, CancellationToken ct = default)
            => new(D2Result<bool>.Ok(data: false));

        public ValueTask<D2Result> MarkSeenAsync(
            string messageId, CancellationToken ct = default)
            => new(D2Result.Ok());
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
