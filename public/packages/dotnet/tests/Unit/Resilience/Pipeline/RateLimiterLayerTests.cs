// -----------------------------------------------------------------------
// <copyright file="RateLimiterLayerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class RateLimiterLayerTests
{
    // ----------------------------------------------------------------------
    // Basic admit / reject
    // ----------------------------------------------------------------------

    [Fact]
    public async Task WrapAsync_UnderLimit_RunsAndReturns()
    {
        using var layer = new RateLimiterLayer<string, int>(new RateLimiterOptions(maxConcurrency: 2));

        var result = await layer.WrapAsync("k", _ => ValueTask.FromResult(42), default);

        result.Should().Be(42);
    }

    [Fact]
    public async Task WrapAsync_GateFull_AcquisitionTimeoutZero_ThrowsRateLimitRejectedException()
    {
        var layer = new RateLimiterLayer<string, int>(
            new RateLimiterOptions(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero));

        try
        {
            var gate = new TaskCompletionSource();
            var acquired = new TaskCompletionSource(); // signals: permit is held
            var first = layer.WrapAsync(
                "k",
                async _ =>
                {
                    acquired.SetResult();
                    await gate.Task;
                    return 1;
                },
                default).AsTask();

            // Wait until the permit is demonstrably held before attempting the +1 call.
            await acquired.Task;

            await Assert.ThrowsAsync<RateLimitRejectedException>(
                () => layer.WrapAsync("k", _ => ValueTask.FromResult(99), default).AsTask());

            gate.SetResult();
            await first;
        }
        finally
        {
            layer.Dispose();
        }
    }

    [Fact]
    public async Task WrapAsync_ReleaseOnOpThrow_PermitAvailableForNextCaller()
    {
        using var layer = new RateLimiterLayer<string, int>(
            new RateLimiterOptions(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => layer.WrapAsync(
                "k",
                _ => throw new InvalidOperationException("boom"),
                default).AsTask());

        // Permit must be released — next call succeeds.
        var result = await layer.WrapAsync("k", _ => ValueTask.FromResult(7), default);
        result.Should().Be(7);
    }

    // ----------------------------------------------------------------------
    // Pipeline boundary mapping
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_RateLimitRejectedException_MapsToTooManyRequests()
    {
        var pipeline = new ResilientPipeline<string, int>(
            new RateLimiterLayer<string, int>(
                new RateLimiterOptions(maxConcurrency: 1, acquisitionTimeout: TimeSpan.Zero)));

        var gate = new TaskCompletionSource();
        var acquired = new TaskCompletionSource(); // signals: permit is held
        var first = pipeline.ExecuteAsync("k", async _ =>
        {
            acquired.SetResult();
            await gate.Task;
            return 1;
        }).AsTask();

        // Wait until the permit is demonstrably held before attempting the rejected call.
        await acquired.Task;

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(99));

        result.Success.Should().BeFalse();
        result.IsRateLimited.Should().BeTrue();
        result.IsTransientRetryable.Should().BeTrue();

        gate.SetResult();
        await first;
    }

    // ----------------------------------------------------------------------
    // Keyed-DI overload (§1.3 DI resolution test)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_UseRateLimiter_KeyedDI_ResolvesAndAdmits()
    {
        const string key = "test-rl";
        var services = new ServiceCollection();
        services.AddKeyedSingleton<RateLimiter>(key, (_, _) => new RateLimiter(new(maxConcurrency: 10)));
        services.AddResilientPipeline<string, int>(key, p => p.UseRateLimiter(key));

        using var sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>(key);

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(42));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    // ----------------------------------------------------------------------
    // Inline overload
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Pipeline_UseRateLimiter_InlineOptions_BuildsAndAdmits()
    {
        var services = new ServiceCollection();
        services.AddResilientPipeline<string, int>(
            "inline-rl",
            p => p.UseRateLimiter(new RateLimiterOptions(maxConcurrency: 5)));

        using var sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("inline-rl");

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.Success.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Explicit-instance overload (used in builder tests + manual composition)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task RateLimiterLayer_ExplicitLimiterInstance_DoesNotOwnLimiter()
    {
        // When constructed with an explicit RateLimiter, the layer must NOT
        // dispose the shared limiter on Dispose (the shared-limiter story).
        using var limiter = new RateLimiter(new(maxConcurrency: 3));
        var layer = new RateLimiterLayer<string, int>(limiter);

        // Dispose the layer — the shared limiter should still be usable.
        layer.Dispose();

        var result = await limiter.ExecuteAsync(_ => ValueTask.FromResult(1));
        result.Should().Be(1);
    }

    // ----------------------------------------------------------------------
    // Dispose
    // ----------------------------------------------------------------------

    [Fact]
    public void Dispose_OwnedLimiter_ReleasesResources_NoThrow()
    {
        var layer = new RateLimiterLayer<string, int>(
            new RateLimiterOptions(maxConcurrency: 5));

        var act = layer.Dispose;

        act.Should().NotThrow();
    }
}
