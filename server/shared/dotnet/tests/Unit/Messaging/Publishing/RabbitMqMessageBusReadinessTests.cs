// -----------------------------------------------------------------------
// <copyright file="RabbitMqMessageBusReadinessTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging.Publishing;

using AwesomeAssertions;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Channels;
using D2.Shared.Messaging.RabbitMq.Connection;
using D2.Shared.Messaging.RabbitMq.Publishing;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// <see cref="IMessageBus.WaitForReadyAsync"/> cancellation contract.
/// Hosted services often gate "first publish" on this readiness signal;
/// the cancellation path must surface as <see cref="OperationCanceledException"/>
/// so the host shutdown sequence terminates cleanly.
/// </summary>
public sealed class RabbitMqMessageBusReadinessTests
{
    [Fact]
    public async Task WaitForReadyAsync_CancelledBeforeReady_ThrowsOperationCanceled()
    {
        // Connection that never becomes ready — TaskCompletionSource left
        // unresolved. Cancellation must surface as OperationCanceledException
        // (not silently swallowed, not TaskCanceledException uncaught).
        var bus = BuildBusWithStubConnection(neverReady: true);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        var token = cts.Token;

        var act = async () => await bus.WaitForReadyAsync(token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitForReadyAsync_AlreadyReady_CompletesImmediately()
    {
        var bus = BuildBusWithStubConnection(neverReady: false);
        await bus.WaitForReadyAsync(CancellationToken.None);
    }

    private static IMessageBus BuildBusWithStubConnection(bool neverReady)
    {
        var conn = new StubConnection(neverReady);
        var pool = new StubChannelPool();
        var publisherOpts = Options.Create(new RabbitMqPublisherOptions());
        var scopeFactory = new ServiceCollection().BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var logger = LoggerFactory.Create(_ => { })
            .CreateLogger<RabbitMqMessageBus>();
        return new RabbitMqMessageBus(
            pool,
            scopeFactory,
            conn,
            publisherOpts,
            logger);
    }

    private sealed class StubConnection : ID2Connection
    {
        public StubConnection(bool neverReady)
        {
            ReadyTask = neverReady
                ? new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously).Task
                : Task.CompletedTask;
        }

        public bool IsOpen => ReadyTask.IsCompletedSuccessfully;

        public Task ReadyTask { get; }

        public void StartReconnectLoop()
        {
        }

        public ValueTask<IChannel> CreateChannelAsync(
            CreateChannelOptions? options = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask DisposeAsync() => default;
    }

    private sealed class StubChannelPool : IChannelPool
    {
        public ValueTask<ChannelLease> AcquireAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask DisposeAsync() => default;
    }
}
