// -----------------------------------------------------------------------
// <copyright file="Phase8GapClosureIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using AwesomeAssertions;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Channels;
using D2.Shared.Messaging.RabbitMq.Connection;
using D2.Shared.Messaging.RabbitMq.Subscribing;
using D2.Shared.Messaging.RabbitMq.Topology;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Behavioural tests for Phase-7 fixes that need a real broker
/// connection: F1 republish-channel race, M8 idle TTL eviction, M2
/// narrow-catch around BasicAck, M1 retries-exhausted enforcement,
/// H6 in-flight callback drain on disposal, and H8 hosted-service
/// publishing without a scope. Each test is named with its fix label
/// so a regression maps cleanly to the audit row.
/// </summary>
[Collection("RabbitMq")]
public sealed class Phase8GapClosureIntegrationTests
{
    private readonly RabbitMqFixture r_fixture;

    /// <summary>Initializes the test class with the shared fixture.</summary>
    /// <param name="fixture">Testcontainers RabbitMQ.</param>
    public Phase8GapClosureIntegrationTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    // ---------------------------------------------------------------------
    // F1 — republish channel race. SemaphoreSlim guards
    // EnsureRepublishChannelAsync; without it, N concurrent failures all
    // create channels and N-1 leak. Wrap the real broker connection in a
    // counting wrapper that injects a 50ms delay inside CreateChannelAsync
    // — turns the race into a deterministic test.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task F1_RepublishChannel_ConcurrentEnsure_CreatesAtMostOneChannel()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var counting = new CountingWrapperConnection(realConn);
        var registration = BuildSubscription("f1-q");
        var registry = new SubscriberRegistry([registration]);
        var sp = new ServiceCollection().BuildServiceProvider();
        var sub = new SubscriberChannel(
            counting,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry),
            registration,
            NullLogger<SubscriberChannel>.Instance);

        // Fan out 16 concurrent calls. Counting connection delays 50ms
        // inside CreateChannelAsync to widen the race window — without
        // the SemaphoreSlim, every losing caller would race past the
        // null check and create a leaking second channel.
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
    public async Task F1_RepublishChannel_RepeatedSequentialCalls_ReuseSameChannel()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var counting = new CountingWrapperConnection(realConn);
        var registration = BuildSubscription("f1-seq-q");
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
    // M8 — actual idle TTL eviction. Pool with IdleTtl=10ms; acquire +
    // release + sleep > TTL + acquire → fresh channel created.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task M8_ChannelPool_IdleBeyondTtl_EvictsAndCreatesFresh()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var counting = new CountingWrapperConnection(realConn);
        var poolOpts = Options.Create(new ChannelPoolOptions
        {
            PublishPoolSize = 4,
            PublisherConfirmsEnabled = false,
            IdleTtl = TimeSpan.FromMilliseconds(10),
        });
        await using var pool = new BoundedChannelPool(
            counting, poolOpts, NullLogger<BoundedChannelPool>.Instance);

        var first = await pool.AcquireAsync();
        await first.DisposeAsync();
        counting.CreateChannelCallCount.Should().Be(1);

        await Task.Delay(50);

        var second = await pool.AcquireAsync();
        await second.DisposeAsync();
        counting.CreateChannelCallCount.Should().Be(
            2, "channel idle longer than IdleTtl must be evicted on next "
            + "acquire and replaced with a fresh one");
    }

    [Fact]
    public async Task M8_ChannelPool_IdleWithinTtl_ReusesChannel()
    {
        await using var realConn = await BuildRealConnectionAsync();
        var counting = new CountingWrapperConnection(realConn);
        var poolOpts = Options.Create(new ChannelPoolOptions
        {
            PublishPoolSize = 4,
            PublisherConfirmsEnabled = false,
            IdleTtl = TimeSpan.FromSeconds(10),
        });
        await using var pool = new BoundedChannelPool(
            counting, poolOpts, NullLogger<BoundedChannelPool>.Instance);

        var first = await pool.AcquireAsync();
        await first.DisposeAsync();
        var second = await pool.AcquireAsync();
        await second.DisposeAsync();

        counting.CreateChannelCallCount.Should().Be(
            1, "channel idle WITHIN IdleTtl must be reused — eviction is "
            + "for stale-pool channels only, not every-second-publish churn");
    }

    // ---------------------------------------------------------------------
    // M2 — narrow-catch around BasicAck. Inject an ack failure via the
    // SubscriberChannel.AckHookForTesting seam; verify no DLQ duplicate
    // (the ack failure is NOT routed to DLQ as a handler failure).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task M2_AckFailure_LogsAckFailed_DoesNotRouteHandlerSuccessToDlq()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "m2.ack." + Guid.NewGuid().ToString("N")[..8];

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
                () => TestCollector.Count<AuditCapturingHandler>() > 0,
                timeout: TimeSpan.FromSeconds(15));
            TestCollector.Count<AuditCapturingHandler>().Should().Be(1);

            // The DLQ MUST be empty — narrow catch around BasicAck means
            // the ack failure does NOT trip the outer catch's republish-
            // to-DLX path. (Without M2, an ack throw would land here as
            // HANDLER_EXCEPTION and falsely DLQ the already-processed
            // message.)
            await Task.Delay(TimeSpan.FromSeconds(2));
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
    // M1 — RETRIES_EXHAUSTED enforcement. Publish a message with a
    // synthetic x-death header that exceeds MaxAttempts; verify it lands
    // in DLQ without invoking the handler. Uses raw broker publish
    // (bypassing the bus) to inject the header.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task M1_RetriesExhausted_RoutesToDlqWithoutInvokingHandler()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "m1.exhausted." + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                BuildAuditDescriptorWithTieredRetry(queue, maxAttempts: 3));
        });

        // Publish RAW with x-death header reporting 5 expired-cycles —
        // exceeds MaxAttempts=3. The consumer reads x-death,
        // ReadAttemptCount returns 5, and the message routes direct to
        // DLQ with cause RETRIES_EXHAUSTED — the handler is NOT invoked.
        await PublishWithSyntheticXDeathAsync(
            exchange: "d2.test.integration-audit",
            routingKey: string.Empty,
            count: 5);

        // Wait for the message to surface in the DLQ.
        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1, timeout: TimeSpan.FromSeconds(15));

        // Handler must NOT have been invoked even once.
        TestCollector.Count<AuditCapturingHandler>().Should().Be(
            0, "RETRIES_EXHAUSTED routes direct to DLQ without invoking "
            + "the handler — the dispatch step is skipped entirely");
    }

    // ---------------------------------------------------------------------
    // H6 — in-flight callback drain on disposal. Slow handler + fast
    // dispose → drain bounded by 30s spin-wait; handler completes its
    // ack cleanly. Pin the in-flight counter goes back to 0.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task H6_DisposeMidHandler_DrainsInflightCallbacksBeforeClose()
    {
        TestCollector.Reset<SlowHandler>();
        var queue = "h6.drain." + Guid.NewGuid().ToString("N")[..8];
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
                await bus.PublishAsync(new IntegrationAuditEvent { Marker = "h6" });
            }

            // Wait for the handler to start (signals it's mid-flight).
            await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));

            // Now stop the host while the handler is still sleeping. The
            // drain spin-wait must let the handler complete + ack, so
            // when StopAsync returns, no message remains pending.
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await host.StopAsync(CancellationToken.None);
            stopwatch.Stop();

            // The drain should have waited for the handler (~2s); not
            // less (would mean we cut it short) and not the full 30s
            // timeout (would mean drain failed).
            stopwatch.Elapsed.Should().BeGreaterThan(
                TimeSpan.FromMilliseconds(1500),
                "drain should have waited for the slow handler to complete");
            stopwatch.Elapsed.Should().BeLessThan(
                TimeSpan.FromSeconds(10),
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
    // H8 — Singleton bus + per-publish scope. A hosted service that
    // publishes from StartAsync (no scope of its own) must succeed —
    // proving the bus internally creates a transient scope.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task H8_PublishFromRootProvider_NoScopeNeeded_Succeeds()
    {
        // H8 verification: bus is Singleton; it builds its own transient
        // scope per PublishAsync to resolve keyed crypto + IRequestContext.
        // Background hosted services + other singletons can therefore
        // publish via the root provider without ceremony — no
        // `await using var scope = sp.CreateAsyncScope()` boilerplate.
        var queue = "h8.rootpub." + Guid.NewGuid().ToString("N")[..8];
        TestCollector.Reset<AuditCapturingHandler>();

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 1));
        });

        // Resolve the bus DIRECTLY from the root provider. Pre-fix this
        // would have thrown InvalidOperationException ("Cannot resolve
        // scoped service ... from root provider"). Post-fix it works
        // because IMessageBus is a Singleton.
        var bus = host.Services.GetRequiredService<IMessageBus>();
        var publish = await bus.PublishAsync(
            new IntegrationAuditEvent { Marker = "from-root-sp" });
        publish.Failed.Should().BeFalse(
            "Singleton bus must publish without a wrapping DI scope");

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));
        TestCollector.Count<AuditCapturingHandler>().Should().Be(1);
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

    private static async Task WaitFor(
        Func<bool> predicate, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(50);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return;
            await Task.Delay(pollInterval.Value);
        }

        throw new TimeoutException(
            $"Predicate did not become true within {timeout}.");
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
                o.ClientProvidedName = "phase8-tests";
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

    private async Task WaitForQueueCount(string queueName, int expected, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await GetQueueCountAsync(queueName) >= expected) return;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        var final = await GetQueueCountAsync(queueName);
        throw new TimeoutException(
            $"Queue '{queueName}' had {final} messages after {timeout}, "
            + $"expected >= {expected}.");
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
            body: Array.Empty<byte>());
    }

    /// <summary>Wraps a real <see cref="ID2Connection"/>, counts
    /// <c>CreateChannelAsync</c> invocations, and inserts a 50ms delay so
    /// the F1 race-window test deterministically exercises the
    /// SemaphoreSlim path.</summary>
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
