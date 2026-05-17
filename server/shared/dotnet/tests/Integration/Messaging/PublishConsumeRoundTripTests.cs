// -----------------------------------------------------------------------
// <copyright file="PublishConsumeRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using System.Diagnostics;
using AwesomeAssertions;
using D2.Shared.Handler;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq;
using D2.Shared.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[Collection("RabbitMq")]
public sealed class PublishConsumeRoundTripTests
{
    private readonly RabbitMqFixture r_fixture;

    public PublishConsumeRoundTripTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task EncryptedMessage_RoundTrips_HandlerSeesPayload()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "rt.audit." + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(
                    queue,
                    pattern: QueuePattern.CompetingConsumer,
                    prefetch: 5));
        });

        var marker = "marker-" + Guid.NewGuid().ToString("N");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse();
        }

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));

        var captured = TestCollector.Captured<AuditCapturingHandler, IntegrationAuditEvent>();
        captured.Should().ContainSingle();
        captured[0].Marker.Should().Be(marker);
    }

    [Fact]
    public async Task PlaintextMessage_RoundTrips_HandlerSeesPayload()
    {
        TestCollector.Reset<PlaintextCapturingHandler>();
        var queue = "rt.plain." + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<PlaintextCapturingHandler>();
            services.AddD2Subscriber<PlaintextCapturingHandler, IntegrationPlaintextEvent>(
                IntegrationSubscriptionFactory.ForPlaintextEvent(
                    queue,
                    pattern: QueuePattern.CompetingConsumer,
                    prefetch: 5));
        });

        var marker = "plain-" + Guid.NewGuid().ToString("N");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(new IntegrationPlaintextEvent { Marker = marker });
            publish.Failed.Should().BeFalse();
        }

        await WaitFor(
            () => TestCollector.Count<PlaintextCapturingHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));

        var captured = TestCollector
            .Captured<PlaintextCapturingHandler, IntegrationPlaintextEvent>();
        captured.Should().ContainSingle();
        captured[0].Marker.Should().Be(marker);
    }

    [Fact]
    public async Task MultipleSubscribers_NoCrossTalk()
    {
        TestCollector.Reset<MultiSubA>();
        TestCollector.Reset<MultiSubB>();
        var queueA = "rt.crossA." + Guid.NewGuid().ToString("N")[..8];
        var queueB = "rt.crossB." + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<MultiSubA>();
            services.AddTransient<MultiSubB>();

            services.AddD2Subscriber<MultiSubA, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(
                    queueA, routingKeyBinding: "queue.a"));
            services.AddD2Subscriber<MultiSubB, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(
                    queueB, routingKeyBinding: "queue.b"));
        });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = "to-a" },
                new PublisherOptions { RoutingKey = "queue.a" });
            await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = "to-b" },
                new PublisherOptions { RoutingKey = "queue.b" });
        }

        await WaitFor(
            () => TestCollector.Count<MultiSubA>() > 0 && TestCollector.Count<MultiSubB>() > 0,
            timeout: TimeSpan.FromSeconds(15));

        TestCollector.Captured<MultiSubA, IntegrationAuditEvent>()
            .Should().ContainSingle().Which.Marker.Should().Be("to-a");
        TestCollector.Captured<MultiSubB, IntegrationAuditEvent>()
            .Should().ContainSingle().Which.Marker.Should().Be("to-b");
    }

    [Fact]
    public async Task ConcurrentPublishes_AllDelivered()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        const int total = 20;
        var queue = "rt.conc." + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 50));
        });

        // Fan out N publishes from N tasks against the same singleton bus.
        // The channel pool (default 4) bounds concurrency; the test verifies
        // every message arrives despite contention.
        var publishTasks = Enumerable.Range(0, total).Select(async i =>
        {
            await using var scope = host.Services.CreateAsyncScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var result = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = $"m-{i}" });
            result.Failed.Should().BeFalse();
        }).ToArray();

        await Task.WhenAll(publishTasks);

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() >= total,
            timeout: TimeSpan.FromSeconds(20));

        var captured = TestCollector.Captured<AuditCapturingHandler, IntegrationAuditEvent>();
        captured.Count.Should().Be(total);
        var markers = captured.Select(c => c.Marker).Distinct().ToArray();
        markers.Length.Should().Be(total, "no duplicates / no losses");
    }

    [Fact]
    public async Task Traceparent_PropagatesToConsumerSpan_ParentChildLinkage()
    {
        // H5 verification: producer-side publish span sets traceparent on
        // the AMQP message, consumer-side OnReceivedAsync starts a Consumer
        // span whose parent is the publish span (same TraceId, SpanId == publish
        // SpanId). Without H5 the consume span would either not exist or
        // start a fresh trace.
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "rt.trace." + Guid.NewGuid().ToString("N")[..8];

        // Listen on BOTH the messaging-rabbitmq source (publish + consume
        // spans) and the test source (the producer-side root span we start
        // explicitly below).
        var collected = new List<Activity>();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = src =>
            src.Name is "D2.Shared.Messaging.RabbitMq" or "D2.Tests.H5";
        listener.Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = a =>
        {
            lock (collected) collected.Add(a);
        };
        ActivitySource.AddActivityListener(listener);

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        using var rootSource = new ActivitySource("D2.Tests.H5");
        ActivityTraceId publishTraceId;
        var rootSpanName = "h5-root";
        await using (var scope = host.Services.CreateAsyncScope())
        using (var rootSpan = rootSource.StartActivity(rootSpanName))
        {
            rootSpan.Should().NotBeNull(
                "ActivityListener must record the root span so the publish "
                + "span sees an Activity.Current to inherit TraceId from");
            publishTraceId = Activity.Current!.TraceId;
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = "trace-test" });
            publish.Failed.Should().BeFalse();
        }

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));

        // Give the consume span a moment to stop after the handler returns.
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Activity[] snapshot;
        lock (collected) snapshot = [.. collected];

        var consumeSpan = snapshot.SingleOrDefault(
            a => a.Kind == ActivityKind.Consumer
                && a.OperationName == $"receive {queue}");
        consumeSpan.Should().NotBeNull(
            "consumer-side activity must be started for the dispatched delivery");
        consumeSpan.TraceId.Should().Be(
            publishTraceId,
            "consume span must share the publish span's trace id");
        consumeSpan.ParentSpanId.Should().NotBe(
            default(ActivitySpanId),
            "consume span must have a parent span id (the publish span)");
    }

    [Fact]
    public async Task MessagingOperationType_PublisherEmitsPublishValue_ConsumerEmitsReceiveValue()
    {
        // §21.10 runtime-emission pin: the OTel canonical
        // MESSAGING_OPERATION_TYPE attribute carries the value
        // "publish" on producer spans and "receive" on consumer spans.
        // Spec-driving the attribute NAME (MessagingActivityTags catalog)
        // closes the name-level drift surface; this test closes the
        // value-level drift surface — a refactor renaming the publisher's
        // emission from "publish" to "send" would NOT fail any
        // name-only test, but breaks downstream dashboards / alert
        // filters. The two values are enumerated in the spec catalog at
        // contracts/otel-messaging-tags/otel-messaging-tags.spec.json
        // doc text; this runtime pin asserts the production emit sites
        // ship those literal values.
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "rt.op-type." + Guid.NewGuid().ToString("N")[..8];

        var collected = new List<Activity>();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = src =>
            src.Name is "D2.Shared.Messaging.RabbitMq";
        listener.Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = a =>
        {
            lock (collected) collected.Add(a);
        };
        ActivitySource.AddActivityListener(listener);

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = "op-type-test" });
            publish.Failed.Should().BeFalse();
        }

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));

        // Give the consume span a moment to stop after the handler returns.
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Activity[] snapshot;
        lock (collected) snapshot = [.. collected];

        var publishSpan = snapshot.SingleOrDefault(
            a => a.Kind == ActivityKind.Producer);
        publishSpan.Should().NotBeNull(
            "publisher activity must be started for the publish call");
        publishSpan.Tags
            .Should().Contain(
                t => t.Key == MessagingActivityTags.MESSAGING_OPERATION_TYPE
                    && t.Value == "publish",
                "publisher emit site must ship the literal value \"publish\"");

        var consumeSpan = snapshot.SingleOrDefault(
            a => a.Kind == ActivityKind.Consumer
                && a.OperationName == $"receive {queue}");
        consumeSpan.Should().NotBeNull(
            "consumer activity must be started for the dispatched delivery");
        consumeSpan.Tags
            .Should().Contain(
                t => t.Key == MessagingActivityTags.MESSAGING_OPERATION_TYPE
                    && t.Value == "receive",
                "consumer emit site must ship the literal value \"receive\"");
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

    /// <summary>Distinct subscriber A for cross-talk testing.</summary>
    public sealed class MultiSubA
        : BaseHandler<MultiSubA, IntegrationAuditEvent, Unit>
    {
        /// <summary>Initializes the handler.</summary>
        /// <param name="context">Handler context (DI-resolved).</param>
        public MultiSubA(HandlerContext<MultiSubA> context)
            : base(context)
        {
        }

        /// <inheritdoc />
        protected override ValueTask<D2Result<Unit>> ExecuteAsync(
            IntegrationAuditEvent input, CancellationToken ct)
        {
            TestCollector.Add<MultiSubA, IntegrationAuditEvent>(input);
            return new ValueTask<D2Result<Unit>>(D2Result<Unit>.Ok(Unit.Value));
        }
    }

    /// <summary>Distinct subscriber B for cross-talk testing.</summary>
    public sealed class MultiSubB
        : BaseHandler<MultiSubB, IntegrationAuditEvent, Unit>
    {
        /// <summary>Initializes the handler.</summary>
        /// <param name="context">Handler context (DI-resolved).</param>
        public MultiSubB(HandlerContext<MultiSubB> context)
            : base(context)
        {
        }

        /// <inheritdoc />
        protected override ValueTask<D2Result<Unit>> ExecuteAsync(
            IntegrationAuditEvent input, CancellationToken ct)
        {
            TestCollector.Add<MultiSubB, IntegrationAuditEvent>(input);
            return new ValueTask<D2Result<Unit>>(D2Result<Unit>.Ok(Unit.Value));
        }
    }
}
