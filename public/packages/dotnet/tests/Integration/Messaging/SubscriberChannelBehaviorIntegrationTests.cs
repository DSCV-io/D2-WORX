// -----------------------------------------------------------------------
// <copyright file="SubscriberChannelBehaviorIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Integration coverage for <see cref="SubscriberChannel"/> runtime behavior
/// against a real broker — republish-channel races, ack-failure boundary,
/// retries-exhausted DLQ routing, and disposal drain. Each test isolates
/// its own queue (GUID-suffixed) so they're safe to run in parallel.
/// </summary>
[Collection("RabbitMq")]
public sealed class SubscriberChannelBehaviorIntegrationTests
{
    // Genuine-stuck guard for the async-delivery waits below: a poll-ATTEMPT
    // budget, never a wall-clock deadline. A DateTime.UtcNow deadline in the success
    // path flakes because xUnit runs the RabbitMq collection in parallel with the
    // Unit-test collections — under that thread-pool saturation the wall clock
    // elapses while the starved pool defers the handler-dispatch continuation and the
    // broker's async dead-lettering, tripping the deadline mid-progress. An attempt
    // budget caps the number of polls, not elapsed time: each iteration awaits
    // pollInterval, so under load the effective wait GROWS with the slowdown instead
    // of expiring. At 50 ms/attempt this floor is ~2 min — far above any healthy
    // delivery (<20 attempts), so never load-reachable, yet a permanently-stuck test
    // still terminates (this project ships no xunit.runner.json / test timeout).
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private readonly RabbitMqFixture r_fixture;

    /// <summary>Initializes the test class with the shared fixture.</summary>
    /// <param name="fixture">Testcontainers RabbitMQ.</param>
    public SubscriberChannelBehaviorIntegrationTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    // ---------------------------------------------------------------------
    // Republish channel — SemaphoreSlim guards EnsureRepublishChannelAsync;
    // without it, N concurrent failures all create channels and N-1 leak.
    // ---------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepublishChannel_ConcurrentEnsure_CreatesAtMostOneChannel()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var counting = new CountingWrapperConnection(realConn);
        var registration = BuildSubscription("republish-concurrent-q");
        var registry = new SubscriberRegistry([registration]);
        var sp = new ServiceCollection().BuildServiceProvider();
        var sub = new SubscriberChannel(
            counting,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry),
            registration,
            NullLogger<SubscriberChannel>.Instance);

        // Fan out 16 concurrent calls. The counting connection delays 50ms
        // inside CreateChannelAsync to widen the race window — without the
        // SemaphoreSlim, every losing caller would race past the null check
        // and create a leaking second channel.
        var tasks = Enumerable.Range(0, 16)
            .Select(_ => sub.EnsureRepublishChannelAsync().AsTask())
            .ToArray();
        var channels = await Task.WhenAll(tasks);

        counting.CreateChannelCallCount.Should().Be(
            1, "SemaphoreSlim must serialize creation; concurrent callers "
            + "see the first-created channel via the in-lock recheck");
        channels.Distinct().Should().ContainSingle(
            "every concurrent caller must observe the same channel instance");

        await sub.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepublishChannel_RepeatedSequentialCalls_ReuseSameChannel()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var counting = new CountingWrapperConnection(realConn);
        var registration = BuildSubscription("republish-sequential-q");
        var registry = new SubscriberRegistry([registration]);
        var sp = new ServiceCollection().BuildServiceProvider();
        var sub = new SubscriberChannel(
            counting,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry),
            registration,
            NullLogger<SubscriberChannel>.Instance);

        var first = await sub.EnsureRepublishChannelAsync();
        var second = await sub.EnsureRepublishChannelAsync();
        var third = await sub.EnsureRepublishChannelAsync();
        ReferenceEquals(first, second).Should().BeTrue();
        ReferenceEquals(second, third).Should().BeTrue();
        counting.CreateChannelCallCount.Should().Be(1);

        await sub.DisposeAsync();
    }

    // ---------------------------------------------------------------------
    // Ack failure — narrow-catch around BasicAck. An ack throw on a
    // successful handler must NOT republish to DLX (ack failure is not a
    // handler failure).
    // ---------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AckFailureOnHandlerSuccess_LogsAckFailed_DoesNotRouteToDlq()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "ack-fail." + Guid.NewGuid().ToString("N")[..8];

        // Wire the hook to throw on every ack call. The handler runs OK,
        // returns Ok, then the ack throws — which the narrow catch
        // converts to AckFailed log + counter (NOT republish-to-DLX).
        SubscriberChannel.AckHookForTesting =
            (_, _) => throw new InvalidOperationException("ack-blew-up");

        try
        {
            using var host = await StartHostAsync(services =>
            {
                services.AddTransient<AuditCapturingHandler>();
                services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                    IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 1));
            });

            await using (var scope = host.Services.CreateAsyncScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                await bus.PublishAsync(new IntegrationAuditEvent { Marker = "ack-fail" });
            }

            // Handler runs once.
            await WaitFor(
                () => TestCollector.Count<AuditCapturingHandler>() > 0);
            TestCollector.Count<AuditCapturingHandler>().Should().Be(1);

            // The DLQ MUST be empty — narrow catch around BasicAck means
            // the ack failure does NOT trip the outer catch's republish-
            // to-DLX path. Without this fix, an ack throw would land in
            // the broad handler-exception catch and falsely DLQ the
            // already-processed message.
            //
            // The handler is already confirmed complete (WaitFor above). If the
            // buggy republish-to-DLX path fires, it does so in the same async
            // continuation — any DLQ message appears within milliseconds.
            // 500 ms is generous but bounded; no fixed 2 s sleep required.
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            var dlqName = DlqNaming.DlqFor(queue);
            var dlqCount = await GetQueueCountAsync(dlqName);
            dlqCount.Should().Be(
                0, "ack failure on success path is NOT a handler failure "
                + "and must not republish to DLX");
        }
        finally
        {
            SubscriberChannel.AckHookForTesting = null;
        }
    }

    // ---------------------------------------------------------------------
    // RETRIES_EXHAUSTED enforcement — synthetic x-death header that exceeds
    // MaxAttempts routes the message direct to DLQ without invoking the
    // handler.
    // ---------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RetriesExhausted_RoutesToDlqWithoutInvokingHandler()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "retries-exhausted." + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                BuildAuditDescriptorWithTieredRetry(queue, maxAttempts: 3));
        });

        // Publish RAW with x-death header reporting 5 expired-cycles —
        // exceeds MaxAttempts=3. The consumer reads x-death, ReadAttemptCount
        // returns 5, and the message routes direct to DLQ with cause
        // RETRIES_EXHAUSTED — the handler is NOT invoked.
        await PublishWithSyntheticXDeathAsync(
            exchange: "d2.test.integration-audit",
            routingKey: string.Empty,
            count: 5);

        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);

        TestCollector.Count<AuditCapturingHandler>().Should().Be(
            0, "RETRIES_EXHAUSTED routes direct to DLQ without invoking "
            + "the handler — the dispatch step is skipped entirely");
    }

    // ---------------------------------------------------------------------
    // Disposal drain — slow handler + fast dispose; the drain spin-wait
    // must let the handler complete its ack cleanly.
    // ---------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DisposeMidHandler_DrainsInflightCallbacksBeforeClose()
    {
        TestCollector.Reset<SlowHandler>();
        var queue = "dispose-drain." + Guid.NewGuid().ToString("N")[..8];
        var handlerStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        SlowHandler.HandlerStartedSignal = handlerStarted;
        SlowHandler.HandlerHoldDuration = TimeSpan.FromSeconds(2);

        var host = await StartHostAsync(services =>
        {
            services.AddTransient<SlowHandler>();
            services.AddD2Subscriber<SlowHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 1));
        });

        try
        {
            await using (var scope = host.Services.CreateAsyncScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                await bus.PublishAsync(new IntegrationAuditEvent { Marker = "drain" });
            }

            // Wait for the handler to start (signals it's mid-flight).
            await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));

            // Now stop the host while the handler is still sleeping. The
            // drain spin-wait must let the handler complete + ack, so when
            // StopAsync returns, no message remains pending.
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await host.StopAsync(CancellationToken.None);
            stopwatch.Stop();

            // The drain must wait for the SlowHandler (~2 s). The
            // handlerStarted.Task gate fires once the handler is already
            // executing its 2 s sleep, so StopAsync must wait out the
            // remaining sleep. A lower bound of 1500 ms proves the drain
            // actually waited; 500 ms would allow a handler that returned
            // early (i.e. did NOT drain) to pass silently.
            stopwatch.Elapsed.Should().BeGreaterThan(
                TimeSpan.FromMilliseconds(1500),
                "drain should have waited for the slow handler to complete");
            stopwatch.Elapsed.Should().BeLessThan(
                TimeSpan.FromSeconds(15),
                "drain timeout (30s) should NOT have fired for a 2s handler");

            TestCollector.Count<SlowHandler>().Should().Be(
                1, "the slow handler must have been allowed to complete "
                + "before the channel closed");
        }
        finally
        {
            host.Dispose();
            SlowHandler.HandlerStartedSignal = null;
            SlowHandler.HandlerHoldDuration = TimeSpan.Zero;
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static MqSubscriptionDescriptor BuildAuditDescriptorWithTieredRetry(
        string queueName, int maxAttempts)
    {
        return new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(IntegrationAuditEvent).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: "#",
            Prefetch: 1,
            Idempotency: false,
            TieredRetry: new TieredRetryDescriptor(
                Tiers: [TimeSpan.FromSeconds(5)],
                MaxAttempts: maxAttempts));
    }

    private static SubscriberRegistration BuildSubscription(string queueName)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(IntegrationAuditEvent).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 1,
            Idempotency: false,
            TieredRetry: null);

        return new SubscriberRegistration(
            HandlerType: typeof(AuditCapturingHandler),
            MessageType: typeof(IntegrationAuditEvent),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
    }

    // Attempt-budgeted stuck-guard — see _POLL_ATTEMPT_BUDGET; no wall-clock deadline.
    private static async Task WaitFor(
        Func<bool> predicate, TimeSpan? pollInterval = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(50);
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(pollInterval.Value);
        }

        throw new TimeoutException(
            $"Predicate did not become true within {_POLL_ATTEMPT_BUDGET} poll attempts.");
    }

    private async Task<IHost> StartHostAsync(Action<IServiceCollection> configure)
        => await MessagingHostBuilder.BuildAndStartAsync(r_fixture, configure);

    private async Task<ID2Connection> BuildRealConnectionAsync()
    {
        IntegrationMessageFixtures.EnsureRegistered();
        var optsBuilder = new ServiceCollection()
            .AddOptions<RabbitMqConnectionOptions>()
            .Configure(o =>
            {
                o.ConnectionUri = r_fixture.ConnectionString;
                o.ClientProvidedName = "subscriber-channel-tests";
            });
        var sp = optsBuilder.Services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();
        var conn = new RabbitMqConnection(opts, NullLogger<RabbitMqConnection>.Instance);
        conn.StartReconnectLoop();
        await conn.ReadyTask.WaitAsync(TimeSpan.FromSeconds(15));
        return conn;
    }

    private async Task<int> GetQueueCountAsync(string queueName)
    {
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();
        var ok = await channel.QueueDeclarePassiveAsync(queueName);
        return (int)ok.MessageCount;
    }

    private async Task WaitForQueueCount(string queueName, int expected)
    {
        // Attempt-budgeted stuck-guard (see _POLL_ATTEMPT_BUDGET) — dead-lettering
        // is async broker-side, so poll a bounded number of times rather than
        // against a wall-clock deadline a loaded broker can outrun.
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (await GetQueueCountAsync(queueName) >= expected) return;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        var final = await GetQueueCountAsync(queueName);
        throw new TimeoutException(
            $"Queue '{queueName}' had {final} messages after "
            + $"{_POLL_ATTEMPT_BUDGET} poll attempts, expected >= {expected}.");
    }

    private async Task PublishWithSyntheticXDeathAsync(
        string exchange, string routingKey, long count)
    {
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();

        var deathEntries = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["reason"] = "expired",
                ["count"] = count,
            },
        };

        var props = new BasicProperties
        {
            ContentType = "application/octet-stream",
            MessageId = Guid.CreateVersion7().ToString(),
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                ["x-death"] = deathEntries,
            },
        };

        // The body is a JSON-serialized IntegrationAuditEvent — but the
        // consumer routes to DLQ before any deserialization, so empty
        // bytes work too.
        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>
    /// Wraps a real <see cref="ID2Connection"/>, counts
    /// <c>CreateChannelAsync</c> invocations, and inserts a 50ms delay so the
    /// republish-race test deterministically exercises the SemaphoreSlim path.
    /// </summary>
    private sealed class CountingWrapperConnection : ID2Connection
    {
        private readonly ID2Connection r_inner;
        private int _count;

        public CountingWrapperConnection(ID2Connection inner)
        {
            r_inner = inner;
        }

        public int CreateChannelCallCount => Volatile.Read(ref _count);

        public bool IsOpen => r_inner.IsOpen;

        public Task ReadyTask => r_inner.ReadyTask;

        public void StartReconnectLoop() => r_inner.StartReconnectLoop();

        public async ValueTask<IChannel> CreateChannelAsync(
            CreateChannelOptions? options = null, CancellationToken ct = default)
        {
            await Task.Delay(50, ct);
            Interlocked.Increment(ref _count);
            return await r_inner.CreateChannelAsync(options, ct);
        }

        public ValueTask DisposeAsync() => r_inner.DisposeAsync();
    }
}
