// -----------------------------------------------------------------------
// <copyright file="FanoutBroadcastDeliveryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// True fanout two-queue delivery over a real broker — the plaintext
/// redaction-broadcast shape (prod-used by <c>AuthKeyRotated</c>). Sibling
/// <see cref="PublishConsumeRoundTripTests.MultipleSubscribers_NoCrossTalk"/>
/// proves distinct-routing-key ISOLATION on a topic exchange; this proves the
/// complementary property the deferred row left open: ONE publish to a fanout
/// exchange is delivered to EVERY distinct queue bound to it (two separate
/// consumers, each on its own <see cref="QueuePattern.FanoutExclusiveAutoDelete"/>
/// queue, BOTH receive the single message).
/// </summary>
[Collection("RabbitMq")]
public sealed class FanoutBroadcastDeliveryTests
{
    // Genuine-stuck guard for the async-delivery wait below: a poll-ATTEMPT
    // budget, never a wall-clock deadline. Each iteration awaits pollInterval,
    // so under thread-pool saturation (the RabbitMq collection runs parallel to
    // the Unit collections) the effective wait GROWS with the slowdown instead
    // of expiring mid-progress. At 50 ms/attempt this floor is ~2 min of real
    // progress — far above any healthy fanout delivery (well under a second),
    // so it is never load-reachable, yet a permanently-stuck test still
    // terminates (this project ships no xunit.runner.json / framework timeout).
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private readonly RabbitMqFixture r_fixture;

    public FanoutBroadcastDeliveryTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FanoutExchange_DeliversOnePublish_ToEveryDistinctBoundQueue()
    {
        TestCollector.Reset<BroadcastFixtureSubA>();
        TestCollector.Reset<BroadcastFixtureSubB>();

        // Two DISTINCT services, each declaring its OWN exclusive fanout queue
        // (distinct base names → distinct resolved queues) bound to the SAME
        // fanout exchange resolved from BroadcastFixtureEvent.
        var queueA = "fan.a." + Guid.NewGuid().ToString("N")[..8];
        var queueB = "fan.b." + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<BroadcastFixtureSubA>();
            services.AddTransient<BroadcastFixtureSubB>();

            services.AddD2Subscriber<BroadcastFixtureSubA, BroadcastFixtureEvent>(
                IntegrationSubscriptionFactory.ForBroadcastEvent(queueA));
            services.AddD2Subscriber<BroadcastFixtureSubB, BroadcastFixtureEvent>(
                IntegrationSubscriptionFactory.ForBroadcastEvent(queueB));
        });

        var marker = "broadcast-" + Guid.NewGuid().ToString("N");
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new BroadcastFixtureEvent { Marker = marker });
            publish.Failed.Should().BeFalse();
        }

        // BOTH distinct queues must receive the single publish (fanout).
        await WaitFor(
            () => TestCollector.Count<BroadcastFixtureSubA>() > 0
                && TestCollector.Count<BroadcastFixtureSubB>() > 0);

        TestCollector.Captured<BroadcastFixtureSubA, BroadcastFixtureEvent>()
            .Should().ContainSingle(
                "the fanout exchange delivers the one publish to subscriber A's queue")
            .Which.Marker.Should().Be(marker);
        TestCollector.Captured<BroadcastFixtureSubB, BroadcastFixtureEvent>()
            .Should().ContainSingle(
                "the fanout exchange delivers the SAME publish to subscriber B's distinct queue")
            .Which.Marker.Should().Be(marker);
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

    /// <summary>Distinct broadcast subscriber A — its own fanout queue.</summary>
    public sealed class BroadcastFixtureSubA
        : BaseHandler<BroadcastFixtureSubA, BroadcastFixtureEvent, Unit>
    {
        /// <summary>Initializes the handler.</summary>
        /// <param name="context">Handler context (DI-resolved).</param>
        public BroadcastFixtureSubA(HandlerContext<BroadcastFixtureSubA> context)
            : base(context)
        {
        }

        /// <inheritdoc />
        protected override ValueTask<D2Result<Unit>> ExecuteAsync(
            BroadcastFixtureEvent input, CancellationToken ct)
        {
            TestCollector.Add<BroadcastFixtureSubA, BroadcastFixtureEvent>(input);
            return new ValueTask<D2Result<Unit>>(D2Result<Unit>.Ok(Unit.Value));
        }
    }

    /// <summary>Distinct broadcast subscriber B — its own fanout queue.</summary>
    public sealed class BroadcastFixtureSubB
        : BaseHandler<BroadcastFixtureSubB, BroadcastFixtureEvent, Unit>
    {
        /// <summary>Initializes the handler.</summary>
        /// <param name="context">Handler context (DI-resolved).</param>
        public BroadcastFixtureSubB(HandlerContext<BroadcastFixtureSubB> context)
            : base(context)
        {
        }

        /// <inheritdoc />
        protected override ValueTask<D2Result<Unit>> ExecuteAsync(
            BroadcastFixtureEvent input, CancellationToken ct)
        {
            TestCollector.Add<BroadcastFixtureSubB, BroadcastFixtureEvent>(input);
            return new ValueTask<D2Result<Unit>>(D2Result<Unit>.Ok(Unit.Value));
        }
    }
}
