// -----------------------------------------------------------------------
// <copyright file="ChannelPoolIdleTtlIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Integration coverage for <see cref="BoundedChannelPool"/> idle-TTL
/// eviction. Channels idle longer than <see cref="ChannelPoolOptions.IdleTtl"/>
/// must be evicted on the next acquire and replaced with a fresh one;
/// channels idle WITHIN the TTL must be reused.
/// </summary>
[Collection("RabbitMq")]
public sealed class ChannelPoolIdleTtlIntegrationTests
{
    private readonly RabbitMqFixture r_fixture;

    /// <summary>Initializes the test class with the shared fixture.</summary>
    /// <param name="fixture">Testcontainers RabbitMQ.</param>
    public ChannelPoolIdleTtlIntegrationTests(RabbitMqFixture fixture)
    {
        r_fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IdleBeyondTtl_EvictsAndCreatesFresh()
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
            2,
            "channel idle longer than IdleTtl must be evicted on next "
            + "acquire and replaced with a fresh one");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IdleWithinTtl_ReusesChannel()
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
            1,
            "channel idle WITHIN IdleTtl must be reused — eviction is for "
            + "stale-pool channels only, not every-second-publish churn");
    }

    private async Task<ID2Connection> BuildRealConnectionAsync()
    {
        IntegrationMessageFixtures.EnsureRegistered();
        var optsBuilder = new ServiceCollection()
            .AddOptions<RabbitMqConnectionOptions>()
            .Configure(o =>
            {
                o.ConnectionUri = r_fixture.ConnectionString;
                o.ClientProvidedName = "channel-pool-idle-ttl-tests";
            });
        var sp = optsBuilder.Services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<RabbitMqConnectionOptions>>();
        var conn = new RabbitMqConnection(opts, NullLogger<RabbitMqConnection>.Instance);
        conn.StartReconnectLoop();
        await conn.ReadyTask.WaitAsync(TimeSpan.FromSeconds(15));
        return conn;
    }

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
            Interlocked.Increment(ref _count);
            return await r_inner.CreateChannelAsync(options, ct);
        }

        public ValueTask DisposeAsync() => r_inner.DisposeAsync();
    }
}
