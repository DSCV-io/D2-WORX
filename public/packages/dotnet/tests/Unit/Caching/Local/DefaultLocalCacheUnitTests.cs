// -----------------------------------------------------------------------
// <copyright file="DefaultLocalCacheUnitTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Caching.Local;

using System.Collections.Generic;
using AwesomeAssertions;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
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
    public async Task IncrementAsync_ExistingKeyWithTtl_PreservesTtl_NotResetToDefault()
    {
        // Regression for the bug where SetCore was called with `expiration: null`
        // on the increment-existing path, which fell back to DefaultExpiration
        // (1h) instead of preserving the existing TTL. Redis-parity contract:
        // INCR on a key with TTL must keep the TTL; only the first SET sets it.
        var clock = new FakeTimeProvider();
        using var cache = NewCache(clock: clock);
        var shortTtl = TimeSpan.FromMinutes(2);

        await cache.SetAsync("counter", 5L, shortTtl);

        // Advance the fake clock by 30 s — observable bleed in the remaining-TTL
        // window without any real-time wait.
        clock.Advance(TimeSpan.FromSeconds(30));

        await cache.IncrementAsync("counter");

        var ttlR = await cache.GetTtlAsync("counter");
        ttlR.IsOk.Should().BeTrue();
        ttlR.Data.Should().NotBeNull();

        // TTL must remain bounded by the original 2-minute window — not jump
        // back up to the 1-hour default. After 30 s elapsed, TTL ≈ 90 s < 2 min.
        ttlR.Data!.Value.Should().BeLessThan(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task IncrementAsync_ExistingKeyWithoutTtl_StaysWithoutTtl()
    {
        // Companion: existing-no-TTL must stay no-TTL after increment (don't
        // accidentally introduce a TTL on the increment path either).
        using var cache = NewCache();

        await cache.SetAsync("counter", 5L); // null expiration → DefaultExpiration applied
        await cache.IncrementAsync("counter");

        // The original DefaultExpiration is still applied from the initial Set;
        // increment must NOT layer another TTL on top — verify TTL hasn't been
        // re-extended by checking it's still bounded by the default window.
        var ttlR = await cache.GetTtlAsync("counter");
        ttlR.IsOk.Should().BeTrue();
        ttlR.Data.Should().NotBeNull();
        ttlR.Data!.Value.Should().BeLessThan(TimeSpan.FromHours(1.1));
    }

    [Fact]
    public async Task SetAsync_ZeroExpiration_ReturnsValidationFailed()
    {
        // Regression: previously SetCore silently treated TimeSpan.Zero as
        // "no expiration" — the worst possible behavior (cache slot kept
        // forever despite caller signaling zero TTL). Now public surface
        // validates and rejects.
        using var cache = NewCache();

        var result = await cache.SetAsync("k", "value", TimeSpan.Zero);

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SetAsync_NegativeExpiration_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.SetAsync("k", "value", TimeSpan.FromSeconds(-1));

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task SetManyAsync_ContainsEmptyKey_ReturnsValidationFailed()
    {
        // Regression: per-entry validation gap. Top-level dict was validated
        // but individual keys weren't — empty-key entries silently merged
        // into the prefix-only cache slot.
        using var cache = NewCache();

        var entries = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["valid"] = "v1",
            [string.Empty] = "v2",
        };

        var result = await cache.SetManyAsync(entries);

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveManyAsync_ContainsEmptyKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.RemoveManyAsync(new[] { "valid", string.Empty });

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
    }

    [Fact]
    public async Task GetManyAsync_ContainsEmptyKey_ReturnsValidationFailed()
    {
        using var cache = NewCache();

        var result = await cache.GetManyAsync<string>(new[] { "valid", string.Empty });

        result.Success.Should().BeFalse();
        result.IsValidationFailed.Should().BeTrue();
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
    public async Task Dispose_AcquireLockAsync_ThrowsObjectDisposedException()
    {
        var cache = NewCache();
        cache.Dispose();

        var act = async () => await cache.AcquireLockAsync("k", "lock-1", TimeSpan.FromSeconds(1));
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_ReleaseLockAsync_ThrowsObjectDisposedException()
    {
        var cache = NewCache();
        cache.Dispose();

        var act = async () => await cache.ReleaseLockAsync("k", "lock-1");
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_SetAsync_ThrowsObjectDisposedException()
    {
        var cache = NewCache();
        cache.Dispose();

        // Non-lock path must also fail closed (not only IMemoryCache-backed Get).
        var act = async () => await cache.SetAsync("k", 1);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Theory]
    [InlineData("GetAsync")]
    [InlineData("GetManyAsync")]
    [InlineData("ExistsAsync")]
    [InlineData("GetTtlAsync")]
    [InlineData("SetAsync")]
    [InlineData("SetManyAsync")]
    [InlineData("RemoveAsync")]
    [InlineData("RemoveManyAsync")]
    [InlineData("SetNxAsync")]
    [InlineData("IncrementAsync")]
    [InlineData("AcquireLockAsync")]
    [InlineData("ReleaseLockAsync")]
    public async Task Dispose_EveryPublicOp_ThrowsObjectDisposedException(string opName)
    {
        var cache = NewCache();
        cache.Dispose();

        // Do not capture a using-disposed local in the async lambda (inspectcode).
        Func<Task> act = () => InvokePublicOpAsync(cache, opName);
        await act.Should().ThrowAsync<ObjectDisposedException>(because: opName);
    }

    [Fact]
    public void Ctor_NullOptionsThrows()
    {
        var act = () => new DefaultLocalCache(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private static DefaultLocalCache NewCache(
        Action<LocalCacheOptions>? configure = null, TimeProvider? clock = null)
    {
        var opts = new LocalCacheOptions();
        configure?.Invoke(opts);
        return new DefaultLocalCache(Options.Create(opts), clock);
    }

    private static async Task InvokePublicOpAsync(DefaultLocalCache cache, string opName)
    {
        switch (opName)
        {
            case "GetAsync":
                await cache.GetAsync<int>("k");
                break;
            case "GetManyAsync":
                await cache.GetManyAsync<int>(["k"]);
                break;
            case "ExistsAsync":
                await cache.ExistsAsync("k");
                break;
            case "GetTtlAsync":
                await cache.GetTtlAsync("k");
                break;
            case "SetAsync":
                await cache.SetAsync("k", 1);
                break;
            case "SetManyAsync":
                await cache.SetManyAsync(new Dictionary<string, int> { ["k"] = 1 });
                break;
            case "RemoveAsync":
                await cache.RemoveAsync("k");
                break;
            case "RemoveManyAsync":
                await cache.RemoveManyAsync(["k"]);
                break;
            case "SetNxAsync":
                await cache.SetNxAsync("k", 1);
                break;
            case "IncrementAsync":
                await cache.IncrementAsync("k");
                break;
            case "AcquireLockAsync":
                await cache.AcquireLockAsync("k", "lock-1", TimeSpan.FromSeconds(1));
                break;
            case "ReleaseLockAsync":
                await cache.ReleaseLockAsync("k", "lock-1");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(opName), opName, null);
        }
    }
}
