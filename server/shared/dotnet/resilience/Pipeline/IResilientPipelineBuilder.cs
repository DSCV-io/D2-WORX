// -----------------------------------------------------------------------
// <copyright file="IResilientPipelineBuilder.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Resilience.Pipeline;

using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Retry;
using D2.Shared.Resilience.Singleflight;

/// <summary>
/// Fluent builder for composing a <see cref="ResilientPipeline{TKey, TValue}"/>
/// at registration time. The order in which <c>Use*</c> methods are called
/// IS the layer order in the resulting pipeline (outer-first).
/// </summary>
/// <typeparam name="TKey">Per-call key type.</typeparam>
/// <typeparam name="TValue">The value type produced by the operation.</typeparam>
/// <remarks>
/// Resilience primitives MUST be registered as keyed services in DI — every
/// <c>Use*</c> overload that resolves from DI requires an explicit
/// <c>serviceKey</c>. The lib intentionally provides no unkeyed-resolution
/// path because two unkeyed registrations of the same shape silently
/// overwrite each other (last-wins) and that's exactly the footgun this
/// library refuses to allow. The keyed-only rule also keeps the call site
/// unambiguous — every <c>UseX(serviceKey)</c> says exactly which primitive
/// instance it resolves, with no implicit context to track.
/// <para>
/// For tests or manual composition where DI registration isn't appropriate,
/// the explicit-instance overloads (<c>UseSingleflight(instance)</c> /
/// <c>UseCircuitBreaker(instance)</c>) bypass DI entirely.
/// </para>
/// </remarks>
public interface IResilientPipelineBuilder<TKey, TValue>
    where TKey : notnull
{
    /// <summary>
    /// Adds a <see cref="SingleflightLayer{TKey, TValue}"/> resolved from DI
    /// via <paramref name="serviceKey"/>.
    /// </summary>
    /// <param name="serviceKey">The DI key the primitive was registered with.</param>
    IResilientPipelineBuilder<TKey, TValue> UseSingleflight(object serviceKey);

    /// <summary>
    /// Adds a <see cref="SingleflightLayer{TKey, TValue}"/> wrapping the
    /// supplied <paramref name="instance"/>. Bypasses DI — use for tests or
    /// manual composition where DI registration isn't appropriate.
    /// </summary>
    /// <param name="instance">The Singleflight instance.</param>
    IResilientPipelineBuilder<TKey, TValue> UseSingleflight(Singleflight<TKey, TValue> instance);

    /// <summary>
    /// Adds a <see cref="CircuitBreakerLayer{TKey, TValue}"/> resolved from
    /// DI via <paramref name="serviceKey"/>.
    /// </summary>
    /// <param name="serviceKey">The DI key the primitive was registered with.</param>
    IResilientPipelineBuilder<TKey, TValue> UseCircuitBreaker(object serviceKey);

    /// <summary>
    /// Adds a <see cref="CircuitBreakerLayer{TKey, TValue}"/> wrapping the
    /// supplied <paramref name="instance"/>. Bypasses DI.
    /// </summary>
    /// <param name="instance">The CircuitBreaker instance.</param>
    IResilientPipelineBuilder<TKey, TValue> UseCircuitBreaker(CircuitBreaker<TValue> instance);

    /// <summary>
    /// Adds a <see cref="RetryLayer{TKey, TValue}"/> with the supplied
    /// <paramref name="options"/> (or defaults if null). Retry has no DI
    /// primitives to resolve — its config is passed inline.
    /// </summary>
    /// <param name="options">Retry configuration; <c>null</c> = defaults.</param>
    IResilientPipelineBuilder<TKey, TValue> UseRetries(RetryOptions<TValue>? options = null);

    /// <summary>
    /// Snapshots the accumulated layers into a new
    /// <see cref="ResilientPipeline{TKey, TValue}"/>.
    /// </summary>
    ResilientPipeline<TKey, TValue> Build();
}
