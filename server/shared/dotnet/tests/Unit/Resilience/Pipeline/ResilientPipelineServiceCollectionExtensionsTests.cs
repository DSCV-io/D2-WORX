// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Resilience.Pipeline;

using AwesomeAssertions;
using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Pipeline;
using D2.Shared.Resilience.Singleflight;
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
        services.AddResilientPipeline<string, int>("notifications", p => p.UseCircuitBreaker("notifications"));

        var sp = services.BuildServiceProvider();
        var auditPipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("audit");
        var notificationsPipeline = sp.GetRequiredKeyedService<ResilientPipeline<string, int>>("notifications");

        // Trip audit only.
        await auditPipeline.ExecuteAsync("k", _ => throw new InvalidOperationException());

        var auditAfter = await auditPipeline.ExecuteAsync("k", _ => ValueTask.FromResult(1));
        var notificationsAfter = await notificationsPipeline.ExecuteAsync("k", _ => ValueTask.FromResult(2));

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
}
