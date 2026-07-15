// -----------------------------------------------------------------------
// <copyright file="TieredCacheSessionLivenessTrackerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Sessions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Sessions;
using D2.Shared.Caching;
using D2.Shared.I18n;
using D2.Shared.Result;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class TieredCacheSessionLivenessTrackerTests
{
    [Fact]
    public async Task IsAliveAsync_KeyExists_ReturnsAlive()
    {
        var sessionId = Guid.NewGuid();
        var cache = new FakeTieredCache();
        cache.SetExists($"session:{sessionId:N}", true);
        var tracker = MakeTracker(cache);

        var result = await tracker.IsAliveAsync(sessionId);

        result.Success.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task IsAliveAsync_KeyAbsent_ReturnsRevoked()
    {
        var sessionId = Guid.NewGuid();
        var cache = new FakeTieredCache();
        var tracker = MakeTracker(cache);

        var result = await tracker.IsAliveAsync(sessionId);

        result.Success.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task IsAliveAsync_GuidEmpty_ReturnsValidationFailed()
    {
        var tracker = MakeTracker(new FakeTieredCache());

        var result = await tracker.IsAliveAsync(Guid.Empty);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IsAliveAsync_CacheReturnsFailure_ReturnsUnavailableWithUserFacingMessage()
    {
        // Cache outage → fail-closed (ServiceUnavailable, NOT Ok(false)). Caller
        // must convert to 401; never let a request through under unknown liveness.
        var cache = new FakeTieredCache();
        cache.SimulateFailure = true;
        var tracker = MakeTracker(cache);

        var result = await tracker.IsAliveAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);
        result.Messages.Should().ContainSingle()
            .Which.Should().Be(TK.Auth.Errors.TEMPORARILY_UNAVAILABLE);
    }

    [Fact]
    public async Task IsAliveAsync_UsesConfiguredCacheKeyPrefix()
    {
        var sessionId = Guid.NewGuid();
        var cache = new FakeTieredCache();
        cache.SetExists($"custom-prefix:{sessionId:N}", true);
        var options = Options.Create(new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
            Sessions = new SessionLivenessOptions(cacheKeyPrefix: "custom-prefix:"),
        });
        var tracker = new TieredCacheSessionLivenessTracker(
            cache, options, NullLogger<TieredCacheSessionLivenessTracker>.Instance);

        var result = await tracker.IsAliveAsync(sessionId);

        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task IsAliveAsync_CanceledToken_PropagatesToCacheCall()
    {
        // ct propagation: the cache backing call must observe the canceled
        // token (caller will see a cancellation rather than a stale result).
        var cache = new FakeTieredCache();
        var tracker = MakeTracker(cache);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var token = cts.Token;

        var act = async () => await tracker.IsAliveAsync(Guid.NewGuid(), token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_NullCache_Throws()
    {
        var options = Options.Create(new AuthOptions
        {
            Issuer = new Uri("https://edge.internal"),
            Audience = "files",
        });

        var act = () => new TieredCacheSessionLivenessTracker(
            cache: null!,
            options: options,
            logger: NullLogger<TieredCacheSessionLivenessTracker>.Instance);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new TieredCacheSessionLivenessTracker(
            cache: new FakeTieredCache(),
            options: null!,
            logger: NullLogger<TieredCacheSessionLivenessTracker>.Instance);

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

        var act = () => new TieredCacheSessionLivenessTracker(
            cache: new FakeTieredCache(),
            options: options,
            logger: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static TieredCacheSessionLivenessTracker MakeTracker(ITieredCache cache)
        => new(
            cache,
            Options.Create(new AuthOptions
            {
                Issuer = new Uri("https://edge.internal"),
                Audience = "files",
            }),
            NullLogger<TieredCacheSessionLivenessTracker>.Instance);

    /// <summary>
    /// Minimal in-memory fake satisfying ITieredCache for the IsAliveAsync
    /// surface this tracker uses (only ExistsAsync). Other ops throw.
    /// </summary>
    private sealed class FakeTieredCache : ITieredCache
    {
        private readonly ConcurrentDictionary<string, bool> r_exists = new(StringComparer.Ordinal);

        public bool SimulateFailure { get; set; }

        public void SetExists(string key, bool exists) => r_exists[key] = exists;

        public ValueTask<D2Result<bool>> ExistsAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (SimulateFailure)
                return new ValueTask<D2Result<bool>>(D2Result<bool>.ServiceUnavailable());
            return new ValueTask<D2Result<bool>>(
                D2Result<bool>.Ok(r_exists.TryGetValue(key, out var present) && present));
        }

        // ICacheBasic — unused by tracker
        public ValueTask<D2Result<T?>> GetAsync<T>(string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAsync(string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<TimeSpan?>> GetTtlAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        // ICacheAtomic — unused
        public ValueTask<D2Result<long>> IncrementAsync(
            string key,
            long delta = 1,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> SetNxAsync<T>(
            string key,
            T value,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> AcquireLockAsync(
            string key, string token, TimeSpan ttl, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> ReleaseLockAsync(
            string key, string token, CancellationToken ct = default)
            => throw new NotImplementedException();

        // ICacheBroadcast — unused
        public ValueTask<D2Result> SetAndBroadcastAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAndBroadcastAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAndBroadcastAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
