// -----------------------------------------------------------------------
// <copyright file="DefaultMemoryCacheTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable AccessToStaticMemberViaDerivedType
namespace D2.Shared.Tests.Unit;

using D2.Shared.Handler;
using D2.Shared.InMemoryCache.Default.Handlers.D;
using D2.Shared.InMemoryCache.Default.Handlers.R;
using D2.Shared.InMemoryCache.Default.Handlers.U;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.D;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.R;
using D2.Shared.Interfaces.Caching.InMemory.Handlers.U;
using D2.Shared.Result;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
/// Tests for the default in-memory cache handlers.
/// </summary>
[MustDisposeResource(false)]
public sealed class DefaultMemoryCacheTests : IDisposable
{
    private readonly IMemoryCache r_cache
        = new Microsoft.Extensions.Caching.Memory.MemoryCache(new MemoryCacheOptions());

    private readonly IHandlerContext r_context = TestHelpers.CreateHandlerContext();

    /// <summary>
    /// Tests that Get returns a NotFound result when the requested key does not exist in the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task Get_WhenKeyMissing_ReturnsNotFound()
    {
        var handler = new Get<string>(r_cache, r_context);
        var result = await handler.HandleAsync(
            new IRead.GetInput("missing-key"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Tests that Get returns the cached value when the requested key exists in the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task Get_WhenKeyExists_ReturnsValue()
    {
        r_cache.Set("test-key", "test-value");

        var handler = new Get<string>(r_cache, r_context);
        var result = await handler.HandleAsync(
            new IRead.GetInput("test-key"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("test-value");
    }

    /// <summary>
    /// Tests that Set successfully stores a value in the cache with valid input.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task Set_WithValidInput_StoresValueInCache()
    {
        var setHandler = new Set<string>(r_cache, r_context);
        var setResult = await setHandler.HandleAsync(
            new IUpdate.SetInput<string>("key", "value"),
            CancellationToken.None);

        setResult.Success.Should().BeTrue();
        r_cache.TryGetValue<string>("key", out var cached).Should().BeTrue();
        cached.Should().Be("value");
    }

    /// <summary>
    /// Tests that Set with an expiration time removes the value from cache after the specified duration.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task Set_WithExpiration_ExpiresAfterDuration()
    {
        var handler = new Set<string>(r_cache, r_context);
        await handler.HandleAsync(
            new IUpdate.SetInput<string>(
                "key", "value", TimeSpan.FromMilliseconds(100)),
            CancellationToken.None);

        await Task.Delay(200, CancellationToken.None);

        r_cache.TryGetValue<string>("key", out _).Should().BeFalse();
    }

    /// <summary>
    /// Tests that Remove successfully deletes an existing key from the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task Remove_WithExistingKey_DeletesKeyFromCache()
    {
        r_cache.Set("key", "value");

        var handler = new Remove(r_cache, r_context);
        await handler.HandleAsync(
            new IDelete.RemoveInput("key"),
            CancellationToken.None);

        r_cache.TryGetValue<string>("key", out _).Should().BeFalse();
    }

    #region GetMany Tests

    /// <summary>
    /// Tests that GetMany returns NotFound when no requested keys exist in the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task GetMany_WhenNoKeysExist_ReturnsNotFound()
    {
        var handler = new GetMany<string>(r_cache, r_context);
        var result = await handler.HandleAsync(
            new IRead.GetManyInput(["missing-key-1", "missing-key-2"]),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
    }

    /// <summary>
    /// Tests that GetMany returns all values when all requested keys exist in the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task GetMany_WhenAllKeysExist_ReturnsAllValues()
    {
        r_cache.Set("key-1", "value-1");
        r_cache.Set("key-2", "value-2");
        r_cache.Set("key-3", "value-3");

        var handler = new GetMany<string>(r_cache, r_context);
        var result = await handler.HandleAsync(
            new IRead.GetManyInput(["key-1", "key-2", "key-3"]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Values.Should().HaveCount(3);
        result.Data.Values["key-1"].Should().Be("value-1");
        result.Data.Values["key-2"].Should().Be("value-2");
        result.Data.Values["key-3"].Should().Be("value-3");
    }

    /// <summary>
    /// Tests that GetMany returns SomeFound when only some requested keys exist in the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task GetMany_WhenSomeKeysExist_ReturnsSomeFound()
    {
        // Arrange - key-2 intentionally missing
        r_cache.Set("key-1", "value-1");

        var handler = new GetMany<string>(r_cache, r_context);
        var result = await handler.HandleAsync(
            new IRead.GetManyInput(["key-1", "key-2"]),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.SOME_FOUND);
        result.Data.Should().NotBeNull();
        result.Data!.Values.Should().HaveCount(1);
        result.Data.Values["key-1"].Should().Be("value-1");
    }

    /// <summary>
    /// Tests that GetMany handles empty input list correctly.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task GetMany_WithEmptyInput_ReturnsNotFound()
    {
        var handler = new GetMany<string>(r_cache, r_context);
        var result = await handler.HandleAsync(
            new IRead.GetManyInput([]),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
    }

    #endregion

    #region SetMany Tests

    /// <summary>
    /// Tests that SetMany successfully stores multiple values in the cache.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task SetMany_WithValidInput_StoresAllValuesInCache()
    {
        var handler = new SetMany<string>(r_cache, r_context);
        var values = new Dictionary<string, string>
        {
            ["key-1"] = "value-1",
            ["key-2"] = "value-2",
            ["key-3"] = "value-3",
        };
        var result = await handler.HandleAsync(
            new IUpdate.SetManyInput<string>(values),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        r_cache.TryGetValue<string>("key-1", out var cached1).Should().BeTrue();
        r_cache.TryGetValue<string>("key-2", out var cached2).Should().BeTrue();
        r_cache.TryGetValue<string>("key-3", out var cached3).Should().BeTrue();
        cached1.Should().Be("value-1");
        cached2.Should().Be("value-2");
        cached3.Should().Be("value-3");
    }

    /// <summary>
    /// Tests that SetMany with an expiration time removes the values from cache after the specified duration.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task SetMany_WithExpiration_ExpiresAfterDuration()
    {
        var handler = new SetMany<string>(r_cache, r_context);
        var values = new Dictionary<string, string>
        {
            ["expiring-key-1"] = "value-1",
            ["expiring-key-2"] = "value-2",
        };
        await handler.HandleAsync(
            new IUpdate.SetManyInput<string>(values, TimeSpan.FromMilliseconds(100)),
            CancellationToken.None);

        await Task.Delay(200, CancellationToken.None);

        r_cache.TryGetValue<string>("expiring-key-1", out _).Should().BeFalse();
        r_cache.TryGetValue<string>("expiring-key-2", out _).Should().BeFalse();
    }

    /// <summary>
    /// Tests that SetMany handles empty input dictionary correctly.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task SetMany_WithEmptyInput_ReturnsSuccess()
    {
        var handler = new SetMany<string>(r_cache, r_context);
        var result = await handler.HandleAsync(
            new IUpdate.SetManyInput<string>([]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    /// <summary>
    /// Tests that SetMany and GetMany work together correctly.
    /// </summary>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous unit test.
    /// </returns>
    [Fact]
    public async Task SetMany_ThenGetMany_RetrievesAllValues()
    {
        var setHandler = new SetMany<string>(r_cache, r_context);
        var values = new Dictionary<string, string>
        {
            ["roundtrip-1"] = "value-1",
            ["roundtrip-2"] = "value-2",
        };
        await setHandler.HandleAsync(
            new IUpdate.SetManyInput<string>(values),
            CancellationToken.None);

        var getHandler = new GetMany<string>(r_cache, r_context);
        var result = await getHandler.HandleAsync(
            new IRead.GetManyInput(["roundtrip-1", "roundtrip-2"]),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Values.Should().HaveCount(2);
        result.Data.Values["roundtrip-1"].Should().Be("value-1");
        result.Data.Values["roundtrip-2"].Should().Be("value-2");
    }

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        r_cache.Dispose();
    }
}
