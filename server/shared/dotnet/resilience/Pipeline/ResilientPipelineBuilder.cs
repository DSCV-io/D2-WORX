// -----------------------------------------------------------------------
// <copyright file="ResilientPipelineBuilder.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Resilience.Pipeline;

using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Retry;
using D2.Shared.Resilience.Singleflight;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default <see cref="IResilientPipelineBuilder{TKey, TValue}"/>
/// implementation. Accumulates layers in registration order; <see cref="Build"/>
/// snapshots them into the resulting pipeline.
/// </summary>
/// <typeparam name="TKey">Per-call key type.</typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
public sealed class ResilientPipelineBuilder<TKey, TValue>
    : IResilientPipelineBuilder<TKey, TValue>
    where TKey : notnull
{
    private readonly IServiceProvider r_serviceProvider;
    private readonly List<IResilientLayer<TKey, TValue>> r_layers = [];

    /// <summary>
    /// Initializes a builder bound to the supplied service provider, used by
    /// the <see cref="UseSingleflight(object)"/> and
    /// <see cref="UseCircuitBreaker(object)"/> overloads to resolve keyed
    /// primitives from DI.
    /// </summary>
    /// <param name="serviceProvider">DI service provider.</param>
    public ResilientPipelineBuilder(IServiceProvider serviceProvider)
        => r_serviceProvider = serviceProvider;

    /// <inheritdoc/>
    public IResilientPipelineBuilder<TKey, TValue> UseSingleflight(object serviceKey)
        => UseSingleflight(
            r_serviceProvider.GetRequiredKeyedService<Singleflight<TKey, TValue>>(serviceKey));

    /// <inheritdoc/>
    public IResilientPipelineBuilder<TKey, TValue> UseSingleflight(
        Singleflight<TKey, TValue> instance)
    {
        r_layers.Add(new SingleflightLayer<TKey, TValue>(instance));
        return this;
    }

    /// <inheritdoc/>
    public IResilientPipelineBuilder<TKey, TValue> UseCircuitBreaker(object serviceKey)
        => UseCircuitBreaker(
            r_serviceProvider.GetRequiredKeyedService<CircuitBreaker<TValue>>(serviceKey));

    /// <inheritdoc/>
    public IResilientPipelineBuilder<TKey, TValue> UseCircuitBreaker(
        CircuitBreaker<TValue> instance)
    {
        r_layers.Add(new CircuitBreakerLayer<TKey, TValue>(instance));
        return this;
    }

    /// <inheritdoc/>
    public IResilientPipelineBuilder<TKey, TValue> UseRetries(RetryOptions<TValue>? options = null)
    {
        r_layers.Add(new RetryLayer<TKey, TValue>(options));
        return this;
    }

    /// <inheritdoc/>
    public ResilientPipeline<TKey, TValue> Build()
        => new([.. r_layers]);
}
