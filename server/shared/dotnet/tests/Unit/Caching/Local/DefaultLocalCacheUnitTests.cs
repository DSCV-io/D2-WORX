// -----------------------------------------------------------------------
// <copyright file="DefaultLocalCacheUnitTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Caching.Local;

using AwesomeAssertions;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Surface tests for <see cref="DefaultLocalCache"/> — D2Result mapping,
/// argument validation, key prefixing, idempotency. Real cache behaviors
/// (eviction under capacity, TTL expiration timing, concurrent access)
/// live in <c>Integration/Caching/Local/</c>.
/// </summary>
public sealed class DefaultLocalCacheUnitTests
{
    [Fact]
    public async Task GetAsync_HitReturnsOk()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 42);

        var result = await cache.GetAsync<int>("k");

        result.IsOk.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public async Task GetAsync_MissReturnsNotFound()
    {
        using var cache = NewCache();
        var result = await cache.GetAsync<int>("missing");

        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_NullKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();
        var result = await cache.GetAsync<int>(null!);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "key");
    }

    [Fact]
    public async Task GetAsync_EmptyKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();
        var result = await cache.GetAsync<int>(string.Empty);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "key");
    }

    [Fact]
    public async Task GetManyAsync_AllHitReturnsOk()
    {
        using var cache = NewCache();
        await cache.SetAsync("a", 1);
        await cache.SetAsync("b", 2);

        var result = await cache.GetManyAsync<int>(["a", "b"]);

        result.IsOk.Should().BeTrue();
        result.Data!["a"].Should().Be(1);
        result.Data!["b"].Should().Be(2);
    }

    [Fact]
    public async Task GetManyAsync_AllMissReturnsNotFound()
    {
        using var cache = NewCache();
        var result = await cache.GetManyAsync<int>(["x", "y"]);
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetManyAsync_PartialHitReturnsSomeFound()
    {
        using var cache = NewCache();
        await cache.SetAsync("a", 1);

        var result = await cache.GetManyAsync<int>(["a", "missing"]);

        result.IsSomeFound.Should().BeTrue();
        result.Data!.Should().ContainKey("a");
        result.Data!.Should().NotContainKey("missing");
    }

    [Fact]
    public async Task GetManyAsync_EmptyKeys_ReturnsValidationFailed()
    {
        using var cache = NewCache();
        var result = await cache.GetManyAsync<int>([]);

        result.IsValidationFailed.Should().BeTrue();
        result.InputErrors.Should().ContainSingle(e => e.Field == "keys");
    }

    [Fact]
    public async Task ExistsAsync_PresentReturnsTrue()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);
        var result = await cache.ExistsAsync("k");
        result.IsOk.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_AbsentReturnsFalse()
    {
        using var cache = NewCache();
        var result = await cache.ExistsAsync("missing");
        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task GetTtlAsync_AbsentKeyReturnsNotFound()
    {
        using var cache = NewCache();
        var result = await cache.GetTtlAsync("missing");
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetTtlAsync_PresentKeyWithTtlReturnsRemaining()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1, TimeSpan.FromMinutes(5));
        var result = await cache.GetTtlAsync("k");
        result.IsOk.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        result.Data!.Value.Should().BeGreaterThan(TimeSpan.FromMinutes(4));
    }

    [Fact]
    public async Task RemoveAsync_AbsentKeyIsIdempotentOk()
    {
        using var cache = NewCache();
        var result = await cache.RemoveAsync("never-set");
        result.IsOk.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_RemovesPreviouslySet()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 1);
        await cache.RemoveAsync("k");
        (await cache.GetAsync<int>("k")).IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveManyAsync_RemovesAll()
    {
        using var cache = NewCache();
        await cache.SetAsync("a", 1);
        await cache.SetAsync("b", 2);
        await cache.RemoveManyAsync(["a", "b"]);

        (await cache.ExistsAsync("a")).Data.Should().BeFalse();
        (await cache.ExistsAsync("b")).Data.Should().BeFalse();
    }

    [Fact]
    public async Task SetNxAsync_NewKeyReturnsTrue()
    {
        using var cache = NewCache();
        var result = await cache.SetNxAsync("k", 1);
        result.IsOk.Should().BeTrue();
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task SetNxAsync_ExistingKeyReturnsFalseAndDoesNotOverwrite()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", 100);

        var result = await cache.SetNxAsync("k", 200);

        result.IsOk.Should().BeTrue();
        result.Data.Should().BeFalse();
        (await cache.GetAsync<int>("k")).Data.Should().Be(100);
    }

    [Fact]
    public async Task IncrementAsync_NewKeyReturnsAmount()
    {
        using var cache = NewCache();
        var result = await cache.IncrementAsync("counter");
        result.IsOk.Should().BeTrue();
        result.Data.Should().Be(1);
    }

    [Fact]
    public async Task IncrementAsync_ExistingNumericIncrements()
    {
        using var cache = NewCache();
        await cache.IncrementAsync("counter", 5);
        var result = await cache.IncrementAsync("counter", 3);
        result.Data.Should().Be(8);
    }

    [Fact]
    public async Task IncrementAsync_NegativeAmountWorks()
    {
        using var cache = NewCache();
        await cache.IncrementAsync("counter", 10);
        var result = await cache.IncrementAsync("counter", -4);
        result.Data.Should().Be(6);
    }

    [Fact]
    public async Task IncrementAsync_NonNumericKeyReturnsConflict()
    {
        using var cache = NewCache();
        await cache.SetAsync("k", "not-a-number");
        var result = await cache.IncrementAsync("k");
        result.IsConflict.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireLockAsync_FirstCallerAcquires()
    {
        using var cache = NewCache();
        var result = await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromSeconds(30));
        result.Data.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireLockAsync_SecondCallerWhileHeldReturnsFalse()
    {
        using var cache = NewCache();
        await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromSeconds(30));
        var result = await cache.AcquireLockAsync("k", "owner-B", TimeSpan.FromSeconds(30));
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLockAsync_CorrectIdReleases()
    {
        using var cache = NewCache();
        await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromSeconds(30));
        await cache.ReleaseLockAsync("k", "owner-A");
        var second = await cache.AcquireLockAsync("k", "owner-B", TimeSpan.FromSeconds(30));
        second.Data.Should().BeTrue();
    }

    [Fact]
    public async Task ReleaseLockAsync_WrongIdIsNoOp()
    {
        using var cache = NewCache();
        await cache.AcquireLockAsync("k", "owner-A", TimeSpan.FromSeconds(30));
        await cache.ReleaseLockAsync("k", "owner-B");  // wrong id
        var second = await cache.AcquireLockAsync("k", "owner-B", TimeSpan.FromSeconds(30));
        second.Data.Should().BeFalse();  // still held by A
    }

    [Fact]
    public async Task ReleaseLockAsync_NeverHeldKeyIsNoOp()
    {
        using var cache = NewCache();
        Exception? thrown = null;
        try
        {
            await cache.ReleaseLockAsync("never-held", "any-id");
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        thrown.Should().BeNull();
    }

    [Fact]
    public async Task KeyPrefix_AppliesAutomatically()
    {
        var prefixedCache = NewCache(opts => opts.KeyPrefix = "test:");
        await prefixedCache.SetAsync("k", 42);

        // The prefix is opaque to callers — they pass "k", lib stores under "test:k" internally.
        var result = await prefixedCache.GetAsync<int>("k");
        result.IsOk.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public async Task Dispose_IsIdempotent()
    {
        var cache = NewCache();
        cache.Dispose();
        var act = cache.Dispose;
        act.Should().NotThrow();
        await Task.CompletedTask;
    }

    [Fact]
    public void Ctor_NullOptionsThrows()
    {
        var act = () => new DefaultLocalCache(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static DefaultLocalCache NewCache(Action<LocalCacheOptions>? configure = null)
    {
        var opts = new LocalCacheOptions();
        configure?.Invoke(opts);
        return new DefaultLocalCache(Options.Create(opts));
    }
}
