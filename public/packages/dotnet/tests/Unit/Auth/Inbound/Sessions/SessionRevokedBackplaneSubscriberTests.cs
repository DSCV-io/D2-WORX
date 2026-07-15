// -----------------------------------------------------------------------
// <copyright file="SessionRevokedBackplaneSubscriberTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Sessions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Sessions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class SessionRevokedBackplaneSubscriberTests
{
    [Fact]
    public async Task StartAsync_NoBackplaneRegistered_NoOps()
    {
        var subscriber = MakeSubscriber(backplane: null);

        await subscriber.StartAsync(CancellationToken.None);
        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_WithBackplane_SubscribesAndUnsubscribesOnStop()
    {
        var backplane = new FakeBackplane();
        var subscriber = MakeSubscriber(backplane);

        await subscriber.StartAsync(CancellationToken.None);
        backplane.Subscribers.Should().Be(1);

        await subscriber.StopAsync(CancellationToken.None);
        backplane.Subscribers.Should().Be(0);
    }

    [Fact]
    public async Task BackplaneEvent_MatchingSessionPrefix_DoesNotThrow()
    {
        // Adversarial: telemetry-only — no return value to assert; verify
        // the handler accepts the matching key without throwing.
        var backplane = new FakeBackplane();
        var subscriber = MakeSubscriber(backplane);

        await subscriber.StartAsync(CancellationToken.None);
        var act = async () => await backplane.PublishAsync("session:abc-123");

        await act.Should().NotThrowAsync();

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BackplaneEvent_UnrelatedKey_DoesNotThrow()
    {
        var backplane = new FakeBackplane();
        var subscriber = MakeSubscriber(backplane);

        await subscriber.StartAsync(CancellationToken.None);
        var act = async () => await backplane.PublishAsync("jwks:default");

        await act.Should().NotThrowAsync();

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new SessionRevokedBackplaneSubscriber(
            options: null!,
            logger: NullLogger<SessionRevokedBackplaneSubscriber>.Instance,
            backplane: null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var options = Options.Create(new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
        });

        var act = () => new SessionRevokedBackplaneSubscriber(
            options: options,
            logger: null!,
            backplane: null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task StopAsync_TwiceIsIdempotent()
    {
        // Pin the disposal idempotency invariant: calling StopAsync twice must
        // not throw / double-dispose / leak. Hosted-service shutdown can cycle
        // through StopAsync more than once during graceful shutdown races.
        var backplane = new FakeBackplane();
        var subscriber = MakeSubscriber(backplane);
        await subscriber.StartAsync(CancellationToken.None);

        await subscriber.StopAsync(CancellationToken.None);
        var act = async () => await subscriber.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        backplane.Subscribers.Should().Be(0);
    }

    [Fact]
    public async Task DisposeAsync_TwiceIsIdempotent()
    {
        var backplane = new FakeBackplane();
        var subscriber = MakeSubscriber(backplane);
        await subscriber.StartAsync(CancellationToken.None);

        await subscriber.DisposeAsync();
        var act = async () => await subscriber.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    private static SessionRevokedBackplaneSubscriber MakeSubscriber(
        ICacheInvalidationBackplane? backplane)
        => new(
            Options.Create(new AuthOptions
            {
                Issuer = new Uri("https://edge.internal"),
                Audience = "files",
            }),
            NullLogger<SessionRevokedBackplaneSubscriber>.Instance,
            backplane);

    private sealed class FakeBackplane : ICacheInvalidationBackplane
    {
        private readonly ConcurrentDictionary<int, Func<string, CancellationToken, ValueTask>>
            r_handlers = new();

        private int _nextId;

        public int Subscribers => r_handlers.Count;

        public IAsyncDisposable Subscribe(Func<string, CancellationToken, ValueTask> handler)
        {
            var id = Interlocked.Increment(ref _nextId);
            r_handlers[id] = handler;
            return new Subscription(this, id);
        }

        public async ValueTask PublishAsync(string key)
        {
            foreach (var handler in r_handlers.Values)
                await handler(key, CancellationToken.None);
        }

        public ValueTask<D2Result> PublishInvalidationAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> PublishInvalidationManyAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask DisposeAsync() => default;

        private sealed class Subscription : IAsyncDisposable
        {
            private readonly FakeBackplane r_owner;
            private readonly int r_id;

            public Subscription(FakeBackplane owner, int id)
            {
                r_owner = owner;
                r_id = id;
            }

            public ValueTask DisposeAsync()
            {
                r_owner.r_handlers.TryRemove(r_id, out _);
                return default;
            }
        }
    }
}
