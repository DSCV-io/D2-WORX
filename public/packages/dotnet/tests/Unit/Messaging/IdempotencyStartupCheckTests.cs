// -----------------------------------------------------------------------
// <copyright file="IdempotencyStartupCheckTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Idempotency;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class IdempotencyStartupCheckTests
{
    [Fact]
    public async Task StartAsync_NoSubscribersWithIdempotency_NoOps()
    {
        var registry = new SubscriberRegistry([
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("q1", idempotency: false),
        ]);
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new IdempotencyStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_IdempotencyEnabledAndCacheRegistered_NoOps()
    {
        var registry = new SubscriberRegistry([
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("q1", idempotency: true),
        ]);
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache>(new StubCache());
        var sp = services.BuildServiceProvider();
        var check = new IdempotencyStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_IdempotencyEnabledButNoCache_ThrowsWithSubscriberList()
    {
        var registry = new SubscriberRegistry([
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("q-idem", idempotency: true),
        ]);
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new IdempotencyStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*idempotency=true*IDistributedCache*");
        ex.WithMessage("*SampleAuditHandler*q-idem*");
    }

    [Fact]
    public async Task StartAsync_NullArgs_Throw()
    {
        var registry = new SubscriberRegistry([]);
        var sp = new ServiceCollection().BuildServiceProvider();

        var act1 = () => new IdempotencyStartupCheck(null!, sp);
        act1.Should().Throw<ArgumentNullException>();

        var act2 = () => new IdempotencyStartupCheck(registry, null!);
        act2.Should().Throw<ArgumentNullException>();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task StartAsync_OperatorProvidedStore_BypassesCacheRequirement()
    {
        // An operator-provided IMessageIdempotencyStore (e.g. integration
        // test fakes) satisfies the startup check even when IDistributedCache
        // is missing. Without this bypass, GetService would throw on the
        // default CacheIdempotencyStore's missing dependency before reaching
        // the operator-store check.
        var registry = new SubscriberRegistry([
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("q-op", idempotency: true),
        ]);
        var services = new ServiceCollection();
        services.AddSingleton<IMessageIdempotencyStore>(new FakeIdemStore());
        var sp = services.BuildServiceProvider();
        var check = new IdempotencyStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync(
            "operator-provided IMessageIdempotencyStore must satisfy the "
            + "startup check even when IDistributedCache is missing");
    }

    [Fact]
    public async Task StartAsync_NoOperatorStore_NoCache_StillFails()
    {
        // Inverse of above: with neither operator-provided store nor
        // distributed cache, the check still hard-fails. Pin so a refactor
        // doesn't soften the guard.
        var registry = new SubscriberRegistry([
            BuildRegistration<SampleAuditHandler, SampleAuditEvent>("q-bare", idempotency: true),
        ]);
        var sp = new ServiceCollection().BuildServiceProvider();
        var check = new IdempotencyStartupCheck(registry, sp);

        var act = async () => await check.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static ISubscriberRegistration BuildRegistration<TSub, TIn>(
        string queueName, bool idempotency)
        where TSub : BaseHandler<TSub, TIn, Unit>
        where TIn : class
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "TestSub",
            MessageTypeName: typeof(TIn).FullName!,
            QueueName: queueName,
            Pattern: QueuePattern.CompetingConsumer,
            RoutingKeyBinding: string.Empty,
            Prefetch: 10,
            Idempotency: idempotency,
            TieredRetry: null);
        return new TestRegistration(
            HandlerType: typeof(TSub),
            MessageType: typeof(TIn),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
    }

    private sealed record TestRegistration(
        Type HandlerType,
        Type MessageType,
        MqSubscriptionDescriptor Descriptor,
        string ResolvedQueueName) : ISubscriberRegistration;

    /// <summary>
    /// Minimal IDistributedCache stub. The startup check only probes for
    /// presence (<c>GetService&lt;IDistributedCache&gt;()</c>); it never calls
    /// any method on the resolved instance, so all members throw.
    /// </summary>
    private sealed class StubCache : IDistributedCache
    {
        public ValueTask<D2Result<bool>> SetNxAsync<T>(
            string k, T v, TimeSpan? e = null, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> ExistsAsync(
            string k, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<T?>> GetAsync<T>(
            string k, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
            IReadOnlyCollection<string> keys, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<TimeSpan?>> GetTtlAsync(
            string k, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAsync<T>(
            string k, T v, TimeSpan? e = null, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAsync<T>(
            IReadOnlyDictionary<string, T> kv,
            TimeSpan? e = null,
            CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAsync(
            string k, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAsync(
            IReadOnlyCollection<string> keys, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<long>> IncrementAsync(
            string k,
            long a = 1,
            TimeSpan? e = null,
            CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> AcquireLockAsync(
            string k, string token, TimeSpan ttl, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> ReleaseLockAsync(
            string k, string token, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAndBroadcastAsync<T>(
            string k, T v, TimeSpan? e = null, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
            IReadOnlyDictionary<string, T> kv,
            TimeSpan? e = null,
            CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAndBroadcastAsync(
            string k, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAndBroadcastAsync(
            IReadOnlyCollection<string> keys, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> SetAddAsync(
            string k, string m, TimeSpan? e = null, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<long>> SetCardinalityAsync(
            string k, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> SetRemoveAsync(
            string k, string m, CancellationToken c = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> SetContainsAsync(
            string k, string m, CancellationToken c = default)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Operator-provided <see cref="IMessageIdempotencyStore"/> stub for the
    /// "bypass cache requirement" tests. The startup check only probes for
    /// presence; it never invokes any method on the resolved instance.
    /// </summary>
    private sealed class FakeIdemStore : IMessageIdempotencyStore
    {
        public ValueTask<D2Result<bool>> HasSeenAsync(
            string messageId, CancellationToken ct = default)
            => new(D2Result<bool>.Ok(data: false));

        public ValueTask<D2Result> MarkSeenAsync(
            string messageId, CancellationToken ct = default)
            => new(D2Result.Ok());
    }
}
