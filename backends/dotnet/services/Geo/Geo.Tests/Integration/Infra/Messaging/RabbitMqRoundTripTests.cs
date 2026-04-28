// -----------------------------------------------------------------------
// <copyright file="RabbitMqRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Integration.Infra.Messaging;

using D2.Events.Protos.V1;
using D2.Shared.Messaging.RabbitMQ;
using D2.Shared.Messaging.RabbitMQ.Conventions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

/// <summary>
/// Round-trip integration tests that publish proto messages via raw AMQP to a real RabbitMQ
/// container and verify <see cref="ProtoConsumer{T}"/> processes them correctly.
/// </summary>
[MustDisposeResource(false)]
public class RabbitMqRoundTripTests : IAsyncLifetime
{
    private RabbitMqContainer _container = null!;
    private IConnection _connection = null!;
    private ProtoPublisher _publisher = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _container = new RabbitMqBuilder("rabbitmq:4.1-management")
            .Build();
        await _container.StartAsync(Ct);

        var factory = new ConnectionFactory
        {
            Uri = new Uri(_container.GetConnectionString()),
        };
        _connection = await factory.CreateConnectionAsync(Ct);
        _publisher = new ProtoPublisher(_connection, Mock.Of<ILogger<ProtoPublisher>>());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _container.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tests that a published GeoRefDataUpdatedEvent is delivered to a broadcast consumer
    /// with the correct version.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Publish_GeoRefDataUpdatedEvent_ConsumerReceivesCorrectVersion()
    {
        // Arrange
        var receivedTcs = new TaskCompletionSource<GeoRefDataUpdatedEvent>();
        var exchange = AmqpConventions.EventExchange("geo");

        await using var consumer = await ProtoConsumer<GeoRefDataUpdatedEvent>.CreateBroadcastAsync(
            _connection,
            exchange,
            "test-01",
            (message, _) =>
            {
                receivedTcs.TrySetResult(message);
                return Task.CompletedTask;
            },
            Mock.Of<ILogger>(),
            Ct);

        // Act
        await _publisher.PublishAsync(
            exchange,
            new GeoRefDataUpdatedEvent { Version = "3.0.0" },
            ct: Ct);

        // Assert
        var received = await receivedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Equal("3.0.0", received.Version);
    }

    /// <summary>
    /// Tests that when the handler throws, the message is NACKed and redelivered.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Publish_WhenHandlerFails_MessageIsRedelivered()
    {
        // Arrange
        var callCount = 0;
        var secondCallTcs = new TaskCompletionSource<GeoRefDataUpdatedEvent>();
        var exchange = AmqpConventions.EventExchange("geo");

        await using var consumer = await ProtoConsumer<GeoRefDataUpdatedEvent>.CreateBroadcastAsync(
            _connection,
            exchange,
            "test-02",
            (message, _) =>
            {
                var count = Interlocked.Increment(ref callCount);
                if (count == 1)
                {
                    throw new TimeoutException("Transient failure");
                }

                secondCallTcs.TrySetResult(message);
                return Task.CompletedTask;
            },
            Mock.Of<ILogger>(),
            Ct);

        // Act
        await _publisher.PublishAsync(
            exchange,
            new GeoRefDataUpdatedEvent { Version = "4.0.0" },
            ct: Ct);

        // Assert
        var received = await secondCallTcs.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Equal("4.0.0", received.Version);
        Assert.True(callCount >= 2, $"Expected at least 2 calls, got {callCount}.");
    }

    /// <summary>
    /// Tests that publishing multiple messages delivers all of them to the consumer.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task Publish_MultipleMessages_AllDelivered()
    {
        // Arrange
        var receivedVersions = new List<string>();
        var allReceived = new TaskCompletionSource();
        var exchange = AmqpConventions.EventExchange("geo");

        await using var consumer = await ProtoConsumer<GeoRefDataUpdatedEvent>.CreateBroadcastAsync(
            _connection,
            exchange,
            "test-03",
            (message, _) =>
            {
                lock (receivedVersions)
                {
                    receivedVersions.Add(message.Version);
                    if (receivedVersions.Count == 2)
                    {
                        allReceived.TrySetResult();
                    }
                }

                return Task.CompletedTask;
            },
            Mock.Of<ILogger>(),
            Ct);

        // Act
        await _publisher.PublishAsync(
            exchange,
            new GeoRefDataUpdatedEvent { Version = "5.0.0" },
            ct: Ct);
        await _publisher.PublishAsync(
            exchange,
            new GeoRefDataUpdatedEvent { Version = "6.0.0" },
            ct: Ct);

        // Assert
        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Contains("5.0.0", receivedVersions);
        Assert.Contains("6.0.0", receivedVersions);
    }
}
