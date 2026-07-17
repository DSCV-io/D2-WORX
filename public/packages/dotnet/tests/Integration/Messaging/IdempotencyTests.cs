// -----------------------------------------------------------------------
// <copyright file="IdempotencyTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

/// <summary>
/// Adversarial coverage for the optional consumer idempotency layer. The
/// store is opt-in per subscriber and the consumer must short-circuit a
/// duplicate without re-invoking the handler.
/// </summary>
[Collection("RabbitMq")]
public sealed class IdempotencyTests
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

    public IdempotencyTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DuplicateMessageId_HandlerInvokedOnce()
    {
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "idem.dup." + Guid.NewGuid().ToString("N")[..8];
        var fakeStore = new FakeIdempotencyStore();

        using var host = await StartHostAsync(services =>
        {
            services.AddSingleton<IMessageIdempotencyStore>(fakeStore);
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(
                    queue, prefetch: 5, idempotency: true));
        });

        // Publish the same logical message twice. We piggyback on the bus's
        // own message-id generation but pre-seed the fake store as if the
        // first delivery had already been recorded — second delivery should
        // be skipped.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new IntegrationAuditEvent { Marker = "first" });
        }

        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0);
        var firstCount = TestCollector.Count<AuditCapturingHandler>();
        firstCount.Should().Be(1);

        // Now: republish the SAME body to the same exchange, but with the
        // already-seen message-id. We do this by directly using the AMQP
        // client to inject a delivery whose message-id matches an entry we
        // pre-seeded into the fake store. The consumer should ack-and-skip.
        var seenMessageId = Guid.CreateVersion7().ToString();
        fakeStore.Seed(seenMessageId);

        await PublishRawAsync(queue, seenMessageId);

        // Wait long enough that the duplicate would have processed if the
        // idempotency check were missing.
        await Task.Delay(TimeSpan.FromSeconds(3));

        TestCollector.Count<AuditCapturingHandler>()
            .Should().Be(firstCount, "duplicate must NOT trigger handler");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MarkSeenFails_MessageRoutesToDlq_HandlerNotReplayed()
    {
        // A failing MarkSeenAsync
        // must NACK to DLQ rather than silently ack. Acking without a
        // written mark would leave the dedup window unguarded for that
        // message-id; a redelivery would re-run the handler, which is the
        // exact scenario idempotency exists to prevent.
        TestCollector.Reset<AuditCapturingHandler>();
        var queue = "idem.markfail." + Guid.NewGuid().ToString("N")[..8];
        var failingStore = new FailingMarkStore();

        using var host = await StartHostAsync(services =>
        {
            services.AddSingleton<IMessageIdempotencyStore>(failingStore);
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(
                    queue, prefetch: 1, idempotency: true));
        });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new IntegrationAuditEvent { Marker = "mark-fail" });
        }

        // Handler runs once (HasSeen returned false → handler invoked).
        await WaitFor(
            () => TestCollector.Count<AuditCapturingHandler>() > 0);
        TestCollector.Count<AuditCapturingHandler>().Should().Be(1);

        // MarkSeen fails → consumer NACKs no-requeue → broker DLX routes
        // the message to the DLQ. Verify it landed there.
        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);

        // And ensure the handler was NOT replayed by the broker (the NACK
        // is no-requeue; redelivery only happens via the DLX path which
        // doesn't loop back to the primary queue).
        await Task.Delay(TimeSpan.FromSeconds(2));
        TestCollector.Count<AuditCapturingHandler>().Should().Be(
            1, "handler must not run twice on a MarkSeen-failure NACK path");
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

    private async Task WaitForQueueCount(string queueName, int expected)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(r_fixture.ConnectionString),
        };

        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();

        // Attempt-budgeted stuck-guard (see _POLL_ATTEMPT_BUDGET) — dead-lettering
        // is async broker-side, so poll a bounded number of times rather than
        // against a wall-clock deadline a loaded broker can outrun.
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            var declareOk = await channel.QueueDeclarePassiveAsync(queueName);
            if (declareOk.MessageCount >= expected) return;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        var final = await channel.QueueDeclarePassiveAsync(queueName);
        throw new TimeoutException(
            $"Queue '{queueName}' had {final.MessageCount} messages after "
            + $"{_POLL_ATTEMPT_BUDGET} poll attempts, expected >= {expected}.");
    }

    private async Task PublishRawAsync(string queueName, string messageId)
    {
        // Direct publish via the broker so we can stamp the message-id
        // header to match a pre-seeded idempotency entry. Bypasses the bus —
        // tests the consumer's idempotency path in isolation.
        var factory = new ConnectionFactory
        {
            Uri = new Uri(r_fixture.ConnectionString),
        };

        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();

        var props = new BasicProperties
        {
            ContentType = "application/octet-stream",
            MessageId = messageId,
            DeliveryMode = DeliveryModes.Persistent,
        };

        // Empty body is enough — consumer's idempotency check fires before
        // body decoding, so we never need to construct a valid encrypted
        // frame for this scenario.
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: props,
            body: ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>Store whose <c>HasSeenAsync</c> always returns false (so the
    /// handler runs) but whose <c>MarkSeenAsync</c> always fails with
    /// ServiceUnavailable — proves the MarkSeenAsync failure path routes to
    /// DLQ instead of silently acking without a written mark.</summary>
    private sealed class FailingMarkStore : IMessageIdempotencyStore
    {
        public ValueTask<D2Result<bool>> HasSeenAsync(
            string messageId, CancellationToken ct = default)
            => new(D2Result<bool>.Ok(data: false));

        public ValueTask<D2Result> MarkSeenAsync(
            string messageId, CancellationToken ct = default)
            => new(D2Result.ServiceUnavailable());
    }

    /// <summary>
    /// In-memory store with explicit pre-seeding for the test scenarios.
    /// Implements only the methods the consumer calls — null defaults
    /// elsewhere.
    /// </summary>
    private sealed class FakeIdempotencyStore : IMessageIdempotencyStore
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

        public void Seed(string messageId)
        {
            lock (_seen)
                _seen.Add(messageId);
        }

        public ValueTask<D2Result<bool>> HasSeenAsync(
            string messageId, CancellationToken ct = default)
        {
            lock (_seen)
            {
                return new ValueTask<D2Result<bool>>(
                    D2Result<bool>.Ok(data: _seen.Contains(messageId)));
            }
        }

        public ValueTask<D2Result> MarkSeenAsync(
            string messageId, CancellationToken ct = default)
        {
            lock (_seen)
                _seen.Add(messageId);
            return new ValueTask<D2Result>(D2Result.Ok());
        }
    }
}
