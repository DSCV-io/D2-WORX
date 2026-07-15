// -----------------------------------------------------------------------
// <copyright file="DlqTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Xunit;

/// <summary>
/// Adversarial DLQ coverage. Handlers fail in different ways and we verify
/// the message lands in the per-queue DLQ. Uses a raw RabbitMQ.Client
/// connection to inspect the DLQ from outside the test host.
/// </summary>
[Collection("RabbitMq")]
public sealed class DlqTests
{
    // Genuine-stuck guard for the async-delivery waits below: a poll-ATTEMPT
    // budget, never a wall-clock deadline. The prior flake was a DateTime.UtcNow
    // deadline in the success path — xUnit runs the RabbitMq collection in parallel
    // with the Unit-test collections, and under that thread-pool saturation the wall
    // clock elapsed while the starved pool deferred the handler-dispatch continuation
    // and the broker's async dead-lettering, tripping the deadline mid-progress. An
    // attempt budget caps the number of polls, not elapsed time: each iteration
    // awaits pollInterval, so under load the effective wait GROWS with the slowdown
    // instead of expiring. At 50 ms/attempt this floor is ~2 min of real progress —
    // far above any healthy delivery (which resolves in well under a second, <20
    // attempts), so it is never load-reachable, yet a permanently-stuck test still
    // terminates (this project ships no xunit.runner.json / framework test timeout).
    private const int _POLL_ATTEMPT_BUDGET = 2400;

    private readonly RabbitMqFixture r_fixture;

    public DlqTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HandlerThrows_MessageGoesToDlq()
    {
        TestCollector.Reset<AlwaysThrowsHandler>();
        var queue = "dlq.thr." + Guid.NewGuid().ToString("N")[..8];
        var marker = "boom-" + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysThrowsHandler>();
            services.AddD2Subscriber<AlwaysThrowsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        // Re-seed after host start so a parallel unit ClearCache cannot leave
        // fixture descriptors missing for this publish.
        IntegrationMessageFixtures.EnsureRegistered();

        // Publish — handler throws — message gets nack'd to DLQ.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse(
                "publish must succeed before waiting for handler/DLQ delivery");
        }

        // Wait for THIS test's delivery (marker-filtered; Count alone is racy
        // against collector Reset and ConcurrentBag.Count approximation).
        await WaitFor(
            () => TestCollector
                .Captured<AlwaysThrowsHandler, IntegrationAuditEvent>()
                .Any(m => m.Marker == marker),
            detail: () =>
                $"HandlerThrows marker={marker}; queue={queue}; "
                + $"collectorCount={TestCollector.Count<AlwaysThrowsHandler>()}; "
                + $"markers=[{string.Join(",", TestCollector
                    .Captured<AlwaysThrowsHandler, IntegrationAuditEvent>()
                    .Select(m => m.Marker ?? "<null>"))}]");

        // Wait for the message to surface in the DLQ. RabbitMQ's
        // dead-lettering is asynchronous from the consumer's NACK.
        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HandlerReturnsFailure_MessageGoesToDlq()
    {
        TestCollector.Reset<AlwaysFailsHandler>();
        var queue = "dlq.fail." + Guid.NewGuid().ToString("N")[..8];
        var marker = "fail-result-" + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysFailsHandler>();
            services.AddD2Subscriber<AlwaysFailsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        // Re-seed after host start so a parallel unit ClearCache cannot leave
        // fixture descriptors missing for this publish (same race class as
        // IntegrationMessageFixtures / PublishConsumeRoundTripTests).
        IntegrationMessageFixtures.EnsureRegistered();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse(
                "publish must succeed before waiting for handler/DLQ delivery");
        }

        // Marker-filtered wait: Count alone can race with collector Reset if a
        // prior host's late delivery lands on AlwaysFailsHandler after Reset.
        await WaitFor(
            () => TestCollector
                .Captured<AlwaysFailsHandler, IntegrationAuditEvent>()
                .Any(m => m.Marker == marker),
            detail: () =>
                $"HandlerReturnsFailure marker={marker}; queue={queue}; "
                + $"collectorCount={TestCollector.Count<AlwaysFailsHandler>()}; "
                + $"markers=[{string.Join(",", TestCollector
                    .Captured<AlwaysFailsHandler, IntegrationAuditEvent>()
                    .Select(m => m.Marker ?? "<null>"))}]");

        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DlqMessage_CarriesFailureReasonHeader_WithCauseAndErrorCode()
    {
        // DLX republish verification: the nacked message is republished to
        // the DLX with an x-d2-failure-reason header attached. Without this
        // behavior the DLQ message would arrive header-less (BasicNack-no-requeue
        // → broker x-dead-letter-exchange routes a copy without our diagnostic header).
        TestCollector.Reset<AlwaysThrowsHandler>();
        var queue = "dlq.hdr." + Guid.NewGuid().ToString("N")[..8];
        var marker = "header-test-" + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysThrowsHandler>();
            services.AddD2Subscriber<AlwaysThrowsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        IntegrationMessageFixtures.EnsureRegistered();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            var publish = await bus.PublishAsync(
                new IntegrationAuditEvent { Marker = marker });
            publish.Failed.Should().BeFalse(
                "publish must succeed before waiting for handler/DLQ delivery");
        }

        await WaitFor(
            () => TestCollector
                .Captured<AlwaysThrowsHandler, IntegrationAuditEvent>()
                .Any(m => m.Marker == marker),
            detail: () =>
                $"DlqHeader marker={marker}; queue={queue}; "
                + $"collectorCount={TestCollector.Count<AlwaysThrowsHandler>()}");

        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1);

        // Pull the DLQ message and inspect its x-d2-failure-reason header.
        var factory = new ConnectionFactory { Uri = new Uri(r_fixture.ConnectionString) };
        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();
        var get = await channel.BasicGetAsync(dlqName, autoAck: true);
        get.Should().NotBeNull("DLQ should contain the failed message");

        var headers = get.BasicProperties.Headers;
        headers.Should().NotBeNull();
        headers.Should().ContainKey(AmqpHeaders.FAILURE_REASON);

        var failureBytes = (byte[])headers[AmqpHeaders.FAILURE_REASON]!;
        var failureJson = Encoding.UTF8.GetString(failureBytes);
        var doc = JsonDocument.Parse(failureJson);

        // BaseHandler's universal try/catch converts the handler's
        // InvalidOperationException into a D2Result.UnhandledException, so
        // the consumer sees a result-failure path (not the bare exception
        // path). Cause/errorCode reflect that conversion.
        doc.RootElement.GetProperty("cause").GetString()
            .Should().Be("HANDLER_RESULT_FAILURE");
        doc.RootElement.GetProperty("errorCode").GetString()
            .Should().Be("UNHANDLED_EXCEPTION");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopologyDeclaration_IsIdempotent()
    {
        var queue = "dlq.idem." + Guid.NewGuid().ToString("N")[..8];

        // First host declares the topology.
        using (var host1 = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue));
        }))
        {
            // Topology is declared during host start. Already verified by the
            // host coming up successfully.
            _ = host1;
        }

        // Second host re-declares against the same broker — must NOT throw
        // (RabbitMQ's *DeclareAsync calls are no-ops on identical pre-existing
        // entities). If we incorrectly tried to declare with different
        // arguments, the broker would reject with PRECONDITION_FAILED.
        using var host2 = await StartHostAsync(services =>
        {
            services.AddTransient<AuditCapturingHandler>();
            services.AddD2Subscriber<AuditCapturingHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue));
        });

        // Reaching here without exception is the assertion.
    }

    // Attempt-budgeted stuck-guard — see _POLL_ATTEMPT_BUDGET; no wall-clock deadline.
    private static async Task WaitFor(
        Func<bool> predicate,
        TimeSpan? pollInterval = null,
        Func<string>? detail = null)
    {
        pollInterval ??= TimeSpan.FromMilliseconds(50);
        for (var attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++)
        {
            if (predicate()) return;
            await Task.Delay(pollInterval.Value);
        }

        var suffix = detail is null ? string.Empty : " " + detail();
        throw new TimeoutException(
            $"Predicate did not become true within {_POLL_ATTEMPT_BUDGET} poll attempts."
            + suffix);
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
}
