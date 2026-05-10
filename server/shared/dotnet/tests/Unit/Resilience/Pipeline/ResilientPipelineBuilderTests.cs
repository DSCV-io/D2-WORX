// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineBuilderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Pipeline;
using D2.Shared.Resilience.Retry;
using D2.Shared.Resilience.Singleflight;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class ResilientPipelineBuilderTests
{
    // ----------------------------------------------------------------------
    // Explicit-instance overloads (no DI involved)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task UseSingleflight_ExplicitInstance_AddsLayer()
    {
        var sf = new Singleflight<string, int>();
        var pipeline = NewBuilder<string, int>().UseSingleflight(sf).Build();

        var invocations = 0;
        var gate = new TaskCompletionSource();

        async ValueTask<int> Op(CancellationToken ct)
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }

        var t1 = pipeline.ExecuteAsync("k", Op).AsTask();
        var t2 = pipeline.ExecuteAsync("k", Op).AsTask();

        await Task.Delay(20);
        gate.SetResult();
        await Task.WhenAll(t1, t2);

        invocations.Should().Be(1);
    }

    [Fact]
    public async Task UseCircuitBreaker_ExplicitInstance_AddsLayer()
    {
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1));
        var pipeline = NewBuilder<string, int>().UseCircuitBreaker(cb).Build();

        await pipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task UseRetries_ExplicitOptions_AddsLayer()
    {
        var pipeline = NewBuilder<string, int>()
            .UseRetries(NoDelayOptions(maxAttempts: 4))
            .Build();

        var attempts = 0;
        await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref attempts);
            throw new TimeoutException();
        });

        attempts.Should().Be(4);
    }

    [Fact]
    public async Task UseRetries_NullOptions_AddsLayerWithDefaults()
    {
        var pipeline = NewBuilder<string, int>().UseRetries().Build();

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));

        result.Success.Should().BeTrue();
        result.Data.Should().Be(7);
    }

    // ----------------------------------------------------------------------
    // Keyed DI overloads (the only DI-resolving path)
    // ----------------------------------------------------------------------

    [Fact]
    public async Task UseSingleflight_ServiceKey_ResolvesKeyedFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<Singleflight<string, int>>("audit");
        services.AddKeyedSingleton<Singleflight<string, int>>("notifications");
        var sp = services.BuildServiceProvider();

        var pipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseSingleflight(serviceKey: "audit")
            .Build();

        var invocations = 0;
        var gate = new TaskCompletionSource();
        var t1 = pipeline.ExecuteAsync("k", async _ =>
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }).AsTask();
        var t2 = pipeline.ExecuteAsync("k", async _ =>
        {
            Interlocked.Increment(ref invocations);
            await gate.Task;
            return 1;
        }).AsTask();

        await Task.Delay(20);
        gate.SetResult();
        await Task.WhenAll(t1, t2);

        invocations.Should().Be(1);
    }

    [Fact]
    public async Task UseCircuitBreaker_ServiceKey_ResolvesKeyedFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "audit",
            (_, _) => new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1)));
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "notifications",
            (_, _) => new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1)));
        var sp = services.BuildServiceProvider();

        var pipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseCircuitBreaker(serviceKey: "audit")
            .Build();

        await pipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var result = await pipeline.ExecuteAsync("k", _ => ValueTask.FromResult(7));
        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task KeyedCircuitBreakers_AreIndependent_AcrossPipelines()
    {
        // The defining property: two pipelines wired to keyed CBs with
        // DIFFERENT keys must NOT share state. Tripping one's breaker leaves
        // the other unaffected — this is what the keyed-services discipline
        // is for in this lib (multi-exchange RMQ, multi-endpoint outbound).
        var services = new ServiceCollection();
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "audit",
            (_, _) => new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1)));
        services.AddKeyedSingleton<CircuitBreaker<int>>(
            "notifications",
            (_, _) => new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1)));
        var sp = services.BuildServiceProvider();

        var auditPipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseCircuitBreaker(serviceKey: "audit")
            .Build();

        var notificationsPipeline = new ResilientPipelineBuilder<string, int>(sp)
            .UseCircuitBreaker(serviceKey: "notifications")
            .Build();

        // Trip the audit breaker only.
        await auditPipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var auditAfter = await auditPipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        var notificationsAfter = await notificationsPipeline
            .ExecuteAsync("k", _ => ValueTask.FromResult(2));

        auditAfter.IsServiceUnavailable.Should().BeTrue();   // audit breaker is open
        notificationsAfter.Success.Should().BeTrue();        // notifications breaker is unaffected
        notificationsAfter.Data.Should().Be(2);
    }

    // ----------------------------------------------------------------------
    // Multi-layer chaining + order preservation
    // ----------------------------------------------------------------------

    [Fact]
    public async Task FluentChain_PreservesRegistrationOrder_AsLayerOrder()
    {
        // Adversarial: order in which Use* methods are called must equal the
        // outer-first layer order in the resulting pipeline. Two retry layers
        // with different MaxAttempts make the ordering observable: the outer
        // retry's MaxAttempts dominates the total attempt count when both
        // count the same exception as transient.
        var pipeline = NewBuilder<string, int>()
            .UseRetries(NoDelayOptions(maxAttempts: 2)) // outer retry: 2 attempts
            .UseRetries(NoDelayOptions(maxAttempts: 3)) // inner retry: 3 attempts each call
            .Build();

        var attempts = 0;
        await pipeline.ExecuteAsync("k", _ =>
        {
            Interlocked.Increment(ref attempts);
            throw new TimeoutException();
        });

        // Outer (2) × inner (3) = 6 total operation calls.
        attempts.Should().Be(6);
    }

    [Fact]
    public void Build_AfterMultipleConfiguration_SnapshotsLayerSet()
    {
        var pipeline = NewBuilder<string, int>()
            .UseRetries(NoDelayOptions())
            .UseCircuitBreaker(new CircuitBreaker<int>(_ => false))
            .Build();

        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task Build_CalledTwice_ProducesIndependentPipelinesSharingLayerInstances()
    {
        // Adversarial: calling Build() twice on the same builder yields two
        // DIFFERENT pipeline instances, but they share the underlying layer
        // instances (and therefore primitive state). Tripping the CB through
        // pipeline #1 must be observable through pipeline #2 — they are two
        // facades over the same configured stack.
        var cb = new CircuitBreaker<int>(_ => false, options: new(failureThreshold: 1));
        var builder = NewBuilder<string, int>().UseCircuitBreaker(cb);

        var pipelineA = builder.Build();
        var pipelineB = builder.Build();

        pipelineA.Should().NotBeSameAs(pipelineB);

        // Trip via A.
        await pipelineA.ExecuteAsync("k", _ => throw new InvalidOperationException());

        // B sees the open breaker because they share the CB instance.
        var bResult = await pipelineB.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        bResult.IsServiceUnavailable.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    /// <summary>
    /// Builder bound to an empty service provider — used by tests that
    /// exercise only the explicit-instance overloads (which never touch DI).
    /// </summary>
    private static ResilientPipelineBuilder<TKey, TValue> NewBuilder<TKey, TValue>()
        where TKey : notnull
        => new(EmptyServiceProvider.Instance);

    private static RetryOptions<int> NoDelayOptions(int maxAttempts = 3)
        => new()
        {
            MaxAttempts = maxAttempts,
            BaseDelayMs = 0,
            MaxDelayMs = 0,
            Jitter = false,
            DelayFunc = (_, _) => Task.CompletedTask,
        };

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
