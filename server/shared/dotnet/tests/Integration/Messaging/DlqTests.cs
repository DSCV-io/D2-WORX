// -----------------------------------------------------------------------
// <copyright file="DlqTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Headers.Amqp;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Topology;
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

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysThrowsHandler>();
            services.AddD2Subscriber<AlwaysThrowsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        // Publish — handler throws — message gets nack'd to DLQ.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new IntegrationAuditEvent { Marker = "boom" });
        }

        // Wait for handler to be invoked (it threw).
        await WaitFor(
            () => TestCollector.Count<AlwaysThrowsHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));

        // Wait for the message to surface in the DLQ. RabbitMQ's
        // dead-lettering is asynchronous from the consumer's NACK.
        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1, timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HandlerReturnsFailure_MessageGoesToDlq()
    {
        TestCollector.Reset<AlwaysFailsHandler>();
        var queue = "dlq.fail." + Guid.NewGuid().ToString("N")[..8];

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysFailsHandler>();
            services.AddD2Subscriber<AlwaysFailsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new IntegrationAuditEvent { Marker = "fail-result" });
        }

        await WaitFor(
            () => TestCollector.Count<AlwaysFailsHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));

        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1, timeout: TimeSpan.FromSeconds(10));
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

        using var host = await StartHostAsync(services =>
        {
            services.AddTransient<AlwaysThrowsHandler>();
            services.AddD2Subscriber<AlwaysThrowsHandler, IntegrationAuditEvent>(
                IntegrationSubscriptionFactory.ForAuditEvent(queue, prefetch: 5));
        });

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new IntegrationAuditEvent { Marker = "header-test" });
        }

        await WaitFor(
            () => TestCollector.Count<AlwaysThrowsHandler>() > 0,
            timeout: TimeSpan.FromSeconds(15));

        var dlqName = DlqNaming.DlqFor(queue);
        await WaitForQueueCount(dlqName, expected: 1, timeout: TimeSpan.FromSeconds(10));

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

    private async Task WaitForQueueCount(string queueName, int expected, TimeSpan timeout)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(r_fixture.ConnectionString),
        };

        await using var conn = await factory.CreateConnectionAsync();
        await using var channel = await conn.CreateChannelAsync();

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var declareOk = await channel.QueueDeclarePassiveAsync(queueName);
            if (declareOk.MessageCount >= expected) return;
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        var final = await channel.QueueDeclarePassiveAsync(queueName);
        throw new TimeoutException(
            $"Queue '{queueName}' had {final.MessageCount} messages "
            + $"after {timeout}, expected >= {expected}.");
    }
}
