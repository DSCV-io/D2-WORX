// -----------------------------------------------------------------------
// <copyright file="CacheIdempotencyStoreTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Messaging.RabbitMq.Idempotency;
using DcsvIo.D2.Result;
using Xunit;

public sealed class CacheIdempotencyStoreTests
{
    [Fact]
    public async Task HasSeenAsync_KnownKey_ReturnsTrue()
    {
        var cache = new FakeDistributedCache();
        cache.SeedExists("msg-idem:abc");
        var store = new CacheIdempotencyStore(cache);

        var result = await store.HasSeenAsync("abc");
        result.Failed.Should().BeFalse();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task HasSeenAsync_UnknownKey_ReturnsFalse()
    {
        var cache = new FakeDistributedCache();
        var store = new CacheIdempotencyStore(cache);

        var result = await store.HasSeenAsync("never-seen");
        result.Failed.Should().BeFalse();
        result.Data.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task HasSeenAsync_BadKey_ReturnsValidationFailure(string? key)
    {
        var store = new CacheIdempotencyStore(new FakeDistributedCache());
        var result = await store.HasSeenAsync(key!);
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public async Task HasSeenAsync_BackingStoreOutage_PropagatesFailure()
    {
        var cache = new FakeDistributedCache { ShouldFail = true };
        var store = new CacheIdempotencyStore(cache);

        var result = await store.HasSeenAsync("abc");

        // Per the contract, store-level failures bubble. The CONSUMER then
        // decides to "fail open" (process the message anyway).
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public async Task MarkSeenAsync_AddsKey()
    {
        var cache = new FakeDistributedCache();
        var store = new CacheIdempotencyStore(cache);

        var result = await store.MarkSeenAsync("xyz");
        result.Failed.Should().BeFalse();
        cache.HasSet("msg-idem:xyz").Should().BeTrue();
    }

    [Fact]
    public async Task MarkSeenAsync_AppliesTtl()
    {
        var cache = new FakeDistributedCache();
        var store = new CacheIdempotencyStore(cache);

        await store.MarkSeenAsync("xyz");
        cache.GetTtl("msg-idem:xyz").Should().Be(TimeSpan.FromHours(24));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task MarkSeenAsync_BadKey_ReturnsValidationFailure(string? key)
    {
        var store = new CacheIdempotencyStore(new FakeDistributedCache());
        var result = await store.MarkSeenAsync(key!);
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public async Task MarkSeenAsync_BackingStoreOutage_PropagatesFailure()
    {
        var cache = new FakeDistributedCache { ShouldFail = true };
        var store = new CacheIdempotencyStore(cache);

        var result = await store.MarkSeenAsync("xyz");
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Ctor_NullCache_Throws()
    {
        var act = () => new CacheIdempotencyStore(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Test-only fake. Does NOT implement the full IDistributedCache surface;
    /// only the methods CacheIdempotencyStore actually calls. Other methods
    /// throw NotImplementedException — fail-fast if test code drifts.
    /// </summary>
    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, TimeSpan?> _store = new(StringComparer.Ordinal);

        public bool ShouldFail { get; set; }

        public void SeedExists(string key) => _store[key] = null;

        public bool HasSet(string key) => _store.ContainsKey(key);

        public TimeSpan? GetTtl(string key) => _store.TryGetValue(key, out var ttl) ? ttl : null;

        public ValueTask<D2Result<bool>> ExistsAsync(string key, CancellationToken ct = default)
        {
            if (ShouldFail) return new(D2Result<bool>.ServiceUnavailable());
            return new(D2Result<bool>.Ok(_store.ContainsKey(key)));
        }

        public ValueTask<D2Result<bool>> SetNxAsync<T>(
            string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
        {
            if (ShouldFail) return new(D2Result<bool>.ServiceUnavailable());
            if (_store.ContainsKey(key)) return new(D2Result<bool>.Ok(data: false));
            _store[key] = expiration;
            return new(D2Result<bool>.Ok(data: true));
        }

        // The remainder of IDistributedCache — fail-fast (test-helper precision).
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
            string k, long a = 1, TimeSpan? e = null, CancellationToken c = default)
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
}
