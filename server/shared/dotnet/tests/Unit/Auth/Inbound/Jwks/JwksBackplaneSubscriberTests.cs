// -----------------------------------------------------------------------
// <copyright file="JwksBackplaneSubscriberTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Jwks;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Jwks;
using D2.Shared.Caching;
using D2.Shared.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class JwksBackplaneSubscriberTests
{
    [Fact]
    public async Task StartAsync_NoBackplaneRegistered_NoOps()
    {
        var jwks = new FakeJwksProvider();
        var subscriber = MakeSubscriber(jwks, backplane: null);

        await subscriber.StartAsync(CancellationToken.None);
        await subscriber.StopAsync(CancellationToken.None);

        jwks.RefreshCount.Should().Be(0);
    }

    [Fact]
    public async Task BackplaneEvent_MatchingKey_TriggersRefresh()
    {
        var jwks = new FakeJwksProvider();
        var backplane = new FakeBackplane();
        var subscriber = MakeSubscriber(jwks, backplane);

        await subscriber.StartAsync(CancellationToken.None);
        await backplane.PublishAsync("d2.security.key-rotated:jwks");

        jwks.RefreshCount.Should().Be(1);

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task BackplaneEvent_UnrelatedKey_DoesNotTriggerRefresh()
    {
        var jwks = new FakeJwksProvider();
        var backplane = new FakeBackplane();
        var subscriber = MakeSubscriber(jwks, backplane);

        await subscriber.StartAsync(CancellationToken.None);
        await backplane.PublishAsync("session:abc-123");
        await backplane.PublishAsync("d2.security.key-rotated:other-domain");

        jwks.RefreshCount.Should().Be(0);

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_DisposesSubscription()
    {
        var jwks = new FakeJwksProvider();
        var backplane = new FakeBackplane();
        var subscriber = MakeSubscriber(jwks, backplane);

        await subscriber.StartAsync(CancellationToken.None);
        backplane.Subscribers.Should().Be(1);

        await subscriber.StopAsync(CancellationToken.None);
        backplane.Subscribers.Should().Be(0);
    }

    private static JwksBackplaneSubscriber MakeSubscriber(
        IJwksProvider jwks,
        ICacheInvalidationBackplane? backplane)
        => new(
            jwks,
            Options.Create(new AuthOptions
            {
                Issuer = new Uri("https://edge.internal"),
                Audience = "files",
            }),
            NullLogger<JwksBackplaneSubscriber>.Instance,
            backplane);

    private sealed class FakeJwksProvider : IJwksProvider
    {
        private int _refreshCount;

        public int RefreshCount => Volatile.Read(ref _refreshCount);

        public ValueTask<D2Result<JwksKeySetSnapshot>> GetKeysAsync(CancellationToken ct = default)
            => throw new NotImplementedException("GetKeysAsync not used in these tests");

        public ValueTask<D2Result> RefreshAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _refreshCount);
            return new ValueTask<D2Result>(D2Result.Ok());
        }
    }

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
