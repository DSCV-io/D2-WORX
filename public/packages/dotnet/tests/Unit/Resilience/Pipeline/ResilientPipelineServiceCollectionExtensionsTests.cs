// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Pipeline;
using DcsvIo.D2.Resilience.RateLimiting;
using DcsvIo.D2.Resilience.Singleflight;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class ResilientPipelineServiceCollectionExtensionsTests
{
    [Fact]
    public void AddResilientPipeline_RegistersAsKeyedSingleton()
    {
        var services = new ServiceCollection();

        services.AddResilientPipeline<string, int>("audit", p => p.UseRetries());

        var sp = services.BuildServiceProvider();
        var first = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("audit");
        var second = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("audit");

        first.Should().BeSameAs(second);
    }

    [Fact]
    public async Task AddResilientPipeline_FullStack_ResolvesKeyedPrimitives()
    {
        // Canonical case: each layer explicitly names which keyed primitive
        // it pulls. Extracting the key to a const (as a real consumer would)
        // makes refactor + grep painless and keeps the duplication safe.
        const string key = "ipinfo";
        var services = new ServiceCollection();
        services.AddKeyedSingleton<Singleflight<string, int>>(key);
        services.AddKeyedSingleton<CircuitBreaker<int>>(key, (_, _) => new(_ => false));
        services.AddResilientPipeline<string, int>(key, p => p
            .UseSingleflight(key)
            .UseCircuitBreaker(key));

        var sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>(key);

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(42));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
    }

    [Fact]
    public async Task AddResilientPipeline_AllowsMultipleInstancesPerSameShape()
    {
        // The whole point of forcing keyed registration — register N
        // pipelines of the SAME (TKey, TValue) shape under distinct keys,
        // each with INDEPENDENT primitive state. Tripping one's CB leaves
        // the others unaffected. (Without keyed registration, MS DI's
        // last-wins semantic would silently shadow earlier registrations.)
        var services = new ServiceCollection();
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "audit", (_, _) => new(_ => false, options: new(failureThreshold: 1)));
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "notifications", (_, _) => new(_ => false, options: new(failureThreshold: 1)));

        services.AddResilientPipeline<string, int>("audit", p => p.UseCircuitBreaker("audit"));
        services.AddResilientPipeline<string, int>(
            "notifications",
            p => p.UseCircuitBreaker("notifications"));

        var sp = services.BuildServiceProvider();
        var auditPipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("audit");
        var notificationsPipeline = sp
            .GetRequiredKeyedService<ResilientPipeline<string, int>>("notifications");

        // Trip audit only.
        await auditPipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var auditAfter = await auditPipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        var notificationsAfter = await notificationsPipeline
            .ExecuteAsync("k", _ => ValueTask.FromResult(2));

        auditAfter.IsServiceUnavailable.Should().BeTrue();
        notificationsAfter.Success.Should().BeTrue();
        notificationsAfter.Data.Should().Be(2);
    }

    [Fact]
    public void AddResilientPipeline_ReturnsServiceCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddResilientPipeline<string, int>("audit", p => p.UseRetries());

        returned.Should().BeSameAs(services);
    }

    // ----------------------------------------------------------------------
    // F-1 regression: IDisposable — inline-options pipeline disposes owned RateLimiter
    // ----------------------------------------------------------------------

    [Fact]
    public async Task Dispose_InlineOptionsPipeline_DisposesOwnedRateLimiter()
    {
        // Regression test for F-1: ResilientPipeline.Dispose() must propagate
        // to IDisposable layers. The inline-options UseRateLimiter path creates
        // a RateLimiterLayer with r_ownsLimiter=true — when the ServiceProvider
        // is disposed, the pipeline (keyed singleton) is disposed, the layer is
        // disposed, and the underlying SemaphoreSlim is disposed.
        //
        // Proof: after ServiceProvider disposal, executing the pipeline triggers
        // an ObjectDisposedException from the disposed SemaphoreSlim (the gate
        // inside RateLimiter.ExecuteAsync is on a disposed SemaphoreSlim).
        // Without the IDisposable impl, the SemaphoreSlim would NOT be disposed
        // and the call would succeed (returning Ok(1)) — the assertion would fail.
        const string key = "inline-rl-dispose-test";
        var services = new ServiceCollection();
        services.AddResilientPipeline<string, int>(
            key,
            p => p.UseRateLimiter(new RateLimiterOptions(maxConcurrency: 5)));

        var sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>(key);

        // Confirm pipeline works before disposal.
        var before = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        before.Success.Should().BeTrue();

        // Dispose the ServiceProvider — triggers Dispose() on the keyed singleton.
        await sp.DisposeAsync();

        // After disposal: RateLimiter.ExecuteAsync calls r_gate.WaitAsync on a
        // disposed SemaphoreSlim → ObjectDisposedException. The pipeline catches
        // it as a non-transient, non-OCE exception → UnhandledException.
        // (The pipeline's own catch-all converts any ObjectDisposedException that
        // escapes the layer to UnhandledException — the pipeline never throws.)
        var after = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(2));

        // Must NOT be Ok — the disposed SemaphoreSlim must have been triggered.
        after.Success.Should().BeFalse();

        // Must land in the non-transient catch-all (ObjectDisposedException is
        // not classified transient by RetryHelper.IsTransientException).
        after.IsUnhandledException.Should().BeTrue();
    }
}
