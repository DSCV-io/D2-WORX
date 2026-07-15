// -----------------------------------------------------------------------
// <copyright file="RedisCacheInvalidationBackplaneTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Caching.Distributed;

using AwesomeAssertions;
using DcsvIo.D2.Caching.Distributed.Redis;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

/// <summary>
/// End-to-end tests for the Redis-backed invalidation backplane.
/// Verifies the universal "everyone acts" rule, multi-subscriber
/// independence, error isolation, and dispose behavior — all against a
/// real Redis pub/sub channel.
/// </summary>
[Collection("Redis")]
public sealed class RedisCacheInvalidationBackplaneTests
{
    private readonly RedisFixture r_fixture;

    public RedisCacheInvalidationBackplaneTests(RedisFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    public async Task Subscribe_ReceivesPublishedKeys_IncludingSelf()
    {
        // Universal "everyone acts" rule: publisher receives its own
        // messages. No sender-ID filter.
        var channel = $"test-channel-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var received = new List<string>();
        var gate = new TaskCompletionSource();
        await using var subscription = backplane.Subscribe((key, _) =>
        {
            lock (received)
            {
                received.Add(key);
                if (received.Count >= 3)
                    gate.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        await backplane.PublishInvalidationAsync("k1");
        await backplane.PublishInvalidationAsync("k2");
        await backplane.PublishInvalidationAsync("k3");

        await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().BeEquivalentTo(["k1", "k2", "k3"]);
    }

    [Fact]
    public async Task Subscribe_MultipleSubscribers_AllReceiveEveryMessage()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var subA = new List<string>();
        var subB = new List<string>();
        var gate = new TaskCompletionSource();
        var locker = new object();

        var totalExpected = 6;  // 2 subs × 3 messages each
        var totalReceived = 0;

        void Track(List<string> store, string key)
        {
            lock (locker)
            {
                store.Add(key);
                totalReceived++;
                if (totalReceived >= totalExpected)
                    gate.TrySetResult();
            }
        }

        await using var sa = backplane.Subscribe((key, _) =>
        {
            Track(subA, key);
            return ValueTask.CompletedTask;
        });
        await using var sb = backplane.Subscribe((key, _) =>
        {
            Track(subB, key);
            return ValueTask.CompletedTask;
        });

        await backplane.PublishInvalidationAsync("k1");
        await backplane.PublishInvalidationAsync("k2");
        await backplane.PublishInvalidationAsync("k3");

        await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        subA.Should().BeEquivalentTo(["k1", "k2", "k3"]);
        subB.Should().BeEquivalentTo(["k1", "k2", "k3"]);
    }

    [Fact]
    public async Task Subscribe_HandlerThrows_DoesNotBreakOtherSubscribers()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var goodReceived = new List<string>();
        var gate = new TaskCompletionSource();

        await using var bad = backplane.Subscribe(
            (_, _) => throw new InvalidOperationException("nope"));
        await using var good = backplane.Subscribe((key, _) =>
        {
            lock (goodReceived)
            {
                goodReceived.Add(key);
                if (goodReceived.Count >= 2)
                    gate.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        await backplane.PublishInvalidationAsync("k1");
        await backplane.PublishInvalidationAsync("k2");

        await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        goodReceived.Should().BeEquivalentTo(["k1", "k2"]);
    }

    [Fact]
    public async Task Dispose_StopsHandlerInvocation()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var received = new List<string>();
        var subscription = backplane.Subscribe((key, _) =>
        {
            lock (received) received.Add(key);
            return ValueTask.CompletedTask;
        });

        await backplane.PublishInvalidationAsync("before-dispose");
        await Task.Delay(200);  // drain

        await subscription.DisposeAsync();
        await backplane.PublishInvalidationAsync("after-dispose");
        await Task.Delay(200);

        received.Should().Contain("before-dispose");
        received.Should().NotContain("after-dispose");
    }

    [Fact]
    public async Task PublishInvalidationManyAsync_AllReceived()
    {
        var channel = $"test-channel-{Guid.NewGuid():N}";
        await using var backplane = NewBackplane(channel);

        var received = new List<string>();
        var gate = new TaskCompletionSource();
        await using var subscription = backplane.Subscribe((key, _) =>
        {
            lock (received)
            {
                received.Add(key);
                if (received.Count >= 5)
                    gate.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        await backplane.PublishInvalidationManyAsync(["k1", "k2", "k3", "k4", "k5"]);
        await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().BeEquivalentTo(["k1", "k2", "k3", "k4", "k5"]);
    }

    [Fact]
    public async Task CrossInstance_PublishOnA_ReceivedOnB()
    {
        // Two backplane instances on the same channel = two "instances" in
        // the cluster sense. A publishes; B receives.
        var channel = $"test-channel-{Guid.NewGuid():N}";
        await using var instanceA = NewBackplane(channel);
        await using var instanceB = NewBackplane(channel);

        var receivedOnB = new List<string>();
        var gate = new TaskCompletionSource();
        await using var subscription = instanceB.Subscribe((key, _) =>
        {
            lock (receivedOnB)
            {
                receivedOnB.Add(key);
                if (receivedOnB.Count >= 1)
                    gate.TrySetResult();
            }

            return ValueTask.CompletedTask;
        });

        await instanceA.PublishInvalidationAsync("from-A");

        await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
        receivedOnB.Should().Contain("from-A");
    }

    [MustDisposeResource(false)]
    private RedisCacheInvalidationBackplane NewBackplane(string channel)
    {
        var redis = ConnectionMultiplexer.Connect(r_fixture.ConnectionString);
        var opts = Options.Create(new RedisCacheOptions
        {
            ConnectionString = r_fixture.ConnectionString,
            InvalidationChannel = channel,
        });
        return new RedisCacheInvalidationBackplane(
            redis,
            opts,
            NullLogger<RedisCacheInvalidationBackplane>.Instance);
    }
}
